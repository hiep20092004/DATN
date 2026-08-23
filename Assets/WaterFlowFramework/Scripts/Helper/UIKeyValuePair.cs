using System;
using TMPro;
using UnityEngine;

public class UIKeyValuePair : MonoBehaviour
{
    [SerializeField] private TMP_InputField keyInputField;
    [SerializeField] private TMP_InputField valueInputField;
    private Action<UIKeyValuePair> OnRemoveClicked;

    public void SetData<T1, T2>(T1 key, T2 value, Action<UIKeyValuePair> onRemoveClicked)
    {
        keyInputField.text = key.ToString();
        valueInputField.text = value.ToString();
        this.OnRemoveClicked = onRemoveClicked;
    }

    public (T1, T2) GetData<T1, T2>()
    {
        string keyText = keyInputField.text;
        string valueText = valueInputField.text;

        T1 key = default;
        T2 value = default;

        try
        {
            key = (T1)Convert.ChangeType(keyText, typeof(T1));
        }
        catch
        {
            Debug.LogWarning("Key convert failed");
        }

        try
        {
            value = (T2)Convert.ChangeType(valueText, typeof(T2));
        }
        catch
        {
            Debug.LogWarning("Value convert failed");
        }

        return (key, value);
    }

    public void RemoveClicked()
    {
        OnRemoveClicked?.Invoke(this);
    }
}