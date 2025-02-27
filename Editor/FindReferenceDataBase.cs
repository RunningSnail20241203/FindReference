using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FindReference.Editor
{
    [FilePath(AssetPath, FilePathAttribute.Location.ProjectFolder)]
    public class FindReferenceDataBase : ScriptableSingleton<FindReferenceDataBase>
    {
        #region Private Data

        [SerializeField] private string versionGuid;
        [SerializeField] private List<FindReferenceData> datas = new();
        private readonly Dictionary<string, FindReferenceData> _referenceDict = new();
        private const string AssetPath = "Library/FindReference/FindReferenceDataBase.asset";
        private bool _isDirty;

        #endregion


        #region Public APIs

        public void SetData(ConcurrentBag<FindReferenceData> referenceArr)
        {
            var reGeTime = EditorApplication.timeSinceStartup;

            BuildReference();
            _isDirty = true;
            return;

            void BuildReference()
            {
                // 将传入的 FindReferenceData 对象存储到字典中，以 Guid 作为键
                foreach (var data in referenceArr)
                {
                    _referenceDict[data.Guid] = data;
                }

                // 清除所有 FindReferenceData 对象已有的引用信息，准备重新构建引用关系
                foreach (var kv in _referenceDict)
                {
                    kv.Value.ClearReferences();
                }

                // 遍历所有 FindReferenceData 对象，根据其依赖项构建引用关系
                var values = _referenceDict.Values.ToList();
                foreach (var value in values)
                {
                    value.Dependencies.ForEach(x =>
                    {
                        if (_referenceDict.TryGetValue(x, out var data))
                        {
                            // 如果依赖项存在于字典中，将当前对象的 Guid 添加到其引用列表中
                            data.AddReference(value.Guid);
                        }
                        else
                        {
                            // 如果依赖项不存在于字典中，创建一个新的 FindReferenceData 对象并添加到字典中
                            var newData = new FindReferenceData(x);
                            newData.AddReference(value.Guid);
                            _referenceDict.TryAdd(x, newData);
                        }
                    });
                }
                FindReferenceLogger.Log($"重建缓存,用时：{EditorApplication.timeSinceStartup - reGeTime}s");
            }
        }

        public List<string> QueryReferences(string guid)
        {
            var ok = _referenceDict.TryGetValue(guid, out var data);
            return ok ? data.References : new List<string>();
        }

        public List<string> QueryDependencies(string guid)
        {
            var ok = _referenceDict.TryGetValue(guid, out var data);
            return ok ? data.Dependencies : new List<string>();
        }

        public void Initialize()
        {
            _referenceDict.Clear();
            datas.ForEach(x => _referenceDict.TryAdd(x.Guid, x));
        }

        public void DeleteAsset(string guid)
        {
            _isDirty |= _referenceDict.Remove(guid, out _);
            _isDirty |= _referenceDict.Aggregate(_isDirty, (current, kv) => current || kv.Value.DeleteReference(guid));

            // if (dirty) Save(true);
        }

        public void Clear()
        {
            _referenceDict.Clear();
            datas.Clear();
            _isDirty = true;
        }

        public void Save()
        {
            if(!_isDirty) return;
            Save(true);
        }

        #endregion

        #region Private Methods

        protected override void Save(bool saveAsText)
        {
            var saveTime = EditorApplication.timeSinceStartup;
            datas = _referenceDict.Values.ToList();
            versionGuid = Guid.NewGuid().ToString();

            base.Save(saveAsText);
            FindReferenceLogger.Log($"保存引用缓存,用时：{EditorApplication.timeSinceStartup - saveTime}s");
        }

        #endregion
    }

    [Serializable]
    public class FindReferenceData : ISerializationCallbackReceiver
    {
        #region Private Data

        [SerializeField] private string guid;
        [SerializeField] private List<string> dependencies = new();
        [SerializeField] private List<string> references = new();

        [NonSerialized] private HashSet<string> _dependenciesSet = new();
        [NonSerialized] private ConcurrentDictionary<string, bool> _referencesSet = new();

        #endregion

        #region Properties

        public List<string> Dependencies => dependencies;
        public List<string> References => references;
        public string Guid => guid;

        #endregion

        #region Public APIs

        public FindReferenceData(string guid)
        {
            this.guid = guid;
        }

        public FindReferenceData(string guid, List<string> dependencies = null, List<string> references = null)
        {
            this.guid = guid;
            if (dependencies != null)
            {
                _dependenciesSet = new HashSet<string>(dependencies);
                this.dependencies = dependencies;
            }

            if (references != null)
            {
                _referencesSet = new ConcurrentDictionary<string, bool>(references.Select(x =>
                    new KeyValuePair<string, bool>(x, true)));
                this.references = references;
            }
        }

        public void AddDependency(string dependency)
        {
            if (_dependenciesSet.Add(dependency))
            {
                dependencies.Add(dependency);
            }
        }

        public void DeleteDependency(string dependency)
        {
            if (_dependenciesSet.Remove(dependency))
            {
                dependencies.Remove(dependency);
            }
        }

        public void AddReference(string reference)
        {
            if (_referencesSet.TryAdd(reference, true))
            {
                // lock (_lock)
                {
                    references.Add(reference);
                }
            }
        }

        public bool DeleteReference(string reference)
        {
            if (_referencesSet.TryRemove(reference, out _))
            {
                // lock (_lock)
                {
                    return references.Remove(reference);
                }
            }

            return false;
        }

        public void ClearReferences()
        {
            _referencesSet.Clear();
            references.Clear();
        }

        public void OnBeforeSerialize()
        {
            references = _referencesSet.Keys.ToList();
            dependencies = _dependenciesSet.ToList();
        }

        public void OnAfterDeserialize()
        {
            _referencesSet = new ConcurrentDictionary<string, bool>(references.Select(x =>
                new KeyValuePair<string, bool>(x, true)));
            _dependenciesSet = new HashSet<string>(dependencies);
        }

        #endregion
    }
}