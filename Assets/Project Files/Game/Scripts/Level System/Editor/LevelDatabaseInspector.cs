using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    [CustomEditor(typeof(LevelDatabase))]
    public class LevelDatabaseInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            LevelDatabase database = (LevelDatabase)target;

            if (LevelDatabaseReferenceHealer.HasStaleReferences(database))
            {
                EditorGUILayout.HelpBox(
                    "Some prefab references were broken by an asset reload (typically a git branch switch that deleted and restored prefabs). The asset file on disk is still correct.",
                    MessageType.Warning);

                if (GUILayout.Button("Resolve Broken Prefab References"))
                {
                    LevelDatabaseReferenceHealer.Heal(database);
                    serializedObject.Update();
                }

                EditorGUILayout.Space();
            }

            DrawDefaultInspector();
        }
    }
}
