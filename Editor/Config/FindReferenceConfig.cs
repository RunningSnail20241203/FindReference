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
            ".vfxblock", ".vfxoperator", ".wlt"
        };

        // public static readonly Regex FindGuidRegex = new("(?:m_AssetGUID|guid|GUID|value|m_SceneGUID): ([0-9a-f]{32})");
        public static readonly Regex FindGuidRegex = new(
            "(?:(?:m_AssetGUID|guid|GUID|value|m_SceneGUID)\\s*:\\s*|\"\"guid\"\"\\s*:\\s*\"\")([0-9a-f]{32})(?:\"\")?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static readonly List<string> PathPrefixes = new()
        {
            "Assets",
            "Packages"
        };
    }
}