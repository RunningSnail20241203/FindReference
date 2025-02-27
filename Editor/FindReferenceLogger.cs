using UnityEngine;

namespace FindReference.Editor
{
    public class FindReferenceLogger
    {
        private static bool _isEnableLog = true;
        public static void Log(string msg)
        {
            if (_isEnableLog)
            {
                Debug.Log($"【FindReference】：{msg}");
            }
        }

        public static void LogError(string msg)
        {
            if (_isEnableLog)
            {
                Debug.LogError($"【FindReference】：{msg}");
            }
        }
    }
}