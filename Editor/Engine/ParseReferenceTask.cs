using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FindReference.Editor.Common;
using FindReference.Editor.Config;
using FindReference.Editor.Data;
using FindReference.Editor.EventListener;

namespace FindReference.Editor.Engine
{
    public class ParseReferenceTask
    {
        public ParseReferenceTask(List<string> files, CancellationToken cancellationToken = default)
        {
            CustomTask = Task.Run(() => GenerateRefData(files, cancellationToken), cancellationToken);
        }

        public Task<List<FindReferenceData>> CustomTask { get; }
        // private static readonly Regex Regex = new("(?:m_AssetGUID|guid|value): ([0-9a-f]{32})");

        private float _progress;

        private void UpdateProgress(float value)
        {
            EventCenter.Instance.Publish(FEventType.ParseTask, new TaskProgressUpdateEvent()
            {
                OldProgress = _progress,
                NewProgress = value
            });
            _progress = value;
        }

        private List<FindReferenceData> GenerateRefData(List<string> files, CancellationToken cancellationToken = default)
        {
            var result = new ConcurrentBag<FindReferenceData>();
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = cancellationToken
            };
            var totalFiles = files.Count;
            var processedCount = 0;
            try
            {
                Parallel.ForEach(files, parallelOptions, (file, state) =>
                {
                    try
                    {
                        var data = ParseOneFile(file);
                        if (data != null)
                        {
                            result.Add(data);
                        }
                    }
                    catch (Exception ex)
                    {
                        FindReferenceLogger.LogError($"解析文件 {file} 时出错: {ex.Message}");
                    }
                    finally
                    {
                        // 线程安全的进度更新
                        var newProcessed = Interlocked.Increment(ref processedCount);
                        TryUpdateProgress(newProcessed, totalFiles);
                    }
                });
            }
            catch (OperationCanceledException)
            {
                EventCenter.Instance.Publish(FEventType.ParseTask, new TaskProgressUpdateEvent()
                {
                    OldProgress = _progress,
                    NewProgress = 0
                });
                throw;
            }
            return result.ToList();
        }

        private void TryUpdateProgress(int newProcessed, int totalFiles)
        {
            if (newProcessed % 100 != 0) return; // 每100个文件更新一次进度，减少UI开销
            var progress = (float)newProcessed / totalFiles;
            UpdateProgress(progress);
        }

        private FindReferenceData ParseOneFile(string file)
        {
            var guid = ConvertPath2Guid(file);

            if (string.IsNullOrEmpty(guid)) return null;
            var set = new HashSet<string>(); // 记录依赖集合
            using var sr = new StreamReader(file);
            var content = sr.ReadToEnd();

            // 应用三种模式提取 GUID，所有匹配加入同一个 HashSet（自动去重）
            var matches1 = FindReferenceConfig.FindGuidRegex1.Matches(content);
            foreach (Match match in matches1)
            {
                set.Add(match.Groups[1].Value);
            }

            var matches2 = FindReferenceConfig.FindGuidRegex2.Matches(content);
            foreach (Match match in matches2)
            {
                set.Add(match.Groups[1].Value);
            }

            var matches3 = FindReferenceConfig.FindGuidRegex3.Matches(content);
            foreach (Match match in matches3)
            {
                set.Add(match.Groups[1].Value);
            }

            // 去掉文件自身 GUID（避免自引用）
            set.Remove(guid);

            var children = set.ToArray();
            var data = new FindReferenceData(guid, children, null);
            return data;
        }

        private string ConvertPath2Guid(string s)
        {
            var metaPath = s + ".meta";
            if (!File.Exists(metaPath))
            {
                FindReferenceLogger.LogError($".meta文件不存在: {metaPath}");
                return null;
            }

            try
            {
                using var metaSr = new StreamReader(metaPath);
                string line;
                while ((line = metaSr.ReadLine()) != null)
                {
                    if (line.StartsWith("guid: ", StringComparison.Ordinal))
                    {
                        return line.Substring("guid: ".Length);
                    }
                }
                FindReferenceLogger.LogError($".meta文件中未找到guid行: {metaPath}");
                return null;
            }
            catch (Exception ex)
            {
                FindReferenceLogger.LogError($"读取.meta失败 {metaPath}: {ex.Message}");
                return null;
            }
        }
    }
}