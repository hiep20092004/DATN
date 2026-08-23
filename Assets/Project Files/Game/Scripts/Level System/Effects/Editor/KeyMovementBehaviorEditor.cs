using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    [CustomEditor(typeof(KeyMovementBehavior))]
    public class KeyMovementBehaviorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(10);
            
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to simulate key movement.", MessageType.Info);
            }
            
            GUI.enabled = Application.isPlaying;
            if (GUILayout.Button("Simulate"))
            {
                ((KeyMovementBehavior)target).SimulateInRuntime();
            }

            GUI.enabled = true;
        }
    }
}
