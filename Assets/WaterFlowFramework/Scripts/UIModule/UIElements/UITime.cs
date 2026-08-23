using WaterFlow.Framework.Utils;
using TMPro;
using UnityEngine;

public class UITime : MonoBehaviour
{
    [SerializeField] private TMP_Text txtValue;
    [SerializeField] private TxtTimeFormat timeFormat;

    public void SetData(long seconds)
    {
        txtValue.text = FrameworkUtils.GetTimeByFormat(seconds, timeFormat);
    }
}
