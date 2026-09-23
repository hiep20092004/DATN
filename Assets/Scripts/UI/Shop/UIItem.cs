using WaterFlow.Enums;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIItem : MonoBehaviour
{
    public Image icon;
    public TMP_Text quantity;
    public void SetData(ResourceData resourceData, Sprite iconOverride = null)
    {
        if (quantity)
        {
            string prefix = resourceData.seconds == 0 ? "x" : "";
            quantity.text = prefix + resourceData.quantity.ToString();

        }
        if (icon)
        {
            icon.sprite = iconOverride != null
                ? iconOverride
                : Services.GameResourceService.GetSprite(resourceData.gameResource);
        }
    }
}
