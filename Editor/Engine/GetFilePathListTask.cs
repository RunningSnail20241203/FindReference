using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FindReference.Editor.Config;
using FindReference.Editor.EventListener;

namespace FindReference.Editor.Engine
{
    public class GetFilePathListTask
    {
        public Task<List<string>> CustomTask { get; }

        public GetFilePathListTask(string path)
        {
            CustomTask = Task.Run(() => GenerateFileList(path));
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

        private List<string> GenerateFileList(string directory)
        {
            UpdateProgress(0f);
            
            var result = new ConcurrentBag<string>();
            // 获取所有文件
            var files = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories);
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };
            var totalFiles = files.Length;
            var processedCount = 0;

            Parallel.ForEach(files, parallelOptions, (file, state) =>
            {
                var extension = Path.GetExtension(file).ToLowerInvariant();
                if (FindReferenceConfig.IsSupportedExtension(extension))
                {
                    result.Add(file);
                    // Debug.Log($"Added file: {file}");
                }
                
                // 线程安全的进度更新
                var newProcessed = Interlocked.Increment(ref processedCount);
                TryUpdateProgress(newProcessed, totalFiles);
            });
            
            return result.ToList();
        }

        private void TryUpdateProgress(int newProcessed, int totalFiles)
        {
            if (newProcessed % 100 != 0) return; // 每100个文件更新一次进度，减少UI开销
            var progress = GetFilesProgress + (float)newProcessed / totalFiles * (1 - GetFilesProgress);
            UpdateProgress(progress);
        }
    }
}