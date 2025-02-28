using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace FindReference.Editor.Common
{
    public class EventCenter
    {
        private static EventCenter _instance;

        public static EventCenter Instance
        {
            get
            {
                return _instance ??= new EventCenter();
            }
        }

        public void Register<T>(FEventType eventType, Action<T> action)
        {
            FindReferenceLogger.Log($"Register {eventType} {action.Method.Name}");
            if (_listeners.TryGetValue(eventType, out var listenerList))
            {
                if (!listenerList.Contains(action)) listenerList.Add(action);
            }
            else
            {
                _listeners.Add(eventType, new List<object> { action });
            }
        }

        public void UnRegister<T>(FEventType eventType, Action<T> action)
        {
            if (_listeners.TryGetValue(eventType, out var listenerList))
            {
                listenerList.Remove(action);
            }
        }

        public void Publish<T>(FEventType eventType, T data)
        {
            if (!_listeners.TryGetValue(eventType, out var listenerList)) return;
            
            foreach (var listener in listenerList)
            {
                (listener as Action<T>)?.Invoke(data);
            }
        }

        public void Clear()
        {
            _listeners.Clear();
        }

        private readonly Dictionary<FEventType, List<object>> _listeners = new();
    }

}