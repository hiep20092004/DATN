#if UNITY_EDITOR && using_addressable
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class AddressableFolderImporterWindow : EditorWindow
{
    private List<string> folders = new List<string>();
    private AddressableAssetGroup selectedGroup;

    [MenuItem("Tools/Addressables/Folder Importer")]
    public static void ShowWindow()
    {
        GetWindow<AddressableFolderImporterWindow>("Addressable Folder Importer");
    }

    private void OnGUI()
    {
        GUILayout.Label("📦 Addressable Folder Importer", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // Chọn Group
        if (selectedGroup == null)
        {
            selectedGroup = AddressableAssetSettingsDefaultObject.Settings.DefaultGroup;
        }

        selectedGroup = (AddressableAssetGroup)EditorGUILayout.ObjectField("Target Group", selectedGroup, typeof(AddressableAssetGroup), false);

        GUILayout.Space(10);

        // Danh sách folder
        GUILayout.Label("📁 Folders to Import:");
        for (int i = 0; i < folders.Count; i++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(folders[i], EditorStyles.textField);
            if (GUILayout.Button("❌", GUILayout.Width(30)))
            {
                folders.RemoveAt(i);
                i--;
            }

            GUILayout.EndHorizontal();
        }

        GUILayout.Space(10);

        // Nút chọn folder
        if (GUILayout.Button("➕ Add Folder"))
        {
            string folderPath = EditorUtility.OpenFolderPanel("Select Folder", "Assets", "");
            if (!string.IsNullOrEmpty(folderPath))
            {
                // Chuyển sang relative path (Assets/...)
                if (folderPath.StartsWith(Application.dataPath))
                {
                    folderPath = "Assets" + folderPath.Substring(Application.dataPath.Length);
                }

                if (!folders.Contains(folderPath))
                {
                    folders.Add(folderPath);
                }
            }
        }

        GUILayout.Space(20);

        if (GUILayout.Button("🚀 Import Selected Folders to Addressables", GUILayout.Height(35)))
        {
            ImportFolders();
        }
    }

    private void ImportFolders()
    {
        if (folders.Count == 0)
        {
            EditorUtility.DisplayDialog("No Folder", "Please add at least one folder to import.", "OK");
            return;
        }

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (selectedGroup == null)
        {
            selectedGroup = settings.DefaultGroup;
        }

        int importedCount = 0;

        foreach (var folderPath in folders)
        {
            string[] files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);

            foreach (string filePath in files)
            {
                if (filePath.EndsWith(".meta")) continue;

                string guid = AssetDatabase.AssetPathToGUID(filePath);
                if (string.IsNullOrEmpty(guid)) continue;

                string relativePath = filePath.Replace("\\", "/");
                string assetName = Path.GetFileNameWithoutExtension(relativePath);

                // Giữ cấu trúc thư mục
                string folderPrefix = Path.GetDirectoryName(relativePath)
                    ?.Replace("\\", "/")
                    ?.Replace(folderPath.Replace("\\", "/") + "/", "")
                    ?.Replace("Assets/", "");
                string address = string.IsNullOrEmpty(folderPrefix)
                    ? assetName
                    : $"{folderPrefix}/{assetName}";

                var entry = settings.CreateOrMoveEntry(guid, selectedGroup);
                entry.address = address;
                importedCount++;
            }
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("✅ Done", $"Imported {importedCount} assets into Addressables!", "OK");
        Debug.Log($"✅ Imported {importedCount} assets into Addressables (no file extensions).");
    }
}
#endif