using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace FindReference.Editor
{
    public class FindReferenceParseJob
    {
        public float Progress { get; set; }
        public bool IsDone { get; private set; }

        private Task _task;
        private static readonly Regex Regex = new("(?:m_AssetGUID|guid|value): ([0-9a-f]{32})");
        private readonly List<string> _files;
        private readonly ConcurrentBag<FindReferenceData> _parseResult;
        private readonly int _jobId;
        private CancellationToken _cancellationToken;

        public FindReferenceParseJob(List<string> files, ref ConcurrentBag<FindReferenceData> parseResult, int jobIdx)
        {
            _files = files;
            _parseResult = parseResult;
            _jobId = jobIdx;
        }

        public void Start(CancellationToken cancellationToken)
        {
            var startTime = DateTime.Now.Ticks;
            _cancellationToken = cancellationToken;
            _task = Task.Run(Parse, cancellationToken);
            _task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    if (t.Exception == null) return;
                    foreach (var innerEx in t.Exception.InnerExceptions)
                    {
                        FindReferenceLogger.LogError(innerEx is OperationCanceledException ? $"【{_jobId}】：任务已取消" : $"【{_jobId}】：发生异常: {innerEx.Message}");
                    }
                }
                else if (t.IsCanceled)
                {
                    FindReferenceLogger.LogError($"【{_jobId}】：任务已取消");
                }
                else
                {
                    FindReferenceLogger.Log($"【{_jobId}】：解析结束~ 耗时：{1f * (DateTime.Now.Ticks- startTime) / TimeSpan.TicksPerSecond}s");
                }
            }, cancellationToken);
        }

        private string ConvertPath2Guid(string s)
        {
            var metaPath = s + ".meta";
            if (!File.Exists(metaPath))
            {
                FindReferenceLogger.LogError($"Meta 文件不存在: {metaPath}");
                return string.Empty;
            }
            using var metaSr = new StreamReader(metaPath);
            _ = metaSr.ReadLine();
            var sL = metaSr.ReadLine();
            return !string.IsNullOrEmpty(sL) ? Regex.Match(sL).Groups[1].Value : string.Empty;
        }

        private void Parse()
        {
            FindReferenceLogger.Log($"【{_jobId}】：解析开始~,需要处理{_files.Count}个文件");
            IsDone = false;

            var idx = 0;
            _files.ForEach(x =>
            {
                if (_cancellationToken.IsCancellationRequested)
                {
                    FindReferenceLogger.Log($"【{_jobId}】：解析任务已取消");
                    IsDone = true;
                    _cancellationToken.ThrowIfCancellationRequested();
                }
                try
                {
                    var set = new HashSet<string>(); // 记录依赖集合
                    using (var sr = new StreamReader(x))
                    {
                        while (sr.ReadLine() is {} line) // 逐行解析
                        {
                            var matches = Regex.Matches(line);
                            foreach (Match match in matches)
                            {
                                set.Add(match.Groups[1].Value);
                            }
                        }
                    }

                    var guid = ConvertPath2Guid(x);
                    if (!string.IsNullOrEmpty(guid))
                    {
                        var dependencies = set.ToList();
                        var data = new FindReferenceData(guid, dependencies);
                        _parseResult.Add(data);
                    }
                    else
                    {
                        FindReferenceLogger.LogError($"没有找到guid, 路径为{x}");
                    }
                }
                catch(Exception ex)
                {
                    FindReferenceLogger.LogError($"解析文件 {x} 时出错: {ex.Message}");
                }

                Progress = (float)idx++ / _files.Count;
            });
            Progress = 1f;
            IsDone = true;
        }
    }
}