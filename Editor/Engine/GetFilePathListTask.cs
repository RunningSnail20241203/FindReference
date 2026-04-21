using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FindReference.Editor.Config;
using FindReference.Editor.EventListener;
using FindReference.Editor.Common;

namespace FindReference.Editor.Engine
{
    public class GetFilePathListTask
    {
        public Task<List<string>> CustomTask { get; }

        // 方向1：接收主线程预取的 AssetDatabase 路径，去掉 Directory.GetFiles IO
        public GetFilePathListTask(string[] allAssetPaths, CancellationToken cancellationToken = default)
        {
            CustomTask = Task.Run(() => FilterFileList(allAssetPaths, cancellationToken), cancellationToken);
        }

        private const float GetFilesProgress = 0.5f;
        private float _progress;

        private void UpdateProgress(float value)
        {
            EventCenter.Instance.Publish(FEventType.GetFilesTask, new TaskProgressUpdateEvent()
            {
                OldProgress = _progress,
                NewProgress = value
            });
            _progress = value;
        }

        // 方向3：线程本地 List 模式，替代 ConcurrentBag
        private List<string> FilterFileList(string[] allAssetPaths, CancellationToken cancellationToken = default)
        {
            UpdateProgress(0f);

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = cancellationToken
            };
            var totalFiles = allAssetPaths.Length;
            var processedCount = 0;
            var result = new List<string>();
            var lockObj = new object();

            try
            {
                Parallel.ForEach(
                    allAssetPaths,
                    parallelOptions,
                    () => new List<string>(),  // localInit
                    (path, state, localList) => // body
                    {
                        // 主动检查取消令牌，使取消能立即生效
                        cancellationToken.ThrowIfCancellationRequested();

                        if (!FindReferenceConfig.ExcludedPathPrefixes.Any(path.StartsWith))
                        {
                            var extension = Path.GetExtension(path).ToLowerInvariant();
                            if (FindReferenceConfig.IsSupportedExtension(extension))
                                localList.Add(path);
                        }

                        var processed = Interlocked.Increment(ref processedCount);
                        TryUpdateProgress(processed, totalFiles);

                        return localList;
                    },
                    localList =>  // localFinally
                    {
                        lock (lockObj)
                        {
                            result.AddRange(localList);
                        }
                    });
            }
            catch (OperationCanceledException)
            {
                EventCenter.Instance.Publish(FEventType.GetFilesTask, new TaskProgressUpdateEvent()
                {
                    OldProgress = _progress,
                    NewProgress = 0
                });
                throw;
            }

            return result;
        }

        private void TryUpdateProgress(int newProcessed, int totalFiles)
        {
            if (newProcessed % 1000 != 0) return; // 改为每1000个文件更新一次，减少UI抖动
            var progress = GetFilesProgress + (float)newProcessed / totalFiles * (1 - GetFilesProgress);
            UpdateProgress(progress);
        }
    }
}