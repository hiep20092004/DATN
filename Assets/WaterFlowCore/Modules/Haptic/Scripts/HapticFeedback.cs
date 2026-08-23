namespace WaterFlow.Core
{
    /// <summary>
    /// Semantic haptic vocabulary for gameplay/UI code, mapped onto the raw <see cref="Haptic"/> patterns.
    /// Callers name the moment ("win", "click", "water flowing") instead of hard-coding intensity
    /// curves, so retuning the feel of the whole game is a single change here.
    /// </summary>
    public enum HapticType
    {
        None = -1,

        // Generic
        LightImpact = 0,
        MediumImpact = 1,
        HeavyImpact = 2,
        ClickButton = 3,

        // Game state
        Win = 4,
        Lose = 5,
        Warning = 6,

        // Currency / rewards
        CoinAtHome = 7,
        ItemAppear = 8,
        ItemCollect = 9,

        // Block interaction
        Block_Select = 20,

        // Water pouring — escalates with how many cells the pour fills.
        WaterFlowShort = 30,
        WaterFlowMedium = 31,
        WaterFlowLong = 32,
        WaterFlowSuperLong = 33,
        WaterFlowMax = 34,

        // Boosters
        Expand = 40,
        Hammer = 41,
    }

    // Deliberately NOT named HapticController: Lofelt.NiceVibrations already owns that name,
    // and a same-named type in this namespace would shadow it for every file here.
    public static class HapticFeedback
    {
        public static void Play(HapticType type)
        {
            switch (type)
            {
                case HapticType.None:
                    return;

                case HapticType.LightImpact:
                case HapticType.ItemAppear:
                case HapticType.Block_Select:
                    Haptic.Play(Haptic.HAPTIC_LIGHT);
                    break;

                case HapticType.MediumImpact:
                case HapticType.Warning:
                case HapticType.Expand:
                    Haptic.Play(Haptic.HAPTIC_MEDIUM);
                    break;

                case HapticType.HeavyImpact:
                case HapticType.Hammer:
                    Haptic.Play(Haptic.HAPTIC_HARD);
                    break;

                case HapticType.ClickButton:
                    Haptic.Play(Haptic.PATTERN_BUTTON_CLICK);
                    break;

                case HapticType.Win:
                    Haptic.Play(Haptic.PATTERN_WIN);
                    break;

                case HapticType.Lose:
                    Haptic.Play(Haptic.PATTERN_BOOSTER_DENIED);
                    break;

                case HapticType.CoinAtHome:
                case HapticType.ItemCollect:
                case HapticType.WaterFlowShort:
                    Haptic.Play(Haptic.PATTERN_WATER_COMPLETE);
                    break;

                case HapticType.WaterFlowMedium:
                    Haptic.Play(Haptic.PATTERN_WATER_FLOW_SMOOTH);
                    break;

                case HapticType.WaterFlowLong:
                case HapticType.WaterFlowSuperLong:
                case HapticType.WaterFlowMax:
                    Haptic.Play(Haptic.PATTERN_WATER_FLOW_MEDIUM);
                    break;
            }
        }
    }
}
