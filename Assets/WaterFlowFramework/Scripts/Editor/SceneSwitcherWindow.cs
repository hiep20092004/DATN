using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

public class SceneSwitcherWindow : EditorWindow
{
    private bool autoSaveEnabled = true;
    private bool showBuildSettingsScenes = true;
    private bool showCustomScenes = true;
    private List<string> customScenes;
    private ReorderableList customSceneList;
    private string PrefKey;
    private Vector2 scrollPosition;


    private List<string> buildScenes;
    private ReorderableList buildSceneList;
    private int buildScenesHash;



    [MenuItem("Tools/Scene Switcher")]
    public static void ShowWindow()
    {
        GetWindow<SceneSwitcherWindow>("Scene Switcher");
    }
    public string GetProjectName()
    {
        string[] s = Application.dataPath.Split('/');
        string projectName = s[s.Length - 2];
        Debug.Log("Scene Switcher loaded scenes from " + projectName);
        return projectName;
    }
    private void OnEnable()
    {
        PrefKey = GetProjectName();
        customScenes = new List<string>();
        buildScenes = new List<string>();
        LoadCustomScenes();
        LoadBuildScenes();

        customSceneList = new ReorderableList(customScenes, typeof(string), true, true, false, false);
        customSceneList.drawElementCallback = DrawCustomListItems;
        customSceneList.drawHeaderCallback = DrawCustomHeader;


        buildSceneList = new ReorderableList(buildScenes, typeof(string), true, true, false, false);
        buildSceneList.drawElementCallback = DrawBuildListItems;
        buildSceneList.drawHeaderCallback = (Rect) => { };

        EditorApplication.update += Update;
    }
    private void LoadBuildScenes()
    {
        buildScenes.Clear();
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
            {
                buildScenes.Add(scene.path);
            }
        }
        buildScenesHash = GetBuildScenesHash();
    }

    private void OnDisable()
    {
        EditorApplication.update -= Update;
        SaveScenes();
    }

    private void Update()
    {



        int currentBuildHash = GetBuildScenesHash();
        if (currentBuildHash != buildScenesHash)
        {
            UpdateBuildScenesList();
            buildScenesHash = currentBuildHash;
            Repaint();
        }
    }

    private void UpdateBuildScenesList()
    {
        GetScenesFromBuildSettings();

        // Khởi tạo hoặc cập nhật ReorderableList
        if (buildSceneList == null)
        {
            buildSceneList = new ReorderableList(buildScenes, typeof(string), true, true, false, false);
            buildSceneList.drawElementCallback = DrawBuildListItems;
            buildSceneList.drawHeaderCallback = (Rect) => { };

        }
        else
        {
            buildSceneList.list = buildScenes;
        }
    }
    private void GetScenesFromBuildSettings()
    {
        buildScenes = new List<string>();
        foreach (var scene in EditorBuildSettings.scenes)
        {
            buildScenes.Add(scene.path);
        }
    }

    private int GetBuildScenesHash()
    {
        int hash = 17;
        foreach (var scene in EditorBuildSettings.scenes)
        {
            hash = hash * 31 + scene.path.GetHashCode();
        }
        return hash;
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        autoSaveEnabled = EditorGUILayout.ToggleLeft("Auto Save", autoSaveEnabled, GUILayout.Width(80));

        GUILayout.FlexibleSpace();


        EditorGUILayout.EndHorizontal();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        // Foldout for Build Settings scenes
        showBuildSettingsScenes = EditorGUILayout.Foldout(showBuildSettingsScenes, $"Scenes in Build Settings ({buildScenes.Count})");

        if (showBuildSettingsScenes)
        {
            if (buildSceneList != null)
            {
                buildSceneList.DoLayoutList();
            }
        }

        // Foldout for custom scenes
        showCustomScenes = EditorGUILayout.Foldout(showCustomScenes, $"Other scenes ({customScenes.Count})");

        if (GUILayout.Button("Clear other scenes"))
        {
            ClearCustomScenes();
        }
        if (showCustomScenes)
        {
            if (customSceneList != null)
            {
                customSceneList.DoLayoutList();
            }
        }

        EditorGUILayout.EndScrollView();

        HandleDragAndDrop();
    }

    private void ClearCustomScenes()
    {
        customScenes.Clear();
        SaveScenes();
        Repaint();
    }

    private void DrawBuildListItems(Rect rect, int index, bool isActive, bool isFocused)
    {
        var scenePath = buildScenes[index];
        var sceneName = Path.GetFileNameWithoutExtension(scenePath);

        rect.y += 2;

        EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
        if (GUI.Button(new Rect(rect.x, rect.y, rect.width - 95, EditorGUIUtility.singleLineHeight), sceneName))
        {
            SwitchScene(scenePath);
        }

        if (GUI.Button(new Rect(rect.x + rect.width - 90, rect.y, 50, EditorGUIUtility.singleLineHeight), "Focus"))
        {
            FocusOnScene(scenePath);
        }

        if (GUI.Button(new Rect(rect.x + rect.width - 40, rect.y, 50, EditorGUIUtility.singleLineHeight), "Play"))
        {
            PlayScene(scenePath);
        }

        EditorGUI.EndDisabledGroup();
    }

    private void DrawCustomListItems(Rect rect, int index, bool isActive, bool isFocused)
    {
        //if (index >= customScenes.Count ||index <customScenes.Count)
        //{
        if (index > customScenes.Count - 1) return;
        //    Debug.LogError("out of bounds");
        //    return;
        //}
        var scenePath = customScenes[index];
        var sceneName = Path.GetFileNameWithoutExtension(scenePath);

        rect.y += 2;

        EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

        if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && rect.Contains(Event.current.mousePosition))
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Remove Scene"), false, () => RemoveCustomScene(scenePath));
            menu.ShowAsContext();
            Event.current.Use();
        }
        else if (GUI.Button(new Rect(rect.x, rect.y, rect.width - 145, EditorGUIUtility.singleLineHeight), sceneName) && Event.current.button == 0)
        {
            SwitchScene(scenePath);
        }

        if (GUI.Button(new Rect(rect.x + rect.width - 140, rect.y, 50, EditorGUIUtility.singleLineHeight), "Focus"))
        {
            FocusOnScene(scenePath);
        }

        if (GUI.Button(new Rect(rect.x + rect.width - 90, rect.y, 50, EditorGUIUtility.singleLineHeight), "Play"))
        {
            PlayScene(scenePath);
        }

        if (GUI.Button(new Rect(rect.x + rect.width - 40, rect.y, 50, EditorGUIUtility.singleLineHeight), "Clear"))
        {
            RemoveCustomScene(scenePath);
        }

        EditorGUI.EndDisabledGroup();
    }


    private void DrawCustomHeader(Rect rect)
    {

    }

    private void SwitchScene(string scenePath)
    {
        if (autoSaveEnabled)
        {
            EditorSceneManager.SaveOpenScenes();
            EditorSceneManager.OpenScene(scenePath);
        }
        else
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(scenePath);
            }
        }
    }

    private void PlayScene(string scenePath)
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
        }
        EditorSceneManager.OpenScene(scenePath);
        EditorApplication.isPlaying = true;
    }

    private void FocusOnScene(string scenePath)
    {
        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
        if (sceneAsset != null)
        {
            EditorGUIUtility.PingObject(sceneAsset);
        }
        else
        {
            Debug.LogError($"Scene at path '{scenePath}' not found.");
        }
    }

    private void AddScene(string scenePath)
    {
        if (!customScenes.Contains(scenePath))
        {
            customScenes.Add(scenePath);
            SaveScenes();
            Repaint();
        }
    }

    private void AddScenesFromFolder(string folderPath)
    {
        var guids = AssetDatabase.FindAssets("t:Scene", new[] { folderPath });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            AddScene(path);
        }
    }

    private void RemoveCustomScene(string scenePath)
    {
        customScenes.Remove(scenePath);
        SaveScenes();
        Repaint();
    }

    private void OnRemoveCustomScene(ReorderableList list)
    {
        // Handle remove callback if needed
    }

    private void HandleDragAndDrop()
    {
        var evt = Event.current;
        if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (var draggedObject in DragAndDrop.objectReferences)
                {
                    var path = AssetDatabase.GetAssetPath(draggedObject);
                    if (draggedObject is SceneAsset)
                    {
                        AddScene(path);
                    }
                    else if (Directory.Exists(path))
                    {
                        AddScenesFromFolder(path);
                    }
                }
            }
            evt.Use();
        }
    }

    private void SaveScenes()
    {
        var origin = JsonUtility.ToJson(new SaveData(customScenes));
        EditorPrefs.SetString(PrefKey, origin);
    }


    private void LoadCustomScenes()
    {
        var origin = EditorPrefs.GetString(PrefKey);
        if (!string.IsNullOrEmpty(origin))
        {
            customScenes = JsonUtility.FromJson<SaveData>(origin).customScenesSaved;
        }

    }
    private class SaveData
    {
        public List<string> customScenesSaved;
        public SaveData(List<string> data)
        {
            customScenesSaved = data;
        }
    }
}

