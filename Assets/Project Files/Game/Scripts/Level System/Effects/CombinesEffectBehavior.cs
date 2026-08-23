using System.Collections.Generic;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    public sealed class CombinesEffectBehavior : BlockEffectBehavior<CombinesBlockEffectData>
    {
        [SerializeField] GameObject combinedObjectPrefab;
        private List<LevelBlockBehavior> blocksList;
        private readonly List<CombinesBlockEffectVisuals> spawnedVisuals = new ();
        private static readonly Vector2Int[] DIRECTIONS = { new (0, 1), new (0, -1), new (1, 0), new (-1, 0) };

        private static int s_nextCombineGroupId;
        private GameObject combineGroupObject;

        
        public void SpawnVisualObject(LevelBlockBehavior blockA, List<ConnectedBlocks> blockConnections)
        {
            ClearAllVisuals();

            foreach (ConnectedBlocks connectedBlock in blockConnections)
            {
                Vector3 spawnPosition = (connectedBlock.PositionA + connectedBlock.PositionB) / 2f;
                spawnPosition += new Vector3(0, GameConstant.BLOCK_HEIGHT, 0);

                GameObject visualObject = Instantiate(combinedObjectPrefab, spawnPosition, connectedBlock.Rotation);
                visualObject.transform.SetParent(blockA.ModelParentTransform);
                
                var combinedEffectVisuals = visualObject.GetComponent<CombinesBlockEffectVisuals>();
                combinedEffectVisuals.Init(connectedBlock);
                spawnedVisuals.Add(combinedEffectVisuals);
            }
        }

        public override void OnDisabled(LevelBlockBehavior blockBehavior, DisableSource disableSource)
        {
            Transform combineGroupParent = combineGroupObject ? combineGroupObject.transform.parent : null;
            LeaveCombineGroupIfNeeded(blockBehavior);
            ClearAllVisuals(blockBehavior);

            if (blocksList.IsNullOrEmpty()) return;

            RebuildGroupsAfterBlockDisabled(blockBehavior, combineGroupParent);
        }

        private void RebuildGroupsAfterBlockDisabled(LevelBlockBehavior disabledBlock, Transform fallbackParent)
        {
            if (!disabledBlock || blocksList.IsNullOrEmpty())
                return;

            List<LevelBlockBehavior> remainingBlocks = new List<LevelBlockBehavior>();
            for (int i = 0; i < blocksList.Count; i++)
            {
                LevelBlockBehavior block = blocksList[i];
                if (!block || block == disabledBlock)
                    continue;

                CombinesEffectBehavior combineEffect = block.GetEffect<CombinesEffectBehavior>(BlockEffectType.Combines);
                if (!combineEffect || !combineEffect.IsActive)
                    continue;

                if (!remainingBlocks.Contains(block))
                    remainingBlocks.Add(block);
            }

            if (remainingBlocks.Count == 0)
            {
                if (combineGroupObject)
                {
                    GameObject oldGroup = combineGroupObject;
                    combineGroupObject = null;
                    Destroy(oldGroup);
                }
                return;
            }

            Transform parentTransform = fallbackParent ? fallbackParent : remainingBlocks[0].transform.parent;
            DetachBlocksFromCurrentGroup(remainingBlocks, parentTransform, disabledBlock);
            DestroyCurrentGroupIfExists();

            List<List<LevelBlockBehavior>> connectedComponents = BuildConnectedComponents(remainingBlocks);
            for (int i = 0; i < connectedComponents.Count; i++)
            {
                List<LevelBlockBehavior> component = connectedComponents[i];
                if (component.Count <= 1)
                {
                    // Isolated blocks should no longer move as a combined group.
                    LevelBlockBehavior singleBlock = component[0];
                    CombinesEffectBehavior singleEffect = singleBlock.GetEffect<CombinesEffectBehavior>(BlockEffectType.Combines);
                    if (singleEffect)
                    {
                        singleEffect.blocksList = new List<LevelBlockBehavior> { singleBlock };
                        singleEffect.combineGroupObject = null;
                        singleEffect.ClearAllVisuals();
                    }
                    continue;
                }

                CombineBlocks(component, parentTransform);
            }
        }

        /// <summary>Reparents the disabled block out of the combine group and destroys the group object when empty.</summary>
        private void LeaveCombineGroupIfNeeded(LevelBlockBehavior block)
        {
            if (!block || !combineGroupObject)
                return;
            if (block.transform.parent != combineGroupObject.transform)
                return;

            Transform origParent = block.ConsumeParentBeforeGrouping();
            block.transform.SetParent(origParent, true);
            block.RestoreIndividualRigidbody();

            GameObject group = combineGroupObject;
            if (group.transform.childCount == 0)
            {
                ClearCombineGroupReferenceOnAllBlocks();
                combineGroupObject = null;
                Destroy(group);
            }
        }

        private void ClearCombineGroupReferenceOnAllBlocks()
        {
            if (blocksList.IsNullOrEmpty())
                return;

            foreach (LevelBlockBehavior b in blocksList)
            {
                if (!b) continue;
                CombinesEffectBehavior eff = b.GetEffect<CombinesEffectBehavior>(BlockEffectType.Combines);
                if (eff)
                    eff.combineGroupObject = null;
            }
        }

        public override void OnBlockFullAfterAnimationFilled()
        {
            DisableEffect();
        }

        public override void OnBlockSplit()
        {
            if (blocksList.IsNullOrEmpty()) return;
            for (int i = 0; i < blocksList.Count; i++)
            {
                LevelBlockBehavior block = blocksList[i];
                if(block)
                {
                    var combinesEffect = block.GetEffect<CombinesEffectBehavior>(BlockEffectType.Combines);
                    if (combinesEffect)
                    {
                        combinesEffect.DisableEffect();
                    }
                }
            }
        }

        public override bool MoveMultiplyObjects()
        {
            return combineGroupObject && blocksList is { Count: > 1 };
        }

        public override IReadOnlyList<LevelBlockBehavior> GetLinkedBlocks()
        {
            return blocksList;
        }

        public static void CombineBlocks(List<LevelBlockBehavior> combinedBlocks, Transform rooTransform)
        {
            if (combinedBlocks.IsNullOrEmpty())
            {
                return;
            }

            List<List<LevelBlockBehavior>> connectedComponents = BuildConnectedComponents(combinedBlocks);
            for (int i = 0; i < connectedComponents.Count; i++)
            {
                List<LevelBlockBehavior> component = connectedComponents[i];
                if (component.Count <= 1)
                {
                    LevelBlockBehavior singleBlock = component[0];
                    if (!singleBlock) continue;

                    CombinesEffectBehavior singleEffect = singleBlock.GetEffect<CombinesEffectBehavior>(BlockEffectType.Combines);
                    if (!singleEffect) continue;

                    singleEffect.blocksList = new List<LevelBlockBehavior> { singleBlock };
                    singleEffect.combineGroupObject = null;
                    singleEffect.ClearAllVisuals();
                    continue;
                }

                CombineConnectedComponent(component, rooTransform);
            }
        }

        private static void CombineConnectedComponent(List<LevelBlockBehavior> combinedBlocks, Transform rooTransform)
        {
            GameObject groupGo = CreateCombineGroupParent(combinedBlocks, rooTransform);
            Rigidbody groupRb = groupGo.AddComponent<Rigidbody>();
            Rigidbody sourceRb = null;
            for (int i = 0; i < combinedBlocks.Count; i++)
            {
                LevelBlockBehavior block = combinedBlocks[i];
                if (block && block.BlockRigidbody)
                {
                    sourceRb = block.BlockRigidbody;
                    break;
                }
            }
            LevelBlockBehavior.CopyRigidbodySettings(sourceRb, groupRb);

            foreach (LevelBlockBehavior b in combinedBlocks)
            {
                b.SetGroupRigidbody(groupRb);
            }

            foreach (LevelBlockBehavior b in combinedBlocks)
            {
                CombinesEffectBehavior effect = b.GetEffect<CombinesEffectBehavior>(BlockEffectType.Combines);
                if (effect)
                {
                    effect.combineGroupObject = groupGo;
                    effect.blocksList = new List<LevelBlockBehavior>(combinedBlocks);
                }
            }

            // Find all touching pairs for pin visualization
            var blockConnections = new Dictionary<LevelBlockBehavior, List<ConnectedBlocks>>();
            for (int i = 0; i < combinedBlocks.Count; i++)
            {
                LevelBlockBehavior blockA = combinedBlocks[i];
                Vector2Int[] cellsA = blockA.GetOccupiedCells();

                blockConnections.Add(blockA, new List<ConnectedBlocks>());

                for (int j = i + 1; j < combinedBlocks.Count; j++)
                {
                    LevelBlockBehavior blockB = combinedBlocks[j];
                    Vector2Int[] cellsB = blockB.GetOccupiedCells();

                    // Check for adjacency between any cell in A and any cell in B
                    foreach (Vector2Int cellA in cellsA)
                    {
                        foreach (Vector2Int dir in DIRECTIONS)
                        {
                            Vector2Int neighborCell = cellA + dir;
                            foreach (Vector2Int cellB in cellsB)
                            {
                                if (neighborCell == cellB)
                                {
                                    blockConnections[blockA].Add(new ConnectedBlocks(blockA, blockB, cellA, cellB));
                                }
                            }
                        }
                    }
                }
            }

            foreach(KeyValuePair<LevelBlockBehavior, List<ConnectedBlocks>> pair in blockConnections)
            {
                LevelBlockBehavior blockA = pair.Key;
                List<ConnectedBlocks> connectedPairs = pair.Value;

                var effect = blockA.GetEffect<CombinesEffectBehavior>(BlockEffectType.Combines);
                if (!effect) continue;
                effect.SpawnVisualObject(blockA, connectedPairs);
            }
        }

        private static GameObject CreateCombineGroupParent(List<LevelBlockBehavior> combinedBlocks, Transform rooTransform)
        {
            GameObject groupGo = new GameObject($"CombineGroup_{s_nextCombineGroupId++}");

            if (rooTransform)
                groupGo.transform.SetParent(rooTransform, true);

            foreach (LevelBlockBehavior b in combinedBlocks)
            {
                b.CacheParentBeforeGrouping();
                b.transform.SetParent(groupGo.transform, true);
            }

            return groupGo;
        }

        private void ClearAllVisuals(LevelBlockBehavior disabledBlock = null)
        {
            for (int i = spawnedVisuals.Count - 1; i >= 0; i--)
            {
                CombinesBlockEffectVisuals visual = spawnedVisuals[i];
                if (visual)
                    visual.Hide(visual.InvolvesBlock(disabledBlock));
                spawnedVisuals.RemoveAt(i);
            }
        }

        private void DetachBlocksFromCurrentGroup(List<LevelBlockBehavior> remainingBlocks, Transform fallbackParent, LevelBlockBehavior disabledBlock = null)
        {
            Transform currentGroupTransform = combineGroupObject ? combineGroupObject.transform : null;
            for (int i = 0; i < remainingBlocks.Count; i++)
            {
                LevelBlockBehavior block = remainingBlocks[i];
                if (!block)
                    continue;

                CombinesEffectBehavior effect = block.GetEffect<CombinesEffectBehavior>(BlockEffectType.Combines);
                if (effect)
                {
                    effect.ClearAllVisuals(disabledBlock); 
                    effect.combineGroupObject = null;
                    effect.blocksList = new List<LevelBlockBehavior> { block };
                }

                if (currentGroupTransform && block.transform.parent == currentGroupTransform)
                {
                    Transform originalParent = block.ConsumeParentBeforeGrouping();
                    if (!originalParent)
                        originalParent = fallbackParent;
                    block.transform.SetParent(originalParent, true);
                }

                block.RestoreIndividualRigidbody();
            }
        }

        private void DestroyCurrentGroupIfExists()
        {
            if (!combineGroupObject)
                return;

            GameObject oldGroup = combineGroupObject;
            combineGroupObject = null;
            Destroy(oldGroup);
        }

        private static List<List<LevelBlockBehavior>> BuildConnectedComponents(List<LevelBlockBehavior> blocks)
        {
            var components = new List<List<LevelBlockBehavior>>();
            var visited = new HashSet<LevelBlockBehavior>();

            for (int i = 0; i < blocks.Count; i++)
            {
                LevelBlockBehavior startBlock = blocks[i];
                if (!startBlock || visited.Contains(startBlock))
                    continue;

                var component = new List<LevelBlockBehavior>();
                var queue = new Queue<LevelBlockBehavior>();
                queue.Enqueue(startBlock);
                visited.Add(startBlock);

                while (queue.Count > 0)
                {
                    LevelBlockBehavior current = queue.Dequeue();
                    component.Add(current);

                    for (int j = 0; j < blocks.Count; j++)
                    {
                        LevelBlockBehavior candidate = blocks[j];
                        if (!candidate || visited.Contains(candidate))
                            continue;

                        if (!AreAdjacent(current, candidate))
                            continue;

                        visited.Add(candidate);
                        queue.Enqueue(candidate);
                    }
                }

                components.Add(component);
            }

            return components;
        }

        private static bool AreAdjacent(LevelBlockBehavior blockA, LevelBlockBehavior blockB)
        {
            Vector2Int[] cellsA = blockA.GetOccupiedCells();
            Vector2Int[] cellsB = blockB.GetOccupiedCells();

            var cellsBSet = new HashSet<Vector2Int>(cellsB);
            for (int i = 0; i < cellsA.Length; i++)
            {
                Vector2Int cellA = cellsA[i];
                for (int d = 0; d < DIRECTIONS.Length; d++)
                {
                    if (cellsBSet.Contains(cellA + DIRECTIONS[d]))
                        return true;
                }
            }

            return false;
        }

        public enum Direction { Up, Down, Left, Right }

        public class ConnectedBlocks
        {
            public LevelBlockBehavior BlockA { get; }
            public LevelBlockBehavior BlockB { get; }

            public Vector2Int CellA { get; }
            public Vector2Int CellB { get; }

            public Vector3 PositionA => new Vector3(CellA.x, 0, CellA.y);
            public Vector3 PositionB => new Vector3(CellB.x, 0, CellB.y);

            public Direction Direction { get; }
            public Quaternion Rotation { get; }

            public ConnectedBlocks(LevelBlockBehavior blockA, LevelBlockBehavior blockB, Vector2Int cellA, Vector2Int cellB)
            {
                BlockA = blockA;
                BlockB = blockB;
                CellA = cellA;
                CellB = cellB;

                // Calculate direction based on the difference between the two cells
                Vector2Int diff = cellB - cellA;
                if (diff == new Vector2Int(0, 1))
                {
                    Direction = Direction.Up;
                    Rotation = Quaternion.Euler(0, 180, 0);
                }
                else if (diff == new Vector2Int(0, -1))
                {
                    Direction = Direction.Down;
                    Rotation = Quaternion.Euler(0, 0, 0);
                }
                else if (diff == new Vector2Int(1, 0))
                {
                    Direction = Direction.Right;
                    Rotation = Quaternion.Euler(0, -90, 0);
                }
                else if (diff == new Vector2Int(-1, 0))
                {
                    Direction = Direction.Left;
                    Rotation = Quaternion.Euler(0, 90, 0);
                }
            }

        }
    }
}
