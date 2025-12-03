using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FindReference.Editor.Config
{
    public static class FindReferenceConfig
    {
        public static readonly List<string> FileExtList =
            new()
            {
                ".anim", 
                ".asmdef",
                ".asmref",
                ".asset", 
                ".controller", 
                ".colors",
                ".guiskin",
                ".mat", 
                ".prefab",
                ".playable",
                ".preset",
                ".signal",
                ".spriteatlas",
                ".unity", 
                ".vfx",
                ".vfxblock",
                ".vfxoperator",
                ".wlt"
            };

        // public static readonly Regex FindGuidRegex = new("(?:m_AssetGUID|guid|GUID|value|m_SceneGUID): ([0-9a-f]{32})");
        public static readonly Regex FindGuidRegex = new("(?:(?:m_AssetGUID|guid|GUID|value|m_SceneGUID)\\s*:\\s*|\"\"guid\"\"\\s*:\\s*\"\")([0-9a-f]{32})(?:\"\")?");
        
        public static readonly List<string> PathPrefixes = new()
        {
            "Assets",
            "Packages"
        };
    }
}