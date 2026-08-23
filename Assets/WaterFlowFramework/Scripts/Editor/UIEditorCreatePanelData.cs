using UnityEngine;
using UnityEngine.Serialization;
using static UICreatePanelEditor;

[CreateAssetMenu(menuName = "WaterFlowUI/CreatePanelData")]
public class UIEditorCreatePanelData : ScriptableObject
{
    public string panelName;
    public NewScriptState newScriptState;
    public GameObject panelPrefab;
}