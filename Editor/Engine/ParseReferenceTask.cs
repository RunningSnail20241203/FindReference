using System;
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

// ReSharper disable once CheckNamespace
namespace FindReference.Editor.Engine
{
    public class ParseReferenceTask
    {
        public ParseReferenceTask(List<string> files, CancellationToken token)
        {
            _files = files;
            _token = token;
        }

        public ParseReferenceTask(List<string> files)
        {
            _files = files;
        }

        private readonly List<string> _files;
        private readonly CancellationToken _token;
        private const int FileCountPerTask = 50;
        private static readonly Regex Regex = new("(?:m_AssetGUID|guid|value): ([0-9a-f]{32})");
        
        public async Task<List<FindReferenceData>> Start()
        {
            var numberOfTasks = CalcTaskCount(_files.Count);
            var segmentSize = CalcSegmentSize(_files.Count, numberOfTasks);

            _taskProgress = new float[numberOfTasks];
            // FindReferenceLogger.Log($"将任务分为了 {numberOfTasks} 个job");
            var tasks = new Task<List<FindReferenceData>>[numberOfTasks];
            for (var i = 0; i < numberOfTasks; i++)
            {
                var count = i == numberOfTasks - 1 ? _files.Count - i * segmentSize : segmentSize;
                var temp = _files.GetRange(i * segmentSize, count);
                // FindReferenceLogger.Log($"任务 {i} 处理 {count} 个文件");
                var i1 = i;
                tasks[i] = Task.Run(() => ParseFileList(temp, _token, i1), _token);
            }

            var allResults = await Task.WhenAll(tasks);
            // 合并所有任务的结果
            var combinedResults = new List<FindReferenceData>();
            foreach (var result in allResults)
            {
                combinedResults.AddRange(result);
            }
            return combinedResults;

            int CalcTaskCount(int fileCount)
            {

                var maxTaskCount = Environment.ProcessorCount - 1;
                var taskCount = fileCount / FileCountPerTask + 1;
                return Mathf.Min(maxTaskCount, taskCount);
            }

            int CalcSegmentSize(int fileCount, int taskCount)
            {
                var size = fileCount / taskCount;
                return Mathf.Max(FileCountPerTask, size);
            }
        }

        private List<FindReferenceData> ParseFileList(List<string> files, CancellationToken cancellationToken, int taskIdx)
        {
            var ret = new List<FindReferenceData>();
            files.ForEach(ParseOneFile);

            return ret;

            void ParseOneFile(string x)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var guid = ConvertPath2Guid(x);
                if (string.IsNullOrEmpty(guid)) return;

                try
                {
                    var set = new HashSet<string>(); // 记录依赖集合
                    using (var sr = new StreamReader(x))
                    {
                        var content = sr.ReadToEnd();
                        var matches = Regex.Matches(content);
                        foreach (Match match in matches)
                        {
                            set.Add(match.Groups[1].Value);
                        }
                    }

                    var children = set.ToList();
                    var data = new FindReferenceData(guid, children, null);
                    ret.Add(data);
                    UpdateProgress(taskIdx, (float)ret.Count / files.Count);
                }
                catch (Exception ex)
                {
                    FindReferenceLogger.LogError($"解析文件 {x} 时出错: {ex.Message}");
                }

            }

            string ConvertPath2Guid(string s)
            {
                var metaPath = s + ".meta";
                try
                {
                    using var metaSr = new StreamReader(metaPath);
                    _ = metaSr.ReadLine();
                    var sL = metaSr.ReadLine();
                    return sL?["guid: ".Length..];
                }
                catch (FileNotFoundException)
                {
                    FindReferenceLogger.LogError($"未找到文件: {metaPath}");
                    return null;
                }
                catch (Exception ex)
                {
                    FindReferenceLogger.LogError($"读取文件 {metaPath} 时发生错误: {ex.Message}");
                    return null;
                }
            }
        }

        private float[] _taskProgress;
        private void UpdateProgress(int taskIdx, float value)
        {
            var oldValue = _taskProgress.Average(); 
            _taskProgress[taskIdx] = value;
            var newValue = _taskProgress.Average(); 
            EventCenter.Instance.Publish(FEventType.ParseTask, new TaskProgressUpdateEvent()
            {
                OldProgress = oldValue,
                NewProgress = newValue
            });
        }
    }
}