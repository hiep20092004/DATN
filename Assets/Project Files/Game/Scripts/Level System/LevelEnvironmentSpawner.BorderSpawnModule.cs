using System.Collections.Generic;
using BorderSpawnModule;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    public partial class LevelEnvironmentSpawner
    {
        private readonly List<GameObject> spawnedBorderObjects = new List<GameObject>();
        
        private static readonly Vector2Int[] ExpandNeighborOffsets =
        {
            new (-1, 0),
            new (1, 0),
            new (0, -1),
            new (0, 1)
        };

        public void SpawnBorders(bool playAnim)
        {
            ClearSpawnedBorders();

            BorderRuleConfig ruleConfig = data.BorderRuleConfig;
            if (data.SpawnBorders && ruleConfig && ruleConfig.BorderRules is { Count: > 0 })
            {
                SpawnBordersWithRules(playAnim);
            }
        }

        private void SpawnBordersWithRules(bool playAnim)
        {
            PrepareGateLayout();

            HashSet<Vector2Int> pipeOccupiedCells = CollectPipeOccupiedCells();
            HashSet<Vector2Int> borderPositions = CollectRequiredBorderPositions(pipeOccupiedCells);
            if (borderPositions.Count == 0)
                return;

            BorderGrid grid = BuildBorderGrid(borderPositions, pipeOccupiedCells);
            if (grid == null)
                return;

            List<BorderResolveResult> results =
                BorderSpawnResolver.Resolve(grid, data.BorderRuleConfig.BorderRules);

            for (int i = 0; i < results.Count; i++)
            {
                SpawnRuleBorderElement(results[i].Position, results[i].Rule, playAnim);
            }
        }

        private BorderGrid BuildBorderGrid(HashSet<Vector2Int> borderPositions, HashSet<Vector2Int> pipeOccupiedCells)
        {
            var builder = new BorderGridBuilder();

            for (int x = 0; x < bordersMatrix.GetLength(0); x++)
            {
                for (int y = 0; y < bordersMatrix.GetLength(1); y++)
                {
                    BorderData cell = bordersMatrix[x, y];
                    if (cell != null)
                        builder.AddCell(cell.Position, cell.Type.ToBorderCellType());
                }
            }

            foreach (Vector2Int pos in borderPositions)
                builder.MarkAsBorder(pos);

            foreach (Vector2Int pos in pipeOccupiedCells)
                builder.AddForcedContinuity(pos);

            return builder.Build();
        }

        private HashSet<Vector2Int> CollectPipeOccupiedCells()
        {
            HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();
            for (int x = 0; x < bordersMatrix.GetLength(0); x++)
            {
                for (int y = 0; y < bordersMatrix.GetLength(1); y++)
                {
                    BorderData cell = bordersMatrix[x, y];
                    if (cell == null || cell.Type != ElementType.Gate)
                        continue;

                    GateDirection.Type directionType = cell.GateDirectionType;
                    if (directionType == GateDirection.Type.None)
                    {
                        directionType = GetGateDirection(cell);
                        cell.SetGateDirection(directionType);
                    }

                    if (directionType == GateDirection.Type.None)
                        continue;

                    GateDirection gateDirection = GateDirection.DIRECTIONS[(int)directionType];
                    if (gateDirection.PipeOccupiedOffsets.IsNullOrEmpty())
                        continue;

                    for (int i = 0; i < gateDirection.PipeOccupiedOffsets.Length; i++)
                    {
                        occupiedCells.Add(cell.Position + gateDirection.PipeOccupiedOffsets[i]);
                    }
                }
            }

            return occupiedCells;
        }

        private HashSet<Vector2Int> CollectRequiredBorderPositions(HashSet<Vector2Int> pipeOccupiedCells)
        {
            HashSet<Vector2Int> borderPositions = new HashSet<Vector2Int>();
            for (int x = 0; x < bordersMatrix.GetLength(0); x++)
            {
                for (int y = 0; y < bordersMatrix.GetLength(1); y++)
                {
                    BorderData cell = bordersMatrix[x, y];
                    if (cell == null)
                        continue;

                    if (cell.CanSpawnWall())
                    {
                        borderPositions.Add(cell.Position);
                        continue;
                    }

                    if (cell.Type is not (ElementType.InnerTile or ElementType.Block))
                        continue;

                    var neighborOffsets = BorderUtils.BorderNeighborOffsets;
                    for (int i = 0; i < neighborOffsets.Length; i++)
                    {
                        Vector2Int neighborPosition = cell.Position + neighborOffsets[i];
                        BorderData neighbor = GetMatrixElement(neighborPosition, true);
                        if (neighbor != null && neighbor.Type != ElementType.Empty)
                            continue;

                        if (pipeOccupiedCells.Contains(neighborPosition))
                            continue;

                        borderPositions.Add(neighborPosition);
                    }
                }
            }

            return borderPositions;
        }

        private void SpawnRuleBorderElement(Vector2Int borderPosition, ResolvedBorderRule resolved, bool playAnim)
        {
            Vector3 spawnPosition = new Vector3(borderPosition.x, 0f, borderPosition.y) + resolved.PositionOffset;

            GameObject border = Object.Instantiate(resolved.Prefab, spawnPosition, resolved.Rotation, parentTransform);
            border.SetLayersRecursively(GameLayer.LAYER_ENVIRONMENT);
#if UNITY_EDITOR
            string ruleName = string.IsNullOrEmpty(resolved.RuleName) ? "UnnamedRule" : resolved.RuleName;
            border.name = $"{resolved.Prefab.name} [Rule: {ruleName}] ({borderPosition.x}, {borderPosition.y})";
#endif
            if(playAnim) PlaySpawnTween(border, data.BorderSpawnTween);
            spawnedBorderObjects.Add(border);

            TryHideBorderRenderersForGenerator(borderPosition, border);

            if (resolved.Scale != Vector3.one)
            {
                border.transform.localScale = Vector3.Scale(border.transform.localScale, resolved.Scale);
            }

            UpdateBounds(spawnPosition);
        }

        private void TryHideBorderRenderersForGenerator(Vector2Int borderPosition, GameObject borderObject)
        {
            BorderData cellData = GetMatrixElement(borderPosition, true);
            if (cellData == null)
                return;

            if (cellData.Type == ElementType.Generator)
            {
                if (generatorsByPosition.TryGetValue(borderPosition, out GeneratorBehavior gen))
                {
                    LinkBorderToGenerator(gen, borderObject);
                }
                return;
            }

            if (cellData.Type != ElementType.Border)
                return;

            if (linkedBorderCellToGenerator.TryGetValue(borderPosition, out GeneratorBehavior flankGen))
                LinkBorderToGenerator(flankGen, borderObject);
        }

        private static void LinkBorderToGenerator(GeneratorBehavior gen, GameObject borderObject)
        {
            MeshRenderer[] renderers = borderObject.GetComponentsInChildren<MeshRenderer>(true);
            GeneratorLinkedBorderVisual linkedVisual = borderObject.AddComponent<GeneratorLinkedBorderVisual>();
            linkedVisual.Init(renderers);
            gen.RegisterLinkedVisual(linkedVisual);
        }

        private void ClearSpawnedBorders()
        {
            for (int i = 0; i < spawnedBorderObjects.Count; i++)
            {
                GameObject border = spawnedBorderObjects[i];
                if (!border)
                    continue;

                Object.Destroy(border);
            }

            spawnedBorderObjects.Clear();
        }

        public bool CanExpandExtendableBorder(Vector2Int position)
        {
            if (!IsWithinOriginalLevelBounds(position))
                return false;

            BorderData border = GetMatrixElement(position, true);
            if (border == null || border.Type != ElementType.Border || !border.IsExtendable)
                return false;

            bool hasValidNeighbor = false;
            for (int i = 0; i < ExpandNeighborOffsets.Length; i++)
            {
                Vector2Int neighborPosition = position + ExpandNeighborOffsets[i];
                if (!IsWithinOriginalLevelBounds(neighborPosition))
                    continue;

                BorderData neighbor = GetMatrixElement(neighborPosition, true);
                if (neighbor is { Type: ElementType.InnerTile or ElementType.Block })
                {
                    hasValidNeighbor = true;
                    break;
                }
            }

            return hasValidNeighbor;
        }
    }
}
