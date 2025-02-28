using System.Collections.Generic;
using System.Threading;
using FindReference.Editor.Common;
using FindReference.Editor.Engine;
using FindReference.Editor.EventListener;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// ReSharper disable once CheckNamespace
namespace FindReference.Editor.View
{
    public class FindReferenceWindow : EditorWindow
    {
        #region Menu Commands

        [MenuItem("Window/FindReference")]
        public static void ShowWindow()
        {
            GetWindow<FindReferenceWindow>("资源引用查找工具");
        }

        [MenuItem("Assets/FindReference _%#&f")]
        public static void ShowWindow1()
        {
            GetWindow<FindReferenceWindow>("资源引用查找工具");
        }

        [MenuItem("Window/HideProgressBar")]
        public static void HideProgressBar()
        {
            EditorUtility.ClearProgressBar();
        }

        #endregion

        #region Private Data

        private Vector2 _scrollPos;
        private CancellationTokenSource _cancellationTokenSource;

        #endregion

        #region Unity Override Methods

        private void OnGUI()
        {
            _scrollPos = GUILayout.BeginScrollView(_scrollPos);
            DrawWindow();
            GUILayout.EndScrollView();
        }

        #endregion


        #region Private Methods

        private void DrawWindow()
        {
            DrawRefreshBtn();

            var obj = Selection.activeObject;
            if (obj == null)
            {
                GUILayout.Label("请先选择一个物体");
            }
            else
            {
                EditorGUILayout.ObjectField(new GUIContent("当前选中物体:"), obj, typeof(Object), true);
                var path = AssetDatabase.GetAssetPath(obj);
                var guid = AssetDatabase.AssetPathToGUID(path);
                DrawReferencesList(guid);
                DrawDependenciesList(guid);
            }
        }

        private void DrawReferencesList(string guid)
        {
            var refs = FindReferenceCore.Instance.QueryParents(guid);
            GUILayout.Label($"被{refs.Count}个资源直接引用");

            DrawObjectList(refs);
        }

        private void DrawDependenciesList(string guid)
        {
            var refs = FindReferenceCore.Instance.QueryChildren(guid);
            GUILayout.Label($"直接引用了{refs.Count}个资源");

            DrawObjectList(refs);
        }

        private static void DrawObjectList(List<string> guids)
        {
            guids.ForEach(x =>
            {
                var path = AssetDatabase.GUIDToAssetPath(x);
                var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (obj == null)
                {
                    GUILayout.Label($"guid:{x} path:{path} is missing!");
                }
                else
                {
                    EditorGUILayout.ObjectField(new GUIContent(obj.name), obj, typeof(Object), true);
                }
            });
        }

        private void DrawRefreshBtn()
        {
            var text = FindReferenceCore.Instance.IsWorking ? GetWorkingText() : "重建引用数据库";
            if (!FindReferenceCore.Instance.IsWorking)
            {
               
                if (GUILayout.Button(text))
                {
                     EditorApplication.update += EventCenter.Instance.Update;
                     EventCenter.Instance.Register(FEventType.GetFilesTask, OnGetFilesTaskProgress);
                     EventCenter.Instance.Register(FEventType.ParseTask, OnParseReferencesTaskProgress);
                     EventCenter.Instance.Register(FEventType.TaskEnd, OnTaskEnd);
                     
                     FindReferenceCore.Instance.RefreshDataBase();
                }
            }
            else
            {
                GUILayout.Label(text);
            }

            return;

            string GetWorkingText()
            {
                return "正在重建数据库";
            }
        }


        #endregion

        #region Listener Callback

        private void OnGetFilesTaskProgress(BaseEventData evt)
        {
            if (evt is not TaskProgressUpdateEvent evt1) return;
            
            // FindReferenceLogger.Log($"OnGetFilesTaskProgress:{evt1.NewProgress}");
            if (EditorUtility.DisplayCancelableProgressBar("正在搜集文件列表", "", evt1.NewProgress))
            {
                _cancellationTokenSource.Cancel();
            }
        }

        private void OnParseReferencesTaskProgress(BaseEventData evt)
        {
            if (evt is not TaskProgressUpdateEvent evt1) return;

            // FindReferenceLogger.Log($"OnParseReferencesTaskProgress:{evt1.NewProgress}");
            if (EditorUtility.DisplayCancelableProgressBar("正在解析文件引用关系", "", evt1.NewProgress))
            {
                _cancellationTokenSource.Cancel();
            }
        }

        private void OnTaskEnd(BaseEventData obj)
        {
            EditorUtility.ClearProgressBar();

            EditorApplication.update -= EventCenter.Instance.Update;
            EventCenter.Instance.Clear();
        }

        #endregion
    }
}