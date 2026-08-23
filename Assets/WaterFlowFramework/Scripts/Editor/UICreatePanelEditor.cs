#if UNITY_EDITOR
using System;
using System.IO;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using WaterFlow.Framework.Editor;
using WaterFlow.Framework.UIModule;
using UnityEditor;
using UnityEngine;

public enum NewScriptState
{
    None = 0,
    Compile,
    AddScript,
    ReadyToOpen
}

public class UICreatePanelEditor : OdinMenuEditorWindow
{
    private CreateNewPanel createNewPanel;
    //private CreateTemplatePanel createTemplatePanel;

    private void Update()
    {
        if (createNewPanel == null) return;
        createNewPanel.OnUpdateIm();
    }

    [MenuItem("WaterFlow Framework/UI")]
    private static void OpenWindow()
    {
        GetWindow<UICreatePanelEditor>().Show();
    }

    protected override OdinMenuTree BuildMenuTree()
    {
        var tree = new OdinMenuTree();
        tree.Selection.SupportsMultiSelect = false;
        createNewPanel = new CreateNewPanel();
        // = new CreateTemplatePanel();
        tree.Add("Create New Panel", createNewPanel);
        //tree.Add("Create Template Panel", createTemplatePanel);
        //tree.AddAllAssetsAtPath("Panel", "Assets/Scripts", typeof(Panel));
        return tree;
    }

    protected override void OnBeginDrawEditors()
    {
        //OdinMenuTreeSelection selected = this.MenuTree.Selection;

        //SirenixEditorGUI.BeginHorizontalToolbar();
        //{
        //    GUILayout.FlexibleSpace();

        //    if (SirenixEditorGUI.ToolbarButton("Delete Current"))
        //    {
        //    }

        //}
        //SirenixEditorGUI.EndHorizontalToolbar();
    }

    public class CreateNewPanel
    {
        private readonly UIEditorCreatePanelData data;
        private GameObject newPanelObj;
        public string panelName;


        [InlineEditor(Expanded = false)] public GameObject panelPrefab;

        private NewScriptState newScriptState;
        public string uiScripPath = "Scripts/UI/Panel";

        public CreateNewPanel()
        {
            data = (UIEditorCreatePanelData)AssetDatabase.LoadAssetAtPath(FrameworkWiz.createPanelDataPath,
                typeof(UIEditorCreatePanelData));
            if (data == null)
            {
                data = CreateInstance<UIEditorCreatePanelData>();
                AssetDatabase.CreateAsset(data, FrameworkWiz.createPanelDataPath);
            }

            if (data.panelPrefab == null)
                data.panelPrefab =
                    (GameObject)AssetDatabase.LoadAssetAtPath(FrameworkWiz.panelPrefabBasePath, typeof(GameObject));
            panelPrefab = data.panelPrefab;
            panelName = data.panelName;
            newScriptState = data.newScriptState;
        }

        [Button("Load Default Empty Panel", ButtonSizes.Medium)]
        private void LoadEmptyPanelDefault()
        {
            data.panelPrefab = (GameObject)AssetDatabase.LoadAssetAtPath(FrameworkWiz.panelPrefabBasePath, typeof(GameObject));
            panelPrefab = data.panelPrefab;
        }

        public void OnUpdateIm()
        {
            if (string.IsNullOrEmpty(panelName)) return;
            switch (newScriptState)
            {
                case NewScriptState.Compile:
                    var t = FrameworkEditorHelper.GetTypeFromAllAssemblies(panelName);
                    if (t == null) return;
                    CreateObject(t);
                    break;
                case NewScriptState.AddScript:
                    OpenScript();
                    break;
            }
        }


        [Button("Create New Panel", ButtonSizes.Medium)]
        private void CreateNewData()
        {
            if (string.IsNullOrEmpty(panelName) || panelName.Contains(" "))
            {
                FrameworkEditorHelper.ShowNotification("Invalid script name!'" + "'", true, false);
                return;
            }

            if (panelPrefab == null)
            {
                FrameworkEditorHelper.ShowNotification("Panel prefab is empty!'" + "'", true, false);
                return;
            }

            var alreadyExistingTypeFullName = GetFullNameIfScriptExists(panelName);
            if (alreadyExistingTypeFullName != null)
            {
                FrameworkEditorHelper.ShowNotification(
                    "Invalid script name. A script already exists as '" + alreadyExistingTypeFullName + "'", true,
                    false);
                return;
            }

            data.panelName = panelName;
            data.panelPrefab = panelPrefab;
            var text = File.ReadAllText(Path.Combine(Application.dataPath, FrameworkWiz.templateScriptPath));
            var newText = text.Replace(FrameworkWiz.TEMPLATE_PANEL_NAME, panelName);
            SaveScript(newText);
        }

        private void CreateObject(Type t)
        {
            var panelContainer = FindObjectOfType<PanelManager>()?.transform;
            if (panelContainer == null) panelContainer = Selection.activeTransform;

            newPanelObj = Instantiate(panelPrefab, panelContainer);
            var prePanel = newPanelObj.GetComponent<Panel>();

            newPanelObj.name = panelName;
            var newPanel = newPanelObj.AddComponent(t) as Panel;

            if (prePanel != null)
            {
                newPanel.openTween = prePanel.openTween;
                newPanel.closeTween = prePanel.closeTween;
                DestroyImmediate(prePanel);
            }

            data.newScriptState = NewScriptState.AddScript;
            newScriptState = NewScriptState.AddScript;
        }

        private void OpenScript()
        {
            data.newScriptState = NewScriptState.None;
            newScriptState = NewScriptState.None;
            OpenScript(newPanelObj.GetComponent<Panel>());
        }

        private void SaveScript(string newText)
        {
            var direcPath = Path.Combine(Application.dataPath, uiScripPath);
            if (!Directory.Exists(direcPath))
                try
                {
                    Directory.CreateDirectory(direcPath);
                }
                catch
                {
                    Debug.LogError("Could not create directory: " + direcPath);
                    return;
                }

            var scriptPath = Path.Combine(direcPath, $"{panelName}.cs");
            File.WriteAllTextAsync(scriptPath, newText);
            AssetDatabase.ImportAsset(FileUtil.GetProjectRelativePath(scriptPath));
            data.newScriptState = NewScriptState.Compile;
            newScriptState = NewScriptState.Compile;
        }

        private void OpenScript(Panel panel)
        {
            var monoScript = MonoScript.FromMonoBehaviour(panel);
            var success = AssetDatabase.OpenAsset(monoScript);
            if (!success) Debug.Log("Could not open '" + panel.GetType().Name + "' in external code editor");
        }

        public string GetFullNameIfScriptExists(string scriptName, bool fullnameProvided = false)
        {
            //var scriptNameOrig = scriptName;
            scriptName = scriptName.ToLower();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAbstract)
                    continue;
                if (!type.IsClass)
                    continue;
                if (type.IsGenericType)
                    continue;
                if (type.IsNotPublic)
                    continue;
                if (!FrameworkEditorHelper.DotNETCoreCompat_IsAssignableFrom(typeof(MonoBehaviour), type))
                    continue;

                if (fullnameProvided)
                {
                    if (type.FullName.ToLower() == scriptName)
                        return type.FullName;
                }
                else
                {
                    if (type.Name.ToLower() == scriptName)
                        return type.FullName;
                }
            }

            return null;
        }
    }
}

#endif