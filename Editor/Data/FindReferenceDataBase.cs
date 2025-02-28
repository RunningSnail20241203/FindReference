using System.Collections.Generic;
using System.Linq;
using FindReference.Editor.Common;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace FindReference.Editor.Data
{
    [FilePath(AssetPath, FilePathAttribute.Location.ProjectFolder)]
    [InitializeOnLoad]
    public class FindReferenceDataBase : ScriptableSingleton<FindReferenceDataBase>
    {
        #region Private Data

        [SerializeField] private List<FindReferenceData> datas = new();
        private readonly Dictionary<string, FindReferenceData> _referenceDict = new();
        private const string AssetPath = "Library/FindReference/FindReferenceDataBase.asset";
        private bool _isDirty;

        #endregion


        #region Public APIs

        public void SetData(List<FindReferenceData> referenceArr)
        {
            var reGeTime = EditorApplication.timeSinceStartup;
            BuildReference();
            FindReferenceLogger.Log($"重建缓存,用时：{EditorApplication.timeSinceStartup - reGeTime}s");
            return;

            void BuildReference()
            {
                _referenceDict.Clear();

                // 将传入的 FindReferenceData 对象存储到字典中，以 Guid 作为键
                foreach (var data in referenceArr)
                {
                    _referenceDict[data.Guid] = data;
                }

                // 遍历所有 FindReferenceData 对象，构建子节点引用关系
                var values = _referenceDict.Values.ToList();
                foreach (var value in values)
                {
                    UpdateChildRelation(value);
                }
                _isDirty = true;
            }
        }

        public void UpdateData(List<FindReferenceData> referenceArr)
        {
            foreach (var data in referenceArr)
            {
                if (!_referenceDict.TryGetValue(data.Guid, out var dataInDict))
                {
                    _referenceDict.Add(data.Guid, data);
                    _isDirty = true;
                }
                else
                {
                    _isDirty |= DeleteChildRelation(dataInDict);
                }
                _isDirty |= UpdateChildRelation(data);
            }
        }

        public List<string> QueryParents(string guid)
        {
            var ok = _referenceDict.TryGetValue(guid, out var data);
            return ok ? data.Parents : new List<string>();
        }

        public List<string> QueryChildren(string guid)
        {
            var ok = _referenceDict.TryGetValue(guid, out var data);
            return ok ? data.Children : new List<string>();
        }

        public void Initialize()
        {
            _referenceDict.Clear();
            datas.ForEach(x => _referenceDict.TryAdd(x.Guid, x));
        }

        public void DeleteAsset(string guid)
        {
            _isDirty = _referenceDict.Remove(guid, out var data);
            if (_isDirty)
            {
                DeleteChildRelation(data);
                DeleteParentRelation(data);
            }
            else
            {
                // todo 性能优化 考虑是否有必要删除末端节点的引用关系，可以考虑不显示即可，或者延迟到其父节点更新时删除
                foreach (var kv in _referenceDict)
                {
                    _isDirty |= kv.Value.DeleteChild(guid);
                }
            }
        }

        public void Clear()
        {
            _referenceDict.Clear();
            datas.Clear();
            _isDirty = true;
        }

        #endregion

        #region Override Methods

        protected override void Save(bool saveAsText)
        {
            var saveTime = EditorApplication.timeSinceStartup;
            datas = _referenceDict.Values.ToList();

            base.Save(saveAsText);
            _isDirty = false;
            FindReferenceLogger.Log($"保存引用缓存,用时：{EditorApplication.timeSinceStartup - saveTime}s");
        }

        #endregion

        #region Private Methods

        static FindReferenceDataBase()
        {
            EditorApplication.quitting -= OnEditorQuitting;
            EditorApplication.quitting += OnEditorQuitting;
        }

        private static void OnEditorQuitting()
        {
            instance.Save();
        }

        private void Save()
        {
            if (!_isDirty) return;
            Save(true);
        }

        /// <summary>
        /// 更新子节点关系
        /// </summary>
        /// <param name="node"></param>
        private bool UpdateChildRelation(FindReferenceData node)
        {
            var dirty = false;
            node.Children.ForEach(x =>
            {
                if (_referenceDict.TryGetValue(x, out var data))
                {
                    // 如果子节点存在于字典中，将当前对象的 Guid 添加到其引用列表中
                    dirty |= data.AddParent(node.Guid);
                }
                else
                {
                    // 如果子节点不存在于字典中，创建一个新的 FindReferenceData 对象并添加到字典中
                    // 对于末端节点：json、png之类的，不在解析类型中，所以不会有 FindReferenceData 对象
                    var newData = new FindReferenceData(x);
                    newData.AddParent(node.Guid);
                    _referenceDict.TryAdd(x, newData);
                    dirty = true;
                }
            });
            return dirty;
        }

        /// <summary>
        /// 删除子节点对自己的引用
        /// </summary>
        /// <param name="data"></param>
        private bool DeleteChildRelation(FindReferenceData data)
        {
            var dirty = false;
            foreach (var childGuid in data.Children)
            {
                if (_referenceDict.TryGetValue(childGuid, out var child))
                {
                    dirty |= child.DeleteParent(data.Guid);
                }
            }
            return dirty;
        }

        /// <summary>
        /// 删除父节点对自己的引用
        /// </summary>
        /// <param name="data"></param>
        private void DeleteParentRelation(FindReferenceData data)
        {
            foreach (var parentGuid in data.Parents)
            {
                if (_referenceDict.TryGetValue(parentGuid, out var parent))
                {
                    _isDirty |= parent.DeleteChild(data.Guid);
                }
            }
        }

        #endregion
    }
}