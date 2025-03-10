using System;
using UnityEngine;

namespace Pumkin.EditorScreenshot
{
    internal static class EditorScreenshotLogger
    {
        const string LogSuffix = "<b>Editor Screenshot</b>"; 
        
        public static void Log(string message)
        {
            Debug.Log($"{LogSuffix}: {message}");
        }

        public static void LogError(string message)
        {
            Debug.LogError($"{LogSuffix}: {message}");
        }

        public static void LogWarning(string message)
        {
            Debug.LogWarning($"{LogSuffix}: {message}");
        }
    }
}