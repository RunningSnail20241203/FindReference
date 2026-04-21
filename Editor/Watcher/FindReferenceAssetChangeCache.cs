using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FindReference.Editor
{
    [FilePath(AssetPath, FilePathAttribute.Location.ProjectFolder)]
    public class FindReferenceAssetChangeCache : ScriptableSingleton<FindReferenceAssetChangeCache>
    {
        [SerializeField] private List<string> changeAssetPaths = new();
        private readonly HashSet<string> _changeAssetPathsSet = new();
        private bool _hashSetInitialized;
        private const string AssetPath = "Library/FindReference/AssetsChangeCache.asset";

        public List<string> ChangeAssetPaths => changeAssetPaths;

        public void CacheChangeAssetPaths(string[] assetPaths)
        {
            // domain reload 后，HashSet 被重新创建（空），需要从 List 恢复
            if (!_hashSetInitialized && changeAssetPaths.Count > 0)
            {
                foreach (var p in changeAssetPaths)
                    _changeAssetPathsSet.Add(p);
                _hashSetInitialized = true;
            }

            foreach (var path in assetPaths)
            {
                if (_changeAssetPathsSet.Add(path))
                {
                    changeAssetPaths.Add(path);
                }
            }

            Save(true);
        }

        public void Clear()
        {
            changeAssetPaths.Clear();
            _changeAssetPathsSet.Clear();
            _hashSetInitialized = false;
            Save(true);
        }

        protected override void Save(bool saveAsText)
        {
            // 不用 HashSet 覆盖 List，List 已在 CacheChangeAssetPaths/Clear 中保持同步
            base.Save(saveAsText);
        }
    }
}