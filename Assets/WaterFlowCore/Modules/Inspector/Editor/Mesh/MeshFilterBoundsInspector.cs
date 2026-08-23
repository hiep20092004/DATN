using UnityEditor;
using UnityEngine;

namespace WaterFlow.Core
{
    [CustomEditor(typeof(MeshFilter))]
    public class MeshFilterBoundsInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
        
            MeshFilter meshFilter = (MeshFilter)target;
            if (meshFilter.sharedMesh)
            {
                Bounds bounds = meshFilter.sharedMesh.bounds;
            
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Mesh Bounds Info", EditorStyles.boldLabel);
            
                EditorGUILayout.LabelField("Center:", bounds.center.ToString("F3"));
                EditorGUILayout.LabelField("Size:", bounds.size.ToString("F3"));
            
                EditorGUILayout.LabelField("Min:", bounds.min.ToString("F3"));
                EditorGUILayout.LabelField("Max:", bounds.max.ToString("F3"));
            }
        }
    }
}