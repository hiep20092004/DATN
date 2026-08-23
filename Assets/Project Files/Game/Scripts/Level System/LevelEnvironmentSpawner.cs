using System.Collections.Generic;
using DG.Tweening;
using BorderSpawnModule;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    public partial class LevelEnvironmentSpawner
    {
        private const int BorderPadding = 1;
        private EnvironmentData data;
        private LevelRepresentation levelRepresentation;
        private Transform parentTransform;
        private LevelData levelData;
        private IBlockCellWatchService watchService;

        private Vector2Int size;
        private Vector2Int matrixOrigin;
        private BorderData[,] bordersMatrix;

        private float minYPosition = int.MaxValue;
        private float maxYPosition = int.MinValue;

        private float minXPosition = int.MaxValue;
        private float maxXPosition = int.MinValue;
        private bool hasSpawnedBounds;
        private bool hasPreparedGateLayout;
        private Bounds fallbackBounds;

        private ILevelContentProvider contentProvider;
        private List<GateBehavior> gates = new ();
        private List<GeneratorBehavior> generators = new ();
        private Dictionary<Vector2Int, GeneratorBehavior> generatorsByPosition = new ();
        /// <summary>Flank border cells beside each generator (from <see cref="GateDirection.GetFlankOffsets"/>), built in <see cref="SpawnGenerator"/>.</summary>
        private Dictionary<Vector2Int, GeneratorBehavior> linkedBorderCellToGenerator = new ();
        private List<InteractableObjectBehavior> interactableObjects = new ();
        private List<ObstacleBehavior> obstacles = new ();
        
        public List<GateBehavior> Gates => gates;
        public List<GeneratorBehavior> Generators => generators;
        public List<InteractableObjectBehavior> InteractableObjects => interactableObjects;
        public List<ObstacleBehavior> Obstacles => obstacles;
        public ExtraLayerHandlerBehavior ExtraLayerHandler { get; private set; }
        public float GateReadyDelay { get; private set; }

        public LevelEnvironmentSpawner(LevelRepresentation levelRepresentation, ILevelContentProvider contentProvider,
            IBlockCellWatchService watchService)
        {
            this.contentProvider = contentProvider;
            this.levelRepresentation = levelRepresentation;
            this.watchService = watchService;
            data = levelRepresentation.EnvironmentData;
            parentTransform = levelRepresentation.LevelTransform;
            levelData = levelRepresentation.LevelData;
            size = levelData.Size;

            LevelElementData[] levelElements = levelRepresentation.LevelElements;

            matrixOrigin = new Vector2Int(-BorderPadding, -BorderPadding);
            bordersMatrix = new BorderData[size.x + BorderPadding * 2, size.y + BorderPadding * 2];
            for (int i = 0; i < levelElements.Length; i++)
            {
                if (levelElements[i] == null) continue;
                BorderData borderData = new BorderData(levelElements[i]);
                Vector2Int matrixIndex = WorldToMatrixIndex(borderData.Position);
                if (!IsWithinMatrixBounds(matrixIndex))
                    continue;

                bordersMatrix[matrixIndex.x, matrixIndex.y] = borderData;
            }

            InitFallbackBounds(levelElements);
        }

        public void SetWatchService(IBlockCellWatchService watchService)
        {
            this.watchService = watchService;
        }

        public void SpawnGates()
        {
            if (!data.SpawnGates) return;
            PrepareGateLayout();
            gates.Clear();
            GateReadyDelay = 0f;

            for (int x = 0; x < bordersMatrix.GetLength(0); x++)
            {
                for (int y = 0; y < bordersMatrix.GetLength(1); y++)
                {
                    BorderData borderData = bordersMatrix[x, y];
                    if (borderData == null)
                        continue;

                    if (borderData.Type == ElementType.Gate)
                    {
                        Vector3 spawnPosition = new Vector3(borderData.Position.x, 0f, borderData.Position.y);
                        SpawnGate(borderData, spawnPosition, borderData.Rotation);
                    }
                }
            }
            
            LevelController.Instance?.RuntimePrecomputedCache?.BindGates(gates);
        }


        public void SpawnGenerators()
        {
            PrepareGateLayout();
            generators.Clear();
            generatorsByPosition.Clear();
            linkedBorderCellToGenerator.Clear();
            
            for (int x = 0; x < bordersMatrix.GetLength(0); x++)
            {
                for (int y = 0; y < bordersMatrix.GetLength(1); y++)
                {
                    BorderData borderData = bordersMatrix[x, y];
                    if (borderData == null)
                        continue;

                    if (borderData.Type == ElementType.Generator)
                    {
                        if (!IsGeneratorLayoutValid(borderData))
                        {
                            Debug.LogWarning(
                                $"[WaterFlow] Generator at {borderData.Position} was not spawned: need Empty behind, " +
                                "spawnable block in front, Border on both flanks (same rules as editor).");
                            continue;
                        }

                        Vector3 spawnPosition = new Vector3(borderData.Position.x, 0f, borderData.Position.y);
                        SpawnGenerator(borderData, spawnPosition);
                    }
                }
            }
        }
        
        
        /// <summary>
        /// Instantiates the runtime handler for the level's extra layer (if any) and initializes it.
        /// The concrete handler is resolved data-driven from <see cref="EnvironmentData"/>; an unmapped
        /// type (or no extra layer) is a no-op. Mirrors <see cref="SpawnGenerator"/>'s init/destroy contract.
        /// </summary>
        public void SpawnExtraLayer()
        {
            if (levelData == null || !levelData.HasExtraLayer)
                return;

            LevelElementData[] extraElements = levelData.ExtraLayerElements;
            if (extraElements == null || extraElements.Length == 0)
                return;

            ExtraLayerHandlerBehavior prefab = data.GetExtraLayerHandlerPrefab(levelData.ExtraLayerType);
            if (!prefab)
                return;

            ExtraLayerHandlerBehavior handler = Object.Instantiate(prefab, parentTransform);
            bool ok = handler.Init(
                levelRepresentation,
                contentProvider,
                watchService,
                extraElements,
                () => LevelController.Instance.LevelStarted);

            if (!ok && Application.isPlaying)
            {
                Object.Destroy(handler.gameObject);
                return;
            }

            ExtraLayerHandler = handler;
        }

        private bool IsGeneratorLayoutValid(BorderData borderData)
        {
            return GeneratorPlacementRules.TryGetGeneratorGateDirection(borderData.Position, size, cellPos =>
            {
                BorderData neighbor = GetMatrixElement(cellPos, true);
                return neighbor?.Type;
            }, out _);
        }

        private void PrepareGateLayout()
        {
            if (hasPreparedGateLayout)
                return;

            for (int x = 0; x < bordersMatrix.GetLength(0); x++)
            {
                for (int y = 0; y < bordersMatrix.GetLength(1); y++)
                {
                    BorderData borderData = bordersMatrix[x, y];
                    if (borderData == null)
                        continue;

                    if (borderData.Type == ElementType.Gate || borderData.Type == ElementType.Generator)
                    {
                        borderData.SetGateDirection(GetGateDirection(borderData));
                    }
                }
            }

            hasPreparedGateLayout = true;
        }

        
        public void SpawnInteractiveObjects()
        {
            interactableObjects.Clear();

            LevelElementData[] levelElements = levelRepresentation.LevelElements;
            for(int i = 0; i < levelElements.Length; i++)
            {
                if (levelElements[i] is not InteractableObjectLevelElementData ioElement) continue;
                
                InteractableObjectType interactableObjectType = ioElement.InteractableObjectData.Type;
                if (interactableObjectType == InteractableObjectType.None) continue;


                LevelInteractableObjectData interactableObjectData = contentProvider.GetInteractableObject(interactableObjectType);
                if (interactableObjectData == null) continue;
                
                GameObject interactableObject = Object.Instantiate(interactableObjectData.Behavior.gameObject, levelRepresentation.LevelTransform);
                interactableObject.transform.localPosition = new Vector3(ioElement.Position.x, 0, ioElement.Position.y);

                var interactableObjectBehavior = interactableObject.GetComponent<InteractableObjectBehavior>();
                interactableObjectBehavior.Init(ioElement.InteractableObjectData, ioElement.Position,
                    levelRepresentation);
                
                PlaySpawnTween(interactableObject, data.InteractiveObjectSpawnTween);
                
                interactableObjects.Add(interactableObjectBehavior);
            }
        }

        /// <summary>Deregisters an interactable that removed itself mid-level (e.g. an exploded Grinder).</summary>
        public void RemoveInteractableObject(InteractableObjectBehavior interactableObject)
        {
            if (!interactableObject) return;
            interactableObjects.Remove(interactableObject);
        }

        public void SpawnGroundAndObstacles()
        {
            obstacles.Clear();
            Dictionary<Vector2Int, ResolvedBorderRule> obstacleRulesByPosition = null;
            BorderRuleConfig obstacleRuleConfig = data.ObstacleRuleConfig;
            if (obstacleRuleConfig && obstacleRuleConfig.BorderRules is { Count: > 0 })
            {
                obstacleRulesByPosition = BuildObstacleRulesLookup(obstacleRuleConfig);
            }

            for (int x = 0; x < bordersMatrix.GetLength(0); x++)
            {
                for (int y = 0; y < bordersMatrix.GetLength(1); y++)
                {
                    BorderData element = bordersMatrix[x, y];
                    if (element == null)
                        continue;
                    Vector2 position = element.Position;
                    Vector3 spawnPosition = new Vector3(position.x, 0f, position.y);

                    if (element.Type is ElementType.InnerTile or ElementType.Block or ElementType.InteractableObject)
                    {
                        var innerTile = SpawnInnerTile(spawnPosition, x + y);
                        PlaySpawnTween(innerTile, data.InnerTileSpawnTween);
                        UpdateBounds(spawnPosition);
                    }
                    else if (element.Type == ElementType.Obstacle)
                    {
                        Vector2Int cell = element.Position;
                        GameObject obstacle = TrySpawnResolvedObstacle(obstacleRulesByPosition, cell, spawnPosition);
                        RegisterObstacle(obstacle, cell);
                        PlaySpawnTween(obstacle, data.InnerObstacleSpawnTween);
                        
                        var innerTile = SpawnInnerTile(spawnPosition, x + y);
                        PlaySpawnTween(innerTile, data.InnerTileSpawnTween);
                        UpdateBounds(spawnPosition);
                    }
                }
            }
        }

        private Dictionary<Vector2Int, ResolvedBorderRule> BuildObstacleRulesLookup(BorderRuleConfig obstacleRuleConfig)
        {
            var builder = new BorderGridBuilder();
            var obstaclePositions = new List<Vector2Int>();

            for (int x = 0; x < bordersMatrix.GetLength(0); x++)
            {
                for (int y = 0; y < bordersMatrix.GetLength(1); y++)
                {
                    BorderData cell = bordersMatrix[x, y];
                    if (cell == null)
                        continue;

                    builder.AddCell(cell.Position, cell.Type.ToBorderCellType());
                    if (cell.Type == ElementType.Obstacle)
                        obstaclePositions.Add(cell.Position);
                }
            }

            if (obstaclePositions.Count == 0)
                return null;

            BorderGrid grid = builder.Build();
            if (grid == null)
                return null;

            List<BorderResolveResult> resolved = BorderSpawnResolver.ResolveObstacles(grid, obstaclePositions, obstacleRuleConfig.BorderRules);
            if (resolved == null || resolved.Count == 0)
                return null;

            var map = new Dictionary<Vector2Int, ResolvedBorderRule>(resolved.Count);
            for (int i = 0; i < resolved.Count; i++)
            {
                BorderResolveResult r = resolved[i];
                map[r.Position] = r.Rule;
            }

            return map;
        }

        private GameObject TrySpawnResolvedObstacle(
            Dictionary<Vector2Int, ResolvedBorderRule> obstacleRulesByPosition,
            Vector2Int cellPosition,
            Vector3 spawnPosition)
        {
            if (obstacleRulesByPosition != null && obstacleRulesByPosition.TryGetValue(cellPosition, out ResolvedBorderRule rule) && rule.Prefab)
            {
                GameObject obstacle = Object.Instantiate(rule.Prefab, spawnPosition + rule.PositionOffset, rule.Rotation, parentTransform);

                if (rule.Scale != Vector3.one)
                    obstacle.transform.localScale = Vector3.Scale(obstacle.transform.localScale, rule.Scale);

#if UNITY_EDITOR
                string ruleName = string.IsNullOrEmpty(rule.RuleName) ? "UnnamedRule" : rule.RuleName;
                obstacle.name = $"{rule.Prefab.name} [ObstacleRule: {ruleName}] ({cellPosition.x}, {cellPosition.y})";
#endif
                return obstacle;
            }

            return SpawnInnerObstacle(spawnPosition);
        }

        private void UpdateBounds(Vector3 position)
        {
            hasSpawnedBounds = true;

            if (position.x < minXPosition)
            {
                minXPosition = position.x;
            }

            if (position.x > maxXPosition)
            {
                maxXPosition = position.x;
            }

            if (position.z < minYPosition)
            {
                minYPosition = position.z;
            }

            if (position.z > maxYPosition)
            {
                maxYPosition = position.z;
            }
        }

        private void InitFallbackBounds(LevelElementData[] levelElements)
        {
            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;
            bool hasAnyCell = false;

            foreach (LevelElementData element in levelElements)
            {
                if (element == null || element.Type == ElementType.Empty)
                    continue;

                hasAnyCell = true;
                minX = Mathf.Min(minX, element.Position.x);
                maxX = Mathf.Max(maxX, element.Position.x);
                minY = Mathf.Min(minY, element.Position.y);
                maxY = Mathf.Max(maxY, element.Position.y);
            }

            if (!hasAnyCell)
            {
                minX = 0;
                minY = 0;
                maxX = Mathf.Max(0, size.x - 1);
                maxY = Mathf.Max(0, size.y - 1);
            }

            Vector3 center = new Vector3((minX + maxX) * 0.5f, 0f, (minY + maxY) * 0.5f);
            Vector3 boundsSize = new Vector3(Mathf.Max(1f, maxX - minX + 1f), 0f, Mathf.Max(1f, maxY - minY + 1f));

            fallbackBounds = new Bounds(center, boundsSize);
        }

        private GameObject SpawnInnerTile(Vector3 position, int index)
        {
            GameObject innerTile = Object.Instantiate(data.InnerTilePrefab(index), position, Quaternion.identity, parentTransform);
#if UNITY_EDITOR
            // Toggle lives in the Level Editor window's Editor tab; key kept in sync with LevelEditorWindow.PREFS_SHOW_CELL_POS.
            {
                Vector2Int cellPosition = new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.z));
                GameObject cellLabel = new GameObject($"CellPos_{cellPosition.x}_{cellPosition.y}");
                cellLabel.transform.SetParent(innerTile.transform, false);
                cellLabel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                TextMesh textMesh = cellLabel.AddComponent<TextMesh>();
                textMesh.text = $"{cellPosition.x},{cellPosition.y}";
                textMesh.anchor = TextAnchor.MiddleCenter;
                textMesh.alignment = TextAlignment.Center;
                textMesh.characterSize = 0.08f;
                textMesh.fontSize = 32;
                textMesh.color = Color.white;

                // Render on top of all 3D geometry (ZTest Always via high renderQueue).
                // sharedMaterial avoids instantiating a material copy during edit-mode level preview.
                MeshRenderer cellLabelRenderer = cellLabel.GetComponent<MeshRenderer>();
                if (cellLabelRenderer != null)
                    cellLabelRenderer.sharedMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay + 1;

                cellLabel.SetActive(UnityEditor.EditorPrefs.GetBool("editor_show_cell_pos", false));
            }
#endif
            return innerTile;
        }

        private void SpawnGate(BorderData borderData, Vector3 position, Vector3 rotation)
        {
            GameObject gate = Object.Instantiate(data.GatePrefab, position, Quaternion.Euler(rotation), parentTransform);
            var gateSpawnTween = data.GateSpawnTween;

            GateBehavior gateBehavior = gate.GetComponent<GateBehavior>();
            gateBehavior.Init(borderData);
            if (gateSpawnTween.Enabled)
            {
                // Wait until gate spawn tween is visually finished.
                GateReadyDelay = Mathf.Max(GateReadyDelay, gateBehavior.PlayInitAnimation(gateSpawnTween.TotalTime));
            }
            else
            {
                GateReadyDelay = Mathf.Max(GateReadyDelay, gateBehavior.PlayInitAnimation());
            }
            
            
            if (borderData.LevelElementData is GateLevelElementData gateElement)
            {
                GateEffectData[] gateEffects = gateElement.GateEffects;
                if (!gateEffects.IsNullOrEmpty())
                {
                    foreach (GateEffectData effect in gateEffects)
                    {
                        if (effect == null || effect.Type == GateEffectType.None) continue;
                        LevelGateEffectData effectData = contentProvider.GetGateEffectData(effect.Type);
                        if (effectData == null) continue;
                        effectData.Behavior.ApplyEffect(gateBehavior, effect.Type, effect);
                    }
                }
            }

            gates.Add(gateBehavior);
            PlaySpawnTween(gate, gateSpawnTween);

            // spawn Border Inner Ground element
            if (gateBehavior.GateDirection != null)
            {
                Vector3 borderInnerTileOffset = position + gateBehavior.GateDirection.BorderInnerGroundOffset;
                GameObject innerGround = Object.Instantiate(data.BorderInnerGround, borderInnerTileOffset,
                    Quaternion.Euler(rotation), parentTransform);
                PlaySpawnTween(innerGround, data.BorderInnerGroundSpawnTween);
            }
        }
        
        
        private void SpawnGenerator(BorderData borderData, Vector3 position)
        {
            GameObject generator = Object.Instantiate(data.GeneratorPrefab, position, Quaternion.identity, parentTransform);
            var spawnTween = data.BorderSpawnTween;
            
            var generatorBehavior = generator.GetComponent<GeneratorBehavior>();
            bool canSpawn = generatorBehavior.Init(
                borderData,
                contentProvider,
                watchService,
                levelRepresentation,
                () => LevelController.Instance.LevelStarted);

            if (!canSpawn)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(generator);
                }
                return;
            }
            generators.Add(generatorBehavior);
            generatorsByPosition[borderData.Position] = generatorBehavior;
            if (borderData.GateDirectionType != GateDirection.Type.None)
            {
                GateDirection.GetFlankOffsets(borderData.GateDirectionType, out Vector2Int flankA,
                    out Vector2Int flankB);
                Vector2Int genPos = borderData.Position;
                linkedBorderCellToGenerator[genPos + flankA] = generatorBehavior;
                linkedBorderCellToGenerator[genPos + flankB] = generatorBehavior;
            }

            PlaySpawnTween(generator, spawnTween);

            GateDirection dir = generatorBehavior.GateDirection;
            if (dir != null && borderData.GateDirectionType != GateDirection.Type.None)
            {
                Quaternion innerGroundRotation = Quaternion.Euler(borderData.Rotation);
                GateDirection.GetFlankOffsets(borderData.GateDirectionType, out Vector2Int flankA,
                    out Vector2Int flankB);
                Vector2Int genCell = borderData.Position;

                void SpawnInnerGroundAtCell(Vector2Int cell)
                {
                    Vector3 cellCenter = new Vector3(cell.x, 0f, cell.y);
                    Vector3 innerGroundPos = cellCenter + dir.BorderInnerGroundOffset;
                    GameObject innerGround = Object.Instantiate(data.BorderInnerGround, innerGroundPos,
                        innerGroundRotation, parentTransform);
                    PlaySpawnTween(innerGround, data.BorderInnerGroundSpawnTween);
                }

                SpawnInnerGroundAtCell(genCell);
                SpawnInnerGroundAtCell(genCell + flankA);
                SpawnInnerGroundAtCell(genCell + flankB);
            }
        }
        
        private GameObject SpawnInnerObstacle(Vector3 position)
        {
            GameObject innerObstacle =
                Object.Instantiate(data.InnerObstaclePrefab, position, Quaternion.identity, parentTransform);

            return innerObstacle;
        }

        private void RegisterObstacle(GameObject obstacle, Vector2Int cell)
        {
            if (!obstacle)
                return;

            ObstacleBehavior behavior = obstacle.GetComponent<ObstacleBehavior>();
            if (!behavior)
                behavior = obstacle.AddComponent<ObstacleBehavior>();

            behavior.Init(cell, levelRepresentation);
            obstacles.Add(behavior);
        }

        private void PlaySpawnTween(GameObject targetObject, EnvironmentData.SpawnTweenConfig tweenConfig)
        {
            if (targetObject == null || tweenConfig == null || !Application.isPlaying || !tweenConfig.Enabled)
                return;

            Transform targetTransform = targetObject.transform;
            targetTransform.localScale = Vector3.zero;
            targetTransform.DOScale(1f, tweenConfig.Duration)
                .SetEase(tweenConfig.Ease)
                .SetDelay(tweenConfig.Delay);
        }

        private GateDirection.Type GetGateDirection(BorderData borderData)
        {
            if (borderData.Type == ElementType.Generator)
            {
                if (GeneratorPlacementRules.TryGetGeneratorGateDirection(borderData.Position, size, cellPos =>
                    {
                        BorderData neighbour = GetMatrixElement(cellPos, true);
                        return neighbour?.Type;
                    }, out GateDirection.Type genDir))
                    return genDir;

                return GateDirection.Type.None;
            }

            BorderData neighbour;

            // checking right neighbour
            neighbour = GetMatrixElement(borderData.Position.AddToX(1), true);

            if (neighbour == null || neighbour.Type == ElementType.Empty)
            {
                return GateDirection.Type.Right;
            }

            // checking left neighbour
            neighbour = GetMatrixElement(borderData.Position.AddToX(-1), true);
            if (neighbour == null || neighbour.Type == ElementType.Empty)
            {
                return GateDirection.Type.Left;
            }

            // checking top neighbour
            neighbour = GetMatrixElement(borderData.Position.AddToY(1), true);
            if (neighbour == null || neighbour.Type == ElementType.Empty)
            {
                return GateDirection.Type.Top;
            }

            // checking bottom neighbour
            neighbour = GetMatrixElement(borderData.Position.AddToY(-1), true);
            if (neighbour == null || neighbour.Type == ElementType.Empty)
            {
                return GateDirection.Type.Bottom;
            }

            return GateDirection.Type.None;
        }

        public BorderData GetMatrixElement(Vector2 position, bool skipOutOfBoundsLog = false)
        {
            if (!IsWithinLevelBounds(position))
            {
                if (!skipOutOfBoundsLog)
                    Debug.LogErrorFormat("Requested element with position {0} is outside of the level's bounds",
                        position);

                return null;
            }

            Vector2Int matrixIndex = WorldToMatrixIndex(new Vector2Int((int)position.x, (int)position.y));
            return bordersMatrix[matrixIndex.x, matrixIndex.y];
        }

        public bool IsWithinLevelBounds(Vector2 position)
        {
            Vector2Int matrixIndex = WorldToMatrixIndex(new Vector2Int((int)position.x, (int)position.y));
            return IsWithinMatrixBounds(matrixIndex);
        }

        private Vector2Int WorldToMatrixIndex(Vector2Int worldPosition)
        {
            return new Vector2Int(worldPosition.x - matrixOrigin.x, worldPosition.y - matrixOrigin.y);
        }

        private bool IsWithinMatrixBounds(Vector2Int matrixIndex)
        {
            if (matrixIndex.x < 0 || matrixIndex.y < 0)
                return false;

            if (matrixIndex.x > bordersMatrix.GetLength(0) - 1)
                return false;

            if (matrixIndex.y > bordersMatrix.GetLength(1) - 1)
                return false;

            return true;
        }

        private bool IsWithinOriginalLevelBounds(Vector2Int position)
        {
            if (position.x < 0 || position.y < 0)
                return false;

            if (position.x > size.x - 1)
                return false;

            if (position.y > size.y - 1)
                return false;

            return true;
        }


        public (GateDirection.Type, GateBehavior)? NearGate(LevelBlockBehavior levelBlockBehavior)
        {
            // Compute the block origin once and probe cells directly via IsOverlapsCell, avoiding the
            // per-call array allocation of GetOccupiedCells — this runs on the movement hot path.
            Vector2Int blockPosition = levelBlockBehavior.MatrixPosition;

            foreach (GateBehavior gate in gates)
            {
                Vector2Int gatePosition = gate.Data.Position;
                Vector2Int gateCheckPosition = gatePosition - gate.GateDirection.PositionOffset;

                // Block must occupy the cell in front of the gate, but not the gate cell itself.
                if (!levelBlockBehavior.IsOverlapsCell(gateCheckPosition, blockPosition))
                    continue;

                if (levelBlockBehavior.IsOverlapsCell(gatePosition, blockPosition))
                    continue;

                if (gate.CanGoThroughGate(levelBlockBehavior) != BlockGateState.Enterable)
                    continue;

                return (gate.Data.GateDirectionType, gate);
            }

            return null;
        }

        public bool IsGate(Vector2Int position)
        {
            BorderData element = GetMatrixElement(position, true);
            return element != null && element.Type == ElementType.Gate;
        }

        public bool HasAdjacentBorder(Vector2Int position, Vector2Int offset)
        {
            BorderData neighbor = GetMatrixElement(position + offset, true);
            return neighbor is { Type: ElementType.Border };
        }
        
        public bool HasAdjacentGround(Vector2Int position, Vector2Int offset)
        {
            BorderData neighbor = GetMatrixElement(position + offset, true);
            return neighbor is { Type: ElementType.InnerTile or ElementType.Block };
        }
        
        public bool HasAdjacentEmpty(Vector2Int position, Vector2Int offset)
        {
            BorderData neighbor = GetMatrixElement(position + offset, true);
            return neighbor is null or { Type: ElementType.Empty };
        }

        public bool TryExpandExtendableBorder(Vector2Int position)
        {
            if (!IsWithinOriginalLevelBounds(position))
                return false;

            if (!levelRepresentation.TryConvertExtendableBorderToInnerTile(position, out LevelElementData updatedElement))
                return false;

            levelRepresentation.LevelMatrix[position.x, position.y] = updatedElement;

            Vector2Int matrixIndex = WorldToMatrixIndex(position);
            if (IsWithinMatrixBounds(matrixIndex))
            {
                bordersMatrix[matrixIndex.x, matrixIndex.y] = new BorderData(updatedElement);
            }

            Vector3 spawnPosition = new Vector3(position.x, 0f, position.y);
            SpawnInnerTile(spawnPosition, position.x + position.y);
            UpdateBounds(spawnPosition);
            SpawnBorders(false);
            return true;
        }


        public Bounds GetBounds()
        {
            if (!hasSpawnedBounds)
                return fallbackBounds;

            Bounds bounds = new Bounds();
            bounds.center = new Vector3((minXPosition + maxXPosition) * 0.5f, 0f, (minYPosition + maxYPosition) * 0.5f);
            bounds.size = new Vector3(maxXPosition - minXPosition, 0f, maxYPosition - minYPosition);

            return bounds;
        }
    }
}