using System;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Cached <see cref="LevelFigure"/> geometry per <see cref="BlockType"/> (offsets + piece counts).
    /// Populated via <see cref="Rebuild"/> from <see cref="BlocksVisualsData"/>; auto-refreshed after scene load from <see cref="LevelController"/>.
    /// </summary>
    public static class BlockFigureGeometryCache
    {
        private static Vector2Int[][] offsetsByTypeIndex;
        private static int[] pieceCountByTypeIndex;
        private static bool[] hasFigureByTypeIndex;

        public static bool IsBuilt { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void TryRebuildFromLevelController()
        {
            LevelController controller = UnityEngine.Object.FindFirstObjectByType<LevelController>();
            if (controller == null)
                return;

            BlocksVisualsData visuals = controller.GetBlocksVisualsData();
            if (visuals != null)
                Rebuild(visuals);
        }

        /// <summary>Rebuilds all arrays from block prefabs (calls <see cref="BlocksVisualsData.Init"/>).</summary>
        public static void Rebuild(BlocksVisualsData data)
        {
            int slotCount = MaxBlockTypeIndexInclusive() + 1;

            offsetsByTypeIndex = new Vector2Int[slotCount][];
            pieceCountByTypeIndex = new int[slotCount];
            hasFigureByTypeIndex = new bool[slotCount];

            for (int i = 0; i < slotCount; i++)
            {
                offsetsByTypeIndex[i] = Array.Empty<Vector2Int>();
                pieceCountByTypeIndex[i] = 1;
            }

            if (data == null)
            {
                IsBuilt = true;
                return;
            }

            data.Init();

            BlockData[] blocks = data.Blocks;
            if (blocks == null)
            {
                IsBuilt = true;
                return;
            }

            foreach (BlockData block in blocks)
            {
                int idx = (int)block.Type;
                if (idx < 0 || idx >= slotCount)
                    continue;

                LevelFigure figure = block.Figure;
                if (figure == null)
                    continue;

                Vector2Int[] offs = figure.GetOffsetsRelativeToPivot();
                offsetsByTypeIndex[idx] = offs;
                pieceCountByTypeIndex[idx] = offs.Length + 1;
                hasFigureByTypeIndex[idx] = true;
            }

            IsBuilt = true;
        }

        public static bool HasCachedFigure(BlockType blockType)
        {
            int idx = (int)blockType;
            return hasFigureByTypeIndex != null
                   && idx >= 0
                   && idx < hasFigureByTypeIndex.Length
                   && hasFigureByTypeIndex[idx];
        }

        public static Vector2Int[] GetOffsetsRelativeToPivot(BlockType blockType)
        {
            int idx = (int)blockType;
            if (offsetsByTypeIndex == null || idx < 0 || idx >= offsetsByTypeIndex.Length)
                return Array.Empty<Vector2Int>();

            Vector2Int[] o = offsetsByTypeIndex[idx];
            return o ?? Array.Empty<Vector2Int>();
        }

        public static int GetBlockPieceCount(BlockType blockType)
        {
            int idx = (int)blockType;
            if (pieceCountByTypeIndex == null || idx < 0 || idx >= pieceCountByTypeIndex.Length)
                return 1;

            return pieceCountByTypeIndex[idx];
        }

        private static int MaxBlockTypeIndexInclusive()
        {
            int max = 0;
            foreach (BlockType t in Enum.GetValues(typeof(BlockType)))
                max = Math.Max(max, (int)t);
            return max;
        }
    }
}
