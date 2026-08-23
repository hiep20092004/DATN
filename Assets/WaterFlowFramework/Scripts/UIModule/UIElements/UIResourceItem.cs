using WaterFlow.Enums;
using WaterFlow.Framework.UIModule.SpriteService;
using WaterFlow.Framework.UIModule.UIElements;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;

public class UIResourceItem : MonoBehaviour
{
    [SerializeField] private FixedImageRatio icon;
    [SerializeField] private TMP_Text txtQuantity;

    [SerializeField] private Service<SpriteAtlasService> spriteAtlasService = new();
    private GameResourceKey resourceKey;
    public Transform Transform => transform;

    public void SetData(GameResourceKey resourceKey, int quantity = 0)
    {
        this.resourceKey = resourceKey;
        if (txtQuantity)
            txtQuantity.text = quantity.ToString();
        if (icon)
            icon.sprite = spriteAtlasService.Instance.GetSprite($"ico_{this.resourceKey.gameResource}");
    }

    // Updates the quantity text only, keeping the icon as configured on the prefab (used by fly-in
    // collect effects that don't carry a GameResourceKey, e.g. card-star counters).
    public void SetQuantity(int quantity)
    {
        if (txtQuantity)
            txtQuantity.text = quantity.ToString();
    }

    public FixedImageRatio Icon => icon;
}