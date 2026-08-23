using UnityEditor;

namespace WaterFlow.Game
{
    internal static class LevelEditorTestPlaySession
    {
        private const string SESSION_STARTED_KEY = "WaterFlow.Game.LevelEditor.TestPlaySessionStarted";

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            if (!SessionState.GetBool(SESSION_STARTED_KEY, false))
                LevelDatabase.ClearEditorPlayModeLevelOverride();
        }

        internal static void MarkStarted()
        {
            SessionState.SetBool(SESSION_STARTED_KEY, true);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingPlayMode)
                return;

            SessionState.SetBool(SESSION_STARTED_KEY, false);
            LevelDatabase.ClearEditorPlayModeLevelOverride();
        }
    }
}
