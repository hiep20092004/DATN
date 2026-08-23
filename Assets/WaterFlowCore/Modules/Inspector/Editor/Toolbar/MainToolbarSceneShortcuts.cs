#if UNITY_6000_3_OR_NEWER
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace WaterFlow.Core
{
    public static class MainToolbarSceneShortcuts
    {
        const string kToolbarPath = "Scenes/Shortcuts";

        /// <summary>For <see cref="MainToolbar.Refresh"/> after settings assets change.</summary>
        public const string RefreshPath = kToolbarPath;

        /// <summary>Level load mode (ScriptableObject vs Remote/device). See <see cref="MainToolbarButtons.LevelLoaderModeDropdown"/>.</summary>
        public const string LevelLoaderModeToolbarRefreshPath = "LevelLoad/A_Mode";

        [MainToolbarElement(kToolbarPath, defaultDockPosition = MainToolbarDockPosition.Middle)]
        public static IEnumerable<MainToolbarElement> CreateSceneShortcutElements()
        {
            var settings = ToolbarSceneShortcutsSettings.Instance;

            if (settings == null)
            {
                yield return BuildSetupButton();
                yield break;
            }

            if (settings.displayAsButtons)
            {
                var any = false;
                foreach (var entry in EnumerateValidEntries(settings))
                {
                    any = true;
                    var captured = entry;
                    var content = new MainToolbarContent(captured.label, $"Open scene: {captured.path}");
                    yield return new MainToolbarButton(content, () => OpenScene(captured.path))
                    {
                        populateContextMenu = menu => PopulateSceneGroupMenu(menu, settings)
                    };
                }

                if (!any)
                {
                    yield return new MainToolbarButton(
                        new MainToolbarContent("Scenes", "Add Scene entries in the Toolbar Scene Shortcuts asset"),
                        () =>
                        {
                            Selection.activeObject = settings;
                            EditorGUIUtility.PingObject(settings);
                        })
                    {
                        populateContextMenu = menu => PopulateSceneGroupMenu(menu, settings)
                    };
                }
            }
            else
            {
                yield return new MainToolbarDropdown(
                    new MainToolbarContent("Scenes", "Open a configured scene"),
                    rect => ShowScenesDropdown(rect, settings))
                {
                    populateContextMenu = menu => PopulateSceneGroupMenu(menu, settings)
                };
            }
        }

        static MainToolbarButton BuildSetupButton()
        {
            var content = new MainToolbarContent("Scenes", "Create WaterFlow > Toolbar Scene Shortcuts asset to add Home, Game, Loading, …");
            return new MainToolbarButton(content, CreateSettingsAsset);
        }

        static IEnumerable<(string label, string path)> EnumerateValidEntries(ToolbarSceneShortcutsSettings settings)
        {
            foreach (var e in settings.shortcuts)
            {
                if (e.scene == null)
                    continue;
                var path = AssetDatabase.GetAssetPath(e.scene);
                if (string.IsNullOrEmpty(path))
                    continue;
                var label = string.IsNullOrWhiteSpace(e.label) ? System.IO.Path.GetFileNameWithoutExtension(path) : e.label.Trim();
                yield return (label, path);
            }
        }

        static void OpenScene(string scenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(scenePath);
        }

        static void ShowScenesDropdown(Rect dropDownRect, ToolbarSceneShortcutsSettings settings)
        {
            var menu = new GenericMenu();
            var any = false;
            foreach (var entry in EnumerateValidEntries(settings))
            {
                any = true;
                var path = entry.path;
                menu.AddItem(new GUIContent(entry.label), false, () => OpenScene(path));
            }

            if (!any)
                menu.AddDisabledItem(new GUIContent("No scenes configured"));

            menu.DropDown(dropDownRect);
        }

        static void PopulateSceneGroupMenu(DropdownMenu menu, ToolbarSceneShortcutsSettings settings)
        {
            menu.AppendAction(
                settings.displayAsButtons ? "Display as dropdown" : "Display as buttons",
                _ =>
                {
                    settings.displayAsButtons = !settings.displayAsButtons;
                    EditorUtility.SetDirty(settings);
                    MainToolbar.Refresh(kToolbarPath);
                });
            menu.AppendAction("Select settings asset", _ =>
            {
                Selection.activeObject = settings;
                EditorGUIUtility.PingObject(settings);
            });
        }

        static void CreateSettingsAsset()
        {
            var asset = ScriptableObject.CreateInstance<ToolbarSceneShortcutsSettings>();
            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/ToolbarSceneShortcuts.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            ToolbarSceneShortcutsSettings.ClearCache();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            MainToolbar.Refresh(kToolbarPath);
        }
    }

    sealed class ToolbarSceneShortcutsPostprocessor : AssetPostprocessor
    {
        static bool RefreshIfSettingsAsset(string path)
        {
            if (!path.EndsWith(".asset"))
                return false;
            if (AssetDatabase.LoadAssetAtPath<ToolbarSceneShortcutsSettings>(path) == null)
                return false;
            ToolbarSceneShortcutsSettings.ClearCache();
            MainToolbar.Refresh(MainToolbarSceneShortcuts.RefreshPath);
            return true;
        }

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedTo, string[] movedFrom)
        {
            foreach (var p in importedAssets)
            {
                if (RefreshIfSettingsAsset(p))
                    return;
            }

            foreach (var p in movedTo)
            {
                if (RefreshIfSettingsAsset(p))
                    return;
            }

            foreach (var p in deletedAssets)
            {
                if (p.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase)
                    && p.IndexOf("ToolbarSceneShortcuts", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ToolbarSceneShortcutsSettings.ClearCache();
                    MainToolbar.Refresh(MainToolbarSceneShortcuts.RefreshPath);
                }
            }
        }
    }
}
#endif
