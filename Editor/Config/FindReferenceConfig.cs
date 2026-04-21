using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FindReference.Editor.Config
{
    public static class FindReferenceConfig
    {
        public static bool IsSupportedExtension(string extension)
            => _fileExtSet.Contains(extension);

        private static readonly HashSet<string> _fileExtSet = new(StringComparer.OrdinalIgnoreCase)
        {
            ".anim", ".asmdef", ".asmref", ".asset", ".controller",
            ".colors", ".guiskin", ".mat", ".prefab", ".playable",
            ".preset", ".signal", ".spriteatlas", ".unity", ".vfx",
            ".vfxblock", ".vfxoperator", ".wlt",
            // 新增支持的扩展名
            ".overrideController", ".mask", ".shadervariants"
        };

        // 模式1：键值简写型（guid: <32hex>  |  m_AssetGUID: <32hex>  等）
        public static readonly Regex FindGuidRegex1 = new(
            @"(?:m_AssetGUID|m_Script|m_SourcePrefab|m_CorrespondingSourceObject|m_ObjectReference|m_Texture|texture|guid|GUID|m_SceneGUID)\s*:\s*([0-9a-f]{32})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // 模式2：内联对象型（{fileID: N, guid: XXXX, type: N}）
        public static readonly Regex FindGuidRegex2 = new(
            @"\{\s*fileID\s*:\s*-?\d+\s*,\s*guid\s*:\s*([0-9a-f]{32})\s*,\s*type\s*:\s*\d+\s*\}",
            RegexOptions.Compiled);

        // 模式3：JSON字符串型（"guid": "XXXX"）
        public static readonly Regex FindGuidRegex3 = new(
            @"""guid""\s*:\s*""([0-9a-f]{32})""",
            RegexOptions.Compiled);

        // 合并正则：一次扫描匹配三种格式（方向2：合并正则）
        public static readonly Regex FindGuidRegexAll = new(
            @"(?:(?:m_AssetGUID|m_Script|m_SourcePrefab|m_CorrespondingSourceObject|m_ObjectReference|m_Texture|texture|guid|GUID|m_SceneGUID)\s*:\s*([0-9a-f]{32})|" +
            @"\{\s*fileID\s*:\s*-?\d+\s*,\s*guid\s*:\s*([0-9a-f]{32})\s*,\s*type\s*:\s*\d+\s*\}|" +
            @"""guid""\s*:\s*""([0-9a-f]{32})"")",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // 兼容旧代码的单一正则（已弃用，但保留以防万一）
        public static readonly Regex FindGuidRegex = FindGuidRegex1;

        public static readonly List<string> PathPrefixes = new()
        {
            "Assets",
            "Packages"
        };
    }
}