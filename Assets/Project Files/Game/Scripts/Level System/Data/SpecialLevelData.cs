using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    [CreateAssetMenu(menuName = "Data/Level/Special Level Data", fileName = "Special Level Data")]
    public class SpecialLevelData : LevelData
    {
        [SerializeField, LevelEditorSetting] private SpecialLevelMode mode = SpecialLevelMode.GoldMode;
        [SerializeField] private int levelUnlock;
        [SerializeField, ShowIf(nameof(IsRescueColorMode)), LevelEditorSetting]
        private RescueColorConfig rescueColorConfig = new RescueColorConfig();
        [SerializeField, ShowIf(nameof(IsRescueBlockMode)), LevelEditorSetting]
        private RescueBlockConfig rescueBlockConfig = new RescueBlockConfig();

        public SpecialLevelMode Mode => mode;
        public int LevelUnlock => levelUnlock;
        public RescueColorConfig RescueColorConfig => rescueColorConfig;
        public RescueBlockConfig RescueBlockConfig => rescueBlockConfig;

        private bool IsGoldMode => mode == SpecialLevelMode.GoldMode;
        private bool IsRescueColorMode => mode == SpecialLevelMode.RescueColor;
        private bool IsRescueBlockMode => mode == SpecialLevelMode.RescueBlock;
    }
}
