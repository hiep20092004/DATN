using UnityEditor;

namespace WaterFlow.Core
{
    [CustomEditor(typeof(CoreSettings))]
    public class CoreSettingsEditor : Editor
    {
        private CoreSettings coreSettings;

        private void OnEnable()
        {
            coreSettings = (CoreSettings)target;
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();

            base.OnInspectorGUI();

            if (EditorGUI.EndChangeCheck())
            {
                if (coreSettings)
                {
                    CoreEditor.ApplySettings(coreSettings);
                }
            }
        }

        [MenuItem("Window/WaterFlow Core/Core Settings", priority = 50)]
        private static void SelectSettings()
        {
            CoreSettings coreSettings = EditorUtils.GetAsset<CoreSettings>();
            if(coreSettings)
            {
                Selection.activeObject = coreSettings;

                EditorGUIUtility.PingObject(coreSettings);
            }
        }
    }
}