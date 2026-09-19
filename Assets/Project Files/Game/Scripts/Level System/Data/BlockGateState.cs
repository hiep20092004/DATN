namespace WaterFlow.Game
{
    public enum BlockGateState
    {
        Enterable,
        
        Blocked,
        BlockFullFilled,
        BlockLayerMissMatchColor,
        
        GateLocked,
        GateValveLocked,
        GateEmpty,
        MissMatchColor,
        GateFrozen,
        GateChainLocked,
        BlockSwitchLayerMissMatchColor
    }
}