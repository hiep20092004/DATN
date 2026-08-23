namespace WaterFlow.Game
{
    [System.Serializable]
    public struct EditorSelectBlockData
    {
        public BlockType blockType;
        public BlockColor blockColor;
        /// <summary>When set, spawn writes the full element via SerializedProperty.managedReferenceValue (e.g. duplicate block).</summary>
        public LevelElementData spawnFromFullCopy;
    }
}