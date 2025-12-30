using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FindReference.Editor.Data
{
    [Serializable]
    public class FindReferenceData : ISerializationCallbackReceiver
    {
        #region Private Data

        [SerializeField] private string guid;
        [SerializeField] private string[] serializedChildren;
        [SerializeField] private string[] serializedParents;

        [NonSerialized] private HashSet<string> _childrenSet = new();
        [NonSerialized] private HashSet<string> _parentsSet = new();


        private bool _childrenDirty;
        private bool _parentsDirty;

        #endregion

        #region Properties

        public HashSet<string> ChildrenSet => _childrenSet;
        public HashSet<string> ParentSet => _parentsSet;
        public string Guid => guid;

        #endregion

        #region Public APIs

        public FindReferenceData(string guid)
        {
            this.guid = guid;
        }

        public FindReferenceData(string guid, string[] serializedChildren, string[] serializedParents)
        {
            this.guid = guid;
            if (serializedChildren != null)
            {
                _childrenSet = new HashSet<string>(serializedChildren);
                _childrenDirty = true;
            }

            if (serializedParents != null)
            {
                _parentsSet = new HashSet<string>(serializedParents);
                _parentsDirty = true;
            }
        }

        public bool AddChild(string child)
        {
            if (!_childrenSet.Add(child)) return false;
            // 延迟序列化更新，只在需要时重建数组
            _childrenDirty = true;
            return true;
        }

        public bool DeleteChild(string child)
        {
            if (!_childrenSet.Remove(child)) return false;
            _childrenDirty = true;
            return true;
        }

        public bool AddParent(string parent)
        {
            if (!_parentsSet.Add(parent)) return false;
            _parentsDirty = true;
            return true;
        }

        public bool DeleteParent(string parent)
        {
            if (!_parentsSet.Remove(parent)) return false;
            _parentsDirty = true;
            return true;
        }

        public void ClearParents()
        {
            _parentsSet.Clear();
            _parentsDirty = true;
        }

        public void OnBeforeSerialize()
        {
            TrySaveChildren();
            TrySaveParents();
        }

        public void OnAfterDeserialize()
        {
            TryLoadChildren();
            TryLoadParents();
        }

        #endregion

        #region Private Methods

        private void TrySaveChildren()
        {
            if (!_childrenDirty) return;
            serializedChildren = _childrenSet.ToArray();
            _childrenDirty = false;
        }

        private void TrySaveParents()
        {
            if (!_parentsDirty) return;
            serializedParents = _parentsSet.ToArray();
            _parentsDirty = false;
        }

        private void TryLoadChildren()
        {
            if (serializedChildren == null) return;
            _childrenSet = new HashSet<string>(serializedChildren);
        }

        private void TryLoadParents()
        {
            if (serializedParents == null) return;
            _parentsSet = new HashSet<string>(serializedParents);
        }

        #endregion
    }
}