#if UNITY_EDITOR
using System;
using UnityEditor;

namespace WaterFlow.Game
{
    /// <summary>
    /// Raised when a <see cref="SerializedObject"/> targeting <see cref="SelectedBlock"/> commits changes
    /// outside a host inspector's <see cref="EditorGUI.BeginChangeCheck"/> scope (e.g. popup pickers,
    /// <see cref="LevelFigureEditorWindow"/>).
    /// </summary>
    public static class SelectedBlockEditorCommitEvents
    {
        public static event Action<SerializedObject> AfterSerializedObjectCommitted;

        public static void Raise(SerializedObject serializedObject)
        {
            AfterSerializedObjectCommitted?.Invoke(serializedObject);
        }
    }
}
#endif
