using System.Collections.Generic;
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
            
            if (importedAssets.Length == 0) return;
            
            FindReferenceLogger.Log("检测到资源变化------------------------------------");
            foreach (var str in importedAssets)
            {
                FindReferenceLogger.Log($"importedAssets : {str}");
            }

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
            FindReferenceCore.Instance.ProcessChangedAssets(FindReferenceAssetChangeCache.instance
                .ChangeAssetPaths);
            FindReferenceAssetChangeCache.instance.Clear();
        }
    }

    public class FindReferenceModificationProcessor : AssetModificationProcessor
    {
        private static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
        {
            FindReferenceLogger.Log($"deletedAssets : {assetPath}");
            
            // 删除文件之前，先删除引用关系
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            FindReferenceCore.Instance.ProcessDeleteAsset(guid);

            return AssetDeleteResult.DidNotDelete; // 让unity继续删除这个文件
        }
    }
}