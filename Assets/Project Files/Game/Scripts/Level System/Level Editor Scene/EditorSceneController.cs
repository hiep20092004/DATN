using System;
using System.Collections.Generic;
using System.Linq;
using WaterFlow.Core;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WaterFlow.Game
{
    public class EditorSceneController : Singleton<EditorSceneController>
    {
#if UNITY_EDITOR
        private const string PREFS_WIDTH_KEY = "EditorCameraOverrideWidth";
        private const string PREFS_BLOCK_MENU_POS_X = "LevelEditor_BlockHandlesMenu_PosX";
        private const string PREFS_BLOCK_MENU_POS_Y = "LevelEditor_BlockHandlesMenu_PosY";
        private const string PREFS_GATE_MENU_POS_X = "LevelEditor_GateHandlesMenu_PosX";
        private const string PREFS_GATE_MENU_POS_Y = "LevelEditor_GateHandlesMenu_PosY";
        private const string PREFS_GENERATOR_MENU_POS_X = "LevelEditor_GeneratorHandlesMenu_PosX";
        private const string PREFS_GENERATOR_MENU_POS_Y = "LevelEditor_GeneratorHandlesMenu_PosY";
        private const string PREFS_INTERACTABLE_MENU_POS_X = "LevelEditor_InteractableHandlesMenu_PosX";
        private const string PREFS_INTERACTABLE_MENU_POS_Y = "LevelEditor_InteractableHandlesMenu_PosY";
        public const string PREFS_DISABLE_PICKING_ON_LOAD = "editor_disable_level_picking";
        private const float MENU_HEADER_HEIGHT = 22f;
        private static readonly Vector2 DEFAULT_BLOCK_MENU_POS = new Vector2(100f, 10f);
        private static readonly Vector2 DEFAULT_GATE_MENU_POS = new Vector2(100f, 10f);
        private static readonly Vector2 DEFAULT_GENERATOR_MENU_POS = new Vector2(420f, 10f);
        private static readonly Vector2 DEFAULT_INTERACTABLE_MENU_POS = new Vector2(100f, 10f);

        [SerializeField] EnvironmentData environmentData;

        [SerializeField] LevelDatabase levelDatabase;

        // [SerializeField] LevelSkinDatabase skinDatabase;
        // [SerializeField, SkinPicker] string skinId;
        [SerializeField] Camera mainCamera;

        [Space] [SerializeField] Vector3 handlesCubeOffset;
        [SerializeField] BlocksVisualsData blocksVisuals;
        [SerializeField] SelectedBlock selectedBlockEditor;
        [SerializeField] BlockEffect effectEditor;

        private LevelRepresentation levelRepresentation;
        private bool isInitiazed;
        private List<BlockMovementData> blockMovementData;
        private List<GateData> gateDatas;
        private BlockMovementData selectedBlock;
        private int selectedBlockMovementBehaviourIndex;
        private BlockMovementManager movementManager;
        private bool levelPreviewInitialized;
        private Color backupHandlesColor;
        private Action<Vector2Int, Vector2Int> handleBlockChangeCallback;
        private Action<Vector2Int, BlockType> handleBlockTypeChangeCallback;
        private Action<Vector2Int, Vector2Int, EditorSelectBlockData> handleBlockSpawnCallback;
        private Action<Vector2Int> handleBlockDeleteCallback;
        // Gate/Generator both mount on a Border cell, so deleting either reverts the cell to Border (mirrors the move handlers).
        private Action<Vector2Int> handleBorderElementDeleteCallback;
        private Action<Vector2Int, BlockColor> handleBlockColorChangeCallback;
        private Action<Vector2Int, LevelFigure[], BlockType[], Action<EditorSelectBlockData>> handleCreateFigureSelectionPopup;
        private Action<Vector2Int> handleCreateBlockSceneInspector;
        private Action<List<Vector2Int>> handleApplyBlockSceneInspector;
        private LevelData levelData;
        // Elements of the layer currently being edited (extra layer when active, else main layer).
        // Duplicate/lookup must read this, not always levelData.Elements, or extra-layer blocks won't be found.
        private LevelElementData[] activeLayerElements;
        private int selectedGateIndex;
        private MonoBehaviorInspector blockHandlesDataEditor;
        private int blockDataInspectorHeight;
        private bool isDuplicating = false;
        private BlockMovementData duplicatedBlock;
        private Vector2Int duplicateSourcePosition;
        private List<int> multiSelectedBlockIndices = new List<int>();
        private List<Vector2Int> pendingMultiSelectPositions = new List<Vector2Int>();

        private int selectedGateMovementIndex = -1;
        private GateData selectedGateForMovement;
        private Vector2Int gateOriginalGridPosition;
        private Vector3 gateOriginalWorldPosition;
        private List<Vector2Int> gateUnifiedCellPositions;
        private Action<Vector2Int[], Vector2Int[]> handleGateChangeCallback;
        private MonoBehaviorInspector gateHandlesEditor;
        private int gateInspectorHeight;
        private Action<Vector2Int> handleCreateGateEditorCallback;
        private Action<Vector2Int, List<Vector2Int>> handleUpdateGateDataCallback;
        private Action<string> handleBeginLevelChangeUndoCallback;
        private Rect blockHandlesMenuRect;
        private Rect gateHandlesMenuRect;
        private bool isDraggingBlockMenu;
        private bool isDraggingGateMenu;
        private Vector2 blockMenuDragOffset;
        private Vector2 gateMenuDragOffset;

        private List<GeneratorData> generatorDatas;
        private int selectedGeneratorIndex = -1;
        private GeneratorBehavior selectedGeneratorForMenu;
        private Vector2Int generatorOriginalGridPosition;
        private Vector3 generatorOriginalWorldPosition;
        private MonoBehaviorInspector generatorHandlesEditor;
        private int generatorInspectorHeight;
        private Action<Vector2Int> handleCreateGeneratorEditorCallback;
        private Action<Vector2Int> handleUpdateGeneratorDataCallback;
        private Action<Vector2Int, Vector2Int> handleGeneratorMoveCallback;
        // Ctrl+Click pie menu: switches the targeted cell to the chosen element type.
        private Action<Vector2Int, ElementType> handleSwitchCellElementCallback;
        private readonly ScenePieMenu scenePieMenu = new ScenePieMenu();
        private Vector2Int pieMenuTargetCell;
        private Rect generatorHandlesMenuRect;
        private bool isDraggingGeneratorMenu;
        private Vector2 generatorMenuDragOffset;
        private static string generatorQueueClipboardJson;

        private List<InteractableObjectEntry> interactableDatas;
        private int selectedInteractableIndex = -1;
        private InteractableObjectBehavior selectedInteractableForMenu;
        private Vector2Int interactableOriginalGridPosition;
        private Vector3 interactableOriginalWorldPosition;
        private MonoBehaviorInspector interactableHandlesEditor;
        private int interactableInspectorHeight;
        private Action<Vector2Int> handleCreateInteractableEditorCallback;
        private Action<Vector2Int> handleUpdateInteractableDataCallback;
        private Action<Vector2Int, Vector2Int> handleInteractableMoveCallback;
        private Action<Vector2Int, Vector2Int> handleInteractableDuplicateCallback;
        private Rect interactableHandlesMenuRect;
        private bool isDraggingInteractableMenu;
        private Vector2 interactableMenuDragOffset;
        private bool isDuplicatingInteractable;

        //private bool levelChanged;
        //private ItemSave[] itemsCached;

        public SelectedBlock SelectedBlockEditor
        {
            get => selectedBlockEditor;
            set => selectedBlockEditor = value;
        }

        public BlockEffect EffectEditor
        {
            get => effectEditor;
            set => effectEditor = value;
        }

        public MonoBehaviorInspector BlockHandlesDataEditor
        {
            get => blockHandlesDataEditor;
            set => blockHandlesDataEditor = value;
        }

        public MonoBehaviorInspector GateHandlesEditor
        {
            get => gateHandlesEditor;
            set => gateHandlesEditor = value;
        }

        public MonoBehaviorInspector GeneratorHandlesEditor
        {
            get => generatorHandlesEditor;
            set => generatorHandlesEditor = value;
        }

        public MonoBehaviorInspector InteractableHandlesEditor
        {
            get => interactableHandlesEditor;
            set => interactableHandlesEditor = value;
        }

        /// <summary>
        /// When true (default), the loaded level GameObject hierarchy is excluded from Scene view picking
        /// so the editor's custom handles are the only way to select blocks/gates/generators.
        /// </summary>
        public static bool DisablePickingOnLoad
        {
            get => EditorPrefs.GetBool(PREFS_DISABLE_PICKING_ON_LOAD, true);
            set => EditorPrefs.SetBool(PREFS_DISABLE_PICKING_ON_LOAD, value);
        }

        // Read-only accessors used by LevelCaptureExporter to frame/render the loaded level
        // without going through the interactive editing path.
        public Camera EditorMainCamera => mainCamera;
        public LevelDatabase LevelDatabase => levelDatabase;
        public Bounds CurrentLevelBounds =>
            levelRepresentation != null ? levelRepresentation.LevelBounds : default;

        // protected override void OnAwake()
        // {
        //     base.OnAwake();
        //     isMovementRestricted = true;
        // }

        [Button]
        public void SaveCameraPosition()
        {
            PlayerPrefs.SetFloat(PREFS_WIDTH_KEY, SceneView.lastActiveSceneView.size);
        }

        [Button]
        public void SetUpCamera()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            sceneView.AlignViewToObject(mainCamera.transform);

            if (PlayerPrefs.HasKey(PREFS_WIDTH_KEY))
            {
                sceneView.size = PlayerPrefs.GetFloat(PREFS_WIDTH_KEY, 6f);
            }
            else
            {
                sceneView.size = PlayerPrefs.GetFloat("CameraScalerWidth", 6f);
            }


            SceneView.RepaintAll();
        }


        public void LoadLevel(LevelData levelData,
            Action<Vector2Int, Vector2Int> handleBlockChange,
            Action<Vector2Int, Vector2Int, EditorSelectBlockData> handleBlockSpawnCallback,
            Action<Vector2Int> handleBlockDelete,
            Action<Vector2Int, BlockColor> handleBlockColorChange,
            Action<Vector2Int, BlockType> handleBlockTypeChange,
            Action<Vector2Int, LevelFigure[], BlockType[], Action<EditorSelectBlockData>> createWindow,
            Action<Vector2Int> handleCreateBlockSceneInspector,
            Action<List<Vector2Int>> handleApplyBlockSceneInspector,
            Action<Vector2Int[], Vector2Int[]> handleGateChange = null,
            Action<Vector2Int> handleCreateGateEditor = null,
            Action<Vector2Int, List<Vector2Int>> handleUpdateGateData = null,
            Action<string> handleBeginLevelChangeUndo = null,
            Action<Vector2Int> handleCreateGeneratorEditor = null,
            Action<Vector2Int> handleUpdateGeneratorData = null,
            Action<Vector2Int, Vector2Int> handleGeneratorMove = null,
            Action<Vector2Int, ElementType> handleSwitchCellElement = null,
            Action<Vector2Int> handleBorderElementDelete = null,
            Action<Vector2Int> handleCreateInteractableEditor = null,
            Action<Vector2Int> handleUpdateInteractableData = null,
            Action<Vector2Int, Vector2Int> handleInteractableMove = null,
            Action<Vector2Int, Vector2Int> handleInteractableDuplicate = null,
            LevelElementData[] activeLayerElements = null)
        {
            if (!isInitiazed)
            {
                levelDatabase.Init();
                ReflectionUtils.InjectStaticComponent<LevelController>("levelDatabase", levelDatabase);
                SceneView.duringSceneGui += DuringSceneGui;
                SelectedBlockEditorCommitEvents.AfterSerializedObjectCommitted -=
                    OnSelectedBlockBufferSerializedExternalCommit;
                SelectedBlockEditorCommitEvents.AfterSerializedObjectCommitted +=
                    OnSelectedBlockBufferSerializedExternalCommit;
                isInitiazed = true;
            }

            this.handleBlockChangeCallback = handleBlockChange;
            this.handleBlockSpawnCallback = handleBlockSpawnCallback;
            this.handleBlockDeleteCallback = handleBlockDelete;
            this.handleBlockColorChangeCallback = handleBlockColorChange;
            this.handleBlockTypeChangeCallback = handleBlockTypeChange;
            this.handleCreateFigureSelectionPopup = createWindow;
            this.handleCreateBlockSceneInspector = handleCreateBlockSceneInspector;
            this.handleApplyBlockSceneInspector = handleApplyBlockSceneInspector;
            this.handleGateChangeCallback = handleGateChange;
            this.handleCreateGateEditorCallback = handleCreateGateEditor;
            this.handleUpdateGateDataCallback = handleUpdateGateData;
            this.handleBeginLevelChangeUndoCallback = handleBeginLevelChangeUndo;
            this.handleCreateGeneratorEditorCallback = handleCreateGeneratorEditor;
            this.handleUpdateGeneratorDataCallback = handleUpdateGeneratorData;
            this.handleGeneratorMoveCallback = handleGeneratorMove;
            this.handleSwitchCellElementCallback = handleSwitchCellElement;
            this.handleBorderElementDeleteCallback = handleBorderElementDelete;
            this.handleCreateInteractableEditorCallback = handleCreateInteractableEditor;
            this.handleUpdateInteractableDataCallback = handleUpdateInteractableData;
            this.handleInteractableMoveCallback = handleInteractableMove;
            this.handleInteractableDuplicateCallback = handleInteractableDuplicate;
            this.levelData = levelData;
            this.activeLayerElements = activeLayerElements;
            scenePieMenu.Close();

            if (levelRepresentation != null && levelRepresentation.LevelTransform)
            {
                DestroyImmediate(levelRepresentation.LevelTransform.gameObject);
            }

            //removing old level representations
            GameObject[] rootGameObjects = this.gameObject.scene.GetRootGameObjects();

            for (int i = 0; i < rootGameObjects.Length; i++)
            {
                if (rootGameObjects[i].name.Equals("[LEVEL]"))
                {
                    DestroyImmediate(rootGameObjects[i]);
                }
            }

            var contentProvider = new LevelContentProvider(levelDatabase, blocksVisuals);
            levelRepresentation = new LevelRepresentation(levelData, 
                environmentData, 
                mainCamera.GetComponent<CameraController>(), 
                contentProvider, 
                elementsOverride: activeLayerElements);
            levelRepresentation.Spawn();

            ApplyPickingSetting();

            PrepareGateData(levelData.Size);
            PrepareGeneratorData();
            PrepareInteractableData();
            PrepareForBlocksMovement();
        }

        /// <summary>
        /// Applies the current <see cref="DisablePickingOnLoad"/> setting to the currently loaded level hierarchy.
        /// Safe to call when no level is loaded.
        /// </summary>
        public void ApplyPickingSetting()
        {
            if (levelRepresentation == null || levelRepresentation.LevelTransform == null)
            {
                return;
            }

            GameObject levelRoot = levelRepresentation.LevelTransform.gameObject;
            if (DisablePickingOnLoad)
            {
                SceneVisibilityManager.instance.DisablePicking(levelRoot, true);
            }
            else
            {
                SceneVisibilityManager.instance.EnablePicking(levelRoot, true);
            }
        }


        private void PrepareGateData(Vector2Int levelSize)
        {
            GateBehavior[] gateBehaviourBehaviors =
                levelRepresentation.LevelTransform.GetComponentsInChildren<GateBehavior>();
            gateDatas = new List<GateData>();

            for (int i = 0; i < gateBehaviourBehaviors.Length; i++)
            {
                gateDatas.Add(new GateData(gateBehaviourBehaviors[i], levelSize));
            }
        }

        private void PrepareGeneratorData()
        {
            GeneratorBehavior[] generatorBehaviors =
                levelRepresentation.LevelTransform.GetComponentsInChildren<GeneratorBehavior>();
            generatorDatas = new List<GeneratorData>();

            for (int i = 0; i < generatorBehaviors.Length; i++)
            {
                generatorDatas.Add(new GeneratorData(generatorBehaviors[i]));
            }
        }

        private void PrepareInteractableData()
        {
            InteractableObjectBehavior[] interactableBehaviors =
                levelRepresentation.LevelTransform.GetComponentsInChildren<InteractableObjectBehavior>();
            interactableDatas = new List<InteractableObjectEntry>();

            for (int i = 0; i < interactableBehaviors.Length; i++)
            {
                interactableDatas.Add(new InteractableObjectEntry(interactableBehaviors[i]));
            }
        }

        private void PrepareForBlocksMovement()
        {
            TeardownBlockHandlesDataEditor();
            TeardownGateHandlesEditor();
            TeardownGeneratorHandlesEditor();
            TeardownInteractableHandlesEditor();

            LevelBlockBehavior[] levelBlockBehaviors =
                levelRepresentation.LevelTransform.GetComponentsInChildren<LevelBlockBehavior>();

            // Save multi-select positions BEFORE clearing (for restoration after reload).
            // Only save if not already saved by a batch operation (e.g. color/type change saves before Cancel).
            if (pendingMultiSelectPositions.Count == 0 && multiSelectedBlockIndices.Count > 0)
            {
                SavePendingMultiSelect();
            }

            selectedBlockMovementBehaviourIndex = -1;
            selectedBlock = null;
            multiSelectedBlockIndices.Clear();
            blockMovementData = new List<BlockMovementData>();
            for (int i = 0; i < levelBlockBehaviors.Length; i++)
            {
                blockMovementData.Add(new BlockMovementData(levelBlockBehaviors[i],
                    GetVector2IntPosition(levelBlockBehaviors[i].transform.position)));
            }

            movementManager = new BlockMovementManager();
            movementManager.SetLevelRepresentation(levelRepresentation);

            levelPreviewInitialized = true;
        }

        private void TeardownBlockHandlesDataEditor()
        {
            if (blockHandlesDataEditor != null)
            {
                DestroyImmediate(blockHandlesDataEditor);
                blockHandlesDataEditor = null;
            }
        }

        private void TeardownGateHandlesEditor()
        {
            if (gateHandlesEditor != null)
            {
                DestroyImmediate(gateHandlesEditor);
                gateHandlesEditor = null;
            }
        }

        private void TeardownGeneratorHandlesEditor()
        {
            if (generatorHandlesEditor != null)
            {
                DestroyImmediate(generatorHandlesEditor);
                generatorHandlesEditor = null;
            }
        }

        private void TeardownInteractableHandlesEditor()
        {
            if (interactableHandlesEditor != null)
            {
                DestroyImmediate(interactableHandlesEditor);
                interactableHandlesEditor = null;
            }
        }

        public bool IsGateSelected => gateHandlesEditor != null;
        public bool IsGeneratorSelected => generatorHandlesEditor != null;
        public bool IsInteractableSelected => interactableHandlesEditor != null;
        public Vector2Int GateOriginalGridPosition => gateOriginalGridPosition;
        public List<Vector2Int> GateUnifiedCellPositions => gateUnifiedCellPositions;
        public Vector2Int GeneratorOriginalGridPosition => generatorOriginalGridPosition;
        public Vector2Int InteractableOriginalGridPosition => interactableOriginalGridPosition;

        public void SelectBlock(Vector2Int index)
        {
            for (int i = 0; i < blockMovementData.Count; i++)
            {
                if (blockMovementData[i].recordedPosition + blockMovementData[i].pivotPoint == index)
                {
                    OnSelectBlock(i, Event.current.control);
                    return;
                }
            }
        }

        public void SelectGate(Vector2Int position)
        {
            for (int i = 0; i < gateDatas.Count; i++)
            {
                if (gateDatas[i].blockPosition == position)
                {
                    OnGateSelected(i, false);
                    return;
                }
            }
        }

        public void SelectGenerator(Vector2Int position)
        {
            if (generatorDatas == null) return;

            for (int i = 0; i < generatorDatas.Count; i++)
            {
                if (generatorDatas[i] == null || !generatorDatas[i].generator) continue;

                if (generatorDatas[i].position == position)
                {
                    OnGeneratorSelected(i);
                    return;
                }
            }
        }

        public void SelectInteractable(Vector2Int position)
        {
            if (interactableDatas == null) return;

            for (int i = 0; i < interactableDatas.Count; i++)
            {
                if (interactableDatas[i] == null || !interactableDatas[i].interactable) continue;

                if (interactableDatas[i].position == position)
                {
                    OnInteractableSelected(i);
                    return;
                }
            }
        }

        private void RegisterLevelChangeUndo(string actionName)
        {
            handleBeginLevelChangeUndoCallback?.Invoke(actionName);
        }

        private void OnSelectedBlockBufferSerializedExternalCommit(SerializedObject changedObject)
        {
            if (changedObject == null || selectedBlockEditor == null ||
                changedObject.targetObject != selectedBlockEditor)
            {
                return;
            }

            if (selectedGateMovementIndex != -1)
            {
                RegisterLevelChangeUndo("Edit Gate Data");
                handleUpdateGateDataCallback?.Invoke(gateOriginalGridPosition, gateUnifiedCellPositions);
                return;
            }

            if (selectedGeneratorIndex != -1)
            {
                RegisterLevelChangeUndo("Edit Generator Data");
                handleUpdateGeneratorDataCallback?.Invoke(generatorOriginalGridPosition);
                return;
            }

            if (selectedInteractableIndex != -1)
            {
                RegisterLevelChangeUndo("Edit Interactable Data");
                handleUpdateInteractableDataCallback?.Invoke(interactableOriginalGridPosition);
                return;
            }

            if (selectedBlockMovementBehaviourIndex != -1)
            {
                RegisterLevelChangeUndo(multiSelectedBlockIndices.Count > 0
                    ? "Edit Blocks (Multi)"
                    : "Edit Block Data");
                handleApplyBlockSceneInspector?.Invoke(GetAllSelectedPositions());
            }
        }

        private void DuringSceneGui(SceneView view)
        {
            if (!levelPreviewInitialized)
            {
                return;
            }

            if (SceneManager.GetActiveScene().name != GameConstant.SCENE_LEVEL_EDITOR)
            {
                Unsubscribe();
                return;
            }

            CleanupDestroyedGates();
            CleanupDestroyedGenerators();
            CleanupDestroyedInteractables();

            // While the pie menu is open it owns all input; draw it and skip normal scene handling.
            if (scenePieMenu.IsOpen)
            {
                DrawTargetCellHighlight(pieMenuTargetCell);
                scenePieMenu.OnGUI(view);
                return;
            }

            if (selectedBlockMovementBehaviourIndex == -1 && selectedGateMovementIndex == -1 &&
                selectedGeneratorIndex == -1 && selectedInteractableIndex == -1)
            {
                backupHandlesColor = Handles.color;
                Handles.color = new Color(0, 0, 0, 0);
                // Run before the (transparent) selection buttons so a Ctrl+Click claims the event
                // instead of selecting the element underneath.
                HandleQuickEditInput();
                DrawGeneratorButtons();
                DrawInteractableButtons();
                DrawGateButtons();
                DrawBlockButtons();

                Handles.color = backupHandlesColor;
            }
            else if (selectedInteractableIndex != -1)
            {
                DrawInteractableHandlesMenu();
                HandleInteractableMouseEvents();
                DrawInteractableMovementHandles();
            }
            else if (selectedGeneratorIndex != -1)
            {
                DrawGeneratorHandlesMenu();
                HandleGeneratorMouseEvents();
                DrawGeneratorMovementHandles();
            }
            else if (selectedGateMovementIndex != -1)
            {
                DrawGateHandlesMenu();
                HandleGateMouseEvents();
                DrawGateMovementHandles();
            }
            else
            {
                DrawHandlesMenu();
                HandleMouseEvents();
                DrawFigureHandles();
                DrawMultiSelectHighlights();
            }
        }

        // Element options offered by the Ctrl+Click pie menu, keyed by the targeted cell's current type.
        // Empty and Obstacle share the tile list: both are plain board cells rendered as walls, so designers
        // repaint them exactly like an inner tile.
        private static readonly ElementType[] TileSwitchOptions =
        {
            ElementType.Empty, ElementType.InnerTile, ElementType.Border,
            ElementType.Obstacle, ElementType.InteractableObject, ElementType.Block,
        };

        private static readonly ElementType[] BorderSwitchOptions =
        {
            ElementType.InnerTile, ElementType.Gate, ElementType.Generator,
        };

        /// <summary>
        /// Ctrl+Click while nothing is selected: detects the targeted board cell and opens a radial pie menu
        /// of element types it can switch to. Consumes the left MouseDown so the underlying selection buttons
        /// don't also fire.
        /// </summary>
        private void HandleQuickEditInput()
        {
            Event ev = Event.current;
            if (!ev.control || ev.alt || levelData == null)
            {
                return;
            }

            if (!TryGetBoardCell(ev.mousePosition, out Vector2Int cell))
            {
                return;
            }

            ElementType cellType = GetElementTypeAt(cell);
            if (!CanOpenSwitchPieMenu(cellType))
            {
                return;
            }

            DrawTargetCellHighlight(cell);

            if (ev.type == EventType.MouseMove)
            {
                HandleUtility.Repaint();
                return;
            }

            if (ev.type == EventType.MouseDown && ev.button == 0)
            {
                OpenSwitchPieMenu(cell, cellType, ev.mousePosition);
                ev.Use();
            }
        }

        /// <summary>
        /// Cells the pie menu can retarget. Empty cells count even though they hold no visible tile: an Empty
        /// cell touching a playable tile spawns a wall at runtime, so designers see and click it in the scene.
        /// Obstacle cells spawn inner walls and are edited the same way.
        /// </summary>
        private static bool CanOpenSwitchPieMenu(ElementType type)
        {
            switch (type)
            {
                case ElementType.Empty:
                case ElementType.InnerTile:
                case ElementType.Border:
                case ElementType.Obstacle:
                    return true;
                default:
                    return false;
            }
        }

        private void OpenSwitchPieMenu(Vector2Int cell, ElementType cellType, Vector2 screenPosition)
        {
            ElementType[] options = cellType == ElementType.Border ? BorderSwitchOptions : TileSwitchOptions;

            var items = new List<ScenePieMenu.Item>(options.Length);
            for (int i = 0; i < options.Length; i++)
            {
                ElementType option = options[i];
                // Painting a cell with its own type is a toggle-off that silently becomes InnerTile — hide it
                // so every sector maps to the element it shows.
                if (option == cellType)
                {
                    continue;
                }

                items.Add(new ScenePieMenu.Item
                {
                    Label = GetElementShortLabel(option),
                    Icon = GetElementIcon(option),
                    AccentColor = GetElementAccentColor(option),
                    OnSelected = () => ApplyCellSwitch(cell, option),
                });
            }

            pieMenuTargetCell = cell;
            scenePieMenu.Open(screenPosition, items);
            SceneView.RepaintAll();
        }

        private void ApplyCellSwitch(Vector2Int cell, ElementType newType)
        {
            RegisterLevelChangeUndo($"Switch Cell To {newType}");
            handleSwitchCellElementCallback?.Invoke(cell, newType);
        }

        /// <summary>
        /// Maps a screen point to the board cell underneath it by intersecting the camera ray with the board
        /// plane (y = 0, where all cells are spawned). Plane intersection avoids the per-mesh collider rounding
        /// errors that made the previous Physics.Raycast-based inner tile detection land on neighbour cells.
        /// </summary>
        private bool TryGetBoardCell(Vector2 mousePosition, out Vector2Int cell)
        {
            cell = default;

            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            Plane boardPlane = new Plane(Vector3.up, Vector3.zero);
            if (!boardPlane.Raycast(ray, out float enter))
            {
                return false;
            }

            cell = GetVector2IntPosition(ray.GetPoint(enter));
            Vector2Int size = levelData.Size;
            return cell.x >= 0 && cell.y >= 0 && cell.x < size.x && cell.y < size.y;
        }

        private ElementType GetElementTypeAt(Vector2Int position)
        {
            // Read the layer currently being edited so detection reflects the previewed cells.
            LevelElementData[] elements = activeLayerElements ?? levelData.Elements;
            if (elements == null)
            {
                return ElementType.Empty;
            }

            for (int i = 0; i < elements.Length; i++)
            {
                if (elements[i] != null && elements[i].Position == position)
                {
                    return elements[i].Type;
                }
            }

            return ElementType.Empty;
        }

        private void DrawTargetCellHighlight(Vector2Int cell)
        {
            Vector3 center = new Vector3(cell.x, 0f, cell.y) + handlesCubeOffset;
            Color previousColor = Handles.color;
            Handles.color = new Color(1f, 1f, 1f, 0.9f);
            Handles.DrawWireCube(center, Vector3.one * 1.05f);
            Handles.color = previousColor;
        }

        private Texture GetElementIcon(ElementType type)
        {
            ElementTypeEditorData data = levelDatabase != null ? levelDatabase.GetCellEditorData(type) : null;
            return data != null ? data.Texture : null;
        }

        private Color GetElementAccentColor(ElementType type)
        {
            ElementTypeEditorData data = levelDatabase != null ? levelDatabase.GetCellEditorData(type) : null;
            return data != null ? data.Color : new Color(0.3f, 0.6f, 0.9f);
        }

        private static string GetElementShortLabel(ElementType type)
        {
            switch (type)
            {
                case ElementType.InnerTile: return "Inner";
                case ElementType.InteractableObject: return "Inter.";
                case ElementType.Generator: return "Gen.";
                case ElementType.Obstacle: return "Obs.";
                default: return type.ToString();
            }
        }

        private void HandleMouseEvents()
        {
            // Scene GUI can keep running while the selected block preview object is destroyed/rebuilt.
            // Unity's destroyed objects still have a managed reference, but behave like null and throw on access.
            if (selectedBlock != null && !selectedBlock.levelBlock)
            {
                TeardownBlockHandlesDataEditor();
                movementManager?.ReleaseObject();
                selectedBlock = null;
                selectedBlockMovementBehaviourIndex = -1;
                multiSelectedBlockIndices.Clear();
                isDuplicating = false;
                return;
            }

            if ((Event.current.isMouse) && (Event.current.type == EventType.MouseUp))
            {
                if (Event.current.button == 0)
                {
                    if (isDuplicating)
                    {
                        FinishDuplicate();
                    }
                    else
                    {
                        Vector2Int? clickedBlockPosition = GetBlockPositionAtMouse();
                        Vector2Int selectedPos = selectedBlock.recordedPosition + selectedBlock.pivotPoint;

                        if (clickedBlockPosition.HasValue && clickedBlockPosition.Value != selectedPos)
                        {
                            if (Event.current.control)
                            {
                                Save();
                                for (int i = 0; i < blockMovementData.Count; i++)
                                {
                                    if (blockMovementData[i].recordedPosition + blockMovementData[i].pivotPoint == clickedBlockPosition.Value)
                                    {
                                        OnSelectBlock(i, true);
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                Save();
                                SelectBlockAtPosition(clickedBlockPosition.Value);
                            }
                        }
                        else
                        {
                            int gateIndex = GetGateIndexAtMouse();
                            if (gateIndex != -1)
                            {
                                Cancel();
                                OnGateSelected(gateIndex);
                            }
                            else
                            {
                                int generatorIndex = GetGeneratorIndexAtMouse();
                                if (generatorIndex != -1)
                                {
                                    Cancel();
                                    OnGeneratorSelected(generatorIndex);
                                }
                                else
                                {
                                    int interactableIndex = GetInteractableIndexAtMouse();
                                    if (interactableIndex != -1)
                                    {
                                        Cancel();
                                        OnInteractableSelected(interactableIndex);
                                    }
                                    else
                                    {
                                        Vector2Int currentPos = GetVector2IntPosition(selectedBlock.levelBlock.transform.position);
                                        if (currentPos != selectedBlock.recordedPosition)
                                        {
                                            Save();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (isDuplicating)
                    {
                        CancelDuplicate();
                    }
                    else
                    {
                        Cancel();
                    }
                }
            }
        }

        private Vector2Int? GetBlockPositionAtMouse()
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 100f))
            {
                Vector2Int hitPosition = GetVector2IntPosition(hit.point);
                for (int i = 0; i < blockMovementData.Count; i++)
                {
                    Vector2Int blockPos = blockMovementData[i].recordedPosition + blockMovementData[i].pivotPoint;

                    if (blockPos == hitPosition)
                    {
                        return blockPos;
                    }

                    for (int j = 0; j < blockMovementData[i].points.Length; j++)
                    {
                        Vector2Int pointPos = blockMovementData[i].recordedPosition +
                                              new Vector2Int(
                                                  Mathf.RoundToInt(blockMovementData[i].points[j].x),
                                                  Mathf.RoundToInt(blockMovementData[i].points[j].z)
                                              );

                        if (pointPos == hitPosition)
                        {
                            return blockPos;
                        }
                    }
                }
            }

            return null;
        }

        private int GetGateIndexAtMouse()
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                Vector2Int hitPosition = GetVector2IntPosition(hit.point);
                if (gateDatas == null)
                {
                    return -1;
                }
                for (int i = 0; i < gateDatas.Count; i++)
                {
                    if (gateDatas[i] == null || !gateDatas[i].gate)
                    {
                        continue;
                    }

                    if (gateDatas[i].blockPosition == hitPosition)
                        return i;
                }
            }
            return -1;
        }

        private void CleanupDestroyedGates()
        {
            if (gateDatas == null || gateDatas.Count == 0)
            {
                return;
            }

            bool removedAny = false;
            for (int i = gateDatas.Count - 1; i >= 0; i--)
            {
                GateData gd = gateDatas[i];
                if (gd == null || !gd.gate)
                {
                    gateDatas.RemoveAt(i);
                    removedAny = true;
                }
            }

            if (!removedAny)
            {
                return;
            }

            if (selectedGateMovementIndex >= gateDatas.Count || selectedGateIndex >= gateDatas.Count)
            {
                selectedGateMovementIndex = -1;
                selectedGateIndex = -1;
                selectedGateForMovement = null;
                gateUnifiedCellPositions = null;
            }
        }

        private void SelectBlockAtPosition(Vector2Int position)
        {
            for (int i = 0; i < blockMovementData.Count; i++)
            {
                if (blockMovementData[i].recordedPosition + blockMovementData[i].pivotPoint == position)
                {
                    OnSelectBlock(i);
                    return;
                }
            }
        }

        private void OnSelectBlock(int i, bool isCtrlHeld = false)
        {
            if (isCtrlHeld && selectedBlockMovementBehaviourIndex != -1)
            {
                // Ctrl+Click: toggle block in multi-select list (primary stays unchanged)
                if (i == selectedBlockMovementBehaviourIndex)
                    return; // Can't deselect primary via Ctrl+Click

                if (multiSelectedBlockIndices.Contains(i))
                    multiSelectedBlockIndices.Remove(i);
                else
                    multiSelectedBlockIndices.Add(i);
                return;
            }

            // Normal single select - clear multi-select
            
            multiSelectedBlockIndices.Clear();
            selectedBlockMovementBehaviourIndex = i;
            selectedBlock = blockMovementData[i];
            movementManager.Enable(selectedBlock.levelBlock);
            handleCreateBlockSceneInspector?.Invoke(selectedBlock.recordedPosition + selectedBlock.pivotPoint);
        }

        private void DrawGateButtons()
        {
            if (gateDatas == null || gateDatas.Count == 0)
            {
                return;
            }

            for (int gateIndex = 0; gateIndex < gateDatas.Count; gateIndex++)
            {
                if (TryHandleGateButton(gateIndex))
                    return;
            }
        }

        private bool TryHandleGateButton(int gateIndex)
        {
            if (gateDatas == null || gateIndex < 0 || gateIndex >= gateDatas.Count)
            {
                return false;
            }

            GateData gateData = gateDatas[gateIndex];
            if (gateData == null || !gateData.gate)
            {
                return false;
            }

            if (Handles.Button(
                    gateData.gate.transform.position + handlesCubeOffset,
                    Quaternion.identity,
                    1.05f,
                    1.05f,
                    Handles.CubeHandleCap))
            {
                OnGateSelected(gateIndex);
                return true;
            }

            return false;
        }

        private void OnGateSelected(int gateIndex, bool openFigureSelector = false)
        {
            if (gateDatas == null || gateIndex < 0 || gateIndex >= gateDatas.Count)
            {
                return;
            }

            if (gateDatas[gateIndex] == null || !gateDatas[gateIndex].gate)
            {
                return;
            }

            selectedGateIndex = gateIndex;
            selectedGateMovementIndex = gateIndex;
            selectedGateForMovement = gateDatas[gateIndex];
            gateOriginalWorldPosition = selectedGateForMovement.gate.transform.position;
            gateOriginalGridPosition = selectedGateForMovement.blockPosition;

            gateUnifiedCellPositions = new List<Vector2Int> { gateOriginalGridPosition };

            handleCreateGateEditorCallback?.Invoke(gateOriginalGridPosition);
            if (openFigureSelector)
            {
                SpawnBlockFromGateMenu();
            }
            Handles.color = backupHandlesColor;
        }

        private void CollectValidBlocks(
            out LevelFigure[] figures,
            out BlockType[] types)
        {
            var figureList = new List<LevelFigure>();
            var typeList = new List<BlockType>();

            foreach (var block in blocksVisuals.Blocks)
            {
                var figure = block.Prefab
                    .GetComponent<LevelBlockBehavior>()
                    .Figure;

                // if (!IsFigureFitGate(figure, gate, gateLength))
                //     continue;

                figureList.Add(figure);
                typeList.Add(block.Type);
            }

            figures = figureList.ToArray();
            types = typeList.ToArray();
        }


        private void SpawnBlockFromGate(EditorSelectBlockData data)
        {
            GateData gateData = gateDatas[selectedGateIndex];
            RegisterLevelChangeUndo("Spawn Block");
            handleBlockSpawnCallback?.Invoke(gateData.blockPosition, gateData.blockSpawnPosition, data);
        }

        private void DrawBlockButtons()
        {
            for (int i = 0; i < blockMovementData.Count; i++)
            {
                for (int j = 0; j < blockMovementData[i].points.Length; j++)
                {
                    if (Handles.Button(
                            blockMovementData[i].Position + blockMovementData[i].points[j] + handlesCubeOffset,
                            Quaternion.identity, 1f, 1f, Handles.CubeHandleCap))
                    {
                        OnSelectBlock(i, Event.current.control);
                        Handles.color = backupHandlesColor;
                        return;
                    }
                }
            }
        }


        private void DrawHandlesMenu()
        {
            Handles.BeginGUI();
            Event ev = Event.current;
            bool isMultiSelect = multiSelectedBlockIndices.Count > 0;

            if (ev is { type: EventType.KeyDown })
            {
                if (ev.control && ev.keyCode == KeyCode.D && !isDuplicating && !isMultiSelect)
                {
                    StartDuplicate();
                    ev.Use();
                    Handles.EndGUI();
                    return;
                }

                if (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter || ev.keyCode == KeyCode.Escape)
                {
                    if (isDuplicating)
                    {
                        CancelDuplicate();
                    }
                    else
                    {
                        Cancel();
                    }

                    ev.Use();
                    Handles.EndGUI();
                    return;
                }

                if (ev.keyCode == KeyCode.Delete && !isDuplicating)
                {
                    List<Vector2Int> deletePositions = GetAllSelectedPositions();
                    if (deletePositions.Count > 0)
                    {
                        RegisterLevelChangeUndo(deletePositions.Count > 1 ? "Delete Blocks" : "Delete Block");
                    }
                    Cancel();
                    foreach (var pos in deletePositions)
                    {
                        handleBlockDeleteCallback?.Invoke(pos);
                    }
                    ev.Use();
                    Handles.EndGUI();
                    return;
                }
            }

            int totalSelected = 1 + multiSelectedBlockIndices.Count;
            int menuBaseHeight = isDuplicating ? 100 : (isMultiSelect ? 130 : 100);
            int inspectorDrawHeight = isDuplicating ? 0 : blockDataInspectorHeight;
            string menuTitle = isDuplicating ? "Duplicating Block (Place or Esc to cancel)" 
                : isMultiSelect ? $"Multi-Select ({totalSelected} blocks)" 
                : "Handles menu";
            EnsureMenuRectInitialized(ref blockHandlesMenuRect, DEFAULT_BLOCK_MENU_POS, PREFS_BLOCK_MENU_POS_X, PREFS_BLOCK_MENU_POS_Y);
            blockHandlesMenuRect.size = new Vector2(300, menuBaseHeight + inspectorDrawHeight);
            GUILayout.BeginArea(blockHandlesMenuRect, menuTitle, GUI.skin.window);
    
            if (isDuplicating)
            {
                GUILayout.Label("Duplicating block - Move and click to place");
                GUILayout.Label("Press Esc or Right Click to cancel");
        
                if (GUILayout.Button("Cancel Duplicate (Esc)"))
                {
                    CancelDuplicate();
                }
            }
            else
            {
                if (isMultiSelect)
                {
                    GUILayout.Label($"Selected {totalSelected} blocks (Primary: #{selectedBlockMovementBehaviourIndex + 1})");
                    GUILayout.Label("Ctrl+Click to add/remove blocks");
                }
                else
                {
                    GUILayout.Label("Selected #" + (selectedBlockMovementBehaviourIndex + 1));
                }

                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button(new GUIContent("Ping", "Ping the block GameObject in Hierarchy"), GUILayout.Width(50)))
                {
                    Ping();
                }
                
                if (GUILayout.Button(new GUIContent("Cancel", "Cancel selection (Esc)")))
                {
                    Cancel();
                }

                if (!isMultiSelect)
                {
                    if (GUILayout.Button(new GUIContent("Duplicate", "Duplicate block (Ctrl+D)")))
                    {
                        StartDuplicate();
                    }
                }

                string deleteLabel = isMultiSelect ? $"Delete {totalSelected}" : "Delete";
                string deleteTooltip = isMultiSelect ? $"Delete {totalSelected} blocks (Del)" : "Delete block (Del)";
                if (GUILayout.Button(new GUIContent(deleteLabel, deleteTooltip)))
                {
                    List<Vector2Int> deletePositions = GetAllSelectedPositions();
                    if (deletePositions.Count > 0)
                    {
                        RegisterLevelChangeUndo(deletePositions.Count > 1 ? "Delete Blocks" : "Delete Block");
                    }
                    Cancel();
                    foreach (var pos in deletePositions)
                    {
                        handleBlockDeleteCallback?.Invoke(pos);
                    }
                }


                EditorGUILayout.EndHorizontal();

                if (blockHandlesDataEditor != null && blockHandlesDataEditor.serializedObject != null)
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.BeginVertical();
                    DrawSelectedBlockInspectorWithoutTypePicker(blockHandlesDataEditor.serializedObject);
                    EditorGUILayout.EndVertical();

                    if (Event.current.type == EventType.Repaint)
                    {
                        blockDataInspectorHeight = Mathf.CeilToInt(GUILayoutUtility.GetLastRect().height);
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        RegisterLevelChangeUndo(multiSelectedBlockIndices.Count > 0
                            ? "Edit Blocks (Multi)"
                            : "Edit Block Data");
                        handleApplyBlockSceneInspector?.Invoke(GetAllSelectedPositions());
                    }
                }
            }

            GUILayout.EndArea();

            HandleMenuDragging(ref blockHandlesMenuRect, ref isDraggingBlockMenu, ref blockMenuDragOffset,
                PREFS_BLOCK_MENU_POS_X, PREFS_BLOCK_MENU_POS_Y);

            if (blockHandlesMenuRect.Contains(Event.current.mousePosition) &&
                (Event.current.type == EventType.MouseDown ||
                 Event.current.type == EventType.MouseUp ||
                 Event.current.type == EventType.MouseDrag ||
                 Event.current.type == EventType.ScrollWheel))
            {
                Event.current.Use();
            }

            Handles.EndGUI();
        }

        private void Ping()
        {
            if (selectedBlock == null || !selectedBlock.levelBlock)
            {
                Cancel();
                return;
            }

            Selection.activeGameObject = selectedBlock.levelBlock.gameObject;
        }

        private static readonly HashSet<string> HiddenSelectedBlockFields = new HashSet<string>
        {
            "position",
        };

        private static readonly HashSet<string> HiddenGeneratorFields = new HashSet<string>
        {
            "position",
            "type",
        };

        public static void DrawSelectedBlockInspectorWithoutTypePicker(SerializedObject serializedObject)
            => DrawInspectorWithHiddenFields(serializedObject, HiddenSelectedBlockFields);

        public static void DrawGeneratorInspector(SerializedObject serializedObject)
            => DrawInspectorWithHiddenFields(serializedObject, HiddenGeneratorFields);

        private static void DrawInspectorWithHiddenFields(SerializedObject serializedObject, HashSet<string> hiddenFields)
        {
            serializedObject.UpdateIfRequiredOrScript();

            SerializedProperty dataProperty = serializedObject.FindProperty("data");
            if (dataProperty != null && dataProperty.managedReferenceValue != null)
            {
                SerializedProperty iterator = dataProperty.Copy();
                SerializedProperty endProperty = dataProperty.GetEndProperty();
                bool enterChildren = true;
                while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
                {
                    enterChildren = false;
                    if (hiddenFields.Contains(iterator.name))
                        continue;
                    EditorGUILayout.PropertyField(iterator, true);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void Cancel()
        {
            TeardownBlockHandlesDataEditor();

            if (selectedBlock != null)
            {
                // If the scene preview object was destroyed, we can only clear editor selection state.
                if (selectedBlock.levelBlock)
                {
                    selectedBlock.currentPosition = selectedBlock.recordedPosition;
                    selectedBlock.UpdatePosition();
                }
            }

            movementManager?.ReleaseObject();
            selectedBlock = null;
            selectedBlockMovementBehaviourIndex = -1;
            multiSelectedBlockIndices.Clear();
        }

        /// <summary>
        /// Returns positions of all selected blocks. Multi-selected blocks first, primary block last
        /// (so the primary gets re-selected after level preview reload).
        /// </summary>
        private List<Vector2Int> GetAllSelectedPositions()
        {
            List<Vector2Int> positions = new List<Vector2Int>();
            if (selectedBlock != null)
            {
                foreach (int idx in multiSelectedBlockIndices)
                {
                    if (idx < blockMovementData.Count)
                    {
                        positions.Add(blockMovementData[idx].recordedPosition + blockMovementData[idx].pivotPoint);
                    }
                }
                // Primary block last
                positions.Add(selectedBlock.recordedPosition + selectedBlock.pivotPoint);
            }
            return positions;
        }

        /// <summary>
        /// Saves current multi-select block positions so they can be restored after a level preview reload.
        /// Must be called BEFORE blockMovementData is replaced or multiSelectedBlockIndices is cleared.
        /// </summary>
        private void SavePendingMultiSelect()
        {
            pendingMultiSelectPositions.Clear();
            if (blockMovementData == null) return;

            foreach (int idx in multiSelectedBlockIndices)
            {
                if (idx < blockMovementData.Count)
                {
                    pendingMultiSelectPositions.Add(
                        blockMovementData[idx].recordedPosition + blockMovementData[idx].pivotPoint);
                }
            }
        }

        /// <summary>
        /// Restores multi-select from saved positions after level reload and primary block re-selection.
        /// </summary>
        public void RestoreMultiSelect()
        {
            if (pendingMultiSelectPositions.Count == 0 || blockMovementData == null) return;

            // Can only restore if a primary block is selected
            if (selectedBlockMovementBehaviourIndex == -1)
            {
                pendingMultiSelectPositions.Clear();
                return;
            }

            foreach (var pos in pendingMultiSelectPositions)
            {
                for (int i = 0; i < blockMovementData.Count; i++)
                {
                    if (blockMovementData[i].recordedPosition + blockMovementData[i].pivotPoint == pos
                        && i != selectedBlockMovementBehaviourIndex
                        && !multiSelectedBlockIndices.Contains(i))
                    {
                        multiSelectedBlockIndices.Add(i);
                        break;
                    }
                }
            }

            pendingMultiSelectPositions.Clear();
        }

        private void StartDuplicate()
        {
            if (selectedBlock == null || !selectedBlock.levelBlock)
            {
                Cancel();
                return;
            }

            isDuplicating = true;
            duplicateSourcePosition = selectedBlock.recordedPosition + selectedBlock.pivotPoint;

            // Get block data từ source
            BlockType blockType = selectedBlock.levelBlock.BlockConfig.Type;
            BlockColor blockColor = selectedBlock.levelBlock.OriginColorConfig.Type;

            Debug.Log(
                $"Starting duplicate of block at {duplicateSourcePosition}, Type: {blockType}, Color: {blockColor}");

            // Block đang được drag sẽ là selectedBlock hiện tại, không cần tạo visual mới
            // Chỉ cần track state là đang duplicate
        }

        private void FinishDuplicate()
        {
            if (!isDuplicating) return;
            if (selectedBlock == null || !selectedBlock.levelBlock)
            {
                Cancel();
                return;
            }

            Vector2Int newPosition = GetVector2IntPosition(selectedBlock.levelBlock.transform.position) +
                                     selectedBlock.pivotPoint;
            Vector2Int size = levelData.Size;

            if ((newPosition.x >= 0) && (newPosition.y >= 0) &&
                (newPosition.x < size.x) && (newPosition.y < size.y))
            {
                LevelElementData sourceElement = null;
                // Read from the active layer so duplicating a block inside the extra layer finds its source.
                LevelElementData[] elements = activeLayerElements ?? levelData.Elements;
                if (elements != null)
                {
                    for (int i = 0; i < elements.Length; i++)
                    {
                        LevelElementData el = elements[i];
                        if (el != null && el.Position == duplicateSourcePosition && el.Type == ElementType.Block)
                        {
                            sourceElement = el;
                            break;
                        }
                    }
                }

                if (sourceElement == null)
                {
                    Debug.LogWarning(
                        $"Duplicate Block: no block LevelElementData at source {duplicateSourcePosition}.");
                }
                else
                {
                    LevelElementData copy = sourceElement.Clone();
                    copy.SetPosition(newPosition);
                    BlockLevelElementData copyBlock = copy as BlockLevelElementData;
                    EditorSelectBlockData data = new EditorSelectBlockData
                    {
                        blockType = copyBlock?.BlockType ?? default,
                        blockColor = copyBlock?.BlockColor ?? default,
                        spawnFromFullCopy = copy
                    };
                    RegisterLevelChangeUndo("Duplicate Block");
                    handleBlockSpawnCallback?.Invoke(duplicateSourcePosition, newPosition, data);

                    Debug.Log($"Duplicated block to position {newPosition}");
                }
            }
            else
            {
                Debug.LogWarning("Cannot duplicate to invalid position");
            }

            // Reset state
            isDuplicating = false;

            // Return to original position
            selectedBlock.currentPosition = selectedBlock.recordedPosition;
            selectedBlock.UpdatePosition();

            // Deselect
            movementManager.ReleaseObject();
            selectedBlock = null;
            selectedBlockMovementBehaviourIndex = -1;
        }

        private void CancelDuplicate()
        {
            if (!isDuplicating) return;
            Debug.Log("Cancelled duplicate");
            isDuplicating = false;
            if (selectedBlock != null && selectedBlock.levelBlock)
            {
                selectedBlock.currentPosition = selectedBlock.recordedPosition;
                selectedBlock.UpdatePosition();
            }

            // Keep selection but exit duplicate mode
            if (selectedBlock != null && selectedBlock.levelBlock)
                movementManager.Enable(selectedBlock.levelBlock);
        }

        private void Save()
        {
            if (selectedBlock == null || !selectedBlock.levelBlock)
            {
                Cancel();
                return;
            }

            selectedBlock.currentPosition = GetVector2IntPosition(selectedBlock.levelBlock.transform.position);
            selectedBlock.UpdatePosition();
            Vector2Int cur = selectedBlock.currentPosition;
            Vector2Int size = levelData.Size;

            if ((cur.x >= 0) && (cur.y >= 0) && (cur.x < size.x) && (cur.y < size.y))
            {
                RegisterLevelChangeUndo("Move Block");
                handleBlockChangeCallback?.Invoke(selectedBlock.recordedPosition + selectedBlock.pivotPoint,
                    selectedBlock.currentPosition + selectedBlock.pivotPoint);
                selectedBlock.recordedPosition = selectedBlock.currentPosition;
            }
        }

        private void DrawMultiSelectHighlights()
        {
            if (multiSelectedBlockIndices.Count == 0) return;

            Color originalColor = Handles.color;
            Handles.color = new Color(0f, 1f, 1f, 0.8f); // Cyan highlight for multi-selected blocks

            foreach (int idx in multiSelectedBlockIndices)
            {
                if (idx >= blockMovementData.Count) continue;

                var block = blockMovementData[idx];
                for (int i = 0; i < block.points.Length; i++)
                {
                    Vector3 pos = block.Position + block.points[i] + handlesCubeOffset;
                    Handles.DrawWireCube(pos, Vector3.one * 1.05f);
                }
            }

            Handles.color = originalColor;
        }

        private void HandleGateMouseEvents()
        {
            Event ev = Event.current;

            if (ev.isMouse && ev.type == EventType.MouseUp)
            {
                if (ev.button == 0 && selectedGateForMovement?.gate)
                {
                    Vector2Int currentGateGridPos = GetVector2IntPosition(selectedGateForMovement.gate.transform.position);
                    bool gateMoved = currentGateGridPos != gateOriginalGridPosition;

                    if (gateMoved)
                    {
                        SaveGate();
                        return;
                    }

                    Vector2Int? clickedBlockPosition = GetBlockPositionAtMouse();
                    if (clickedBlockPosition.HasValue)
                    {
                        CancelGate();
                        SelectBlockAtPosition(clickedBlockPosition.Value);
                        return;
                    }

                    int gateIndex = GetGateIndexAtMouse();
                    if (gateIndex != -1 && gateIndex != selectedGateMovementIndex)
                    {
                        CancelGate();
                        OnGateSelected(gateIndex);
                        return;
                    }

                    int generatorIndex = GetGeneratorIndexAtMouse();
                    if (generatorIndex != -1)
                    {
                        CancelGate();
                        OnGeneratorSelected(generatorIndex);
                        return;
                    }

                    int interactableIndex = GetInteractableIndexAtMouse();
                    if (interactableIndex != -1)
                    {
                        CancelGate();
                        OnInteractableSelected(interactableIndex);
                        return;
                    }
                }
                else
                {
                    CancelGate();
                }
            }
        }

        private void DrawGateHandlesMenu()
        {
            Handles.BeginGUI();
            Event ev = Event.current;

            if (ev is { type: EventType.KeyDown })
            {
                if (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter || ev.keyCode == KeyCode.Escape)
                {
                    CancelGate();
                    ev.Use();
                    Handles.EndGUI();
                    return;
                }

                if (ev.keyCode == KeyCode.Delete)
                {
                    DeleteSelectedGate();
                    ev.Use();
                    Handles.EndGUI();
                    return;
                }
            }

            int cellCount = gateUnifiedCellPositions != null ? gateUnifiedCellPositions.Count : 1;
            string menuTitle = "Gate Handles Menu";
            int menuHeight = 80 + gateInspectorHeight;
            EnsureMenuRectInitialized(ref gateHandlesMenuRect, DEFAULT_GATE_MENU_POS, PREFS_GATE_MENU_POS_X,
                PREFS_GATE_MENU_POS_Y);
            gateHandlesMenuRect.size = new Vector2(300, menuHeight);
            GUILayout.BeginArea(gateHandlesMenuRect, menuTitle, GUI.skin.window);

            GUILayout.Label($"Gate at {gateOriginalGridPosition} (size: {cellCount})");

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button(new GUIContent("Ping", "Ping the gate GameObject in Hierarchy"), GUILayout.Width(50)))
            {
                PingGate();
            }
            
            if (GUILayout.Button(new GUIContent("Cancel", "Cancel selection (Esc)")))
            {
                CancelGate();
            }

            if (GUILayout.Button(new GUIContent("Delete", "Delete gate (Del)")))
            {
                DeleteSelectedGate();
            }


            EditorGUILayout.EndHorizontal();

            if (gateHandlesEditor != null && gateHandlesEditor.serializedObject != null)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.BeginVertical();
                DrawSelectedBlockInspectorWithoutTypePicker(gateHandlesEditor.serializedObject);
                EditorGUILayout.EndVertical();

                if (Event.current.type == EventType.Repaint)
                {
                    gateInspectorHeight = Mathf.CeilToInt(GUILayoutUtility.GetLastRect().height);
                }

                bool hostGuiChanged = EditorGUI.EndChangeCheck();
                if (hostGuiChanged)
                {
                    RegisterLevelChangeUndo("Edit Gate Data");
                    handleUpdateGateDataCallback?.Invoke(gateOriginalGridPosition, gateUnifiedCellPositions);
                }
            }

            GUILayout.EndArea();

            HandleMenuDragging(ref gateHandlesMenuRect, ref isDraggingGateMenu, ref gateMenuDragOffset,
                PREFS_GATE_MENU_POS_X, PREFS_GATE_MENU_POS_Y);

            if (gateHandlesMenuRect.Contains(Event.current.mousePosition) &&
                (Event.current.type == EventType.MouseDown ||
                 Event.current.type == EventType.MouseUp ||
                 Event.current.type == EventType.MouseDrag ||
                 Event.current.type == EventType.ScrollWheel))
            {
                Event.current.Use();
            }

            Handles.EndGUI();
        }

        private void PingGate()
        {
            if (selectedGateForMovement != null && selectedGateForMovement.gate != null)
            {
                Selection.activeGameObject = selectedGateForMovement.gate.gameObject;
            }
        }

        private void EnsureMenuRectInitialized(ref Rect menuRect, Vector2 defaultPosition, string xKey, string yKey)
        {
            if (menuRect.width > 0f && menuRect.height > 0f)
            {
                return;
            }

            menuRect.position = new Vector2(
                PlayerPrefs.GetFloat(xKey, defaultPosition.x),
                PlayerPrefs.GetFloat(yKey, defaultPosition.y));
        }

        private void HandleMenuDragging(ref Rect menuRect, ref bool isDragging, ref Vector2 dragOffset, string xKey,
            string yKey)
        {
            Event ev = Event.current;
            Rect headerRect = new Rect(menuRect.x, menuRect.y, menuRect.width, MENU_HEADER_HEIGHT);

            if (ev.type == EventType.MouseDown && ev.button == 0 && headerRect.Contains(ev.mousePosition))
            {
                isDragging = true;
                dragOffset = ev.mousePosition - menuRect.position;
                ev.Use();
                return;
            }

            if (!isDragging)
            {
                return;
            }

            if (ev.type == EventType.MouseDrag)
            {
                menuRect.position = ev.mousePosition - dragOffset;
                SaveMenuPosition(xKey, yKey, menuRect.position, false);
                ev.Use();
            }
            else if (ev.type == EventType.MouseUp)
            {
                isDragging = false;
                SaveMenuPosition(xKey, yKey, menuRect.position, true);
                ev.Use();
            }
        }

        private void SaveMenuPosition(string xKey, string yKey, Vector2 position, bool saveNow)
        {
            PlayerPrefs.SetFloat(xKey, position.x);
            PlayerPrefs.SetFloat(yKey, position.y);
            if (saveNow)
            {
                PlayerPrefs.Save();
            }
        }

        private void DrawGateMovementHandles()
        {
            if (selectedGateForMovement == null || selectedGateForMovement.gate == null) return;

            Transform gateTransform = selectedGateForMovement.gate.transform;
            Vector3 handlePos = gateTransform.position + Vector3.up;

            Vector3 newPosition = Handles.Slider2D(
                handlePos,
                Vector3.up,
                Vector3.right,
                Vector3.forward,
                0.5f,
                Handles.RectangleHandleCap,
                0f);

            if (!Mathf.Approximately(Vector3.Distance(handlePos, newPosition), 0))
            {
                gateTransform.position += (newPosition - handlePos);
            }
        }

        private void SaveGate()
        {
            if (selectedGateForMovement == null) return;

            Vector2Int newGridPos = GetVector2IntPosition(selectedGateForMovement.gate.transform.position);
            Vector2Int offset = newGridPos - gateOriginalGridPosition;

            if (offset == Vector2Int.zero)
            {
                CancelGate();
                return;
            }

            Vector2Int[] oldPositions = gateUnifiedCellPositions.ToArray();
            Vector2Int[] newPositions = oldPositions.Select(p => p + offset).ToArray();

            selectedGateForMovement.gate.transform.position = gateOriginalWorldPosition;

            selectedGateMovementIndex = -1;
            selectedGateForMovement = null;
            gateUnifiedCellPositions = null;
            TeardownGateHandlesEditor();

            RegisterLevelChangeUndo("Move Gate");
            handleGateChangeCallback?.Invoke(oldPositions, newPositions);
        }

        public void CancelGate()
        {
            if (selectedGateForMovement != null && selectedGateForMovement.gate != null)
            {
                selectedGateForMovement.gate.transform.position = gateOriginalWorldPosition;
            }

            selectedGateMovementIndex = -1;
            selectedGateForMovement = null;
            gateUnifiedCellPositions = null;
            TeardownGateHandlesEditor();
        }

        private void DeleteSelectedGate()
        {
            Vector2Int gatePosition = gateOriginalGridPosition;
            RegisterLevelChangeUndo("Delete Gate");
            CancelGate();
            handleBorderElementDeleteCallback?.Invoke(gatePosition);
        }

        private void DrawGeneratorButtons()
        {
            if (generatorDatas == null || generatorDatas.Count == 0)
            {
                return;
            }

            for (int i = 0; i < generatorDatas.Count; i++)
            {
                GeneratorData generatorData = generatorDatas[i];
                if (generatorData == null || !generatorData.generator)
                {
                    continue;
                }

                if (Handles.Button(
                        generatorData.generator.transform.position + handlesCubeOffset,
                        Quaternion.identity,
                        1.05f,
                        1.05f,
                        Handles.CubeHandleCap))
                {
                    OnGeneratorSelected(i);
                    Handles.color = backupHandlesColor;
                    return;
                }
            }
        }

        private void OnGeneratorSelected(int generatorIndex)
        {
            if (generatorDatas == null || generatorIndex < 0 || generatorIndex >= generatorDatas.Count)
            {
                return;
            }

            if (generatorDatas[generatorIndex] == null || !generatorDatas[generatorIndex].generator)
            {
                return;
            }

            selectedGeneratorIndex = generatorIndex;
            selectedGeneratorForMenu = generatorDatas[generatorIndex].generator;
            generatorOriginalGridPosition = generatorDatas[generatorIndex].position;
            generatorOriginalWorldPosition = selectedGeneratorForMenu.transform.position;

            handleCreateGeneratorEditorCallback?.Invoke(generatorOriginalGridPosition);
            Handles.color = backupHandlesColor;
        }

        private int GetGeneratorIndexAtMouse()
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                Vector2Int hitPosition = GetVector2IntPosition(hit.point);
                if (generatorDatas == null)
                {
                    return -1;
                }

                for (int i = 0; i < generatorDatas.Count; i++)
                {
                    if (generatorDatas[i] == null || !generatorDatas[i].generator)
                    {
                        continue;
                    }

                    if (generatorDatas[i].position == hitPosition)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private void HandleGeneratorMouseEvents()
        {
            Event ev = Event.current;

            if (!ev.isMouse || ev.type != EventType.MouseUp)
            {
                return;
            }

            if (ev.button == 0)
            {
                if (selectedGeneratorForMenu != null)
                {
                    Vector2Int currentGridPos = GetVector2IntPosition(selectedGeneratorForMenu.transform.position);
                    bool moved = currentGridPos != generatorOriginalGridPosition;
                    if (moved)
                    {
                        SaveGenerator();
                        return;
                    }
                }

                Vector2Int? clickedBlockPosition = GetBlockPositionAtMouse();
                if (clickedBlockPosition.HasValue)
                {
                    CancelGenerator();
                    SelectBlockAtPosition(clickedBlockPosition.Value);
                    return;
                }

                int gateIndex = GetGateIndexAtMouse();
                if (gateIndex != -1)
                {
                    CancelGenerator();
                    OnGateSelected(gateIndex);
                    return;
                }

                int generatorIndex = GetGeneratorIndexAtMouse();
                if (generatorIndex != -1 && generatorIndex != selectedGeneratorIndex)
                {
                    CancelGenerator();
                    OnGeneratorSelected(generatorIndex);
                    return;
                }

                int interactableIndex = GetInteractableIndexAtMouse();
                if (interactableIndex != -1)
                {
                    CancelGenerator();
                    OnInteractableSelected(interactableIndex);
                }
            }
            else
            {
                CancelGenerator();
            }
        }

        private void DrawGeneratorMovementHandles()
        {
            if (selectedGeneratorForMenu == null) return;

            Transform t = selectedGeneratorForMenu.transform;
            Vector3 handlePos = t.position + Vector3.up;

            Vector3 newPosition = Handles.Slider2D(
                handlePos,
                Vector3.up,
                Vector3.right,
                Vector3.forward,
                0.5f,
                Handles.RectangleHandleCap,
                0f);

            if (!Mathf.Approximately(Vector3.Distance(handlePos, newPosition), 0))
            {
                t.position += (newPosition - handlePos);
            }
        }

        private void SaveGenerator()
        {
            if (selectedGeneratorForMenu == null) return;

            Vector2Int newGridPos = GetVector2IntPosition(selectedGeneratorForMenu.transform.position);
            if (newGridPos == generatorOriginalGridPosition)
            {
                CancelGenerator();
                return;
            }

            // Revert scene object position; LevelEditorWindow will rebuild preview from serialized data.
            selectedGeneratorForMenu.transform.position = generatorOriginalWorldPosition;

            Vector2Int oldPos = generatorOriginalGridPosition;

            selectedGeneratorIndex = -1;
            selectedGeneratorForMenu = null;
            TeardownGeneratorHandlesEditor();

            RegisterLevelChangeUndo("Move Generator");
            handleGeneratorMoveCallback?.Invoke(oldPos, newGridPos);
        }

        private void DrawGeneratorHandlesMenu()
        {
            Handles.BeginGUI();
            Event ev = Event.current;

            if (ev is { type: EventType.KeyDown })
            {
                if (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter || ev.keyCode == KeyCode.Escape)
                {
                    CancelGenerator();
                    ev.Use();
                    Handles.EndGUI();
                    return;
                }

                if (ev.keyCode == KeyCode.Delete)
                {
                    DeleteSelectedGenerator();
                    ev.Use();
                    Handles.EndGUI();
                    return;
                }
            }

            string menuTitle = "Generator Handles Menu";
            int menuHeight = 140 + generatorInspectorHeight;
            EnsureMenuRectInitialized(ref generatorHandlesMenuRect, DEFAULT_GENERATOR_MENU_POS,
                PREFS_GENERATOR_MENU_POS_X, PREFS_GENERATOR_MENU_POS_Y);
            generatorHandlesMenuRect.size = new Vector2(300, menuHeight);
            GUILayout.BeginArea(generatorHandlesMenuRect, menuTitle, GUI.skin.window);

            GUILayout.Label($"Generator at {generatorOriginalGridPosition}");

            if (GUILayout.Button("Cancel selection (Esc)"))
            {
                CancelGenerator();
            }

            if (GUILayout.Button("Delete generator (Del)"))
            {
                DeleteSelectedGenerator();
            }

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Ping", GUILayout.Width(50)))
            {
                PingGenerator();
            }

            if (GUILayout.Button("Copy Queue"))
            {
                CopySelectedGeneratorQueueToClipboard();
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(generatorQueueClipboardJson)))
            {
                if (GUILayout.Button("Paste Queue"))
                {
                    PasteClipboardQueueToSelectedGenerator();
                }
            }

            GUILayout.EndHorizontal();

            if (generatorHandlesEditor != null && generatorHandlesEditor.serializedObject != null)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.BeginVertical();
                DrawGeneratorInspector(generatorHandlesEditor.serializedObject);
                EditorGUILayout.EndVertical();

                if (Event.current.type == EventType.Repaint)
                {
                    generatorInspectorHeight = Mathf.CeilToInt(GUILayoutUtility.GetLastRect().height);
                }

                bool hostGuiChanged = EditorGUI.EndChangeCheck();
                if (hostGuiChanged)
                {
                    RegisterLevelChangeUndo("Edit Generator Data");
                    handleUpdateGeneratorDataCallback?.Invoke(generatorOriginalGridPosition);
                }
            }

            GUILayout.EndArea();

            HandleMenuDragging(ref generatorHandlesMenuRect, ref isDraggingGeneratorMenu,
                ref generatorMenuDragOffset, PREFS_GENERATOR_MENU_POS_X, PREFS_GENERATOR_MENU_POS_Y);

            if (generatorHandlesMenuRect.Contains(Event.current.mousePosition) &&
                (Event.current.type == EventType.MouseDown ||
                 Event.current.type == EventType.MouseUp ||
                 Event.current.type == EventType.MouseDrag ||
                 Event.current.type == EventType.ScrollWheel))
            {
                Event.current.Use();
            }

            Handles.EndGUI();
        }

        [Serializable]
        private class GeneratorQueueClipboardWrapper
        {
            public List<GeneratorBlockEntry> queue;
        }

        private void CopySelectedGeneratorQueueToClipboard()
        {
            LevelElementData rawData = selectedBlockEditor ? selectedBlockEditor.Data : null;
            GeneratorLevelElementData data = rawData as GeneratorLevelElementData;
            if (data == null)
            {
                generatorQueueClipboardJson = null;
                return;
            }

            var wrapper = new GeneratorQueueClipboardWrapper
            {
                queue = data.GeneratorQueue != null ? new List<GeneratorBlockEntry>(data.GeneratorQueue) : new List<GeneratorBlockEntry>()
            };

            generatorQueueClipboardJson = JsonUtility.ToJson(wrapper);
        }

        private void PasteClipboardQueueToSelectedGenerator()
        {
            if (string.IsNullOrEmpty(generatorQueueClipboardJson))
            {
                return;
            }

            LevelElementData rawData = selectedBlockEditor ? selectedBlockEditor.Data : null;
            GeneratorLevelElementData data = rawData as GeneratorLevelElementData;
            if (data == null)
            {
                return;
            }

            GeneratorQueueClipboardWrapper wrapper;
            try
            {
                wrapper = JsonUtility.FromJson<GeneratorQueueClipboardWrapper>(generatorQueueClipboardJson);
            }
            catch
            {
                return;
            }

            if (wrapper?.queue == null)
            {
                return;
            }

            // Apply to the selected generator data and persist via the existing update callback.
            RegisterLevelChangeUndo("Paste Generator Queue");
            data.GeneratorQueue.Clear();
            data.GeneratorQueue.AddRange(wrapper.queue);
            handleUpdateGeneratorDataCallback?.Invoke(generatorOriginalGridPosition);
        }

        private void PingGenerator()
        {
            if (selectedGeneratorForMenu != null)
            {
                Selection.activeGameObject = selectedGeneratorForMenu.gameObject;
            }
        }

        public void CancelGenerator()
        {
            if (selectedGeneratorForMenu != null)
            {
                selectedGeneratorForMenu.transform.position = generatorOriginalWorldPosition;
            }
            selectedGeneratorIndex = -1;
            selectedGeneratorForMenu = null;
            TeardownGeneratorHandlesEditor();
        }

        private void DeleteSelectedGenerator()
        {
            Vector2Int generatorPosition = generatorOriginalGridPosition;
            RegisterLevelChangeUndo("Delete Generator");
            CancelGenerator();
            handleBorderElementDeleteCallback?.Invoke(generatorPosition);
        }

        private void CleanupDestroyedGenerators()
        {
            if (generatorDatas == null || generatorDatas.Count == 0)
            {
                return;
            }

            bool removedAny = false;
            for (int i = generatorDatas.Count - 1; i >= 0; i--)
            {
                GeneratorData gd = generatorDatas[i];
                if (gd == null || !gd.generator)
                {
                    generatorDatas.RemoveAt(i);
                    removedAny = true;
                }
            }

            if (!removedAny)
            {
                return;
            }

            if (selectedGeneratorIndex >= generatorDatas.Count)
            {
                selectedGeneratorIndex = -1;
                selectedGeneratorForMenu = null;
            }
        }

        private void DrawInteractableButtons()
        {
            if (interactableDatas == null || interactableDatas.Count == 0)
            {
                return;
            }

            for (int i = 0; i < interactableDatas.Count; i++)
            {
                InteractableObjectEntry interactableData = interactableDatas[i];
                if (interactableData == null || !interactableData.interactable)
                {
                    continue;
                }

                if (Handles.Button(
                        interactableData.interactable.transform.position + handlesCubeOffset,
                        Quaternion.identity,
                        1.05f,
                        1.05f,
                        Handles.CubeHandleCap))
                {
                    OnInteractableSelected(i);
                    Handles.color = backupHandlesColor;
                    return;
                }
            }
        }

        private void OnInteractableSelected(int interactableIndex)
        {
            if (interactableDatas == null || interactableIndex < 0 || interactableIndex >= interactableDatas.Count)
            {
                return;
            }

            if (interactableDatas[interactableIndex] == null || !interactableDatas[interactableIndex].interactable)
            {
                return;
            }

            isDuplicatingInteractable = false;
            selectedInteractableIndex = interactableIndex;
            selectedInteractableForMenu = interactableDatas[interactableIndex].interactable;
            interactableOriginalGridPosition = interactableDatas[interactableIndex].position;
            interactableOriginalWorldPosition = selectedInteractableForMenu.transform.position;

            handleCreateInteractableEditorCallback?.Invoke(interactableOriginalGridPosition);
            Handles.color = backupHandlesColor;
        }

        private int GetInteractableIndexAtMouse()
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                Vector2Int hitPosition = GetVector2IntPosition(hit.point);
                if (interactableDatas == null)
                {
                    return -1;
                }

                for (int i = 0; i < interactableDatas.Count; i++)
                {
                    if (interactableDatas[i] == null || !interactableDatas[i].interactable)
                    {
                        continue;
                    }

                    if (interactableDatas[i].position == hitPosition)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private void HandleInteractableMouseEvents()
        {
            Event ev = Event.current;

            if (!ev.isMouse || ev.type != EventType.MouseUp)
            {
                return;
            }

            if (ev.button == 0)
            {
                if (isDuplicatingInteractable)
                {
                    FinishDuplicateInteractable();
                    return;
                }

                if (selectedInteractableForMenu != null)
                {
                    Vector2Int currentGridPos = GetVector2IntPosition(selectedInteractableForMenu.transform.position);
                    bool moved = currentGridPos != interactableOriginalGridPosition;
                    if (moved)
                    {
                        SaveInteractable();
                        return;
                    }
                }

                Vector2Int? clickedBlockPosition = GetBlockPositionAtMouse();
                if (clickedBlockPosition.HasValue)
                {
                    CancelInteractable();
                    SelectBlockAtPosition(clickedBlockPosition.Value);
                    return;
                }

                int gateIndex = GetGateIndexAtMouse();
                if (gateIndex != -1)
                {
                    CancelInteractable();
                    OnGateSelected(gateIndex);
                    return;
                }

                int generatorIndex = GetGeneratorIndexAtMouse();
                if (generatorIndex != -1)
                {
                    CancelInteractable();
                    OnGeneratorSelected(generatorIndex);
                    return;
                }

                int interactableIndex = GetInteractableIndexAtMouse();
                if (interactableIndex != -1 && interactableIndex != selectedInteractableIndex)
                {
                    CancelInteractable();
                    OnInteractableSelected(interactableIndex);
                }
            }
            else if (isDuplicatingInteractable)
            {
                CancelDuplicateInteractable();
            }
            else
            {
                CancelInteractable();
            }
        }

        private void DrawInteractableMovementHandles()
        {
            if (selectedInteractableForMenu == null) return;

            Transform t = selectedInteractableForMenu.transform;
            Vector3 handlePos = t.position + Vector3.up;

            Vector3 newPosition = Handles.Slider2D(
                handlePos,
                Vector3.up,
                Vector3.right,
                Vector3.forward,
                0.5f,
                isDuplicatingInteractable ? Handles.CubeHandleCap : Handles.RectangleHandleCap,
                0f);

            if (!Mathf.Approximately(Vector3.Distance(handlePos, newPosition), 0))
            {
                t.position += (newPosition - handlePos);
            }
        }

        private void SaveInteractable()
        {
            if (selectedInteractableForMenu == null) return;

            Vector2Int newGridPos = GetVector2IntPosition(selectedInteractableForMenu.transform.position);
            if (newGridPos == interactableOriginalGridPosition)
            {
                CancelInteractable();
                return;
            }

            // Revert scene object position; LevelEditorWindow will rebuild preview from serialized data.
            selectedInteractableForMenu.transform.position = interactableOriginalWorldPosition;

            Vector2Int oldPos = interactableOriginalGridPosition;

            selectedInteractableIndex = -1;
            selectedInteractableForMenu = null;
            TeardownInteractableHandlesEditor();

            RegisterLevelChangeUndo("Move Interactable Object");
            handleInteractableMoveCallback?.Invoke(oldPos, newGridPos);
        }

        private void DrawInteractableHandlesMenu()
        {
            Handles.BeginGUI();
            Event ev = Event.current;

            if (ev is { type: EventType.KeyDown })
            {
                if (ev.control && ev.keyCode == KeyCode.D && !isDuplicatingInteractable)
                {
                    StartDuplicateInteractable();
                    ev.Use();
                    Handles.EndGUI();
                    return;
                }

                if (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter || ev.keyCode == KeyCode.Escape)
                {
                    if (isDuplicatingInteractable)
                    {
                        CancelDuplicateInteractable();
                    }
                    else
                    {
                        CancelInteractable();
                    }

                    ev.Use();
                    Handles.EndGUI();
                    return;
                }

                if (ev.keyCode == KeyCode.Delete && !isDuplicatingInteractable)
                {
                    DeleteSelectedInteractable();
                    ev.Use();
                    Handles.EndGUI();
                    return;
                }
            }

            string menuTitle = "Interactable Handles Menu";
            int menuHeight = isDuplicatingInteractable ? 90 : 90 + interactableInspectorHeight;
            EnsureMenuRectInitialized(ref interactableHandlesMenuRect, DEFAULT_INTERACTABLE_MENU_POS,
                PREFS_INTERACTABLE_MENU_POS_X, PREFS_INTERACTABLE_MENU_POS_Y);
            interactableHandlesMenuRect.size = new Vector2(300, menuHeight);
            GUILayout.BeginArea(interactableHandlesMenuRect, menuTitle, GUI.skin.window);

            if (isDuplicatingInteractable)
            {
                GUILayout.Label("Duplicating interactable - Move and click to place");
                GUILayout.Label("Press Esc or Right Click to cancel");

                if (GUILayout.Button("Cancel Duplicate (Esc)"))
                {
                    CancelDuplicateInteractable();
                }
            }
            else
            {
                GUILayout.Label($"Interactable at {interactableOriginalGridPosition}");

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button(new GUIContent("Ping", "Ping the interactable GameObject in Hierarchy"), GUILayout.Width(50)))
                {
                    PingInteractable();
                }

                if (GUILayout.Button(new GUIContent("Cancel", "Cancel selection (Esc)")))
                {
                    CancelInteractable();
                }

                if (GUILayout.Button(new GUIContent("Duplicate", "Duplicate interactable (Ctrl+D)")))
                {
                    StartDuplicateInteractable();
                }

                if (GUILayout.Button(new GUIContent("Delete", "Delete interactable (Del)")))
                {
                    DeleteSelectedInteractable();
                }

                EditorGUILayout.EndHorizontal();

                if (interactableHandlesEditor != null && interactableHandlesEditor.serializedObject != null)
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.BeginVertical();
                    DrawSelectedBlockInspectorWithoutTypePicker(interactableHandlesEditor.serializedObject);
                    EditorGUILayout.EndVertical();

                    if (Event.current.type == EventType.Repaint)
                    {
                        interactableInspectorHeight = Mathf.CeilToInt(GUILayoutUtility.GetLastRect().height);
                    }

                    bool hostGuiChanged = EditorGUI.EndChangeCheck();
                    if (hostGuiChanged)
                    {
                        RegisterLevelChangeUndo("Edit Interactable Data");
                        handleUpdateInteractableDataCallback?.Invoke(interactableOriginalGridPosition);
                    }
                }
            }

            GUILayout.EndArea();

            HandleMenuDragging(ref interactableHandlesMenuRect, ref isDraggingInteractableMenu,
                ref interactableMenuDragOffset, PREFS_INTERACTABLE_MENU_POS_X, PREFS_INTERACTABLE_MENU_POS_Y);

            if (interactableHandlesMenuRect.Contains(Event.current.mousePosition) &&
                (Event.current.type == EventType.MouseDown ||
                 Event.current.type == EventType.MouseUp ||
                 Event.current.type == EventType.MouseDrag ||
                 Event.current.type == EventType.ScrollWheel))
            {
                Event.current.Use();
            }

            Handles.EndGUI();
        }

        private void PingInteractable()
        {
            if (selectedInteractableForMenu != null)
            {
                Selection.activeGameObject = selectedInteractableForMenu.gameObject;
            }
        }

        public void CancelInteractable()
        {
            if (selectedInteractableForMenu != null)
            {
                selectedInteractableForMenu.transform.position = interactableOriginalWorldPosition;
            }
            selectedInteractableIndex = -1;
            selectedInteractableForMenu = null;
            isDuplicatingInteractable = false;
            TeardownInteractableHandlesEditor();
        }

        private void StartDuplicateInteractable()
        {
            if (selectedInteractableForMenu == null)
            {
                CancelInteractable();
                return;
            }

            isDuplicatingInteractable = true;
        }

        /// <summary>
        /// Places a copy of the selected interactable at the dragged-to cell. The copy is produced by
        /// <see cref="LevelElementData.Clone"/> on the window side, so new <see cref="InteractableObjectType"/>
        /// values need no changes here.
        /// </summary>
        private void FinishDuplicateInteractable()
        {
            if (!isDuplicatingInteractable) return;

            if (selectedInteractableForMenu == null)
            {
                CancelInteractable();
                return;
            }

            Vector2Int newGridPos = GetVector2IntPosition(selectedInteractableForMenu.transform.position);
            if (newGridPos == interactableOriginalGridPosition)
            {
                CancelDuplicateInteractable();
                return;
            }

            // Revert scene object position; LevelEditorWindow rebuilds the preview from serialized data.
            selectedInteractableForMenu.transform.position = interactableOriginalWorldPosition;

            Vector2Int sourcePos = interactableOriginalGridPosition;

            selectedInteractableIndex = -1;
            selectedInteractableForMenu = null;
            isDuplicatingInteractable = false;
            TeardownInteractableHandlesEditor();

            RegisterLevelChangeUndo("Duplicate Interactable Object");
            handleInteractableDuplicateCallback?.Invoke(sourcePos, newGridPos);
        }

        private void CancelDuplicateInteractable()
        {
            if (!isDuplicatingInteractable) return;

            isDuplicatingInteractable = false;

            // Keep the interactable selected, only drop the pending duplicate.
            if (selectedInteractableForMenu != null)
            {
                selectedInteractableForMenu.transform.position = interactableOriginalWorldPosition;
            }
        }

        private void DeleteSelectedInteractable()
        {
            Vector2Int interactablePosition = interactableOriginalGridPosition;
            RegisterLevelChangeUndo("Delete Interactable Object");
            CancelInteractable();
            // Interactable sits on an InnerTile, so deletion reverts the cell to InnerTile (same path as block delete).
            handleBlockDeleteCallback?.Invoke(interactablePosition);
        }

        private void CleanupDestroyedInteractables()
        {
            if (interactableDatas == null || interactableDatas.Count == 0)
            {
                return;
            }

            bool removedAny = false;
            for (int i = interactableDatas.Count - 1; i >= 0; i--)
            {
                InteractableObjectEntry entry = interactableDatas[i];
                if (entry == null || !entry.interactable)
                {
                    interactableDatas.RemoveAt(i);
                    removedAny = true;
                }
            }

            if (!removedAny)
            {
                return;
            }

            if (selectedInteractableIndex >= interactableDatas.Count)
            {
                selectedInteractableIndex = -1;
                selectedInteractableForMenu = null;
            }
        }

        private void SpawnBlockFromGateMenu()
        {
            int gateIndex = selectedGateIndex;
            var gate = gateDatas[gateIndex];

            CollectValidBlocks(out var figures, out var types);

            handleCreateFigureSelectionPopup?.Invoke(
                gate.blockPosition,
                figures,
                types,
                SpawnBlockFromGate
            );
        }

        private void DrawFigureHandles()
        {
            //handle cancel selection button
            if (selectedBlock == null || !selectedBlock.levelBlock)
            {
                return;
            }

            for (int i = 0; i < selectedBlock.points.Length; i++)
            {
                Vector3 newPosition = Handles.Slider2D(
                    selectedBlock.levelBlock.transform.position + selectedBlock.points[i] + Vector3.up,
                    Vector3.up,
                    Vector3.right,
                    Vector3.forward,
                    0.5f,
                    isDuplicating ? Handles.CubeHandleCap : Handles.RectangleHandleCap,
                    0f);

                if (!Mathf.Approximately(
                        Vector3.Distance(
                            selectedBlock.levelBlock.transform.position + selectedBlock.points[i] + Vector3.up,
                            newPosition), 0))
                {
                    // if (isMovementRestricted)
                    // {
                    //     movementManager.UpdateBlockMovement(newPosition - selectedBlock.points[i]);
                    // }
                    // else
                    // {
                    selectedBlock.levelBlock.transform.position = newPosition - selectedBlock.points[i];
                    // }
                }
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        public void Unsubscribe()
        {
            TeardownBlockHandlesDataEditor();
            TeardownGateHandlesEditor();
            TeardownGeneratorHandlesEditor();
            TeardownInteractableHandlesEditor();
            isInitiazed = false;
            levelPreviewInitialized = false;
            handleBeginLevelChangeUndoCallback = null;
            SceneView.duringSceneGui -= DuringSceneGui;
            SelectedBlockEditorCommitEvents.AfterSerializedObjectCommitted -=
                OnSelectedBlockBufferSerializedExternalCommit;
        }

        public GameObject GetBlockGameObject(Vector2Int position)
        {
            foreach (var block in levelRepresentation.LevelTransform.Children())
            {
                if (GetVector2IntPosition(block.position) == position)
                {
                    return block.gameObject;
                }
            }

            return null;
        }

        public void SelectGameObject(GameObject selectedGameObject)
        {
            Selection.activeGameObject = selectedGameObject;
        }

        public void Clear()
        {
        }

        private Vector2Int GetVector2IntPosition(Vector3 position)
        {
            return new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.z));
        }

        private class GateData
        {
            public GateBehavior gate;
            public Vector2Int blockSpawnPosition;
            public Vector2Int blockPosition;
            public int gateSize;

            public GateData(GateBehavior gate, Vector2Int levelSize)
            {
                this.gate = gate;
                blockPosition = new Vector2Int(Mathf.RoundToInt(gate.transform.position.x),
                    Mathf.RoundToInt(gate.transform.position.z));
                blockSpawnPosition = gate.Data.Position - gate.GateDirection.PositionOffset;
            }
        }

        private class GeneratorData
        {
            public GeneratorBehavior generator;
            public Vector2Int position;

            public GeneratorData(GeneratorBehavior generator)
            {
                this.generator = generator;
                position = new Vector2Int(Mathf.RoundToInt(generator.transform.position.x),
                    Mathf.RoundToInt(generator.transform.position.z));
            }
        }

        private class InteractableObjectEntry
        {
            public InteractableObjectBehavior interactable;
            public Vector2Int position;

            public InteractableObjectEntry(InteractableObjectBehavior interactable)
            {
                this.interactable = interactable;
                position = new Vector2Int(Mathf.RoundToInt(interactable.transform.position.x),
                    Mathf.RoundToInt(interactable.transform.position.z));
            }
        }

        private class BlockMovementData
        {
            public LevelBlockBehavior levelBlock;
            public Vector2Int recordedPosition;
            public Vector2Int currentPosition;
            public Vector2Int pivotPoint;
            public Vector3[] points;

            public Vector3 Position
            {
                get
                {
                    // Unity destroyed objects compare equal to null; keep last known grid position without throwing.
                    float y = levelBlock ? levelBlock.transform.position.y : 0f;
                    return new Vector3(currentPosition.x, y, currentPosition.y);
                }
            }


            public BlockMovementData(LevelBlockBehavior levelBlock, Vector2Int recordedPosition)
            {
                this.levelBlock = levelBlock;
                pivotPoint = levelBlock.Figure.PivotPoint;
                this.recordedPosition = recordedPosition;
                this.currentPosition = recordedPosition;

                List<Vector3> list = new List<Vector3>();
                int index = 0;

                for (int y = 0; y < levelBlock.Figure.Size.y; y++)
                {
                    for (int x = 0; x < levelBlock.Figure.Size.x; x++)
                    {
                        if (levelBlock.Figure.Points[index].IsFilled)
                        {
                            list.Add(new Vector3(x, 0, y));
                        }

                        index++;
                    }
                }

                points = list.ToArray();
            }

            public void UpdatePosition()
            {
                if (!levelBlock)
                {
                    return;
                }

                levelBlock.transform.position = new Vector3(currentPosition.x, 0, currentPosition.y);
            }
        }


#endif
    }
}