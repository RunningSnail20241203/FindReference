using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace FindReference.Editor
{
    [FilePath(AssetPath, FilePathAttribute.Location.ProjectFolder)]
    public class FindReferenceAssetChangeCache : ScriptableSingleton<FindReferenceAssetChangeCache>
    {
        private List<string> _changeAssetPaths = new ();
        private readonly HashSet<string> _changeAssetPathsSet = new();
        private const string AssetPath = "Library/FindReference/AssetsChangeCache.asset";
        
        public List<string> ChangeAssetPaths => _changeAssetPaths;

        public void CacheChangeAssetPaths(string[] assetPaths)
        {
            foreach (var path in assetPaths)
            {
                if (_changeAssetPathsSet.Add(path))
                {
                    _changeAssetPaths.Add(path);
                }
            }
            
            Save(true);
        }

        public void Clear()
        {
            _changeAssetPaths.Clear();
            _changeAssetPathsSet.Clear();
            Save(true);
        }

        protected override void Save(bool saveAsText)
        {
            _changeAssetPaths = _changeAssetPathsSet.ToList();
            base.Save(saveAsText);
        }
    }
}