namespace WaterFlow.Game
{
    public enum BlockGateState
    {
        Enterable,
        
        Blocked,
        Shuttered,
        BlockFullFilled,
        BlockLayerMissMatchColor,
        
        GateLocked,
        GateValveLocked,
        GateEmpty,
        MissMatchColor,
        GateFrozen,
        GateMovingLock,
        GateMovingLockMatchColor,
        GateChainLocked,
        BlockSwitchLayerMissMatchColor
    }
}