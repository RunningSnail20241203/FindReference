// ReSharper disable once CheckNamespace
namespace FindReference.Editor.EventListener
{
    public class TaskProgressUpdateEvent : BaseEventData
    {
        public float OldProgress;
        public float NewProgress;
    }
}