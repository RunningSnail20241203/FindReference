using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using FindReference.Editor.Common;

// ReSharper disable once CheckNamespace
namespace FindReference.Editor.EventListener
{
    /// <summary>
    /// 子线程抛出的消息，主线程监听
    /// </summary>
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

        private readonly Dictionary<FEventType, List<Action<BaseEventData>>> _listeners = new();
        private readonly ConcurrentQueue<(FEventType, BaseEventData)> _eventQueue = new();
        private readonly object _listenersLock = new();

        public void Register(FEventType eventType, Action<BaseEventData> action)
        {
            lock (_listenersLock)
            {
                // FindReferenceLogger.Log($"Register {eventType} {action.Method.Name}");
                if (_listeners.TryGetValue(eventType, out var listenerList))
                {
                    if (!listenerList.Contains(action)) listenerList.Add(action);
                }
                else
                {
                    _listeners.Add(eventType, new List<Action<BaseEventData>> { action });
                }
            }
        }

        public void UnRegister(FEventType eventType, Action<BaseEventData> action)
        {
            lock (_listenersLock)
            {
                if (_listeners.TryGetValue(eventType, out var listenerList))
                {
                    if (listenerList.Remove(action))
                    {
                        // FindReferenceLogger.Log($"UnRegister {eventType} {action.Method.Name}");
                    }
                }
            }
        }

        public void Clear()
        {
            lock (_listenersLock)
            {
                _listeners.Clear();
            }
            _eventQueue.Clear();
        }

        public void Update()
        {
            while (_eventQueue.TryDequeue(out var evt))
            {
                lock (_listenersLock)
                {
                    if (!_listeners.TryGetValue(evt.Item1, out var listenerList)) continue;
                    foreach (var listener in listenerList)
                    {
                        try
                        {
                            listener.Invoke(evt.Item2);
                        }
                        catch (Exception e)
                        {
                            FindReferenceLogger.LogError($"{e.Message}");
                        }
                    }
                }
            }
        }
    }
}