#if UNITY_EDITOR
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
// using Sirenix.OdinInspector.Demos;

namespace WaterFlow.Framework.Editor
{
    public class ExportTweenConfigWindow : OdinEditorWindow
    {
        [EnumToggleButtons]
        [InfoBox(
            "Inherit from OdinEditorWindow instead of EditorWindow in order to create editor windows like you would inspectors - by exposing members and using attributes.")]
        public ViewTool SomeField;

        public static void OpenWindow()
        {
            var window = GetWindow<ExportTweenConfigWindow>();

            // Nifty little trick to quickly position the window in the middle of the editor.
            window.position = GUIHelper.GetEditorWindowRect().AlignCenter(700, 700);
        }
    }
}
#endif