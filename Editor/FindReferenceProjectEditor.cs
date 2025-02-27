using UnityEditor;
using UnityEngine;

namespace FindReference.Editor
{
    public static class FindReferenceProjectEditor
    {
        [InitializeOnLoadMethod] //这个特性作用：C#编译完成后首先执行该方法
        private static void InitializeOnLoadMethod()
        {
            var style = new GUIStyle
            {
                fontSize = 9,
                normal = {textColor = Color.white}
            };
            EditorApplication.projectWindowItemOnGUI = (guid, selectionRect) =>
            {
                var count = FindReferenceCore.Instance.QueryReferencesCount(guid);
                if (count == 0) return;
                selectionRect.x -= 13f;
                selectionRect.y += 3f;
                GUI.Label(selectionRect, count.ToString(), style);
            };
        }
    }
}