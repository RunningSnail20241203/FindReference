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
        public Task<(List<FindReferenceData> data, Dictionary<string, long> mtimes)> CustomTask { get; }

        public ParseReferenceTask(
            List<string> files,
            CancellationToken cancellationToken = default,
            IReadOnlyDictionary<string, long> mtimeCache = null,
            IReadOnlyDictionary<string, FindReferenceData> existingData = null)
        {
            CustomTask = Task.Run(
                () => GenerateRefData(files, cancellationToken, mtimeCache, existingData),
                cancellationToken);
        }

        public Task<(List<FindReferenceData>, Dictionary<string, long>)> CustomTask { get; }
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

        private (List<FindReferenceData>, Dictionary<string, long>) GenerateRefData(
            List<string> files,
            CancellationToken cancellationToken = default,
            IReadOnlyDictionary<string, long> mtimeCache = null,
            IReadOnlyDictionary<string, FindReferenceData> existingData = null)
        {
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = cancellationToken
            };
            var totalFiles = files.Count;
            var processedCount = 0;
            var newMtimes = new ConcurrentDictionary<string, long>();

            // 方向3：线程本地 List + 末尾合并，避免 ConcurrentBag 争用
            var result = new List<FindReferenceData>();
            var lockObj = new object();

            try
            {
                Parallel.ForEach(
                    files,
                    parallelOptions,
                    () => new List<FindReferenceData>(),  // localInit: 每线程本地 List
                    (file, state, localList) =>           // body
                    {
                        try
                        {
                            var data = ParseOneFile(
                                file,
                                mtimeCache,
                                existingData,
                                newMtimes);
                            if (data != null)
                            {
                                localList.Add(data);
                            }
                        }
                        catch (Exception ex)
                        {
                            FindReferenceLogger.LogError($"解析文件 {file} 时出错: {ex.Message}");
                        }
                        finally
                        {
                            var newProcessed = Interlocked.Increment(ref processedCount);
                            TryUpdateProgress(newProcessed, totalFiles);
                        }

                        return localList;
                    },
                    localList =>                          // localFinally: 合并
                    {
                        lock (lockObj)
                        {
                            result.AddRange(localList);
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

            return (result, new Dictionary<string, long>(newMtimes));
        }

        private void TryUpdateProgress(int newProcessed, int totalFiles)
        {
            if (newProcessed % 100 != 0) return; // 每100个文件更新一次进度，减少UI开销
            var progress = (float)newProcessed / totalFiles;
            UpdateProgress(progress);
        }

        private FindReferenceData ParseOneFile(
            string file,
            IReadOnlyDictionary<string, long> mtimeCache,
            IReadOnlyDictionary<string, FindReferenceData> existingData,
            ConcurrentDictionary<string, long> newMtimes)
        {
            var guid = ConvertPath2Guid(file);
            if (string.IsNullOrEmpty(guid)) return null;

            // 方向4：mtime 检查，命中则跳过 content 解析
            long currentMtime = 0;
            try
            {
                currentMtime = File.GetLastWriteTimeUtc(file).Ticks;
            }
            catch
            {
                currentMtime = 0;
            }

            newMtimes[file] = currentMtime;

            // 如果 mtime 未变且有缓存数据，直接复用
            if (mtimeCache != null &&
                mtimeCache.TryGetValue(file, out var cachedMtime) &&
                cachedMtime == currentMtime &&
                existingData != null &&
                existingData.TryGetValue(guid, out var cachedData))
            {
                // 复用缓存数据（children 不变，parents 稍后重建）
                var reuseData = new FindReferenceData(guid, cachedData.ChildrenSet.ToArray(), null);
                return reuseData;
            }

            // 方向2：使用合并正则，一次扫描获取所有格式的 GUID
            var set = new HashSet<string>();
            try
            {
                using var sr = new StreamReader(file);
                var content = sr.ReadToEnd();

                var matches = FindReferenceConfig.FindGuidRegexAll.Matches(content);
                foreach (Match match in matches)
                {
                    // 三个捕获组，取第一个非空的
                    for (int i = 1; i <= 3; i++)
                    {
                        if (match.Groups[i].Success)
                        {
                            set.Add(match.Groups[i].Value);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                FindReferenceLogger.LogError($"读取文件内容失败 {file}: {ex.Message}");
                return null;
            }

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