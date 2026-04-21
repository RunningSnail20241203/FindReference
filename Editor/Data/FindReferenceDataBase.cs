using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using FindReference.Editor.Common;
using UnityEditor;
using UnityEngine;

namespace FindReference.Editor.Data
{
    [FilePath(AssetPath, FilePathAttribute.Location.ProjectFolder)]
    [InitializeOnLoad]
    public class FindReferenceDataBase : ScriptableSingleton<FindReferenceDataBase>
    {
        #region Private Data

        [SerializeField] private List<FindReferenceData> dataList = new();
        [SerializeField] private List<string> mtimeFiles = new();       // mtime cache: 文件路径列表
        [SerializeField] private List<long> mtimeTicks = new();         // mtime cache: 对应的修改时间(ticks)
        private readonly ConcurrentDictionary<string, FindReferenceData> _referenceDict = new();
        private readonly object _listenersLock = new();
        private Dictionary<string, long> _mtimeCacheSnapshot;           // 缓存 mtime 字典避免每次重建
        private const string AssetPath = "Library/FindReference/FindReferenceDataBase.asset";
        private bool _isDirty;

        #endregion


        #region Public APIs

        public void SetData(List<FindReferenceData> referenceArr, Dictionary<string, long> newMtimes = null)
        {
            var reGeTime = EditorApplication.timeSinceStartup;
            BuildReference();
            FindReferenceLogger.Log($"重建缓存,用时：{EditorApplication.timeSinceStartup - reGeTime}s");
            return;

            void BuildReference()
            {
                _referenceDict.Clear();

                // Phase A: 并行写入字典
                Parallel.ForEach(referenceArr, data => _referenceDict[data.Guid] = data);

                // Phase B: 并行构建倒排索引 child -> parents（HashSet 安全隔离在独立对象中）
                var childToParents = new ConcurrentDictionary<string, ConcurrentBag<string>>();
                Parallel.ForEach(_referenceDict.Values, node =>
                {
                    foreach (var childGuid in node.ChildrenSet)
                    {
                        childToParents.GetOrAdd(childGuid, _ => new ConcurrentBag<string>()).Add(node.Guid);
                    }
                });

                // Phase C: 顺序应用 parent 链接（保证 HashSet<string> 写入线程安全）
                foreach (var kvp in childToParents)
                {
                    if (!_referenceDict.TryGetValue(kvp.Key, out var childNode))
                    {
                        childNode = new FindReferenceData(kvp.Key);
                        _referenceDict.TryAdd(kvp.Key, childNode);
                    }
                    foreach (var parentGuid in kvp.Value)
                    {
                        _isDirty |= childNode.AddParent(parentGuid);
                    }
                }

                // 更新 mtime 缓存
                if (newMtimes != null)
                {
                    UpdateMtimeCache(newMtimes);
                }

                Save();
            }
        }

        public void UpdateData(List<FindReferenceData> referenceArr)
        {
            foreach (var data in referenceArr)
            {
                if (_referenceDict.TryGetValue(data.Guid, out var dataInDict))
                {
                    // 删除旧 data 的 children 对其他节点的 parent 关系
                    _isDirty |= DeleteChildRelation(dataInDict);
                }

                // 用新 data 替换字典中的记录
                _referenceDict[data.Guid] = data;

                // 对新 data 的 children 建立 parent 关系
                _isDirty |= UpdateChildRelation(data);
            }

            Save();
        }

        public string[] QueryParents(string guid)
        {
            var ok = _referenceDict.TryGetValue(guid, out var data);
            return ok ? data.ParentSet.ToArray() : Array.Empty<string>();
        }

        public string[] QueryChildren(string guid)
        {
            var ok = _referenceDict.TryGetValue(guid, out var data);
            return ok ? data.ChildrenSet.ToArray() : Array.Empty<string>();
        }

        public void Initialize()
        {
            _referenceDict.Clear();
            dataList.ForEach(x => _referenceDict.TryAdd(x.Guid, x));
        }

        public IReadOnlyDictionary<string, long> GetMtimeCache()
        {
            if (_mtimeCacheSnapshot != null) return _mtimeCacheSnapshot;
            _mtimeCacheSnapshot = new Dictionary<string, long>(mtimeFiles.Count);
            for (int i = 0; i < mtimeFiles.Count && i < mtimeTicks.Count; i++)
            {
                _mtimeCacheSnapshot[mtimeFiles[i]] = mtimeTicks[i];
            }
            return _mtimeCacheSnapshot;
        }

        public IReadOnlyDictionary<string, FindReferenceData> GetReferenceDataDict()
        {
            return _referenceDict;
        }

        private void UpdateMtimeCache(Dictionary<string, long> newMtimes)
        {
            _mtimeCacheSnapshot = null; // 失效缓存
            mtimeFiles.Clear();
            mtimeTicks.Clear();
            foreach (var kvp in newMtimes)
            {
                mtimeFiles.Add(kvp.Key);
                mtimeTicks.Add(kvp.Value);
            }
            _isDirty = true;
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
            Save();
        }

        public void Clear()
        {
            _referenceDict.Clear();
            dataList.Clear();
            _isDirty = true;
        }

        #endregion

        #region Override Methods

        protected override void Save(bool saveAsText)
        {
            var saveTime = EditorApplication.timeSinceStartup;
            dataList = _referenceDict.Values.ToList();

            base.Save(saveAsText);
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
            _isDirty = false;
            Save(true);
        }

        /// <summary>
        /// 更新子节点关系
        /// </summary>
        /// <param name="node"></param>
        private bool UpdateChildRelation(FindReferenceData node)
        {
            var dirty = false;
            foreach (var x in node.ChildrenSet)
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
            }
            return dirty;
        }

        /// <summary>
        /// 删除子节点对自己的引用
        /// </summary>
        /// <param name="data"></param>
        private bool DeleteChildRelation(FindReferenceData data)
        {
            var dirty = false;
            foreach (var childGuid in data.ChildrenSet)
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
            foreach (var parentGuid in data.ParentSet)
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