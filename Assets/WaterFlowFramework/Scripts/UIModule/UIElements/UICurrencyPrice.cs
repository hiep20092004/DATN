using WaterFlow.Framework.UIModule.SpriteService;
using WaterFlow.Framework.UIModule.UIElements;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using TMPro;
using UnityEngine;

public class UICurrencyPrice : MonoBehaviour
{
    [SerializeField] private TMP_Text txtValue;

    [SerializeField] private FixedImageRatio icon;
    [SerializeField] private string iconNameFormat = "ico_{0}";

    public void SetData(CurrencyData data)
    {
        if (txtValue != null)
        {
            txtValue.text = data.value.ToString();
        }

        if (icon != null)
        {
            icon.SetIcon(string.Format(iconNameFormat, data.currency));
        }
    }
}