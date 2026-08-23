#if UNITY_6000_3_OR_NEWER
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace WaterFlow.Core
{
    public class MainToolbarButtons {
        // Keep in sync with FlowOut.LevelLoaderSystem.LoaderModeEditorPrefsKey
        const string kLevelLoaderModePrefsKey = "FlowOut.LevelLoaderSystem.LoaderMode";

        [MainToolbarElement(MainToolbarSceneShortcuts.LevelLoaderModeToolbarRefreshPath, defaultDockPosition = MainToolbarDockPosition.Middle)]
        public static MainToolbarElement LevelLoaderModeDropdown()
        {
            int mode = EditorPrefs.GetInt(kLevelLoaderModePrefsKey, 0);
            string label = mode == 0 ? "Lvl Editor" : "Lvl Remote";
            var content = new MainToolbarContent(label, "Level load: Editor = ScriptableObject direct; Remote = same as device (remote/local LevelData assets).");
            var dropdown = new MainToolbarDropdown(
                content,
                rect =>
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Editor (SO)"), mode == 0, () =>
                    {
                        EditorPrefs.SetInt(kLevelLoaderModePrefsKey, 0);
                        MainToolbar.Refresh(MainToolbarSceneShortcuts.LevelLoaderModeToolbarRefreshPath);
                    });
                    menu.AddItem(new GUIContent("Remote"), mode == 1, () =>
                    {
                        EditorPrefs.SetInt(kLevelLoaderModePrefsKey, 1);
                        MainToolbar.Refresh(MainToolbarSceneShortcuts.LevelLoaderModeToolbarRefreshPath);
                    });
                    menu.DropDown(rect);
                });
            MainToolbarElementStyler.StyleLevelLoaderModeDropdown();
            return dropdown;
        }

        [MainToolbarElement("Timescale/B_Reset", defaultDockPosition = MainToolbarDockPosition.Middle)]
        public static MainToolbarElement ResetTimeScaleButton() {
            var icon = EditorGUIUtility.IconContent("Refresh").image as Texture2D;
            var content = new MainToolbarContent(icon, "Reset");
            var button = new MainToolbarButton(content, () => {
                Time.timeScale = 1f;
                MainToolbar.Refresh("Timescale/A_Slider");
            });
        
            MainToolbarElementStyler.StyleElement<UnityEditor.Toolbars.EditorToolbarButton>("Timescale/B_Reset", element => {
                element.style.paddingLeft = 2f;
                element.style.paddingRight = 2f;
                element.style.marginLeft = 0f;
                element.style.marginRight = 2f;
                element.style.minWidth = 24f;
                element.style.maxWidth = 24f;
            
                var image = element.Q<Image>();
                if (image != null) {
                    image.style.width = 14f;
                    image.style.height = 14f;
                }
            });
        
            return button;
        }
    }
}
#endif