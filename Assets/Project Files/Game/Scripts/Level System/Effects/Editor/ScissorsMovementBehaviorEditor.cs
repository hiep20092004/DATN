using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    [CustomEditor(typeof(ScissorsMovementBehavior))]
    public class ScissorsMovementBehaviorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(10);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to simulate scissors movement.", MessageType.Info);
            }

            GUI.enabled = Application.isPlaying;
            if (GUILayout.Button("Simulate"))
            {
                ((ScissorsMovementBehavior)target).SimulateInRuntime();
            }

            GUI.enabled = true;
        }
    }
}
