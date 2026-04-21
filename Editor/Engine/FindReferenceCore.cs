using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FindReference.Editor.Common;
using FindReference.Editor.Config;
using FindReference.Editor.Data;
using FindReference.Editor.EventListener;
using UnityEditor;
using UnityEngine;

namespace FindReference.Editor.Engine
{
    public class FindReferenceCore
    {
        #region Private Data

        // private readonly List<string> _fileContainGuid =
        //     new() { ".prefab", ".unity", ".mat", ".anim", ".asset", ".controller" };

        // private const string PathPrefix = "Assets/";

        private FindReferenceDataBase _dataBase;
        private static FindReferenceCore _instance;
        private CancellationTokenSource _cancellationTokenSource;

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
        public string[] QueryParents(string guid)
        {
            if (!IsInitialized())
            {
                FindReferenceLogger.LogError("初始化失败！");
                return Array.Empty<string>();
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
            return QueryParents(guid).Length;
        }

        /// <summary>
        /// 根据guid查找子节点列表
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public string[] QueryChildren(string guid)
        {
            if (!IsInitialized())
            {
                FindReferenceLogger.Log("初始化失败！");
                return Array.Empty<string>();
            }

            return _dataBase.QueryChildren(guid);
        }

        public void RefreshDataBase()
        {
            if (!IsInitialized())
            {
                FindReferenceLogger.Log("初始化失败！");
                return;
            }

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            // 方向1：主线程预取 AssetDatabase 路径，消除 Directory.GetFiles IO
            var allAssetPaths = AssetDatabase.GetAllAssetPaths();

            var task = RefreshCache(allAssetPaths, _cancellationTokenSource.Token);
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    FindReferenceLogger.LogError($"RefreshCache 失败: {t.Exception?.InnerException?.Message}");
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        public void CancelRefresh()
        {
            _cancellationTokenSource?.Cancel();
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

            var task = UpdateCacheSilent(assetPaths, _cancellationTokenSource?.Token ?? CancellationToken.None);
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    FindReferenceLogger.LogError($"UpdateCacheSilent 失败: {t.Exception?.InnerException?.Message}");
            }, TaskScheduler.FromCurrentSynchronizationContext());
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

        private async Task UpdateCacheSilent(List<string> processFiles, CancellationToken cancellationToken = default)
        {
            double startTime = 0;
            try
            {
                startTime = EditorApplication.timeSinceStartup;
                processFiles = Filter(processFiles.ToArray(), true);

                if (processFiles.Count == 0)
                {
                    FindReferenceLogger.Log("没有符合条件的文件");
                    return;
                }

                // 增量更新不用 mtime 优化（files 已是变更文件）
                var (refData, _) = await new ParseReferenceTask(processFiles, cancellationToken).CustomTask;

                _dataBase.UpdateData(refData);
            }
            catch (OperationCanceledException)
            {
                FindReferenceLogger.LogError("静默刷新缓存被取消");
            }
            catch (Exception e)
            {
                FindReferenceLogger.LogError($"静默刷新缓存出错：{e.Message}");
            }
            finally
            {
                FindReferenceLogger.Log(
                    $"处理 {processFiles.Count} 个资源完毕, 耗时：{EditorApplication.timeSinceStartup - startTime}s");
            }
        }

        private async Task RefreshCache(string[] allAssetPaths, CancellationToken cancellationToken = default)
        {
            double reGeTime = 0;
            try
            {
                IsWorking = true;
                reGeTime = EditorApplication.timeSinceStartup;

                // 方向1：用预取的路径替代 Directory.GetFiles
                var filteredPaths = await new GetFilePathListTask(allAssetPaths, cancellationToken).CustomTask;

                // 方向4：获取 mtime 缓存用于跳过未变更文件
                var mtimeCache = _dataBase.GetMtimeCache();
                var existingData = _dataBase.GetReferenceDataDict();

                var (refData, newMtimes) = await new ParseReferenceTask(
                    filteredPaths,
                    cancellationToken,
                    mtimeCache,
                    existingData).CustomTask;

                _dataBase.SetData(refData, newMtimes);
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

        private static List<string> Filter(string[] files, bool filterPrefix)
        {
            var filePaths = (
                from file in files
                where !filterPrefix || FindReferenceConfig.PathPrefixes.Any(file.StartsWith)
                let extension = Path.GetExtension(file)
                where FindReferenceConfig.IsSupportedExtension(extension)
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