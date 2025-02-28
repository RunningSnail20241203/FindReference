using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using FindReference.Editor.Common;
using FindReference.Editor.Data;
using FindReference.Editor.EventListener;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace FindReference.Editor.Engine
{
    public class FindReferenceCore
    {
        #region Private Data

        private readonly List<string> _fileContainGuid =
            new() { ".prefab", ".unity", ".mat", ".anim", ".asset", ".controller" };

        private const string PathPrefix = "Assets/";

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

        /// <summary>
        /// 根据guid查找父节点列表
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public List<string> QueryParents(string guid)
        {
            if (!IsInitialized())
            {
                FindReferenceLogger.LogError("初始化失败！");
                return new List<string>();
            }
            var ret = _dataBase.QueryParents(guid);
            return ret;
        }

        /// <summary>
        /// 根据guid查找父节点数量 todo 优化性能，因为是放在Project视图刷新的时候计算的。
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public int QueryParentsCount(string guid)
        {
            return QueryParents(guid).Count;
        }

        /// <summary>
        /// 根据guid查找子节点列表
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public List<string> QueryChildren(string guid)
        {
            if (!IsInitialized())
            {
                FindReferenceLogger.Log("初始化失败！");
                return new List<string>();
            }

            return _dataBase.QueryChildren(guid);
        }

        /// <summary>
        /// 重建整个缓存 todo 优化搜集文件列表的性能
        /// </summary>
        public CancellationTokenSource RefreshDataBase()
        {
            if (!IsInitialized())
            {
                FindReferenceLogger.Log("初始化失败！");
                return null;
            }

            var cancellationTokenSource = new CancellationTokenSource();

            RefreshCache(cancellationTokenSource.Token);

            return cancellationTokenSource;
        }

        /// <summary>
        /// 后台静默处理资源引用变化
        /// </summary>
        /// <param name="assetPaths"></param>
        public void ProcessChangedAssets(List<string> assetPaths)
        {
            if (!IsInitialized())
            {
                FindReferenceLogger.Log("初始化失败！");
                return;
            }

            UpdateCacheSilent(assetPaths);
        }

        // 后台静默删除资源引用
        public void ProcessDeleteAsset(string guid)
        {
            if (!IsInitialized())
            {
                FindReferenceLogger.Log("初始化失败！");
                return;
            }

            _dataBase.DeleteAsset(guid);
        }

        #endregion

        #region Private Method

        private async void UpdateCacheSilent(List<string> processFiles)
        {
            var startTime = EditorApplication.timeSinceStartup;
            try
            {
                processFiles = Filter(processFiles.ToArray(), _fileContainGuid, true);

                if (processFiles.Count == 0)
                {
                    FindReferenceLogger.Log("没有符合条件的文件");
                    return;
                }

                var refDatas = await new ParseReferenceTask(processFiles).Start();

                _dataBase.UpdateData(refDatas);
            }
            catch (Exception e)
            {
                FindReferenceLogger.LogError($"静默刷新缓存出错：{e.Message}");
            }
            finally
            {
                FindReferenceLogger.Log($"处理 {processFiles.Count} 个资源完毕, 耗时：{EditorApplication.timeSinceStartup - startTime}s");
            }
        }

        private async void RefreshCache(CancellationToken token)
        {
            var reGeTime = EditorApplication.timeSinceStartup;
            IsWorking = true;
            try
            {
                var processFiles = await new GetFilePathListTask(
                    Application.dataPath,
                    _fileContainGuid,
                    token
                ).CustomTask;
                var refDatas = await new ParseReferenceTask(
                    processFiles,
                    token).Start();

                _dataBase.SetData(refDatas);
            }
            catch (OperationCanceledException)
            {
                FindReferenceLogger.LogError("取消刷新缓存");
            }
            catch (Exception e)
            {
                FindReferenceLogger.LogError($"刷新缓存出错：{e.Message}");
            }
            finally
            {
                IsWorking = false;
                EventCenter.Instance.Publish(FEventType.TaskEnd, null);
                FindReferenceLogger.Log($"任务结束,用时：{EditorApplication.timeSinceStartup - reGeTime}s");
            }
        }

        private static List<string> Filter(string[] files, List<string> whiteList, bool filterPrefix)
        {
            var filePaths = (
                from file in files
                where !filterPrefix || file.StartsWith(PathPrefix)
                let extension = Path.GetExtension(file)
                where whiteList?.Contains(extension) ?? true
                select file
            ).ToList();

            return filePaths;
        }

        private bool IsInitialized()
        {
            return _dataBase != null;
        }

        private void Initialize()
        {
            _dataBase = FindReferenceDataBase.instance;
            if (_dataBase == null)
            {
                FindReferenceLogger.LogError("没有配置FindReferenceDataBase的路径");
                return;
            }
            _dataBase.Initialize();
        }

        #endregion
    }
}