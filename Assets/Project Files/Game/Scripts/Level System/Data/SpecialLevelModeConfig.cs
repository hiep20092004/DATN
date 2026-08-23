using System;
using System.Collections.Generic;

namespace WaterFlow.Game
{
    [Serializable]
    public sealed class GoldModeConfig
    {
        
    }

    [Serializable]
    public sealed class RescueColorTarget
    {
        public BlockColor color = BlockColor.None;
        public int requiredCount = 0;
    }

    [Serializable]
    public sealed class RescueColorConfig
    {
        public List<RescueColorTarget> targets = new List<RescueColorTarget>();
    }

    [Serializable]
    public sealed class RescueBlockConfig
    {
        public BlockEffectType markerEffect = BlockEffectType.None;
    }
}
