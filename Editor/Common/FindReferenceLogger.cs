using UnityEngine;

// ReSharper disable once CheckNamespace
namespace FindReference.Editor.Common
{
    public static class FindReferenceLogger
    {
        private const bool IsEnableLog = true;
        public static void Log(string msg)
        {
            if (IsEnableLog)
            {
                Debug.Log($"【FindReference】：{msg}");
            }
        }

        public static void LogError(string msg)
        {
            if (IsEnableLog)
            {
                Debug.LogError($"【FindReference】：{msg}");
            }
        }
    }
}