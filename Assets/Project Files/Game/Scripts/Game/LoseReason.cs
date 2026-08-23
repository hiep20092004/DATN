namespace WaterFlow.Game
{
    /// <summary>
    /// Defines the reasons for losing the game.
    /// Each reason has a corresponding revive handler in the Lose System.
    /// </summary>
    public enum LoseReason
    {
        None = 0,
        
        // Time-based lose conditions
        OutOfTime,
        BombExploded,
        
        // Obstacle-based lose conditions 
        ValveFailed,
        ShutterFailed,
        BlockLayerFailed,
        TntExploded,
        MovingGateLockStuck,
        ChainGateLockFailed,
        BlockSwitchLayerFailed
    }

    public static class LoseReasonExtensions
    {
        public static LoseReason ToLoseReason(this BlockGateState state)
        {
            return state switch
            {
                BlockGateState.GateValveLocked => LoseReason.ValveFailed,
                BlockGateState.Shuttered => LoseReason.ShutterFailed,
                BlockGateState.BlockLayerMissMatchColor => LoseReason.BlockLayerFailed,
                BlockGateState.BlockSwitchLayerMissMatchColor => LoseReason.BlockSwitchLayerFailed,
                BlockGateState.GateMovingLockMatchColor => LoseReason.MovingGateLockStuck,
                BlockGateState.GateChainLocked => LoseReason.ChainGateLockFailed,
                _ => LoseReason.None,
            };
        }

        public static bool IsTimeBasedRevive(this LoseReason reason)
        {
            return reason is LoseReason.OutOfTime or LoseReason.BombExploded;
        }
    }
}
