using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FindReference.Editor.Common;
using FindReference.Editor.Data;
using FindReference.Editor.EventListener;
using UnityEngine;

namespace FindReference.Editor.Engine
{
    public class ParseReferenceTask
    {
        public ParseReferenceTask(List<string> files)
        {
            CustomTask = Task.Run(() => GenerateRefData(files));
        }

        public Task<List<FindReferenceData>> CustomTask { get; }
        private static readonly Regex Regex = new("(?:m_AssetGUID|guid|value): ([0-9a-f]{32})");

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

        private List<FindReferenceData> GenerateRefData(List<string> files)
        {
            var result = new ConcurrentBag<FindReferenceData>();
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };
            var totalFiles = files.Count;
            var processedCount = 0;
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
            var matches = Regex.Matches(content);
            foreach (Match match in matches)
            {
                set.Add(match.Groups[1].Value);
            }

            var children = set.ToArray();
            var data = new FindReferenceData(guid, children, null);
            return data;
        }

        private string ConvertPath2Guid(string s)
        {
            var metaPath = s + ".meta";
            using var metaSr = new StreamReader(metaPath);
            _ = metaSr.ReadLine();
            var sL = metaSr.ReadLine();
            return sL?["guid: ".Length..];
        }
    }
}