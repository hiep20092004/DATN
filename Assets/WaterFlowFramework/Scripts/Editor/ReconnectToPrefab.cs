#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class FullPrefabRelinker : EditorWindow
{
    private GameObject prefab;

    [MenuItem("Tools/Full Prefab Relinker (Preserve All References)")]
    static void Init()
    {
        var window = GetWindow<FullPrefabRelinker>();
        window.titleContent = new GUIContent("Full Prefab Relinker");
        window.Show();
    }

    void OnGUI()
    {
        GUILayout.Label("Relink Prefab (giữ reference đến cả Component)", EditorStyles.boldLabel);
        prefab = (GameObject)EditorGUILayout.ObjectField("Prefab:", prefab, typeof(GameObject), false);

        if (prefab == null)
        {
            EditorGUILayout.HelpBox("Chọn prefab trước khi relink.", MessageType.Info);
            return;
        }

        if (GUILayout.Button("Gán prefab cho các object đang chọn"))
        {
            RelinkSelectedObjects();
        }
    }

    void RelinkSelectedObjects()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects.Length == 0)
        {
            Debug.LogWarning("Hãy chọn ít nhất một object trong Hierarchy.");
            return;
        }

        Dictionary<Object, Object> replaceMap = new();

        foreach (GameObject oldObj in selectedObjects)
        {
            Transform parent = oldObj.transform.parent;
            Vector3 pos = oldObj.transform.position;
            Quaternion rot = oldObj.transform.rotation;
            Vector3 scale = oldObj.transform.localScale;
            string name = oldObj.name;

            // Instantiate prefab
            GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            newObj.transform.SetParent(parent);
            newObj.transform.SetPositionAndRotation(pos, rot);
            newObj.transform.localScale = scale;
            newObj.name = name;

            // Map GameObject
            replaceMap[oldObj] = newObj;

            // Map components by type & order
            var oldComponents = oldObj.GetComponents<Component>();
            var newComponents = newObj.GetComponents<Component>();

            int count = Mathf.Min(oldComponents.Length, newComponents.Length);
            for (int i = 0; i < count; i++)
            {
                if (oldComponents[i] != null && newComponents[i] != null)
                {
                    replaceMap[oldComponents[i]] = newComponents[i];
                }
            }
        }

        // Replace references in all components of the scene
        ReplaceReferencesInScene(replaceMap);

        // Destroy old objects
        foreach (var kv in replaceMap)
        {
            if (kv.Key is GameObject go)
                Undo.DestroyObjectImmediate(go);
        }

        Debug.Log($"✅ Đã relink {selectedObjects.Length} object, giữ nguyên reference đến cả component!");
    }

    void ReplaceReferencesInScene(Dictionary<Object, Object> replaceMap)
    {
        var allComponents = FindObjectsOfType<Component>(true);
        foreach (var comp in allComponents)
        {
            if (comp == null) continue;

            SerializedObject so = new(comp);
            SerializedProperty prop = so.GetIterator();

            while (prop.NextVisible(true))
            {
                if (prop.propertyType == SerializedPropertyType.ObjectReference)
                {
                    Object oldRef = prop.objectReferenceValue;
                    if (oldRef != null && replaceMap.TryGetValue(oldRef, out Object newRef))
                    {
                        prop.objectReferenceValue = newRef;
                    }
                }
            }
            so.ApplyModifiedProperties();
        }
    }
}
#endif
