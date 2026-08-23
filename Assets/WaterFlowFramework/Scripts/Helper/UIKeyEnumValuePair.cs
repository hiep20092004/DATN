using System;
using TMPro;
using UnityEngine;

public class UIKeyEnumValuePair : MonoBehaviour
{
    [SerializeField] private DropdownEnum keyInputEnum;
    [SerializeField] private TMP_InputField valueInputField;
    private Action<UIKeyEnumValuePair> OnRemoveClicked;

    public void SetData<T1, T2>(T1 key, T2 value, Action<UIKeyEnumValuePair> onRemoveClicked) where T1: struct, Enum
    {
        keyInputEnum.InitDropDown<T1>();
        keyInputEnum.SetValueWithoutNotify(key);
        valueInputField.text = value.ToString();
        this.OnRemoveClicked = onRemoveClicked;
    }

    public (T1, T2) GetData<T1, T2>() where T1 : struct, Enum
    {
        string valueText = valueInputField.text;

        T1 key = default;
        T2 value = default;

        try
        {
            key = keyInputEnum.GetValue<T1>();
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
