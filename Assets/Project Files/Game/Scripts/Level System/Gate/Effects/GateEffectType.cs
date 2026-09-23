namespace WaterFlow.Game
{
    public enum GateEffectType
    {
        None,         // 0
        Valve,        // 1
        IceGate,      // 2  
        LockedColor,  // 3
        // RETIRED — implementation removed. The value stays so ChainGate keeps integer 5, which
        // level data and LevelDatabase.gateEffects serialize by number.
        MovingLock,   // 4
        ChainGate,    // 5
    }
}