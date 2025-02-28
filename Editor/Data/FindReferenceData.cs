using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace FindReference.Editor.Data
{
    [Serializable]
    public class FindReferenceData : ISerializationCallbackReceiver
    {
        #region Private Data

        [SerializeField] private string guid;
        [SerializeField] private List<string> children = new();
        [SerializeField] private List<string> parents = new();

        [NonSerialized] private HashSet<string> _childrenSet = new();
        [NonSerialized] private HashSet<string> _parentsSet = new();

        #endregion

        #region Properties

        public List<string> Children => children;
        public List<string> Parents => parents;
        public string Guid => guid;

        #endregion

        #region Public APIs

        public FindReferenceData(string guid)
        {
            this.guid = guid;
        }

        public FindReferenceData(string guid, List<string> children, List<string> parents)
        {
            this.guid = guid;
            if (children != null)
            {
                _childrenSet = new HashSet<string>(children);
                this.children = children;
            }

            if (parents != null)
            {
                _parentsSet = new HashSet<string>(parents);
                this.parents = parents;
            }
        }

        public bool AddChild(string child)
        {
            if (!_childrenSet.Add(child)) return false;

            children.Add(child);
            return true;
        }

        public bool DeleteChild(string child)
        {
            return _childrenSet.Remove(child) && children.Remove(child);
        }

        public bool AddParent(string parent)
        {
            if (!_parentsSet.Add(parent)) return false;

            parents.Add(parent);
            return true;
        }

        public bool DeleteParent(string parent)
        {
            return _parentsSet.Remove(parent) && parents.Remove(parent);
        }

        public void ClearParents()
        {
            _parentsSet.Clear();
            parents.Clear();
        }

        public void OnBeforeSerialize()
        {
            parents = _parentsSet.ToList();
            children = _childrenSet.ToList();
        }

        public void OnAfterDeserialize()
        {
            _parentsSet = new HashSet<string>(parents);
            _childrenSet = new HashSet<string>(children);
        }

        #endregion
    }
}