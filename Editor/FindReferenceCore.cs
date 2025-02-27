using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

namespace FindReference.Editor
{
    public class FindReferenceCore
    {
        #region Private Data

        private readonly List<string> _fileContainGuid = new()
        {
            ".prefab",
            ".unity",
            ".mat",
            ".anim",
            ".asset",
            ".controller"
        };

        private static readonly string PathPrefix = "Assets/";

        private FindReferenceDataBase _dataBase;
        private static FindReferenceCore _instance;

        #endregion

        #region Properties

        public bool IsWorking { get; private set; }

        public static FindReferenceCore Instance
        {
            get
            {
                if (_instance != null)
                {
                    return _instance;
                }

                _instance = new FindReferenceCore();
                _instance.Initialize();
                return _instance;
            }
        }

        #endregion

        #region Public APIs

        public List<string> QueryReferences(string guid)
        {
            if (!IsInitialized())
            {
                FindReferenceLogger.Log("初始化失败！");
                return new List<string>();
            }

            return _dataBase.QueryReferences(guid);
        }

        public int QueryReferencesCount(string guid)
        {
            if (!IsInitialized())
            {
                FindReferenceLogger.Log("初始化失败！");
                return 0;
            }

            return QueryReferences(guid).Count;
        }

        public List<string> QueryDependencies(string guid)
        {
            if (!IsInitialized())
            {
                FindReferenceLogger.Log("初始化失败！");
                return new List<string>();
            }

            return _dataBase.QueryDependencies(guid);
        }

        public void RefreshDataBase()
        {
            if (!IsInitialized())
            {
                FindReferenceLogger.Log("初始化失败！");
                return;
            }

            var processFiles = GenerateFileList(Application.dataPath, _fileContainGuid);

            _dataBase.Clear();
            EditorCoroutineUtility.StartCoroutine(CalcFilesReference(processFiles), this);
        }

        public void ProcessChangedAssets(List<string> assetPaths)
        {
            if (!IsInitialized())
            {
                FindReferenceLogger.Log("初始化失败！");
                return;
            }
            assetPaths = Filter(assetPaths.ToArray(), _fileContainGuid, true);

            EditorCoroutineUtility.StartCoroutine(CalcFilesReference(assetPaths), this);
        }

        public void ProcessDeleteAsset(string guid)
        {
            if (!IsInitialized())
            {
                FindReferenceLogger.Log("初始化失败！");
                return;
            }

            _dataBase.DeleteAsset(guid);
        }

        public void Save()
        {
            if (!IsInitialized())
            {
                FindReferenceLogger.Log("初始化失败！");
                return;
            }

            _dataBase.Save();
        }

        #endregion

        #region Private Method

        /// <summary>
        /// 计算所有文件的引用关系
        /// </summary>
        private IEnumerator CalcFilesReference(List<string> files)
        {
            var startTime = EditorApplication.timeSinceStartup;
            // if (IsWorking)
            // {
            //     FindReferenceLogger.Log("查找引用关系中，请稍后...");
            //     yield break;
            // }

            IsWorking = true;

            FindReferenceLogger.Log($"需要处理{files.Count}个文件");
            if (files.Count > 0)
            {
                yield return CalcAssetsDependencyByThread(files, map =>
                {
                    _dataBase.SetData(map);
                    OnComplete();
                });
            }
            else
            {
                OnComplete();
            }

            yield break;

            void OnComplete()
            {
                FindReferenceLogger.Log($"查询任务结束~,用时：{EditorApplication.timeSinceStartup - startTime}s");
                IsWorking = false;
            }
        }

        /// <summary>
        /// 查找所有文件，返回文件列表
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="whiteList"></param>
        private static List<string> GenerateFileList(string directory, List<string> whiteList)
        {
            // 获取所有文件
            var files = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories);

            return Filter(files, whiteList, false);
        }

        private static List<string> Filter(string[] files, List<string> whiteList, bool filterPrefix)
        {
            var filePaths = (from file in files
                where !filterPrefix || file.StartsWith(PathPrefix)
                let extension = Path.GetExtension(file)
                where whiteList?.Contains(extension) ?? true
                select file).ToList();

            return filePaths;
        }

        /// <summary>
        /// 计算新增的资源的依赖关系
        /// </summary>
        /// <param name="files">资源列表</param>
        /// <param name="callback"></param>
        /// <returns></returns>
        private IEnumerator CalcAssetsDependencyByThread(List<string> files,
            Action<ConcurrentBag<FindReferenceData>> callback)
        {
            var numberOfTasks = files.Count < Environment.ProcessorCount ? 1 : Environment.ProcessorCount;
            var segmentSize = Mathf.FloorToInt((float)files.Count / numberOfTasks);
            var cancelTokenSource = new CancellationTokenSource();
            var cancellationToken = cancelTokenSource.Token;

            FindReferenceLogger.Log($"将任务分为了{numberOfTasks}个job");
            var ret = new ConcurrentBag<FindReferenceData>();
            var jobs = new FindReferenceParseJob[numberOfTasks];
            for (var i = 0; i < numberOfTasks; i++)
            {
                var count = i == numberOfTasks - 1 ? files.Count - i * segmentSize : segmentSize;
                var temp = files.GetRange(i * segmentSize, count);
                var job = new FindReferenceParseJob(temp, ref ret, i);
                jobs[i] = job;
                job.Start(cancellationToken);
            }

            while (jobs.Any(x => x?.IsDone != true))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    FindReferenceLogger.Log("所有任务已取消");
                    EditorUtility.ClearProgressBar();
                    yield break;
                }
                yield return null;
                var cancel = EditorUtility.DisplayCancelableProgressBar("查找引用关系", "查找引用关系中...", jobs.Average(x => x.Progress));
                if (cancel)
                {
                    cancelTokenSource.Cancel();
                }
            }
            EditorUtility.ClearProgressBar();
            callback?.Invoke(ret);
        }

        private bool IsInitialized()
        {
            return _dataBase != null;
        }

        private void Initialize()
        {
            _dataBase = FindReferenceDataBase.instance;
            if (_dataBase != null)
            {
                _dataBase.Initialize();
            }
            else
            {
                FindReferenceLogger.LogError("没有配置FindReferenceDataBase的路径");
            }
        }

        #endregion
    }

    public class ThreadData
    {
        public List<string> AllFiles;
        public ConcurrentBag<FindReferenceData> ReferenceArray;
        public int TaskId;
        public int StartIdx;
        public int EndIdx;
    }
}