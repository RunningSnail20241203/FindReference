using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace FindReference.Editor
{
    [FilePath(AssetPath, FilePathAttribute.Location.ProjectFolder)]
    public class FindReferenceAssetChangeCache : ScriptableSingleton<FindReferenceAssetChangeCache>
    {
        [SerializeField] private List<string> changeAssetPaths = new();
        private readonly HashSet<string> _changeAssetPathsSet = new();
        private const string AssetPath = "Library/FindReference/AssetsChangeCache.asset";

        public List<string> ChangeAssetPaths => changeAssetPaths;

        public void CacheChangeAssetPaths(string[] assetPaths)
        {
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
            Save(true);
        }

        protected override void Save(bool saveAsText)
        {
            changeAssetPaths = _changeAssetPathsSet.ToList();
            base.Save(saveAsText);
        }
    }
}