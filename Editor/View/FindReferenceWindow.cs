using System;
using FindReference.Editor.Config;
using System.Collections.Generic;
using FindReference.Editor.Engine;
using FindReference.Editor.EventListener;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FindReference.Editor.View
{
    public class FindReferenceWindow : EditorWindow
    {
        #region Menu Commands

        [MenuItem("Window/FindReference")]
        public static void ShowWindow()
        {
            GetWindow<FindReferenceWindow>("资源引用查找");
        }

        [MenuItem("Assets/FindReference _%#&f")]
        public static void ShowWindowFromAssets()
        {
            var win = GetWindow<FindReferenceWindow>("资源引用查找");
            win.Focus();
        }

        #endregion

        #region Private Data

        private float _taskProgress;
        private string _taskLabel = "";
        private Vector2 _parentsScroll;
        private Vector2 _childrenScroll;

        private static GUIStyle _headerStyle;
        private static GUIStyle _pathStyle;
        private static GUIStyle _sectionStyle;

        private const float ROW_HEIGHT = 38f;

        #endregion

        #region Unity Override Methods

        private void OnEnable()
        {
            EditorApplication.update += EventCenter.Instance.Update;
            EventCenter.Instance.Register(FEventType.GetFilesTask, OnGetFilesTaskProgress);
            EventCenter.Instance.Register(FEventType.ParseTask, OnParseReferencesTaskProgress);
            EventCenter.Instance.Register(FEventType.TaskEnd, OnTaskEnd);
        }

        private void OnDisable()
        {
            EditorApplication.update -= EventCenter.Instance.Update;
            EventCenter.Instance.Clear();
        }

        private void OnSelectionChange()
        {
            Repaint();
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawToolbar();
            DrawProgress();

            GUILayout.Space(4);
            var obj = Selection.activeObject;
            if (obj == null)
            {
                EditorGUILayout.HelpBox("在 Project 视图中选择一个资产", MessageType.Info);
                return;
            }

            DrawSelectedAsset(obj);
            GUILayout.Space(6);

            var path = AssetDatabase.GetAssetPath(obj);
            var guid = AssetDatabase.AssetPathToGUID(path);

            float halfH = (position.height - 160) / 2f;
            DrawSection("被引用（父节点）", guid, true, ref _parentsScroll, halfH);
            GUILayout.Space(4);
            DrawSection("依赖（子节点）", guid, false, ref _childrenScroll, halfH);
        }

        #endregion

        #region Draw Methods

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                bool working = FindReferenceCore.Instance.IsWorking;

                GUI.enabled = !working;
                if (GUILayout.Button("重建数据库", EditorStyles.toolbarButton, GUILayout.Width(90)))
                    StartRefresh();
                GUI.enabled = true;

                if (working)
                {
                    if (GUILayout.Button("取消", EditorStyles.toolbarButton, GUILayout.Width(50)))
                        FindReferenceCore.Instance.CancelRefresh();
                }

                GUILayout.FlexibleSpace();

                var db = FindReferenceCore.Instance.DataBase;
                string dateStr = (db != null && db.LastBuildTime != DateTime.MinValue)
                    ? db.LastBuildTime.ToString("yyyy-MM-dd HH:mm:ss")
                    : "未构建";
                GUILayout.Label("数据库: " + dateStr, EditorStyles.miniLabel);
            }
        }

        private void DrawProgress()
        {
            if (!FindReferenceCore.Instance.IsWorking) return;

            var rect = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
            rect = new Rect(rect.x + 4, rect.y + 2, rect.width - 8, 14);
            EditorGUI.ProgressBar(rect, _taskProgress, _taskLabel);
            Repaint();
        }

        private void DrawSelectedAsset(Object obj)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            var icon = AssetDatabase.GetCachedIcon(path);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                if (icon != null)
                    GUILayout.Label(icon, GUILayout.Width(28), GUILayout.Height(28));

                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label(obj.name, _headerStyle);
                    GUILayout.Label(path, _pathStyle);
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("定位", GUILayout.Width(44), GUILayout.Height(28)))
                {
                    EditorGUIUtility.PingObject(obj);
                    Selection.activeObject = obj;
                }
            }
        }

        private void DrawSection(string title, string guid, bool isParents, ref Vector2 scroll, float height)
        {
            string[] guids = isParents
                ? FindReferenceCore.Instance.QueryParents(guid)
                : FindReferenceCore.Instance.QueryChildren(guid);

            using (new EditorGUILayout.VerticalScope(_sectionStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label($"{title}  ({guids.Length})", _headerStyle);
                }

                GUILayout.Space(2);

                if (guids.Length == 0)
                {
                    EditorGUILayout.HelpBox("无", MessageType.None);
                    return;
                }

                var scrollRect = GUILayoutUtility.GetRect(0, height, GUILayout.ExpandWidth(true));

                int totalItems = guids.Length;
                int visibleItems = Mathf.Max(1, (int)(height / ROW_HEIGHT));
                int maxScroll = Mathf.Max(0, totalItems - visibleItems);

                scroll.y = GUI.VerticalScrollbar(
                    new Rect(scrollRect.xMax - 15, scrollRect.y, 15, height),
                    scroll.y, visibleItems, 0, totalItems);

                var contentRect = new Rect(scrollRect.x, scrollRect.y, scrollRect.width - 15, totalItems * ROW_HEIGHT);
                GUI.BeginClip(scrollRect);

                int startIdx = Mathf.Max(0, (int)(scroll.y));
                int endIdx = Mathf.Min(totalItems, startIdx + visibleItems + 1);

                for (int i = startIdx; i < endIdx; i++)
                {
                    var itemRect = new Rect(0, (i - startIdx) * ROW_HEIGHT, scrollRect.width - 15, ROW_HEIGHT);
                    GUI.BeginGroup(itemRect);
                    DrawAssetRow(guids[i]);
                    GUI.EndGroup();
                }

                GUI.EndClip();
            }
        }

        private static void DrawAssetRow(string guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                EditorGUILayout.HelpBox($"缺失  guid: {guid}", MessageType.Warning);
                return;
            }

            var icon = AssetDatabase.GetCachedIcon(path);
            var fileName = Path.GetFileNameWithoutExtension(path);

            float x = 4;
            float y = 2;
            float iconSize = 18;

            if (icon != null)
            {
                GUI.DrawTexture(new Rect(x, y, iconSize, iconSize), icon);
                x += iconSize + 4;
            }

            float btnWidth = 40;
            float rightPadding = 8;
            float labelWidth = EditorGUIUtility.currentViewWidth - x - btnWidth - rightPadding - 4;
            GUI.Label(new Rect(x, y, labelWidth, 16), fileName, EditorStyles.label);
            GUI.Label(new Rect(x, y + 16, labelWidth, 14), path, _pathStyle);

            if (GUI.Button(new Rect(EditorGUIUtility.currentViewWidth - btnWidth - rightPadding, y + 4, btnWidth, 20), "定位", EditorStyles.miniButton))
            {
                var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (obj != null)
                {
                    EditorGUIUtility.PingObject(obj);
                    Selection.activeObject = obj;
                }
            }
        }

        #endregion

        #region Helpers

        private void StartRefresh()
        {
            _taskProgress = 0f;
            _taskLabel = "准备中...";
            FindReferenceCore.Instance.RefreshDataBase();
        }

        private static void EnsureStyles()
        {
            if (_headerStyle != null) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12
            };

            _pathStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = false
            };

            _sectionStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(6, 6, 6, 6)
            };
        }


        #endregion

        #region Event Callbacks

        private void OnGetFilesTaskProgress(BaseEventData evt)
        {
            if (evt is not TaskProgressUpdateEvent e) return;
            _taskProgress = e.NewProgress;
            _taskLabel = $"搜集文件列表  {(int)(e.NewProgress * 100)}%";
        }

        private void OnParseReferencesTaskProgress(BaseEventData evt)
        {
            if (evt is not TaskProgressUpdateEvent e) return;
            _taskProgress = e.NewProgress;
            _taskLabel = $"解析引用关系  {(int)(e.NewProgress * 100)}%";
        }

        private void OnTaskEnd(BaseEventData _)
        {
            _taskProgress = 0f;
            _taskLabel = "";
            Repaint();
        }

        #endregion
    }
}
