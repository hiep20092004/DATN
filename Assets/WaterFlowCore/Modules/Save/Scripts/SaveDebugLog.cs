using UnityEngine;

namespace WaterFlow.Core
{
    /// <summary>
    /// Lightweight, QA-friendly logging for the save system.
    /// Toggle at runtime via PlayerPrefs key "SAVE_DEBUG" = 1.
    ///
    /// The enabled state is cached in a plain static bool so that background
    /// save threads can check it without calling PlayerPrefs.GetInt (which is
    /// main-thread-only and throws a UnityException from worker threads).
    ///
    /// Call <see cref="Refresh"/> once from the main thread (done automatically
    /// inside <see cref="Serializer.Init"/>) to pick up the current preference.
    /// Call it again any time you change the PlayerPrefs value at runtime.
    /// </summary>
    public static class SaveDebugLog
    {
        public const string PlayerPrefsKey = "SAVE_DEBUG";

        private static bool _enabled = true;

        /// <summary>
        /// Whether debug logging is active. Safe to read from any thread.
        /// </summary>
        public static bool Enabled => _enabled;

        /// <summary>
        /// Reads PlayerPrefs and caches the enabled flag. Must be called from
        /// the main thread (e.g. from Serializer.Init or SaveController.Init).
        /// </summary>
        public static void Refresh()
        {
            _enabled = PlayerPrefs.GetInt(PlayerPrefsKey, 1) == 1;
        }

        /// <summary>
        /// Convenience: set the flag and persist it to PlayerPrefs (main thread only).
        /// </summary>
        public static void SetEnabled(bool value)
        {
            _enabled = value;
            PlayerPrefs.SetInt(PlayerPrefsKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void Log(string message)
        {
            if (!_enabled) return;
            Debug.Log("[SaveDebug] " + message);
        }

        public static void Warn(string message)
        {
            if (!_enabled) return;
            Debug.LogWarning("[SaveDebug] " + message);
        }

        public static void Error(string message)
        {
            if (!_enabled) return;
            Debug.LogError("[SaveDebug] " + message);
        }
    }
}
