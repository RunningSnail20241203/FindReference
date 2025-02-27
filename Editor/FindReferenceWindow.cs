using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FindReference.Editor
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
            DrawSaveBtn();

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
            var refs = FindReferenceCore.Instance.QueryReferences(guid);
            GUILayout.Label($"被{refs.Count}个资源直接引用");

            DrawObjectList(refs);
        }

        private void DrawDependenciesList(string guid)
        {
            var refs = FindReferenceCore.Instance.QueryDependencies(guid);
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

        private void DrawSaveBtn()
        {
            if (GUILayout.Button("保存缓存到磁盘"))
            {
                FindReferenceCore.Instance.Save();
            }
        }

        #endregion
    }
}