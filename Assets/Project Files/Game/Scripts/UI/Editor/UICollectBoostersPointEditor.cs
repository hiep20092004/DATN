#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UICollectBoostersPoint))]
public class UICollectBoostersPointEditor : UnityEditor.Editor
{
    private const string TestMarkerName = "[CollectEndOffset Test]";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var point = (UICollectBoostersPoint)target;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Editor Test", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            $"World position: {point.GetCollectEndPosition()}",
            MessageType.None);

        if (GUILayout.Button("Create / Update Offset Test Marker"))
        {
            CreateOrUpdateTestMarker(point);
        }
    }

    private static void CreateOrUpdateTestMarker(UICollectBoostersPoint point)
    {
        Transform parent = point.transform;
        Transform existing = parent.Find(TestMarkerName);
        GameObject marker;

        var offsetProp = new SerializedObject(point).FindProperty("collectEndOffset");
        Vector2 offset = offsetProp != null ? offsetProp.vector2Value : Vector2.zero;
        var localPos = new Vector3(offset.x, offset.y, 0f);

        if (existing != null)
        {
            marker = existing.gameObject;
            Undo.RecordObject(marker.transform, "Update Collect End Offset Test Marker");
        }
        else
        {
            marker = new GameObject(TestMarkerName);
            Undo.RegisterCreatedObjectUndo(marker, "Create Collect End Offset Test Marker");
            Undo.SetTransformParent(marker.transform, parent, "Parent Collect End Offset Test Marker");
        }

        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = localPos;
        marker.transform.localRotation = Quaternion.identity;
        marker.transform.localScale = Vector3.one;

        Selection.activeGameObject = marker;
        EditorGUIUtility.PingObject(marker);
    }
}
#endif
