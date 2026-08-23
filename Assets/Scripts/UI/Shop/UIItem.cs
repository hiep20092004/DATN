using WaterFlow.Enums;
using WaterFlow.Framework.Utils;
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
            if (resourceData.gameResource == GameResource.UnlimitedLive)
            {
                quantity.text = FrameworkUtils.GetTimeByFormat(resourceData.seconds, TxtTimeFormat.Shortest);
            }
            else
            {
                GameResourceType type = resourceData.gameResource.ResourceType();
                bool useMultiplierPrefix = resourceData.seconds == 0;
                string prefix = useMultiplierPrefix ? "x" : "";
                quantity.text = prefix + resourceData.quantity.ToString();
            }

        }
        if (icon)
        {
            icon.sprite = iconOverride != null
                ? iconOverride
                : Services.GameResourceService.GetSprite(resourceData.gameResource);
        }
    }
}
