using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FindReference.Editor.Common;

// ReSharper disable once CheckNamespace
namespace FindReference.Editor.Engine
{
    public class GetFilePathListTask
    {
        public Task<List<string>> CustomTask { get; }

        public GetFilePathListTask(string path, List<string> fileContainGuid, CancellationToken token)
        {
            _token = token;
            CustomTask = Task.Run(() => GenerateFileList(path, fileContainGuid), token);
        }

        private const string PathPrefix = "Assets/";
        private const float GetFilesProgress = 0.5f;
        private float _progress;
        private readonly CancellationToken _token;

        private void UpdateProgress(float value)
        {
            EventCenter.Instance.Publish(FEventType.GetFilesTask, value);
        }

        private List<string> GenerateFileList(string directory, List<string> whiteList)
        {
            UpdateProgress(0f);
            // 获取所有文件
            var files = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories);

            UpdateProgress(GetFilesProgress);

            return Filter(files, whiteList, false);
        }

        private List<string> Filter(string[] files, List<string> whiteList, bool filterPrefix)
        {
            var result = new List<string>();
            var totalFiles = files.Length;
            for (var i = 0; i < totalFiles; i++)
            {
                _token.ThrowIfCancellationRequested();
                
                var file = files[i];
                if (!filterPrefix || file.StartsWith(PathPrefix))
                {
                    var extension = Path.GetExtension(file);
                    if (whiteList?.Contains(extension) ?? true)
                    {
                        result.Add(file);
                    }
                }
                // 计算并报告进度
                var value = GetFilesProgress + (float)(i + 1) / totalFiles * (1 - GetFilesProgress);
                UpdateProgress(value);
            }
            return result;
        }
    }
}