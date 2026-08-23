using UnityEngine;

namespace WaterFlow.Game
{
    public static class GameLayer
    {
        public static readonly int LAYER_GROUND = LayerMask.NameToLayer("Ground");
        public static readonly int LAYER_BLOCK = LayerMask.NameToLayer("Block");
        public static readonly int LAYER_BLOCK_TARGET = LayerMask.NameToLayer("BlockTarget");
        public static readonly int LAYER_ENVIRONMENT = LayerMask.NameToLayer("Environment");
    }
}