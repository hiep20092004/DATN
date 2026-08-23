using System;
using System.Collections.Generic;
using WaterFlow.Enums;
using TMPro;
using UnityEngine;

public class DropdownEnum : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown;

    private int value = 0;
    public Action<int> OnValueChanged;
    private Type enumType;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (dropdown == null) dropdown = GetComponent<TMP_Dropdown>();
    }
#endif

    public void SetValue<T>(T value) where T : struct, Enum
    {
        // Convert enum to int
        this.value = Convert.ToInt32(value);

        // Find dropdown option by matching the enum name
        string enumName = value.ToString();
        int index = dropdown.options.FindIndex(o => o.text == enumName);

        // Update dropdown only if found
        if (index >= 0)
        {
            dropdown.value = index;
            dropdown.RefreshShownValue(); // Recommended for UI refresh
            OnValueChanged?.Invoke(index);
        }
        else
        {
            Debug.LogWarning($"Dropdown option '{enumName}' not found.");
        }
    }

    public void SetValueWithoutNotify<T>(T value) where T : struct, Enum
    {
        this.value = Convert.ToInt32(value);
        int index = dropdown.options.FindIndex(x => x.text == value.ToString());
        dropdown.value = index;
    }

    public void InitDropDown<T>()
    {
        List<TMP_Dropdown.OptionData> opts = new List<TMP_Dropdown.OptionData>();

        foreach (System.Enum e in System.Enum.GetValues(typeof(T)))
        {
            if (!Enum.IsDefined(typeof(T), e)) continue;
            TMP_Dropdown.OptionData data = new TMP_Dropdown.OptionData()
            {
                text = e.ToString()
            };
            opts.Add(data);
        }

        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.ClearOptions();
        dropdown.options = opts;
        dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
    }

    private void OnDropdownValueChanged(int index)
    {
        //if (Enum.TryParse(dropdown.options[dropdown.value].text, out value))
        //{
        OnValueChanged?.Invoke(value);
        //}
    }

    public T GetValue<T>() where T : struct, Enum
    {
        int index = dropdown.value;
        string enumName = dropdown.options[index].text;
        return (T)System.Enum.Parse(typeof(T), enumName);
    }
}