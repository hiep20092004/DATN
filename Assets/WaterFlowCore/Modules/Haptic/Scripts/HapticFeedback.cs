namespace WaterFlow.Core
{
    /// <summary>
    /// Semantic haptic vocabulary for gameplay/UI code, mapped onto the raw <see cref="Haptic"/> patterns.
    /// Callers name the moment ("win", "click") instead of hard-coding intensity curves, so retuning
    /// the feel is a single change here.
    /// </summary>
    public enum HapticType
    {
        None = -1,
        LightImpact = 0,
        MediumImpact = 1,
        HeavyImpact = 2,
        ClickButton = 3,
        Win = 4,
        Lose = 5,
        Warning = 6,
        CoinAtHome = 7,
        ItemAppear = 8,
        ItemCollect = 9,
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
                    Haptic.Play(Haptic.HAPTIC_LIGHT);
                    break;
                case HapticType.MediumImpact:
                    Haptic.Play(Haptic.HAPTIC_MEDIUM);
                    break;
                case HapticType.HeavyImpact:
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
                case HapticType.Warning:
                    Haptic.Play(Haptic.HAPTIC_MEDIUM);
                    break;
                case HapticType.CoinAtHome:
                case HapticType.ItemCollect:
                    Haptic.Play(Haptic.PATTERN_WATER_COMPLETE);
                    break;
                case HapticType.ItemAppear:
                    Haptic.Play(Haptic.HAPTIC_LIGHT);
                    break;
            }
        }
    }
}
