using System.Collections.Generic;

namespace WaterFlow.Game
{
    public enum ObstacleCategory
    {
        Block,
        Gate,
        InteractableObject,
        Generator,
        ExtraLayer
    }

    /// <summary>
    /// Config để ignore các effect type không muốn tạo ObstacleUnlockEntry.
    /// Thêm/bớt các type trong HashSet bên dưới để điều chỉnh.
    /// </summary>
    public static class ObstacleUnlockConfig
    {
        /// <summary>
        /// BlockEffectType sẽ bị bỏ qua, không tạo ObstacleUnlockEntry.
        /// </summary>
        public static readonly HashSet<BlockEffectType> IgnoredBlockEffects = new HashSet<BlockEffectType>
        {
            BlockEffectType.None,
            BlockEffectType.KeyChain,
            BlockEffectType.KeyColor,
            BlockEffectType.Scissor,
        };

        /// <summary>
        /// GateEffectType sẽ bị bỏ qua, không tạo ObstacleUnlockEntry.
        /// </summary>
        public static readonly HashSet<GateEffectType> IgnoredGateEffects = new HashSet<GateEffectType>
        {
            GateEffectType.None
        };

        /// <summary>
        /// InteractableObjectType sẽ bị bỏ qua, không tạo ObstacleUnlockEntry.
        /// </summary>
        public static readonly HashSet<InteractableObjectType> IgnoredInteractableObjects = new HashSet<InteractableObjectType>
        {
            InteractableObjectType.None
        };

        /// <summary>
        /// ExtraLayerType sẽ bị bỏ qua, không tạo ObstacleUnlockEntry.
        /// Mặc định rỗng (ExtraLayerType không có giá trị None).
        /// </summary>
        public static readonly HashSet<ExtraLayerType> IgnoredExtraLayerTypes = new HashSet<ExtraLayerType>
        {
        };
    }
}
