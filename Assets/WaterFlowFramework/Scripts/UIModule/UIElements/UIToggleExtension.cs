using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public class UIToggleExtension : MonoBehaviour
{
    public GameObject deactiveObj;
    public GameObject activeObj;
    private Toggle toggle;

    private void Start()
    {
        toggle = GetComponent<Toggle>();
        toggle.onValueChanged.AddListener(OnToggleChanged);
        OnToggleChanged(toggle.isOn);
    }

    private void OnToggleChanged(bool state)
    {
        if (deactiveObj)
            deactiveObj.SetActive(!state);
        if (activeObj)
            activeObj.SetActive(state);
    }
}