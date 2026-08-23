#if UNITY_6000_3_OR_NEWER
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Core
{
    [CreateAssetMenu(fileName = "ToolbarSceneShortcuts", menuName = "WaterFlow/Toolbar Scene Shortcuts", order = 0)]
    public sealed class ToolbarSceneShortcutsSettings : ScriptableObject
    {
        [Tooltip("If empty, the scene file name is used as the button label.")]
        public List<SceneShortcutEntry> shortcuts = new List<SceneShortcutEntry>();

        [Tooltip("Show one row of buttons; otherwise a single Scenes dropdown.")]
        public bool displayAsButtons = true;

        const string kDefaultResourceName = "ToolbarSceneShortcuts";

        static ToolbarSceneShortcutsSettings s_cached;

        public static ToolbarSceneShortcutsSettings Instance
        {
            get
            {
                if (s_cached != null)
                    return s_cached;

                var guids = AssetDatabase.FindAssets($"t:{nameof(ToolbarSceneShortcutsSettings)}");
                if (guids.Length > 0)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    s_cached = AssetDatabase.LoadAssetAtPath<ToolbarSceneShortcutsSettings>(path);
                }

                return s_cached;
            }
        }

        public static void ClearCache() => s_cached = null;

        [MenuItem("Assets/Create/WaterFlow/Toolbar Scene Shortcuts (here)", false, 201)]
        static void CreateHere()
        {
            var asset = ScriptableObject.CreateInstance<ToolbarSceneShortcutsSettings>();
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path))
                path = "Assets";
            else if (!AssetDatabase.IsValidFolder(path))
                path = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets";

            path = AssetDatabase.GenerateUniqueAssetPath($"{path}/{kDefaultResourceName}.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(asset);
        }
    }

    [System.Serializable]
    public struct SceneShortcutEntry
    {
        [Tooltip("Leave empty to use the scene asset name.")]
        public string label;
        public SceneAsset scene;
    }
}
#endif
