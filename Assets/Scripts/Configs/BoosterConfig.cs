using Sirenix.OdinInspector;
using WaterFlow.Enums;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using UnityEngine;

namespace WaterFlow.Game
{
    [CreateAssetMenu(fileName = "BoosterConfig", menuName = "Services/Booster/Config")]
    public class BoosterConfig : ScriptableObject
    {
        public GameResource type;
        private bool isBooster => type.ResourceType() == GameResourceType.Booster;

        [InlineEditor]
        [ShowIf("isBooster")]
        [SerializeReference] BasePowerUpConfig powerUpConfig;
        public CurrencyData price;

        public int levelUnlock;
        public int defaultValue;

        [Header("Buy UI Settings")]
        [SerializeField]
        private string header;
        [SerializeField]
        private string description;
        [SerializeField]
        private string outOfTargetText = string.Empty;
        [SerializeField] private Sprite icon;
        
        public int AmountPerBuy;
        public BasePowerUpConfig GetPowerUpConfig()
        {
            return powerUpConfig;
        }

        public string GetHeader()
        {
            return header;
        }

        public string GetDescription()
        {
            return description;
        }

        public string GetOutOfTargetText()
        {
            return outOfTargetText;
        }

        public Sprite GetIcon()
        {
            return icon;
        }
    }
}
