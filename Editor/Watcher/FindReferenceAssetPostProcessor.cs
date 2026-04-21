using System.Collections.Generic;
using System.Linq;
using FindReference.Editor.Common;
using FindReference.Editor.Engine;
using UnityEditor;

// ReSharper disable once CheckNamespace
namespace FindReference.Editor.Watcher
{
    [InitializeOnLoad]
    public class FindReferenceAssetPostProcessor : AssetPostprocessor
    {
        private static List<string> _importedAssets;

        static FindReferenceAssetPostProcessor()
        {
            AssemblyReloadEvents.afterAssemblyReload += OnAssemblyReload;
        }

        public static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            // 处理外部删除（git rm 等）— 直接从缓存中移除，不走 cache 积累
            foreach (var assetPath in deletedAssets)
            {
                var guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (!string.IsNullOrEmpty(guid))
                {
                    FindReferenceLogger.Log($"检测到资源被删除（外部）：{assetPath} (guid: {guid})");
                    FindReferenceCore.Instance.ProcessDeleteAsset(guid);
                }
            }

            // 处理移动（旧路径的引用已无效，新路径在 importedAssets 中）
            foreach (var oldPath in movedFromAssetPaths)
            {
                var guid = AssetDatabase.AssetPathToGUID(oldPath);
                if (!string.IsNullOrEmpty(guid))
                {
                    FindReferenceLogger.Log($"检测到资源被移动（旧位置）：{oldPath} (guid: {guid})");
                    FindReferenceCore.Instance.ProcessDeleteAsset(guid);
                }
            }

            if (importedAssets.Length == 0) return;

            FindReferenceLogger.Log($"检测到 {importedAssets.Length} 个资源变化");
            // foreach (var str in importedAssets)
            // {
            //     FindReferenceLogger.Log($"importedAssets : {str}");
            // }

            FindReferenceAssetChangeCache.instance.CacheChangeAssetPaths(importedAssets);
            if (EditorApplication.isCompiling)
            {
                FindReferenceLogger.Log("正在编译中,稍后处理资源变化");
            }
            else
            {
                FindReferenceLogger.Log("开始处理资源变化");
                ProcessAssets();
            }
        }

        private static void OnAssemblyReload()
        {
            if (FindReferenceAssetChangeCache.instance.ChangeAssetPaths.Count > 0)
            {
                FindReferenceLogger.Log("编译完成，接着处理刚才那些新增或者修改的资源");
                ProcessAssets();
            }
        }

        private static void ProcessAssets()
        {
            var paths = FindReferenceAssetChangeCache.instance.ChangeAssetPaths.ToList();  // 快照
            FindReferenceAssetChangeCache.instance.Clear();  // 先清，新增的下次再处理
            FindReferenceCore.Instance.ProcessChangedAssets(paths);
        }
    }

    public class FindReferenceModificationProcessor : AssetModificationProcessor
    {
        private static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
        {
            FindReferenceLogger.Log($"检测到资源 {assetPath} 被删除");
            
            // 删除文件之前，先删除引用关系
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            FindReferenceCore.Instance.ProcessDeleteAsset(guid);

            return AssetDeleteResult.DidNotDelete; // 让unity继续删除这个文件
        }
    }
}