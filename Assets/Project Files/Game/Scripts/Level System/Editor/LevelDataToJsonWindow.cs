using System.IO;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Converts a source <see cref="LevelData"/> ScriptableObject asset to JSON text for preview, clipboard, or file export.
    /// </summary>
    public sealed class LevelDataToJsonWindow : EditorWindow
    {
        private int levelNumber = 1;
        private string jsonText = string.Empty;
        private string lastAssetPath = string.Empty;
        private string lastError = string.Empty;
        private Vector2 scrollPos;

        [MenuItem("Tools/Level Data/Level To JSON")]
        private static void Open()
        {
            var window = GetWindow<LevelDataToJsonWindow>();
            window.titleContent = new GUIContent("Level To JSON");
            window.minSize = new Vector2(480f, 360f);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Level Data → JSON", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Loads a source LevelData asset from the editor levels folder, serializes it to JSON, " +
                "copies to the clipboard automatically, and shows a preview below.",
                MessageType.Info);

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                levelNumber = EditorGUILayout.IntField("Level Number", Mathf.Max(1, levelNumber));
                if (GUILayout.Button("Convert", GUILayout.Width(90f), GUILayout.Height(22f)))
                    ConvertLevel();
            }

            if (!string.IsNullOrEmpty(lastAssetPath))
                EditorGUILayout.LabelField("Asset", lastAssetPath, EditorStyles.miniLabel);

            if (!string.IsNullOrEmpty(lastError))
                EditorGUILayout.HelpBox(lastError, MessageType.Error);
            else if (!string.IsNullOrEmpty(jsonText))
                EditorGUILayout.HelpBox("JSON copied to clipboard.", MessageType.None);

            EditorGUILayout.Space(6f);
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(jsonText)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Copy JSON", GUILayout.Height(26f)))
                        CopyToClipboard();

                    if (GUILayout.Button("Export JSON File", GUILayout.Height(26f)))
                        ExportToFile();
                }
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("JSON Preview", EditorStyles.boldLabel);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextArea(
                    string.IsNullOrEmpty(jsonText) ? "(No JSON yet — enter a level number and press Convert.)" : jsonText,
                    GUILayout.ExpandHeight(true));
            }
            EditorGUILayout.EndScrollView();
        }

        private void ConvertLevel()
        {
            lastError = string.Empty;
            jsonText = string.Empty;
            lastAssetPath = LevelSystemUtils.GetSourceLevelAssetPath(levelNumber);

            LevelData levelData = AssetDatabase.LoadAssetAtPath<LevelData>(lastAssetPath);
            if (!levelData)
            {
                lastError = $"Could not load LevelData at:\n{lastAssetPath}";
                return;
            }

            jsonText = JsonUtility.ToJson(levelData, true);
            CopyToClipboard();
            Repaint();
        }

        private void CopyToClipboard()
        {
            if (string.IsNullOrEmpty(jsonText))
                return;

            EditorGUIUtility.systemCopyBuffer = jsonText;
            ShowNotification(new GUIContent("Copied to clipboard"));
        }

        private void ExportToFile()
        {
            if (string.IsNullOrEmpty(jsonText))
                return;

            string defaultName = $"Level {levelNumber:D3}.json";
            string directory = !string.IsNullOrEmpty(lastAssetPath)
                ? Path.GetDirectoryName(lastAssetPath)
                : LevelSystemUtils.SourceLevelsFolder;

            string savePath = EditorUtility.SaveFilePanel(
                "Export Level JSON",
                directory,
                defaultName,
                "json");

            if (string.IsNullOrEmpty(savePath))
                return;

            File.WriteAllText(savePath, jsonText);
            EditorUtility.RevealInFinder(savePath);
            Debug.Log($"[LevelDataToJson] Exported JSON to: {savePath}");
        }
    }
}
