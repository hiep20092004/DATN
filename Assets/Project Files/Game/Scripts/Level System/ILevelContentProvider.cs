namespace WaterFlow.Game
{
    public interface ILevelContentProvider
    {
        BlockData GetBlockData(BlockType blockType);
        BlockColorData GetBlockColorData(BlockColor blockColor);
        LevelBlockEffectData GetEffectData(BlockEffectType effectType);
        LevelGateEffectData GetGateEffectData(GateEffectType effectType);
        LevelInteractableObjectData GetInteractableObject(InteractableObjectType objectType);
    }

    public class LevelContentProvider : ILevelContentProvider
    {
        private readonly LevelDatabase levelDatabase;
        private readonly BlocksVisualsData blocksVisuals;

        public LevelContentProvider(LevelDatabase levelDatabase, BlocksVisualsData blocksVisuals)
        {
            this.levelDatabase = levelDatabase;
            this.blocksVisuals = blocksVisuals;
        }

        public BlockData GetBlockData(BlockType blockType) => blocksVisuals.GetBlockData(blockType);
        public BlockColorData GetBlockColorData(BlockColor blockColor) => blocksVisuals.GetColorData(blockColor);
        public LevelBlockEffectData GetEffectData(BlockEffectType effectType) => levelDatabase.GetEffectData(effectType);
        public LevelGateEffectData GetGateEffectData(GateEffectType effectType) => levelDatabase.GetGateEffectData(effectType);
        public LevelInteractableObjectData GetInteractableObject(InteractableObjectType objectType) => levelDatabase.GetInteractableObjectData(objectType);
    }
}
