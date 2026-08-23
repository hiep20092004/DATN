using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Text;
using WaterFlow.Core;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace WaterFlow.Game
{
    public class LevelEditorWindow : LevelEditorBase
    {
        private const string GAME_SCENE_PATH = "Assets/Project Files/Game/Scenes/Game.unity";
        private const string EDITOR_SCENE_PATH = "Assets/Project Files/Game/Scenes/Level Editor.unity";
        private const string EDITOR_SCENE_NAME = "Level Editor";
        private const string PREFS_AUTO_OPEN_EDITOR_SCENE = "editor_auto_open_scene";
        private const string SHOW_LEVEL_ANALYSIS_EDITOR_SCENE = "editor_show_level_analysis";
        private const string PREFS_MULTI_COLUMN_LEVEL_LIST = "editor_multi_column_level_list";
        // Also referenced (as a literal) by LevelEnvironmentSpawner under UNITY_EDITOR.
        public const string PREFS_SHOW_CELL_POS = "editor_show_cell_pos";

        //used variables
        private const string LEVELS_PROPERTY_NAME = "levels";
        private const string SPECIAL_LEVELS_PROPERTY_NAME = "specialLevels";
        private const string SPECIAL_MODE_LIMITS_PROPERTY_NAME = "specialModeLimits";
        private const string CELLS_PROPERTY_NAME = "cells";
        private const string EDITOR_COLORS_DATA_PROPERTY_NAME = "editorColorData";
        private const string LEVEL_GENERAL_CONFIG_DATA_PROPERTY_NAME = "levelGeneralConfigData";
        private const string TYPE_PROPERTY_NAME = "type";
        private const string COLOR_PROPERTY_NAME = "color";
        private const string TEXTURE_PROPERTY_NAME = "texture";

        private SerializedProperty levelsSerializedProperty;
        private SerializedProperty specialLevelsSerializedProperty;
        private SerializedProperty specialModeLimitsSerializedProperty;
        private SerializedProperty cellsSerializedProperty;
        private SerializedProperty editorColorsDataSerializedProperty;
        private SerializedProperty levelGeneralConfigDataSerializedProperty;
        /// <summary>When non-null, the grid edits this variant asset; slot in <see cref="levelsSerializedProperty"/> still holds the base level.</summary>
        private LevelData editingVariantAsset;
        private LevelAssetRepresentation selectedLevelRepresentation;
        private bool _needUpdateLevelPreview;
        private bool useDebouncedLevelPreview;
        private bool levelStatisticsDirty = true;
        private LevelStatistics cachedLevelStatistics;
        private Texture2D[] cellTypeTextureCache;
        private Vector2Int drawLevelGridSize;

        private bool needUpdateLevelPreview
        {
            get => _needUpdateLevelPreview;
            set
            {
                if (value)
                {
                    levelStatisticsDirty = true;
                    // The open level's content changed; drop its cached tag mask so the tag filter re-scans it.
                    LevelTagScanner.Invalidate(selectedLevelRepresentation?.EditedLevelObject as LevelData);
                }
                _needUpdateLevelPreview = value;
            }
        }
        private double lastDebouncedLevelPreviewRequestTime;
        private const double LevelPreviewDebounceSeconds = 0.3;
        private LevelsHandler levelsHandler;
        private SpecialLevelsHandler specialLevelsHandler;
        private CellTypesHandler cellTypeHandler;
        private CellTypesHandler cellColorHandler;

        //sidebar
        private const int SIDEBAR_WIDTH = 320;

        /// <summary>When false, level slots cannot be reordered by dragging the list (Set position in context menu still works).</summary>
        private const bool LevelListDragReorderEnabled = false;

        //PlayerPrefs
        private const string PREFS_LEVEL = "editor_level_index";
        private const string PREFS_WIDTH = "editor_sidebar_width";
        private const string PREFS_MIN_ELEMENT_SIZE = "editor_min_grid_cell_size";
        private const int LEVEL_GRID_MIN_SIZE = 2;

        // EditorPrefs keys for restoring the tested variant after exiting play mode
        private const int SPECIAL_LEVEL_TEST_SLOT = 0;

        // Last editor session (main vs special list + asset) — survives window close and play mode
        private const string PREFS_SHOW_SPECIAL_LEVELS = "editor_show_special_levels";
        private const string PREFS_LAST_MAIN_LEVEL_ASSET_GUID = "editor_last_main_level_asset_guid";
        private const string PREFS_LAST_SPECIAL_LEVEL_ASSET_GUID = "editor_last_special_level_asset_guid";
        private const string PREFS_LAST_MAIN_LEVEL_INDEX = "editor_last_main_level_index";
        private const string PREFS_HAS_EDITOR_SESSION = "editor_has_saved_session";

        // Collapsible section states for the Editor tab (persisted per-user).
        private const string PREFS_SECTION_EDITOR_SETTINGS = "editor_section_editor_settings";
        private const string PREFS_SECTION_GENERAL_CONFIG = "editor_section_general_config";
        private const string PREFS_SECTION_SPECIAL_MODE = "editor_section_special_mode";
        private const string PREFS_SECTION_LEVEL_CONFIGURE = "editor_section_level_configure";
        private const string PREFS_SECTION_EFFECTS = "editor_section_effects";
        private const string PREFS_SECTION_OTHER = "editor_section_other";

        // LevelDatabase field names rendered explicitly in their own sections; excluded from the catch-all "Other" group.
        private static readonly HashSet<string> EditorTabExplicitProperties = new HashSet<string>
        {
            "effects",
            "gateEffects",
            "interactableObjects",
            "levelGeneralConfigData",
            "blockEffectCompatibility",
        };

        /// <summary>Blocks handler init from overwriting saved session via <see cref="OpenLevel"/>.</summary>
        private bool suppressEditorSessionPersistence;

        public bool ShowSpecialLevelsList => showSpecialLevelsList;

        //instructions
        private const string LEVEL_INSTRUCTION =
            "Draw environment using buttons on the bottom left. Left click on canvas to draw.";

        private const string RIGHT_CLICK_INSTRUCTION =
            "Spawn block by clickin on the gate in Scene view. Right click to deselect the block.";

        private const int INFO_HEIGH = 150; //found out using Debug.Log(infoRect) on worst case scenario
        private const string TEST_LEVEL = "Test Level";
        private Rect infoRect;

        //level drawing
        private Rect drawRect;
        private float xSize;
        private float ySize;
        private float elementSize;
        private Event currentEvent;
        private Vector2 elementUnderMouseIndex;
        private Vector2Int elementPosition;
        private int invertedY;
        private float buttonRectX;
        private float buttonRectY;
        private Rect buttonRect;
        private BlockEffectType tempBlockEffect;
        private readonly Color GRID_COLOR = new Color(0.4f, 0.4f, 0.4f);

        private Rect separatorRect;
        private bool separatorIsDragged;
        private int currentSideBarWidth;
        private bool lastActiveLevelOpened;
        private List<Vector2Int> positions;
        private List<ILevelValidationRule> cachedValidationRules;
        private SerializedProperty tempCellProperty;
        private Vector2Int tempCellPosition;
        private TabHandler tabHandler;
        private Texture2D tempTexture;

        //cellTypeButton
        private ElementType[] cellTypeButtons =
        {
            ElementType.Empty,
            ElementType.InnerTile,
            ElementType.Border,
            ElementType.Obstacle,
            ElementType.InteractableObject,
            ElementType.Gate,
            ElementType.Generator,
            ElementType.Block
        };

        private GUIStyle cellTypeLabelStyle;
        private GUIStyle sectionHeaderStyle;
        private bool sectionBodyIndented;
        private Rect cellTypeButtonsDrawRect;
        private float cachedButtonsContainerWidth;
        private int cellTypeButtonWidth;
        private int cellTypeButtonTextHeight;
        private int cellTypeButtonOffset;
        private int buttonsPerRow;
        private int rows;
        private float currentX;
        private float currentY;
        private Rect labelRect;
        private Rect textureRect;
        private Dictionary<Vector2Int, int> figuresDictionary;
        private Rect lineRect;
        private Vector2Int selectedBlockPosition;
        private SerializedProperty selectedBlockProperty;
        private List<SerializedProperty> selectedGateNeighbours;
        private bool isBlockSelected;
        private bool isSelectedBlockGate;
        private int minGridCellSize;
        private bool needToSelectLevelBlock;
        private Vector2Int levelBlockPosition;
        private bool tempDrawBlockEffectLabel;
        private string tempBlockEffectLabel;
        private Vector2Int blockSceneInspectorPosition = new Vector2Int(-1, -1);
        private Vector2Int generatorSceneInspectorPosition = new Vector2Int(-1, -1);
        private LevelElementData blockSceneInspectorSnapshot;
        private string selectedBlockLabel;
        private bool autoOpenEditorScene;
        private bool isShowLevelAnalysis = true;
        private bool enableMultiColumnLevelList;
        private bool showCellPos;

        // Tag filter (main levels list). selectedTagMask drives an AND-match filtered view in LevelsHandler.
        private ulong selectedTagMask;
        private bool tagFilterPanelExpanded = true;
        private GUIStyle tagChipStyle;
        private static readonly Color TagChipActiveTint = new Color(0.30f, 0.62f, 0.95f, 1f);
        private Vector2 variantsToolbarScroll;
        private static readonly Color VariantBuildButtonTint = new Color(0.35f, 0.78f, 0.42f, 1f);
        private static readonly Color ExtraLayerLiftButtonTint = new Color(0.95f, 0.55f, 0.18f, 1f);
        private static readonly Color ExtraLayerTunnelButtonTint = new Color(0.20f, 0.70f, 0.82f, 1f);
        private GUIStyle blockIdLabelStyle;
        private GUIStyle generatorQueueLabelStyle;

        // Reused every repaint by DrawGrinderTapePreviews to keep the grid draw allocation-free.
        private readonly List<Vector2Int> grinderTapeCellsBuffer = new List<Vector2Int>();
        private readonly List<(Vector2Int core, GrinderLayout layout)> grinderCoreCellsBuffer =
            new List<(Vector2Int, GrinderLayout)>();
        private readonly Dictionary<Vector2Int, ElementType> grinderElementTypesBuffer =
            new Dictionary<Vector2Int, ElementType>();
        private readonly HashSet<Vector2Int> grinderBlockCoveredCellsBuffer = new HashSet<Vector2Int>();

        private int tempBlockId;
        private bool needToSelectGate;
        private Vector2Int gateSelectPosition;
        private MonoBehaviorInspector gateEditorInspector;
        private MonoBehaviorInspector generatorEditorInspector;
        private bool needToSelectGenerator;
        private Vector2Int generatorSelectPosition;
        private Vector2Int interactableSceneInspectorPosition = new Vector2Int(-1, -1);
        private MonoBehaviorInspector interactableEditorInspector;
        private bool needToSelectInteractable;
        private Vector2Int interactableSelectPosition;
        private bool isPaintingLevelGrid;
        private bool drawBorderAsExtendable;
        private InteractableObjectType selectedInteractableType = InteractableObjectData.EditorPaintedType;
        private string[] interactableTypeLabels;
        private PlayModeInspectorModule playModeInspector;
        private bool showSpecialLevelsList;
        private bool isExtraLayerTabActive;
        private readonly Dictionary<int, LevelValidationResult> levelValidationCacheByInstanceId =
            new Dictionary<int, LevelValidationResult>();

        // Pending "Jump to level" request (applied during DrawContent once handlers/scene are ready).
        private bool pendingJumpActive;
        private UnityEngine.Object pendingJumpLevel;
        private int pendingJumpIndex;
        private bool pendingJumpIsSpecial;

        public bool IsBlockSelected
        {
            get => isBlockSelected;
            set
            {
                isBlockSelected = value;
                HandleSelectedBlockEditor();
            }
        }


        private void HandleSelectedBlockEditor()
        {
            if (IsBlockSelected)
            {
                if (EditorSceneController.Instance == null)
                    return;

                // Early-out: same block, inspector already alive — just refresh data
                if (blockSceneInspectorPosition == selectedBlockPosition &&
                    EditorSceneController.Instance.BlockHandlesDataEditor != null)
                {
                    EditorSceneController.Instance.SelectedBlockEditor.Data =
                        selectedBlockProperty.managedReferenceValue as LevelElementData;
                    return;
                }

                if (EditorSceneController.Instance.BlockHandlesDataEditor)
                {
                    DestroyImmediate(EditorSceneController.Instance.BlockHandlesDataEditor);
                    EditorSceneController.Instance.BlockHandlesDataEditor = null;
                }

                blockSceneInspectorPosition = selectedBlockPosition;
                EditorSceneController.Instance.SelectedBlockEditor.Data =
                    selectedBlockProperty.managedReferenceValue as LevelElementData;
                blockSceneInspectorSnapshot =
                    EditorSceneController.Instance.SelectedBlockEditor.Data?.Clone();

                MonoBehaviorInspector inspector =
                    (MonoBehaviorInspector)Editor.CreateEditor(
                        EditorSceneController.Instance.SelectedBlockEditor,
                        typeof(MonoBehaviorInspector));
                inspector.SetScriptFieldState(false);
                EditorSceneController.Instance.BlockHandlesDataEditor = inspector;
            }
            else
            {
                selectedBlockPosition = new Vector2Int(-1, -1);

                if (EditorSceneController.Instance != null && EditorSceneController.Instance.BlockHandlesDataEditor)
                {
                    DestroyImmediate(EditorSceneController.Instance.BlockHandlesDataEditor);
                    EditorSceneController.Instance.BlockHandlesDataEditor = null;
                }
            }
        }

        protected override WindowConfiguration SetUpWindowConfiguration(WindowConfiguration.Builder builder)
        {
            return builder.SetWindowMinSize(new Vector2(700, 500)).Build();
        }

        protected override Type GetLevelsDatabaseType()
        {
            return typeof(LevelDatabase);
        }

        public override Type GetLevelType()
        {
            return typeof(LevelData);
        }

        protected override void ReadLevelDatabaseFields()
        {
            levelsSerializedProperty = levelsDatabaseSerializedObject.FindProperty(LEVELS_PROPERTY_NAME);
            specialLevelsSerializedProperty = levelsDatabaseSerializedObject.FindProperty(SPECIAL_LEVELS_PROPERTY_NAME);
            specialModeLimitsSerializedProperty =
                levelsDatabaseSerializedObject.FindProperty(SPECIAL_MODE_LIMITS_PROPERTY_NAME);
            cellsSerializedProperty = levelsDatabaseSerializedObject.FindProperty(CELLS_PROPERTY_NAME);
            editorColorsDataSerializedProperty =
                levelsDatabaseSerializedObject.FindProperty(EDITOR_COLORS_DATA_PROPERTY_NAME);
            levelGeneralConfigDataSerializedProperty =
                levelsDatabaseSerializedObject.FindProperty(LEVEL_GENERAL_CONFIG_DATA_PROPERTY_NAME);

            LevelDataUtilities.PruneNullVariantReferences(levelsDatabaseSerializedObject);
            levelsDatabaseSerializedObject.Update();
        }

        protected override void InitializeVariables()
        {
            HandleCellsInitialization();
            tabHandler = new TabHandler();
            tabHandler.AddTab(new TabHandler.Tab("Levels", DisplayLevelsTab));
            tabHandler.AddTab(new TabHandler.Tab("Editor", DisplayEditorTab));
            currentSideBarWidth = PlayerPrefs.GetInt(PREFS_WIDTH, SIDEBAR_WIDTH);
            positions = new List<Vector2Int>();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += UnloadEditor;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            SelectedBlockEditorCommitEvents.AfterSerializedObjectCommitted -=
                OnSelectedBlockEditorSerializedExternalCommit;
            SelectedBlockEditorCommitEvents.AfterSerializedObjectCommitted +=
                OnSelectedBlockEditorSerializedExternalCommit;
            minGridCellSize = PlayerPrefs.GetInt(PREFS_MIN_ELEMENT_SIZE, 18);
            selectedGateNeighbours = new List<SerializedProperty>();
            autoOpenEditorScene = EditorPrefs.GetBool(PREFS_AUTO_OPEN_EDITOR_SCENE, false);
            isShowLevelAnalysis = EditorPrefs.GetBool(SHOW_LEVEL_ANALYSIS_EDITOR_SCENE, true);
            enableMultiColumnLevelList = EditorPrefs.GetBool(PREFS_MULTI_COLUMN_LEVEL_LIST, true);
            showCellPos = EditorPrefs.GetBool(PREFS_SHOW_CELL_POS, false);
            playModeInspector = new PlayModeInspectorModule(Repaint);
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode && FigureSelectorWindow.window != null)
            {
                FigureSelectorWindow.window.Close();
                FigureSelectorWindow.window = null;
            }

            if (change == PlayModeStateChange.EnteredEditMode && autoOpenEditorScene)
            {
                if (SceneManager.GetActiveScene().name != EDITOR_SCENE_NAME)
                {
                    OpenScene(EDITOR_SCENE_PATH);
                    return;
                }
            }

            if (SceneManager.GetActiveScene().name != EDITOR_SCENE_NAME)
            {
                return;
            }

            if (change != PlayModeStateChange.ExitingEditMode)
            {
                return;
            }

            bool hasSelectedSpecial = showSpecialLevelsList && specialLevelsHandler != null && specialLevelsHandler.HasSelection;
            bool hasSelectedMain = levelsHandler != null && levelsHandler.SelectedLevelIndex != -1;
            bool testStarted = (hasSelectedSpecial || hasSelectedMain) && TestLevel();

            if (!testStarted)
            {
                OpenScene(GAME_SCENE_PATH);
            }
        }


        private void HandleCellsInitialization()
        {
            cellTypeHandler = new CellTypesHandler();
            cellColorHandler = new CellTypesHandler();

            //some validation
            string[] names = Enum.GetNames(typeof(ElementType));
            cellsSerializedProperty.arraySize = names.Length;
            Color color;

            for (int i = 0; i < cellsSerializedProperty.arraySize; i++)
            {
                cellsSerializedProperty.GetArrayElementAtIndex(i).FindPropertyRelative(TYPE_PROPERTY_NAME).intValue = i;
                color = cellsSerializedProperty.GetArrayElementAtIndex(i).FindPropertyRelative(COLOR_PROPERTY_NAME)
                    .colorValue;
                cellTypeHandler.AddCellType(new CellTypesHandler.CellType(i, names[i], color));
            }

            cellTypeHandler.GetCellType((int)ElementType.Block).extraPropsEnabled = true;
            cellTypeHandler.GetCellType((int)ElementType.InteractableObject).label = "Interactable";

            //more validation
            names = Enum.GetNames(typeof(BlockColor));
            editorColorsDataSerializedProperty.arraySize = names.Length;

            for (int i = 0; i < editorColorsDataSerializedProperty.arraySize; i++)
            {
                editorColorsDataSerializedProperty.GetArrayElementAtIndex(i).FindPropertyRelative(TYPE_PROPERTY_NAME)
                    .intValue = i;
                color = editorColorsDataSerializedProperty.GetArrayElementAtIndex(i)
                    .FindPropertyRelative(COLOR_PROPERTY_NAME).colorValue;
                cellColorHandler.AddCellType(new CellTypesHandler.CellType(i, names[i], color));
            }

            names = Enum.GetNames(typeof(BlockType));
            cellTypeHandler.AddExtraProp(new CellTypesHandler.ExtraProp(0, names[0], false));

            for (int i = 1; i < names.Length; i++)
            {
                cellTypeHandler.AddExtraProp(new CellTypesHandler.ExtraProp(i, names[i]));
            }

            BlocksVisualsData blocksVisualsData = EditorUtils.GetAsset<BlocksVisualsData>();
            figuresDictionary = new Dictionary<Vector2Int, int>();

            if (blocksVisualsData == null)
            {
                Debug.LogError("BlocksVisualsData not found");
                return;
            }

            BlockFigureGeometryCache.Rebuild(blocksVisualsData);

            cachedValidationRules = null;
            RebuildCellTypeTextureCache();
        }

        private void RebuildCellTypeTextureCache()
        {
            if (cellsSerializedProperty == null)
            {
                cellTypeTextureCache = null;
                return;
            }

            int count = cellsSerializedProperty.arraySize;
            if (cellTypeTextureCache == null || cellTypeTextureCache.Length != count)
                cellTypeTextureCache = new Texture2D[count];

            for (int i = 0; i < count; i++)
            {
                cellTypeTextureCache[i] = cellsSerializedProperty.GetArrayElementAtIndex(i)
                    .FindPropertyRelative(TEXTURE_PROPERTY_NAME).objectReferenceValue as Texture2D;
            }
        }

        private Texture2D GetCachedCellTypeTexture(int value)
        {
            if (cellTypeTextureCache != null && value >= 0 && value < cellTypeTextureCache.Length)
                return cellTypeTextureCache[value];

            return cellsSerializedProperty.GetArrayElementAtIndex(value).FindPropertyRelative(TEXTURE_PROPERTY_NAME)
                .objectReferenceValue as Texture2D;
        }

        private void SaveEditorSessionState()
        {
            EditorPrefs.SetBool(PREFS_SHOW_SPECIAL_LEVELS, showSpecialLevelsList);
            EditorPrefs.SetBool(PREFS_HAS_EDITOR_SESSION, true);

            bool isSpecial = showSpecialLevelsList && specialLevelsHandler != null && specialLevelsHandler.HasSelection;
            int mainIndex = levelsHandler != null ? levelsHandler.SelectedLevelIndex : -1;
            string assetGuid = string.Empty;

            if (isSpecial)
            {
                Object specialObj = specialLevelsHandler.SelectedLevelObject;
                if (specialObj)
                {
                    string path = AssetDatabase.GetAssetPath(specialObj);
                    assetGuid = AssetDatabase.AssetPathToGUID(path);
                }

            }
            else if (mainIndex >= 0 && levelsSerializedProperty != null &&
                     mainIndex < levelsSerializedProperty.arraySize)
            {
                PlayerPrefs.SetInt(PREFS_LEVEL, mainIndex);
                PlayerPrefs.Save();
                EditorPrefs.SetInt(PREFS_LAST_MAIN_LEVEL_INDEX, mainIndex);

                Object slot = levelsSerializedProperty.GetArrayElementAtIndex(mainIndex).objectReferenceValue;
                LevelData levelForGuid = editingVariantAsset ? editingVariantAsset : slot as LevelData;
                if (levelForGuid)
                    assetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(levelForGuid));

            }

            if (!string.IsNullOrEmpty(assetGuid))
            {
                if (isSpecial)
                    EditorPrefs.SetString(PREFS_LAST_SPECIAL_LEVEL_ASSET_GUID, assetGuid);
                else
                    EditorPrefs.SetString(PREFS_LAST_MAIN_LEVEL_ASSET_GUID, assetGuid);
            }
        }

        private void RestoreLastEditorSession()
        {
            if (lastActiveLevelOpened)
                return;

            bool hasSessionFlag = EditorPrefs.GetBool(PREFS_HAS_EDITOR_SESSION, false);
            bool hasLevelIndex = PlayerPrefs.HasKey(PREFS_LEVEL) || EditorPrefs.HasKey(PREFS_LAST_MAIN_LEVEL_INDEX);
            bool wantsSpecial = EditorPrefs.GetBool(PREFS_SHOW_SPECIAL_LEVELS, false);
            string savedMainGuid = EditorPrefs.GetString(PREFS_LAST_MAIN_LEVEL_ASSET_GUID, string.Empty);
            string savedSpecialGuid = EditorPrefs.GetString(PREFS_LAST_SPECIAL_LEVEL_ASSET_GUID, string.Empty);
            int savedMainIndex = -1;
            if (EditorPrefs.HasKey(PREFS_LAST_MAIN_LEVEL_INDEX))
                savedMainIndex = EditorPrefs.GetInt(PREFS_LAST_MAIN_LEVEL_INDEX, 0);
            else if (PlayerPrefs.HasKey(PREFS_LEVEL))
                savedMainIndex = PlayerPrefs.GetInt(PREFS_LEVEL, 0);

            if (!hasSessionFlag && !hasLevelIndex)
            {
                lastActiveLevelOpened = true;
                return;
            }

            showSpecialLevelsList = wantsSpecial;

            if (showSpecialLevelsList)
            {
                if (specialLevelsHandler == null)
                    return;

                if (!string.IsNullOrEmpty(savedSpecialGuid))
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(savedSpecialGuid);
                    Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                    if (asset && specialLevelsHandler.SelectLevel(asset))
                    {
                        lastActiveLevelOpened = true;
                        return;
                    }
                }

                lastActiveLevelOpened = true;
                return;
            }

            if (levelsHandler == null || levelsSerializedProperty == null || levelsSerializedProperty.arraySize <= 0)
                return;

            int levelIndex = hasLevelIndex
                ? Mathf.Clamp(savedMainIndex, 0, levelsSerializedProperty.arraySize - 1)
                : 0;

            string variantPath = LevelDatabase.EditorPlayModeLevelOverridePath;
            if (!string.IsNullOrEmpty(variantPath))
            {
                LevelDatabase.ClearEditorPlayModeLevelOverride();
                LevelData variantData = AssetDatabase.LoadAssetAtPath<LevelData>(variantPath);
                if (variantData)
                {
                    suppressEditorSessionPersistence = true;
                    try
                    {
                        levelsHandler.CustomList.SetSelectedIndexAndNotify(levelIndex);
                        OpenLevel(variantData, levelIndex);
                    }
                    finally
                    {
                        suppressEditorSessionPersistence = false;
                    }

                    lastActiveLevelOpened = true;
                    return;
                }
            }

            if (hasLevelIndex && savedMainIndex >= 0)
            {
                suppressEditorSessionPersistence = true;
                try
                {
                    levelsHandler.CustomList.SetSelectedIndexAndNotify(levelIndex);

                    if (!string.IsNullOrEmpty(savedMainGuid))
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(savedMainGuid);
                        LevelData levelData = AssetDatabase.LoadAssetAtPath<LevelData>(assetPath);
                        if (levelData && levelIndex < levelsSerializedProperty.arraySize &&
                            levelsSerializedProperty.GetArrayElementAtIndex(levelIndex).objectReferenceValue != levelData)
                        {
                            OpenLevel(levelData, levelIndex);
                        }
                        else
                        {
                            levelsHandler.OpenLevel(levelIndex);
                        }
                    }
                    else
                    {
                        levelsHandler.OpenLevel(levelIndex);
                    }
                }
                finally
                {
                    suppressEditorSessionPersistence = false;
                }

                lastActiveLevelOpened = true;
                return;
            }

            lastActiveLevelOpened = true;
        }


        protected override void Styles()
        {
            cellTypeHandler?.SetDefaultLabelStyle();

            cellColorHandler?.SetDefaultLabelStyle();

            tabHandler?.SetDefaultToolbarStyle();

            if (levelsDatabase)
            {
                suppressEditorSessionPersistence = true;
                try
                {
                    levelsHandler = new LevelsHandler(levelsDatabaseSerializedObject, levelsSerializedProperty);
                    levelsHandler.IgnoreDragEvents = !LevelListDragReorderEnabled;
                    // Keep the freshly-created handler in sync with the window's active tag filter.
                    levelsHandler.ApplyTagFilter(selectedTagMask);
                    if (specialLevelsSerializedProperty != null)
                    {
                        specialLevelsHandler =
                            new SpecialLevelsHandler(levelsDatabaseSerializedObject, specialLevelsSerializedProperty);
                        specialLevelsHandler.IgnoreDragEvents = !LevelListDragReorderEnabled;
                    }

                    ApplyMultiColumnLayoutToLevelList();

                    if (SceneManager.GetActiveScene().name == EDITOR_SCENE_NAME)
                        RestoreLastEditorSession();
                }
                finally
                {
                    suppressEditorSessionPersistence = false;
                }
            }

            cellTypeLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel);
            cellTypeLabelStyle.alignment = TextAnchor.MiddleCenter;
            cellTypeLabelStyle.fontSize = 8;
            cellTypeLabelStyle.padding = new RectOffset(0, 0, 0, 0);

            blockIdLabelStyle = new GUIStyle(EditorStyles.miniLabel);
            blockIdLabelStyle.fontSize = 9;
            blockIdLabelStyle.fontStyle = FontStyle.Bold;
            blockIdLabelStyle.normal.textColor = Color.black;
            blockIdLabelStyle.alignment = TextAnchor.UpperLeft;
            blockIdLabelStyle.padding = new RectOffset(2, 0, 1, 0);

            generatorQueueLabelStyle = new GUIStyle(GUI.skin.label);
            generatorQueueLabelStyle.alignment = TextAnchor.LowerCenter;
            generatorQueueLabelStyle.fontStyle = FontStyle.Bold;
            generatorQueueLabelStyle.normal.textColor = Color.white;
            generatorQueueLabelStyle.active.textColor = Color.white;

            // Rebuilt here (not lazily cached) so it survives domain reloads: a cached GUIStyle keeps
            // references to EditorStyles textures that Unity destroys on reload, rendering the chips black.
            BuildTagChipStyle();
        }

        public override void OpenLevel(UnityEngine.Object levelObject, int index)
        {
            if (levelsSerializedProperty != null && index >= 0 && index < levelsSerializedProperty.arraySize)
            {
                LevelData slotBase = levelsSerializedProperty.GetArrayElementAtIndex(index).objectReferenceValue as LevelData;
                editingVariantAsset = (levelObject != null && levelObject != slotBase) ? levelObject as LevelData : null;
            }
            else
                editingVariantAsset = null;

            bool shouldPersistSession = !suppressEditorSessionPersistence;
            if (!showSpecialLevelsList && levelObject is SpecialLevelData)
                shouldPersistSession = false;

            if (shouldPersistSession)
            {
                if (!showSpecialLevelsList && index >= 0)
                {
                    PlayerPrefs.SetInt(PREFS_LEVEL, index);
                    PlayerPrefs.Save();
                    EditorPrefs.SetInt(PREFS_LAST_MAIN_LEVEL_INDEX, index);
                }

                if (levelObject != null)
                {
                    string path = AssetDatabase.GetAssetPath(levelObject);
                    if (!string.IsNullOrEmpty(path))
                    {
                        string guid = AssetDatabase.AssetPathToGUID(path);
                        if (showSpecialLevelsList)
                            EditorPrefs.SetString(PREFS_LAST_SPECIAL_LEVEL_ASSET_GUID, guid);
                        else
                            EditorPrefs.SetString(PREFS_LAST_MAIN_LEVEL_ASSET_GUID, guid);
                    }
                }

                EditorPrefs.SetBool(PREFS_SHOW_SPECIAL_LEVELS, showSpecialLevelsList);
                EditorPrefs.SetBool(PREFS_HAS_EDITOR_SESSION, true);
            }

            AssetDatabase.SaveAssets();
            IsBlockSelected = false;
            selectedLevelRepresentation = new LevelAssetRepresentation(levelObject);

            // Reset extra layer tab when opening a level that has no extra layer
            LevelData openedLevel = levelObject as LevelData;
            if (openedLevel == null || !openedLevel.HasExtraLayer)
                isExtraLayerTabActive = false;

            if (isExtraLayerTabActive)
                selectedLevelRepresentation.SwitchActiveLayer(true);

            needUpdateLevelPreview = true;
        }

        /// <summary>
        /// Public API: jump to (open) any level in the editor without breaking the list/grid UI.
        /// Handles both Normal levels and Special levels. The request is deferred and applied during the
        /// next <see cref="DrawContent"/> so it is safe to call right after opening/focusing the window
        /// (before its handlers and the Editor scene are ready).
        /// </summary>
        /// <param name="levelObject">Level asset to open (a <see cref="LevelData"/> for normal, a SpecialLevelData for special).</param>
        /// <param name="levelIndex">Slot index in the main levels list (ignored for special levels).</param>
        /// <param name="isSpecial">True when <paramref name="levelObject"/> is a special level.</param>
        public void JumpToLevel(UnityEngine.Object levelObject, int levelIndex, bool isSpecial)
        {
            if (levelObject == null)
            {
                Debug.LogWarning("[LevelEditor] JumpToLevel called with a null level.");
                return;
            }

            pendingJumpActive = true;
            pendingJumpLevel = levelObject;
            pendingJumpIndex = levelIndex;
            pendingJumpIsSpecial = isSpecial;

            // The Editor scene is required for the level grid; open it if necessary.
            if (SceneManager.GetActiveScene().name != EDITOR_SCENE_NAME)
                OpenScene(EDITOR_SCENE_PATH);

            Focus();
            Repaint();
        }

        private void ProcessPendingJump()
        {
            if (!pendingJumpActive)
                return;

            // Handlers are created in Styles() on the first OnGUI pass; wait until they exist.
            if (levelsHandler == null)
                return;

            pendingJumpActive = false;

            UnityEngine.Object target = pendingJumpLevel;
            pendingJumpLevel = null;
            if (target == null)
                return;

            // Force the Levels tab so the list/grid are visible.
            tabHandler?.SetTabIndex(0);
            IsBlockSelected = false;

            if (pendingJumpIsSpecial)
            {
                if (specialLevelsHandler == null)
                    return;

                showSpecialLevelsList = true;
                if (!specialLevelsHandler.SelectLevel(target))
                    Debug.LogWarning(
                        $"[LevelEditor] Could not locate special level '{target.name}' in any mode list.");
            }
            else
            {
                showSpecialLevelsList = false;

                int maxIndex = Mathf.Max(0, levelsSerializedProperty.arraySize - 1);
                int index = Mathf.Clamp(pendingJumpIndex, 0, maxIndex);

                // Mark the "restore last active level" step as done so it doesn't override this jump.
                lastActiveLevelOpened = true;

                // Select + open exactly like a manual click / jump-to-index row.
                levelsHandler.CustomList.SetSelectedIndexAndNotify(index);

                // If the caller asked for a specific asset (e.g. a variant) that differs from the slot,
                // honor it explicitly after the list-driven open.
                if (target is LevelData levelData &&
                    index < levelsSerializedProperty.arraySize &&
                    levelsSerializedProperty.GetArrayElementAtIndex(index).objectReferenceValue != levelData)
                {
                    OpenLevel(levelData, index);
                }
            }

            Repaint();
        }

        public override string GetLevelLabel(UnityEngine.Object levelObject, int index)
        {
            return new LevelAssetRepresentation(levelObject).GetLevelLabel(index, stringBuilder);
        }
        
        public void InvalidateLevelLabelSuffixCache()
        {
            levelValidationCacheByInstanceId.Clear();
        }

        private void InvalidateLevelValidation(UnityEngine.Object levelObject)
        {
            if (levelObject)
                levelValidationCacheByInstanceId.Remove(levelObject.GetInstanceID());
        }

        private LevelValidationResult GetOrComputeLevelValidation(UnityEngine.Object levelObject)
        {
            if (!levelObject)
                return null;

            int instanceId = levelObject.GetInstanceID();
            if (levelValidationCacheByInstanceId.TryGetValue(instanceId, out LevelValidationResult cached))
                return cached;

            LevelAssetRepresentation rep = new LevelAssetRepresentation(levelObject);
            LevelValidationResult result = LevelValidator.Validate(
                levelObject.name,
                rep.itemsProperty,
                rep.sizeProperty.vector2IntValue,
                GetOrCreateValidationRules());
            levelValidationCacheByInstanceId[instanceId] = result;
            return result;
        }

        public override string GetLevelLabelSuffix(Object levelObject)
        {
            LevelValidationResult result = GetOrComputeLevelValidation(levelObject);
            return result == null ? string.Empty : result.GetLabelSuffix();
        }

        public override string FormatLevelListRowLabel(
            int index,
            string labelWithoutValidationSuffix,
            string validationSuffix)
        {
            if (string.IsNullOrEmpty(validationSuffix))
                return labelWithoutValidationSuffix;

            return $"#{index + 1} |{validationSuffix}";
        }

        public override void ClearLevel(UnityEngine.Object levelObject)
        {
            new LevelAssetRepresentation(levelObject).Clear();
        }

        public override void LogErrorsForGlobalValidation(UnityEngine.Object levelObject, int index)
        {
            if (!levelObject)
                return;

            LevelAssetRepresentation level = new LevelAssetRepresentation(levelObject);
            LevelValidator.ValidateAndLog(
                levelObject.name,
                index + 1,
                level.itemsProperty,
                level.sizeProperty.vector2IntValue,
                GetOrCreateValidationRules(),
                logPassedLevel: true);
        }

        protected override void DrawContent()
        {
            if (SceneManager.GetActiveScene().name != EDITOR_SCENE_NAME)
            {
                DrawOpenEditorScene();
                DrawShowCellPosToggle();

                if (EditorApplication.isPlaying)
                    playModeInspector?.Draw();

                return;
            }

            ProcessPendingJump();
            tabHandler.DisplayTab();
        }

        private void DrawShowCellPosToggle()
        {
            EditorGUI.BeginChangeCheck();
            showCellPos = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "Show cell positions",
                    "When enabled, spawned inner tiles display their grid coordinates on camera."),
                showCellPos);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(PREFS_SHOW_CELL_POS, showCellPos);
                ApplyCellPosVisibility(showCellPos);
            }
        }

        private static void ApplyCellPosVisibility(bool visible)
        {
            TextMesh[] all = Object.FindObjectsOfType<TextMesh>(true);
            foreach (TextMesh tm in all)
            {
                if (tm.gameObject.name.StartsWith("CellPos_"))
                    tm.gameObject.SetActive(visible);
            }
        }

        private void DrawOpenEditorScene()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.HelpBox(EDITOR_SCENE_NAME + " scene required for level editor.", MessageType.Error, true);

            if (GUILayout.Button("Open \"" + EDITOR_SCENE_NAME + "\" scene"))
            {
                OpenScene(EDITOR_SCENE_PATH);
            }

            EditorGUILayout.Space();

            // Thêm toggle
            EditorGUI.BeginChangeCheck();
            autoOpenEditorScene = EditorGUILayout.ToggleLeft(
                "Automatically open Level Editor scene when exiting Play Mode",
                autoOpenEditorScene
            );
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(PREFS_AUTO_OPEN_EDITOR_SCENE, autoOpenEditorScene);
            }

            EditorGUILayout.EndVertical();
        }

        private void DisplayLevelsTab()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            DisplayListArea();
            HandleChangingSideBar();
            DisplayMainArea();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            EditorGUILayout.EndVertical();
        }

        private void HandleChangingSideBar()
        {
            separatorRect = EditorGUILayout.BeginHorizontal(GUILayout.MaxWidth(0), GUILayout.ExpandHeight(true));
            EditorGUILayout.EndHorizontal();
            separatorRect.xMin -= GUI.skin.box.margin.right;
            separatorRect.xMax += GUI.skin.box.margin.left;
            EditorGUIUtility.AddCursorRect(separatorRect, MouseCursor.ResizeHorizontal);


            if (separatorRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.MouseDown)
                {
                    separatorIsDragged = true;
                    if (showSpecialLevelsList && specialLevelsHandler != null)
                        specialLevelsHandler.IgnoreDragEvents = true;
                    else
                        levelsHandler.IgnoreDragEvents = true;
                    Event.current.Use();
                }
            }

            if (separatorIsDragged)
            {
                if (Event.current.type == EventType.MouseUp)
                {
                    separatorIsDragged = false;
                    if (showSpecialLevelsList && specialLevelsHandler != null)
                        specialLevelsHandler.IgnoreDragEvents = !LevelListDragReorderEnabled;
                    else
                        levelsHandler.IgnoreDragEvents = !LevelListDragReorderEnabled;
                    PlayerPrefs.SetInt(PREFS_WIDTH, currentSideBarWidth);
                    PlayerPrefs.Save();
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.MouseDrag)
                {
                    currentSideBarWidth = Mathf.RoundToInt(Event.current.delta.x) + currentSideBarWidth;
                    Event.current.Use();
                }
            }
        }

        private void DisplayListArea()
        {
            RestoreLastEditorSession();

            EditorGUILayout.BeginVertical(GUILayout.Width(currentSideBarWidth));
            DrawMainSpecialToggle();

            if (showSpecialLevelsList && specialLevelsHandler != null)
            {
                specialLevelsHandler.DisplayReorderableList();
            }
            else
            {
                DrawTagFilterPanel();
                levelsHandler.DisplayReorderableList();

                // Populate renumbers/reorders the array, so it is unavailable while a tag filter is active.
                using (new EditorGUI.DisabledScope(levelsHandler.IsTagFiltering))
                    levelsHandler.DrawRenameLevelsButton();
            }

            if (IsBlockSelected)
            {
                DrawSelectedBlock();
            }
            else if (EditorSceneController.Instance != null && EditorSceneController.Instance.IsGateSelected)
            {
                DrawSelectedGate();
            }
            else if (EditorSceneController.Instance != null && EditorSceneController.Instance.IsGeneratorSelected)
            {
                DrawSelectedGenerator();
            }
            else if (EditorSceneController.Instance != null && EditorSceneController.Instance.IsInteractableSelected)
            {
                DrawSelectedInteractable();
            }
            else
            {
                DrawButtons();
                DrawInteractableTypeSelector();
                DrawPreSelectColorButtons();
                DrawBorderButtons();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTagFilterPanel()
        {
            if (levelsHandler == null)
                return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            int activeCount = CountSetBits(selectedTagMask);
            string foldoutLabel = activeCount > 0 ? $"Tag Filter (AND) — {activeCount} selected" : "Tag Filter (AND)";
            tagFilterPanelExpanded = EditorGUILayout.Foldout(tagFilterPanelExpanded, foldoutLabel, true);

            using (new EditorGUI.DisabledScope(selectedTagMask == 0UL))
            {
                if (GUILayout.Button("Clear", GUILayout.Width(52f)))
                    SetTagFilterMask(0UL);
            }

            EditorGUILayout.EndHorizontal();

            if (tagFilterPanelExpanded)
            {
                DrawTagChips();

                if (selectedTagMask != 0UL)
                    EditorGUILayout.LabelField("Filtering — add/delete/reorder disabled.", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTagChips()
        {
            EnsureTagChipStyle();

            float available = Mathf.Max(120f, currentSideBarWidth - 28f);
            IReadOnlyList<LevelTagDescriptor> descriptors = LevelTagRegistry.All;

            foreach (ObstacleCategory category in (ObstacleCategory[])Enum.GetValues(typeof(ObstacleCategory)))
            {
                bool headerDrawn = false;
                bool rowOpen = false;
                float rowWidth = 0f;

                for (int i = 0; i < descriptors.Count; i++)
                {
                    LevelTagDescriptor descriptor = descriptors[i];
                    if (descriptor.Category != category)
                        continue;

                    if (!headerDrawn)
                    {
                        EditorGUILayout.LabelField(GetTagCategoryLabel(category), EditorStyles.miniBoldLabel);
                        headerDrawn = true;
                    }

                    GUIContent content = new GUIContent(descriptor.DisplayName);
                    float chipWidth = tagChipStyle.CalcSize(content).x + 6f;

                    if (rowOpen && rowWidth + chipWidth > available)
                    {
                        EditorGUILayout.EndHorizontal();
                        rowOpen = false;
                    }

                    if (!rowOpen)
                    {
                        EditorGUILayout.BeginHorizontal();
                        rowOpen = true;
                        rowWidth = 0f;
                    }

                    bool isOn = (selectedTagMask & descriptor.Bit) != 0UL;
                    Color previous = GUI.backgroundColor;
                    GUI.backgroundColor = isOn ? TagChipActiveTint : previous;

                    if (GUILayout.Button(content, tagChipStyle, GUILayout.Width(chipWidth)))
                        SetTagFilterMask(selectedTagMask ^ descriptor.Bit);

                    GUI.backgroundColor = previous;
                    rowWidth += chipWidth + 2f;
                }

                if (rowOpen)
                    EditorGUILayout.EndHorizontal();
            }
        }

        private void SetTagFilterMask(ulong mask)
        {
            selectedTagMask = mask;
            levelsHandler.ApplyTagFilter(mask);
            Repaint();
        }

        private void EnsureTagChipStyle()
        {
            if (tagChipStyle != null)
                return;

            BuildTagChipStyle();
        }

        private void BuildTagChipStyle()
        {
            tagChipStyle = new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                margin = new RectOffset(2, 2, 2, 2)
            };
        }

        private static int CountSetBits(ulong value)
        {
            int count = 0;
            while (value != 0UL)
            {
                value &= value - 1UL;
                count++;
            }

            return count;
        }

        private static string GetTagCategoryLabel(ObstacleCategory category)
        {
            switch (category)
            {
                case ObstacleCategory.Block: return "Block";
                case ObstacleCategory.Gate: return "Gate";
                case ObstacleCategory.InteractableObject: return "Interactable";
                case ObstacleCategory.Generator: return "Generator";
                case ObstacleCategory.ExtraLayer: return "Extra Layer";
                default: return category.ToString();
            }
        }

        /// <summary>
        /// Writes pending grid edits to disk. Must run before the editor state is torn down
        /// (assembly reload, play mode, window close) — the live SerializedObject does not survive
        /// a domain reload, so anything still buffered there would be silently discarded.
        /// </summary>
        private void FlushEditedLevelToDisk()
        {
            if (selectedLevelRepresentation == null || selectedLevelRepresentation.NullLevel)
                return;

            selectedLevelRepresentation.ApplyChanges();

            Object editedLevel = selectedLevelRepresentation.EditedLevelObject;
            if (editedLevel)
                EditorUtility.SetDirty(editedLevel);

            AssetDatabase.SaveAssets();
        }

        private void UnloadEditor()
        {
            FlushEditedLevelToDisk();

            SelectedBlockEditorCommitEvents.AfterSerializedObjectCommitted -=
                OnSelectedBlockEditorSerializedExternalCommit;
            editingVariantAsset = null;
            selectedLevelRepresentation = null;
            cachedLevelStatistics = null;
            levelStatisticsDirty = true;
            levelsHandler?.ClearSelection();
            specialLevelsHandler?.ClearSelection();
            lastActiveLevelOpened = false;
            EditorSceneController sceneController = FindExistingSceneController();
            sceneController?.Unsubscribe();
            blockSceneInspectorPosition = new Vector2Int(-1, -1);
            generatorSceneInspectorPosition = new Vector2Int(-1, -1);
            interactableSceneInspectorPosition = new Vector2Int(-1, -1);
            IsBlockSelected = false;
            isPaintingLevelGrid = false;
        }

        private static EditorSceneController FindExistingSceneController()
        {
            return Object.FindFirstObjectByType<EditorSceneController>();
        }

        private void OnUndoRedoPerformed()
        {
            if (selectedLevelRepresentation == null || selectedLevelRepresentation.NullLevel)
            {
                return;
            }

            selectedLevelRepresentation.RefreshSerializedObject();
            IsBlockSelected = false;
            needUpdateLevelPreview = true;
            InvalidateLevelLabelSuffixCache();
            levelsHandler?.RefreshDisplayLabels();
            Repaint();
        }

        private void DrawSelectedBlock()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("X"))
            {
                IsBlockSelected = false;
                EditorGUILayout.EndHorizontal();
                return;
            }

            EditorGUILayout.LabelField(selectedBlockLabel);

            EditorGUILayout.EndHorizontal();

            MonoBehaviorInspector inspector = EditorSceneController.Instance?.BlockHandlesDataEditor;
            EditorGUI.BeginChangeCheck();
            if (inspector)
                EditorSceneController.DrawSelectedBlockInspectorWithoutTypePicker(inspector.serializedObject);

            if (EditorGUI.EndChangeCheck())
            {
                ApplySelectedBlockInspectorChangesToLevelAsset();
            }
        }

        private void DrawSelectedGate()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("X"))
            {
                EditorSceneController.Instance?.CancelGate();
                EditorGUILayout.EndHorizontal();
                return;
            }

            var sc = EditorSceneController.Instance;
            int cellCount = sc?.GateUnifiedCellPositions?.Count ?? 1;
            EditorGUILayout.LabelField($"Gate at {sc?.GateOriginalGridPosition} (size: {cellCount})");
            EditorGUILayout.EndHorizontal();

            MonoBehaviorInspector inspector = sc?.GateHandlesEditor;
            EditorGUI.BeginChangeCheck();
            if (inspector != null && inspector.serializedObject != null)
                EditorSceneController.DrawSelectedBlockInspectorWithoutTypePicker(inspector.serializedObject);

            if (EditorGUI.EndChangeCheck())
            {
                BeginLevelUndo("Edit Gate Data");
                HandleUpdateGateData(sc.GateOriginalGridPosition, sc.GateUnifiedCellPositions);
            }
        }

        private void DrawSelectedGenerator()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("X"))
            {
                EditorSceneController.Instance?.CancelGenerator();
                EditorGUILayout.EndHorizontal();
                return;
            }

            var sc = EditorSceneController.Instance;
            EditorGUILayout.LabelField($"Generator at {sc?.GeneratorOriginalGridPosition}");
            EditorGUILayout.EndHorizontal();

            MonoBehaviorInspector inspector = sc?.GeneratorHandlesEditor;
            EditorGUI.BeginChangeCheck();
            if (inspector != null && inspector.serializedObject != null)
                EditorSceneController.DrawGeneratorInspector(inspector.serializedObject);

            if (EditorGUI.EndChangeCheck())
            {
                BeginLevelUndo("Edit Generator Data");
                HandleUpdateGeneratorData(sc.GeneratorOriginalGridPosition);
            }
        }

        private void DrawSelectedInteractable()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("X"))
            {
                EditorSceneController.Instance?.CancelInteractable();
                EditorGUILayout.EndHorizontal();
                return;
            }

            var sc = EditorSceneController.Instance;
            EditorGUILayout.LabelField($"Interactable at {sc?.InteractableOriginalGridPosition}");
            EditorGUILayout.EndHorizontal();

            MonoBehaviorInspector inspector = sc?.InteractableHandlesEditor;
            EditorGUI.BeginChangeCheck();
            if (inspector != null && inspector.serializedObject != null)
                EditorSceneController.DrawSelectedBlockInspectorWithoutTypePicker(inspector.serializedObject);

            if (EditorGUI.EndChangeCheck())
            {
                BeginLevelUndo("Edit Interactable Data");
                HandleUpdateInteractableData(sc.InteractableOriginalGridPosition);
            }
        }

        private void OnSelectedBlockEditorSerializedExternalCommit(SerializedObject changedObject)
        {
            if (changedObject == null || EditorSceneController.Instance == null)
            {
                return;
            }

            if (!IsBlockSelected || selectedBlockProperty == null || EditorSceneController.Instance?.BlockHandlesDataEditor == null)
            {
                return;
            }

            if (changedObject.targetObject != EditorSceneController.Instance.SelectedBlockEditor)
            {
                return;
            }

            ApplySelectedBlockInspectorChangesToLevelAsset();
            Repaint();
        }

        private void ApplySelectedBlockInspectorChangesToLevelAsset()
        {
            if (selectedLevelRepresentation == null || selectedLevelRepresentation.NullLevel)
            {
                return;
            }

            BeginLevelUndo(isSelectedBlockGate ? "Edit Gate Data" : "Edit Block Data");
            selectedBlockProperty.managedReferenceValue = EditorSceneController.Instance.SelectedBlockEditor.Data;

            //copy values to neighbours
            if (isSelectedBlockGate && (selectedGateNeighbours.Count > 0))
            {
                LevelElementData sourceData = selectedBlockProperty.managedReferenceValue as LevelElementData;
                for (int i = 0; i < selectedGateNeighbours.Count; i++)
                {
                    SerializedProperty neighbourProp = selectedGateNeighbours[i];
                    if (sourceData == null) continue;
                    Vector2Int neighbourPos = neighbourProp.FindPropertyRelative(LevelAssetRepresentation.POSITION_PROPERTY_NAME).vector2IntValue;
                    LevelElementData copy = sourceData.Clone();
                    copy.SetPosition(neighbourPos);
                    neighbourProp.managedReferenceValue = copy;
                }
            }

            needUpdateLevelPreview = true;
        }

        private void DrawButtons()
        {
            cellTypeButtonsDrawRect = EditorGUILayout.BeginVertical();
            if (Event.current.type == EventType.Repaint && cellTypeButtonsDrawRect.width > 0)
                cachedButtonsContainerWidth = cellTypeButtonsDrawRect.width;
            float buttonsAvailableWidth = cachedButtonsContainerWidth > 0 ? cachedButtonsContainerWidth : currentSideBarWidth;
            cellTypeButtonWidth = 38;
            cellTypeButtonTextHeight = 10;
            cellTypeButtonOffset = 5;
            buttonsPerRow = Mathf.Max(1,
                Mathf.FloorToInt(buttonsAvailableWidth / ((cellTypeButtonWidth + cellTypeButtonOffset) * 1f)));
            rows = Mathf.CeilToInt(cellTypeButtons.Length / (buttonsPerRow * 1f));
            currentX = cellTypeButtonsDrawRect.x;
            currentY = cellTypeButtonsDrawRect.y;
            GUILayout.Space(rows * (cellTypeButtonWidth + cellTypeButtonTextHeight + cellTypeButtonOffset));


            for (int i = 0; i < cellTypeButtons.Length; i++)
            {
                if (currentX + cellTypeButtonWidth + cellTypeButtonOffset >
                    cellTypeButtonsDrawRect.x + buttonsAvailableWidth)
                {
                    currentX = cellTypeButtonsDrawRect.x;
                    currentY += cellTypeButtonWidth + cellTypeButtonTextHeight + cellTypeButtonOffset;
                }

                buttonRect = new Rect(currentX, currentY, cellTypeButtonWidth,
                    cellTypeButtonWidth + cellTypeButtonTextHeight);
                currentX += cellTypeButtonOffset + cellTypeButtonWidth;

                CellTypesHandler.CellType cellType = cellTypeHandler.GetCellType((int)cellTypeButtons[i]);
                Texture texture = GetCachedCellTypeTexture(cellType.value);


                if (cellType.value == cellTypeHandler.selectedCellTypeValue)
                {
                    GUI.DrawTexture(buttonRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, Color.yellow,
                        2, 0);
                }
                else
                {
                    GUI.DrawTexture(buttonRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, Color.gray,
                        2, 0);
                }

                labelRect = new Rect(buttonRect);
                labelRect.yMin = labelRect.yMax - cellTypeButtonTextHeight - 2f;

                if (texture != null)
                {
                    textureRect = new Rect(buttonRect);
                    textureRect.yMax -= cellTypeButtonTextHeight - 2f;
                    textureRect.xMax -= 2f;
                    textureRect.xMin += 2f;
                    textureRect.yMin += 2f;
                    GUI.DrawTexture(textureRect, texture);
                }

                GUI.Label(labelRect, GetCellTypeButtonLabel(cellType), cellTypeLabelStyle);

                if (GUI.Button(buttonRect, GUIContent.none, GUIStyle.none))
                {
                    cellTypeHandler.selectedCellTypeValue = cellType.value == cellTypeHandler.selectedCellTypeValue
                        ? CellTypesHandler.NO_SELECTION
                        : cellType.value;
                }
            }

            EditorGUILayout.EndVertical();
        }

        private string GetCellTypeButtonLabel(CellTypesHandler.CellType cellType)
        {
            switch ((ElementType)cellType.value)
            {
                case ElementType.InnerTile:
                    return "Inner";
                case ElementType.Obstacle:
                    return "Obs.";
                case ElementType.InteractableObject:
                    return "Inter.";
                case ElementType.Generator:
                    return "Gen.";
                case ElementType.Block:
                    return "Block";
                default:
                    return cellType.label;
            }
        }

        /// <summary>Picks which <see cref="InteractableObjectType"/> the grid paints while "Inter." is the active cell type.</summary>
        private void DrawInteractableTypeSelector()
        {
            if (cellTypeHandler.selectedCellTypeValue != (int)ElementType.InteractableObject)
            {
                return;
            }

            InteractableObjectType[] types = InteractableObjectData.EDITOR_PAINTABLE_TYPES;

            if (interactableTypeLabels == null || interactableTypeLabels.Length != types.Length)
            {
                interactableTypeLabels = new string[types.Length];
                for (int i = 0; i < types.Length; i++)
                    interactableTypeLabels[i] = ObjectNames.NicifyVariableName(types[i].ToString());
            }

            EditorGUILayout.LabelField("Interactable type", EditorStyles.miniBoldLabel);

            int currentIndex = Mathf.Max(0, Array.IndexOf(types, selectedInteractableType));
            int newIndex = EditorGUILayout.Popup(currentIndex, interactableTypeLabels);
            if (newIndex != currentIndex)
            {
                selectedInteractableType = types[newIndex];
            }
        }

        private void DrawPreSelectColorButtons()
        {
            int selectedCellTypeColor = cellTypeHandler.selectedCellTypeValue;
            if (selectedCellTypeColor is not ((int)ElementType.Gate or (int)ElementType.InteractableObject or (int)ElementType.Block))
            {
                return;
            }

            // Grinder is authored by its tape config, so a color pick would be meaningless for it.
            if (selectedCellTypeColor == (int)ElementType.InteractableObject
                && !InteractableObjectData.UsesObstacleColor(selectedInteractableType))
            {
                return;
            }

            string colorLabel;
            if (selectedCellTypeColor == (int)ElementType.InteractableObject)
                colorLabel = "Obstacle color";
            else if (selectedCellTypeColor == (int)ElementType.Block)
                colorLabel = "Block color";
            else
                colorLabel = "Gate color";

            EditorGUILayout.LabelField(colorLabel, EditorStyles.miniBoldLabel);

            BlockColor[] colors = LevelEditorColorCache.AllBlockColors;
            cellTypeButtonsDrawRect = EditorGUILayout.BeginVertical();
            float colorsAvailableWidth = cachedButtonsContainerWidth > 0 ? cachedButtonsContainerWidth : currentSideBarWidth;
            cellTypeButtonWidth = 24;
            cellTypeButtonOffset = 6;
            buttonsPerRow = Mathf.Max(1, Mathf.FloorToInt(colorsAvailableWidth / ((cellTypeButtonWidth + cellTypeButtonOffset) * 1f)));
            rows = Mathf.CeilToInt(colors.Length / (buttonsPerRow * 1f));
            currentX = cellTypeButtonsDrawRect.x;
            currentY = cellTypeButtonsDrawRect.y;
            GUILayout.Space(rows * (cellTypeButtonWidth + cellTypeButtonOffset));

            for (int i = 0; i < colors.Length; i++)
            {
                if (currentX + cellTypeButtonWidth + cellTypeButtonOffset >
                    cellTypeButtonsDrawRect.x + colorsAvailableWidth)
                {
                    currentX = cellTypeButtonsDrawRect.x;
                    currentY += cellTypeButtonWidth + cellTypeButtonOffset;
                }

                buttonRect = new Rect(currentX, currentY, cellTypeButtonWidth, cellTypeButtonWidth);
                currentX += cellTypeButtonOffset + cellTypeButtonWidth;


                CellTypesHandler.CellType cellType = cellColorHandler.GetCellType((int)colors[i]);
                DrawColorRect(buttonRect, cellType.color);

                if (cellType.value == cellColorHandler.selectedCellTypeValue)
                {
                    GUI.DrawTexture(buttonRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, Color.white,
                        2, 0);
                }
                else
                {
                    GUI.DrawTexture(buttonRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, Color.gray,
                        2, 0);
                }

                if (GUI.Button(buttonRect, GUIContent.none, GUIStyle.none))
                {
                    cellColorHandler.selectedCellTypeValue = (int)colors[i];
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawBorderButtons()
        {
            if (cellTypeHandler.selectedCellTypeValue != (int)ElementType.Border)
            {
                return;
            }

            drawBorderAsExtendable = EditorGUILayout.ToggleLeft("Extendable", drawBorderAsExtendable);
        }

        private void DisplayMainArea()
        {
            bool isEditingSpecial = showSpecialLevelsList && specialLevelsHandler != null;
            if (isEditingSpecial && !specialLevelsHandler.HasSelection)
                return;

            if (!isEditingSpecial && levelsHandler.SelectedLevelIndex == -1)
            {
                return;
            }

            EditorGUILayout.BeginVertical(GUI.skin.box);

            if (isEditingSpecial)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("File");
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField(specialLevelsHandler.SelectedLevelObject, typeof(LevelData), false);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
                specialLevelsHandler.DrawSelectedModeConfig();
            }
            else if (editingVariantAsset)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("File");
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField(editingVariantAsset, typeof(LevelData), false);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
            }
            else if (IsPropertyChanged(levelsHandler.SelectedLevelProperty, new GUIContent("File")))
            {
                IsBlockSelected = false;
                levelsHandler.ReopenLevel();
            }

            if (selectedLevelRepresentation.NullLevel)
            {
                EditorGUILayout.HelpBox("Level file is missing or corrupted. Please assign a valid LevelData asset.",
                    MessageType.Error);

                if (GUILayout.Button("Open Levels Folder"))
                {
                    EditorUtils.OpenInProjectWindow(LEVELS_FOLDER_PATH);
                }

                if (GUILayout.Button("Create New Level"))
                {
                    if (!isEditingSpecial)
                        levelsHandler.CreateNewLevelInIndex(levelsHandler.SelectedLevelIndex, true);
                }

                EditorGUILayout.EndVertical();
                return;
            }

            if (!isEditingSpecial)
            {
                DrawVariantsSection();
            }

            DisplayLevelSettings();

            DrawExtraLayerSection();

            Vector2Int sizeBefore = selectedLevelRepresentation.sizeProperty.vector2IntValue;
            Vector2Int sizeAfter = DrawLevelSizePropertyRow(sizeBefore);
            if (sizeAfter != sizeBefore)
            {
                BeginLevelUndo("Resize Level");
                selectedLevelRepresentation.sizeProperty.vector2IntValue = sizeAfter;
                IsBlockSelected = false;
                selectedLevelRepresentation.HandleSizePropertyChange();
                needUpdateLevelPreview = true;
                selectedLevelRepresentation.ApplyChanges();
            }
            EditorGUILayout.Space(5f);
            
            DrawLevel();

            if (!isEditingSpecial)
            {
                Object slotLevelForListLabel = levelsHandler.SelectedLevelProperty.objectReferenceValue;
                InvalidateLevelValidation(slotLevelForListLabel);
                InvalidateLevelValidation(selectedLevelRepresentation.EditedLevelObject);
                levelsHandler.UpdateCurrentLevelLabel(null);
            }
            else
            {
                InvalidateLevelValidation(selectedLevelRepresentation.EditedLevelObject);
            }
            selectedLevelRepresentation.ApplyChanges();


            if (needUpdateLevelPreview)
            {
                if (useDebouncedLevelPreview)
                {
                    if (EditorApplication.timeSinceStartup - lastDebouncedLevelPreviewRequestTime >=
                        LevelPreviewDebounceSeconds)
                    {
                        needUpdateLevelPreview = false;
                        useDebouncedLevelPreview = false;
                        LoadLevelPreview();
                    }
                    else
                    {
                        Repaint();
                    }
                }
                else
                {
                    needUpdateLevelPreview = false;
                    LoadLevelPreview();
                }
            }

            DrawTipsAndWarnings();

            EditorGUILayout.BeginHorizontal();

            var isLevelAnalysis = GUILayout.Toggle(isShowLevelAnalysis, "Show Level Analysis");
            if (isLevelAnalysis != isShowLevelAnalysis)
            {
                isShowLevelAnalysis = isLevelAnalysis;
                EditorPrefs.SetBool(SHOW_LEVEL_ANALYSIS_EDITOR_SCENE, isShowLevelAnalysis);
            }
            
            GUILayout.FlexibleSpace();

            float copyPasteHalfW = EditorGUIUtility.labelWidth * 0.5f - 2f;
            if (GUILayout.Button("Copy", GUILayout.Width(copyPasteHalfW), GUILayout.Height(30f)))
            {
                LevelData copyFrom = selectedLevelRepresentation.EditedLevelObject as LevelData;
                LevelDataUtilities.CopyLevelDataToClipboard(copyFrom);
            }

            if (GUILayout.Button("Paste", GUILayout.Width(copyPasteHalfW), GUILayout.Height(30f)))
            {
                LevelData pasteTarget = selectedLevelRepresentation.EditedLevelObject as LevelData;
                if (LevelDataUtilities.PasteClipboardOntoLevelData(pasteTarget, out string pasteError))
                {
                    IsBlockSelected = false;
                    selectedLevelRepresentation.RefreshSerializedObject();
                    levelStatisticsDirty = true;
                    LoadLevelPreview();
                }
                else
                {
                    EditorUtility.DisplayDialog("Paste Level", pasteError, "OK");
                }
            }

            if (GUILayout.Button(TEST_LEVEL, GUILayout.Width(EditorGUIUtility.labelWidth), GUILayout.Height(30f)))
            {
                TestLevel();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawVariantsSection()
        {
            if (showSpecialLevelsList)
                return;

            LevelData slotRef = levelsHandler.SelectedLevelProperty.objectReferenceValue as LevelData;
            if (!slotRef)
                return;

            LevelDatabase flowDb = levelsDatabase as LevelDatabase;
            LevelData baseLevel = flowDb
                ? LevelDataUtilities.ResolveBaseForSlotAsset(flowDb, slotRef)
                : slotRef;

            levelsDatabaseSerializedObject.Update();
            if (flowDb &&
                LevelDataUtilities.TryMergeVariantAssetsFromDisk(
                    levelsDatabaseSerializedObject,
                    baseLevel,
                    LEVELS_FOLDER_PATH))
            {
                levelsDatabaseSerializedObject.Update();
                levelsHandler.SetLevelLabels();
            }

            SerializedProperty entries = LevelDataUtilities.FindVariantEntriesProperty(levelsDatabaseSerializedObject);
            if (entries == null)
                return;

            int entryIdx = LevelDataUtilities.FindEntryIndex(entries, baseLevel);
            SerializedProperty variantsProp = null;
            SerializedProperty activeProp = null;
            if (entryIdx >= 0)
            {
                SerializedProperty entryProp = entries.GetArrayElementAtIndex(entryIdx);
                variantsProp = entryProp.FindPropertyRelative("variants");
                activeProp = entryProp.FindPropertyRelative("activeVariantIndex");
            }

            bool hasVariantMetadata = variantsProp != null && activeProp != null;
            int activeIdx = hasVariantMetadata ? activeProp.intValue : -1;

            EditorGUILayout.Space(2f);
            variantsToolbarScroll = EditorGUILayout.BeginScrollView(
                variantsToolbarScroll, false, false, GUILayout.Height(35f));
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add variant", GUILayout.Height(18f)))
            {
                LevelData copyFrom = editingVariantAsset ? editingVariantAsset : baseLevel;
                LevelData created = LevelDataUtilities.AddVariantAsset(
                    levelsDatabaseSerializedObject,
                    baseLevel,
                    copyFrom,
                    LEVELS_FOLDER_PATH);
                if (created)
                {
                    levelsDatabaseSerializedObject.Update();
                    AssetDatabase.Refresh();
                    OpenLevel(created, levelsHandler.SelectedLevelIndex);
                    levelsHandler.SetLevelLabels();
                    Repaint();
                }
            }
            
            // --- V0 (base): ship-base toggle only when we persist variantEntries ---
            if (hasVariantMetadata)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.Toggle(activeIdx < 0, EditorStyles.radioButton, GUILayout.Width(18f));
                Rect baseVariantToggleRect = GUILayoutUtility.GetLastRect();
                if (EditorGUI.EndChangeCheck() && activeIdx >= 0)
                    ApplyActiveVariantForBuild(baseLevel, -1);
                TryShowVariantToggleContextMenu(baseVariantToggleRect, -1);
            }
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.Toggle(true, EditorStyles.radioButton, GUILayout.Width(18f));
                EditorGUI.EndDisabledGroup();
            }

            bool baseWasSelected = !hasVariantMetadata || !editingVariantAsset;
            Color prevBg = GUI.backgroundColor;
            if (baseWasSelected)
                GUI.backgroundColor = VariantBuildButtonTint;
            if (GUILayout.Button("V0", EditorStyles.miniButton, GUILayout.MinWidth(36f)))
                OpenLevel(baseLevel, levelsHandler.SelectedLevelIndex);
            GUI.backgroundColor = prevBg;

            // --- V1, V2, V3 … (requires serialized entry) ---
            if (hasVariantMetadata)
            {
                for (int i = 0; i < variantsProp.arraySize; i++)
                {
                    LevelData v = variantsProp.GetArrayElementAtIndex(i).objectReferenceValue as LevelData;
                    if (!v)
                        continue;

                    GUILayout.Space(6f);

                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.Toggle(activeIdx == i, EditorStyles.radioButton, GUILayout.Width(18f));
                    Rect variantToggleRect = GUILayoutUtility.GetLastRect();
                    if (EditorGUI.EndChangeCheck() && activeIdx != i)
                        ApplyActiveVariantForBuild(baseLevel, i);
                    TryShowVariantToggleContextMenu(variantToggleRect, i);

                    bool wasEditing = editingVariantAsset == v;
                    if (wasEditing)
                        GUI.backgroundColor = VariantBuildButtonTint;
                    if (GUILayout.Button("V" + (i + 1), EditorStyles.miniButton, GUILayout.MinWidth(36f)))
                        OpenLevel(v, levelsHandler.SelectedLevelIndex);
                    GUI.backgroundColor = prevBg;

                    if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(22f)))
                    {
                        if (EditorUtility.DisplayDialog(
                                "Remove variant",
                                $"Remove 'V{i + 1}' from this level and delete the asset?",
                                "Remove",
                                "Cancel"))
                        {
                            LevelDataUtilities.RemoveVariantAt(
                                levelsDatabaseSerializedObject,
                                baseLevel,
                                i,
                                deleteAsset: true);
                            levelsDatabaseSerializedObject.Update();
                            if (wasEditing)
                                OpenLevel(baseLevel, levelsHandler.SelectedLevelIndex);
                            levelsHandler.SetLevelLabels();
                            Repaint();
                            break;
                        }
                    }
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        private void DrawExtraLayerSection()
        {
            if (selectedLevelRepresentation == null || selectedLevelRepresentation.NullLevel)
                return;

            LevelData levelData = selectedLevelRepresentation.EditedLevelObject as LevelData;
            if (levelData == null)
                return;

            Rect rowRect = EditorGUILayout.GetControlRect();
            Rect labelRect = new Rect(rowRect.x, rowRect.y, EditorGUIUtility.labelWidth, rowRect.height);
            Rect toggleRect = new Rect(labelRect.xMax, rowRect.y, 18f, rowRect.height);

            EditorGUI.LabelField(labelRect, "Has Extra Layer");
            EditorGUI.BeginChangeCheck();
            bool hasExtraLayer = EditorGUI.Toggle(toggleRect, levelData.HasExtraLayer);
            if (EditorGUI.EndChangeCheck())
            {
                BeginLevelUndo(hasExtraLayer ? "Enable Extra Layer" : "Disable Extra Layer");
                levelData.SetExtraLayerEnabled(hasExtraLayer);
                RuntimeEditorUtils.SetDirty(levelData);
                selectedLevelRepresentation.RefreshSerializedObject();

                if (hasExtraLayer)
                {
                    selectedLevelRepresentation.HandleSizePropertyChange();
                    selectedLevelRepresentation.ApplyChanges();
                    selectedLevelRepresentation.RefreshSerializedObject();
                }

                if (!hasExtraLayer && isExtraLayerTabActive)
                    isExtraLayerTabActive = false;

                selectedLevelRepresentation.SwitchActiveLayer(isExtraLayerTabActive);
                IsBlockSelected = false;
                blockSceneInspectorPosition = new Vector2Int(-1, -1);
                generatorSceneInspectorPosition = new Vector2Int(-1, -1);
                needUpdateLevelPreview = true;
                Repaint();
            }

            if (!hasExtraLayer)
                return;

            const float controlSpacing = 8f;
            const float tabsPreferredWidth = 220f;
            const float tabsMinWidth = 160f;
            const float typeButtonPreferredWidth = 140f;
            const float typeButtonMinWidth = 104f;
            float controlsX = toggleRect.xMax + controlSpacing;
            float controlsWidth = rowRect.xMax - controlsX;
            float typeButtonWidth = controlsWidth >= tabsMinWidth + controlSpacing + typeButtonMinWidth
                ? Mathf.Min(typeButtonPreferredWidth, controlsWidth - tabsMinWidth - controlSpacing)
                : 0f;
            float tabsWidth = Mathf.Max(0f, controlsWidth - typeButtonWidth - controlSpacing);

            Rect tabsRect = new Rect(controlsX, rowRect.y, Mathf.Min(tabsPreferredWidth, tabsWidth), rowRect.height);
            int selectedTab = GUI.Toolbar(tabsRect, isExtraLayerTabActive ? 1 : 0, new[] { "Base Layer", "Extra Layer" });
            bool nextExtraLayerTabActive = selectedTab == 1;
            if (nextExtraLayerTabActive != isExtraLayerTabActive)
                SwitchToLayer(nextExtraLayerTabActive);

            if (typeButtonWidth > 0f)
            {
                Rect typeButtonRect = new Rect(tabsRect.xMax + controlSpacing, rowRect.y,
                    typeButtonWidth, rowRect.height);
                DrawExtraLayerTypeButton(typeButtonRect, levelData);
            }
        }

        private void DrawExtraLayerTypeButton(Rect buttonRect, LevelData levelData)
        {
            Color previousBackgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = GetExtraLayerTypeButtonTint(levelData.ExtraLayerType);

            var content = new GUIContent(
                $"Type: {levelData.ExtraLayerType}",
                "Configure extra layer type");

            if (GUI.Button(buttonRect, content, EditorStyles.miniButton))
                ShowExtraLayerTypeMenu(buttonRect, levelData);

            GUI.backgroundColor = previousBackgroundColor;
        }

        private void ShowExtraLayerTypeMenu(Rect buttonRect, LevelData levelData)
        {
            GenericMenu menu = new GenericMenu();
            foreach (ExtraLayerType type in Enum.GetValues(typeof(ExtraLayerType)))
            {
                ExtraLayerType selectedType = type;
                menu.AddItem(
                    new GUIContent(ObjectNames.NicifyVariableName(selectedType.ToString())),
                    levelData.ExtraLayerType == selectedType,
                    () => SetExtraLayerType(levelData, selectedType));
            }

            menu.DropDown(buttonRect);
        }

        private void SetExtraLayerType(LevelData levelData, ExtraLayerType type)
        {
            if (levelData.ExtraLayerType == type)
                return;

            BeginLevelUndo("Change Extra Layer Type");
            levelData.SetExtraLayerType(type);
            RuntimeEditorUtils.SetDirty(levelData);
            selectedLevelRepresentation.RefreshSerializedObject();
            Repaint();
        }

        private static Color GetExtraLayerTypeButtonTint(ExtraLayerType type)
        {
            switch (type)
            {
                case ExtraLayerType.Tunnel:
                    return ExtraLayerTunnelButtonTint;
                default:
                    return ExtraLayerLiftButtonTint;
            }
        }

        private void SwitchToLayer(bool extraLayer)
        {
            if (isExtraLayerTabActive == extraLayer)
                return;

            isExtraLayerTabActive = extraLayer;
            IsBlockSelected = false;
            blockSceneInspectorPosition = new Vector2Int(-1, -1);
            generatorSceneInspectorPosition = new Vector2Int(-1, -1);
            interactableSceneInspectorPosition = new Vector2Int(-1, -1);
            selectedLevelRepresentation.SwitchActiveLayer(extraLayer);
            needUpdateLevelPreview = true;
            Repaint();
        }

        private void ApplyActiveVariantForBuild(LevelData baseLevel, int activeVariantIndex)
        {
            LevelDataUtilities.SetActiveVariantIndex(
                levelsDatabaseSerializedObject,
                baseLevel,
                activeVariantIndex);
            levelsDatabaseSerializedObject.Update();
            EditorUtility.SetDirty(levelsDatabase);
            levelsHandler.RefreshLabelsForBase(baseLevel);
            Repaint();
        }

        private void TryShowVariantToggleContextMenu(Rect toggleRect, int variantArrayIndex)
        {
            Event e = Event.current;
            if (e.type != EventType.ContextClick || !toggleRect.Contains(e.mousePosition))
                return;

            e.Use();
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Apply all"), false, () => ApplyActiveVariantIndexToAllLevels(variantArrayIndex));
            menu.ShowAsContext();
        }

        private void ApplyActiveVariantIndexToAllLevels(int targetVariantArrayIndex)
        {
            LevelDatabase flowDb = levelsDatabase as LevelDatabase;
            if (!flowDb || levelsSerializedProperty == null)
                return;

            levelsDatabaseSerializedObject.Update();
            if (LevelDataUtilities.ApplyActiveVariantIndexToAllLevels(
                    levelsDatabaseSerializedObject,
                    flowDb,
                    levelsSerializedProperty,
                    targetVariantArrayIndex))
            {
                levelsDatabaseSerializedObject.Update();
                levelsHandler.SetLevelLabels();
                Repaint();
            }
        }

        private void DisplayLevelSettings()
        {
            selectedLevelRepresentation.DisplayProperties();
            selectedLevelRepresentation.ApplyChanges();
        }

        private static Vector2Int DrawLevelSizePropertyRow(Vector2Int current)
        {
            Vector2Int v = current;

            const float stepButtonWidth = 22f;
            const float axisLabelWidth = 16f;
            const float intFieldWidth = 52f;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Size"), GUILayout.Width(EditorGUIUtility.labelWidth));
            EditorGUILayout.LabelField("X", GUILayout.Width(axisLabelWidth));
            v.x = EditorGUILayout.IntField(v.x, GUILayout.Width(intFieldWidth));
            if (GUILayout.Button("▲", EditorStyles.miniButton, GUILayout.Width(stepButtonWidth)))
                v.x++;
            if (GUILayout.Button("▼", EditorStyles.miniButton, GUILayout.Width(stepButtonWidth)))
                v.x = Mathf.Max(LEVEL_GRID_MIN_SIZE, v.x - 1);

            GUILayout.Space(8f);

            EditorGUILayout.LabelField("Y", GUILayout.Width(axisLabelWidth));
            v.y = EditorGUILayout.IntField(v.y, GUILayout.Width(intFieldWidth));
            if (GUILayout.Button("▲", EditorStyles.miniButton, GUILayout.Width(stepButtonWidth)))
                v.y++;
            if (GUILayout.Button("▼", EditorStyles.miniButton, GUILayout.Width(stepButtonWidth)))
                v.y = Mathf.Max(LEVEL_GRID_MIN_SIZE, v.y - 1);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            return new Vector2Int(
                Mathf.Max(LEVEL_GRID_MIN_SIZE, v.x),
                Mathf.Max(LEVEL_GRID_MIN_SIZE, v.y));
        }


        private bool TestLevel()
        {
            bool isSpecialSelection = showSpecialLevelsList && specialLevelsHandler != null && specialLevelsHandler.HasSelection;
            int editorTestIndex = isSpecialSelection ? SPECIAL_LEVEL_TEST_SLOT : levelsHandler.SelectedLevelIndex;
            LevelData levelToTest = ResolveLevelToTest(isSpecialSelection);

            if (!levelToTest || editorTestIndex < 0)
            {
                LevelDatabase.ClearEditorPlayModeLevelOverride();
                Debug.LogWarning("[LevelEditor] Test Level: no level is selected, nothing to test.");
                return false;
            }

            SaveEditorSessionState();

            EditorSceneController.Instance.Unsubscribe();

            ActiveSession.SetEditorLevelIndex(editorTestIndex);
            LevelDatabase.SetEditorPlayModeLevelOverride(editorTestIndex, levelToTest);
            LevelDatabase.SetEditorSpecialTestPlay(isSpecialSelection && specialLevelsHandler.IsTestFilterActive);

            LevelEditorTestPlaySession.MarkStarted();

            UnloadEditor();
            OpenScene(GAME_SCENE_PATH);
            EditorApplication.isPlaying = true;
            return true;
        }

        private LevelData ResolveLevelToTest(bool isSpecialSelection)
        {
            if (editingVariantAsset)
                return editingVariantAsset;

            if (isSpecialSelection)
                return specialLevelsHandler.SelectedLevelObject as LevelData;

            int selectedIndex = levelsHandler.SelectedLevelIndex;
            if (levelsSerializedProperty == null || selectedIndex < 0 || selectedIndex >= levelsSerializedProperty.arraySize)
                return null;

            return levelsSerializedProperty.GetArrayElementAtIndex(selectedIndex).objectReferenceValue as LevelData;
        }

        private void AssignElementIds()
        {
            if (selectedLevelRepresentation == null || selectedLevelRepresentation.itemsProperty == null)
                return;

            selectedLevelRepresentation.ApplyChanges();

            LevelData levelData = selectedLevelRepresentation.EditedLevelObject as LevelData;
            if (levelData)
            {
                levelData.AssignElementIds();
                EditorUtility.SetDirty(levelData);
                selectedLevelRepresentation.RefreshSerializedObject();
            }
        }

        private void LoadLevelPreview()
        {
            LevelData levelData = selectedLevelRepresentation.EditedLevelObject as LevelData;
            AssignElementIds();

            LevelElementData[] activeLayerElements = null;
            if (isExtraLayerTabActive && levelData != null && levelData.HasExtraLayer)
                activeLayerElements = levelData.ExtraLayerElements;

            EditorSceneController.Instance.LoadLevel(
                levelData,
                HandleBlockChange,
                HandleBlockSpawn,
                HandleBlockDelete,
                HandleBlockColorChange,
                HandleBlockTypeChange,
                HandleDisplayWindow,
                HandleCreateBlockSceneInspector,
                HandleApplyBlockSceneInspector,
                HandleGateMove,
                HandleCreateGateEditor,
                HandleUpdateGateData,
                BeginLevelUndo,
                HandleCreateGeneratorEditor,
                HandleUpdateGeneratorData,
                HandleGeneratorMove,
                HandleSwitchCellElement,
                HandleBorderElementDelete,
                HandleCreateInteractableEditor,
                HandleUpdateInteractableData,
                HandleInteractableMove,
                HandleInteractableDuplicate,
                activeLayerElements);

            if (needToSelectLevelBlock)
            {
                needToSelectLevelBlock = false;
                EditorSceneController.Instance.SelectBlock(levelBlockPosition);

                if (EditorSceneController.Instance.SelectedBlockEditor != null)
                {
                    int ix = selectedLevelRepresentation.GetIndex(levelBlockPosition.x, levelBlockPosition.y);
                    if (ix >= 0)
                    {
                        SerializedProperty fresh =
                            selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(ix);
                        EditorSceneController.Instance.SelectedBlockEditor.Data =
                            fresh.managedReferenceValue as LevelElementData;
                    }
                }
            }

            if (needToSelectGate)
            {
                needToSelectGate = false;
                EditorSceneController.Instance.SelectGate(gateSelectPosition);
            }

            if (needToSelectGenerator)
            {
                needToSelectGenerator = false;
                EditorSceneController.Instance.SelectGenerator(generatorSelectPosition);
            }

            if (needToSelectInteractable)
            {
                needToSelectInteractable = false;
                EditorSceneController.Instance.SelectInteractable(interactableSelectPosition);
            }

            // Restore multi-select after level reload (positions saved by PrepareForBlocksMovement or batch operations)
            EditorSceneController.Instance.RestoreMultiSelect();

            // If sidebar block selection was active when the level reloaded, BlockHandlesDataEditor was torn down
            // by PrepareForBlocksMovement. Recreate it so the sidebar inspector stays visible.
            if (IsBlockSelected && selectedBlockPosition.x >= 0 &&
                EditorSceneController.Instance.BlockHandlesDataEditor == null &&
                selectedLevelRepresentation != null && !selectedLevelRepresentation.NullLevel)
            {
                int ix = selectedLevelRepresentation.GetIndex(selectedBlockPosition.x, selectedBlockPosition.y);
                if (ix >= 0)
                    selectedBlockProperty = selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(ix);
                HandleSelectedBlockEditor();
            }
        }

        public void HandleCreateBlockSceneInspector(Vector2Int primaryGridPosition)
        {
            if (selectedLevelRepresentation == null || selectedLevelRepresentation.NullLevel)
            {
                return;
            }

            if (blockSceneInspectorPosition == primaryGridPosition &&
                EditorSceneController.Instance.BlockHandlesDataEditor != null)
            {
                return;
            }

            IsBlockSelected = false;

            if (EditorSceneController.Instance.BlockHandlesDataEditor != null)
            {
                DestroyImmediate(EditorSceneController.Instance.BlockHandlesDataEditor);
                EditorSceneController.Instance.BlockHandlesDataEditor = null;
            }

            blockSceneInspectorPosition = primaryGridPosition;

            int index = selectedLevelRepresentation.GetIndex(primaryGridPosition.x, primaryGridPosition.y);
            if (index == -1)
            {
                return;
            }

            SerializedProperty elementProp =
                selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(index);
            EditorSceneController.Instance.SelectedBlockEditor.Data = elementProp.managedReferenceValue as LevelElementData;
            blockSceneInspectorSnapshot = EditorSceneController.Instance.SelectedBlockEditor.Data?.Clone();

            MonoBehaviorInspector inspector =
                (MonoBehaviorInspector)Editor.CreateEditor(EditorSceneController.Instance.SelectedBlockEditor,
                    typeof(MonoBehaviorInspector));
            inspector.SetScriptFieldState(false);
            EditorSceneController.Instance.BlockHandlesDataEditor = inspector;
        }

        public void HandleApplyBlockSceneInspector(List<Vector2Int> gridPositions)
        {
            if (selectedLevelRepresentation == null || selectedLevelRepresentation.NullLevel)
            {
                return;
            }

            if (gridPositions == null || gridPositions.Count == 0)
            {
                return;
            }

            LevelElementData template = EditorSceneController.Instance.SelectedBlockEditor.Data;
            if (template == null)
            {
                return;
            }

            bool isMultiSelect = gridPositions.Count > 1;

            // When multi-selecting, detect which fields changed so we only propagate those fields.
            // Each block keeps its own values for unchanged fields.
            bool typeChanged      = true;
            bool colorChanged     = true;
            bool extendableChanged = true;
            bool effectsChanged   = true;

            if (isMultiSelect && blockSceneInspectorSnapshot != null)
            {
                BlockLevelElementData tBlock = template as BlockLevelElementData;
                BlockLevelElementData sBlock = blockSceneInspectorSnapshot as BlockLevelElementData;
                BorderLevelElementData sBorder = blockSceneInspectorSnapshot as BorderLevelElementData;
                typeChanged       = tBlock?.BlockType    != sBlock?.BlockType;
                colorChanged      = tBlock?.BlockColor   != sBlock?.BlockColor;
                extendableChanged = (template as BorderLevelElementData)?.IsExtendable != sBorder?.IsExtendable;
                effectsChanged    = !BlockEffectsEqual(tBlock?.BlockEffects, sBlock?.BlockEffects);
            }

            foreach (Vector2Int pos in gridPositions)
            {
                int ix = selectedLevelRepresentation.GetIndex(pos.x, pos.y);
                if (ix < 0)
                {
                    continue;
                }

                SerializedProperty element =
                    selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(ix);
                if (LevelAssetRepresentation.GetElementType(element) != ElementType.Block)
                {
                    continue;
                }

                if (isMultiSelect)
                {
                    ApplyChangedBlockSceneFields(element, template,
                        typeChanged, colorChanged, extendableChanged, effectsChanged);
                }
                else
                {
                    ApplyBlockSceneFieldsFromTemplate(element, template);
                }
            }

            // Update snapshot so the next incremental change compares against the current applied state.
            blockSceneInspectorSnapshot = template.Clone();

            selectedLevelRepresentation.ApplyChanges();
            RequestDebouncedLevelPreview();
            levelBlockPosition = gridPositions[gridPositions.Count - 1];
            needToSelectLevelBlock = true;
            Repaint();
        }

        private void RequestDebouncedLevelPreview()
        {
            lastDebouncedLevelPreviewRequestTime = EditorApplication.timeSinceStartup;
            useDebouncedLevelPreview = true;
            needUpdateLevelPreview = true;
        }

        private static bool BlockEffectsEqual(BlockEffectData[] a, BlockEffectData[] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (EditorJsonUtility.ToJson(a[i]) != EditorJsonUtility.ToJson(b[i])) return false;
            }
            return true;
        }

        private static void ApplyChangedBlockSceneFields(SerializedProperty destElement, LevelElementData template,
            bool typeChanged, bool colorChanged, bool extendableChanged, bool effectsChanged)
        {
            BlockLevelElementData tBlock = template as BlockLevelElementData;
            if (tBlock == null) return;

            if (typeChanged)
                destElement.FindPropertyRelative(LevelAssetRepresentation.BLOCK_TYPE_PROPERTY_NAME).intValue =
                    (int)tBlock.BlockType;

            if (colorChanged)
                destElement.FindPropertyRelative(LevelAssetRepresentation.BLOCK_COLOR_PROPERTY_NAME).intValue =
                    (int)tBlock.BlockColor;

            if (effectsChanged)
            {
                SerializedProperty effects =
                    destElement.FindPropertyRelative(LevelAssetRepresentation.BLOCK_EFFECTS_PROPERTY_NAME);
                BlockEffectData[] srcEffects = tBlock.BlockEffects;
                int len = srcEffects != null ? srcEffects.Length : 0;
                effects.arraySize = len;
                for (int i = 0; i < len; i++)
                    effects.GetArrayElementAtIndex(i).managedReferenceValue = srcEffects[i]?.Clone();
            }
        }

        private static void ApplyBlockSceneFieldsFromTemplate(SerializedProperty destElement,
            LevelElementData template)
        {
            BlockLevelElementData tBlock = template as BlockLevelElementData;
            if (tBlock == null) return;

            destElement.FindPropertyRelative(LevelAssetRepresentation.BLOCK_TYPE_PROPERTY_NAME).intValue =
                (int)tBlock.BlockType;
            destElement.FindPropertyRelative(LevelAssetRepresentation.BLOCK_COLOR_PROPERTY_NAME).intValue =
                (int)tBlock.BlockColor;

            SerializedProperty effects =
                destElement.FindPropertyRelative(LevelAssetRepresentation.BLOCK_EFFECTS_PROPERTY_NAME);
            BlockEffectData[] srcEffects = tBlock.BlockEffects;
            int len = srcEffects != null ? srcEffects.Length : 0;
            effects.arraySize = len;
            for (int i = 0; i < len; i++)
                effects.GetArrayElementAtIndex(i).managedReferenceValue = srcEffects[i]?.Clone();
        }

        private void BeginLevelUndo(string actionName)
        {
            if (selectedLevelRepresentation == null || selectedLevelRepresentation.NullLevel)
            {
                return;
            }

            selectedLevelRepresentation.RecordUndo(actionName);
        }


        public void HandleDisplayWindow(Vector2Int oldPosition, LevelFigure[] figures, BlockType[] types,
            Action<EditorSelectBlockData> action)
        {
            SerializedProperty gateProperty =
                selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(
                    selectedLevelRepresentation.GetIndex(oldPosition.x, oldPosition.y));
            var colors = new List<CellTypesHandler.CellType>();
            SerializedProperty gateDataArray = gateProperty.FindPropertyRelative(LevelAssetRepresentation.GATE_DATA_PROPERTY_NAME);
            for (int j = 0; j < gateDataArray.arraySize; j++)
            {
                SerializedProperty colorData = gateDataArray.GetArrayElementAtIndex(j);
                BlockColor color = (BlockColor)colorData.FindPropertyRelative("color").intValue;
                CellTypesHandler.CellType cellType = cellColorHandler.GetCellType((int)color);
                colors.Add(cellType);
            }
            
            FigureSelectorWindow.CreateWindow(figures, types, colors, action);
        }

        public void HandleBlockColorChange(Vector2Int oldPosition, BlockColor blockColor)
        {
            if (selectedBlockPosition == oldPosition)
            {
                IsBlockSelected = false;
                Debug.LogWarning("Block unselected to prevent data corruption.");
            }

            SerializedProperty oldProperty =
                selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(
                    selectedLevelRepresentation.GetIndex(oldPosition.x, oldPosition.y));
            oldProperty.FindPropertyRelative(LevelAssetRepresentation.BLOCK_COLOR_PROPERTY_NAME).intValue = (int)blockColor;
            selectedLevelRepresentation.ApplyChanges();
            needUpdateLevelPreview = true;
            levelBlockPosition = oldPosition;
            needToSelectLevelBlock = true;
            Repaint();
        }

        public void HandleBlockTypeChange(Vector2Int position, BlockType newBlockType)
        {
            if (selectedBlockPosition == position)
            {
                IsBlockSelected = false;
                Debug.LogWarning("Block unselected to prevent data corruption.");
            }

            SerializedProperty blockProperty = selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(
                selectedLevelRepresentation.GetIndex(position.x, position.y));

            ElementType elementType = LevelAssetRepresentation.GetElementType(blockProperty);

            if (elementType != ElementType.Block && elementType != ElementType.Gate)
            {
                Debug.LogWarning("Can only change type for Block or Gate elements.");
                return;
            }

            BlockType oldBlockType =
                (BlockType)blockProperty.FindPropertyRelative(LevelAssetRepresentation.BLOCK_TYPE_PROPERTY_NAME).intValue;
            blockProperty.FindPropertyRelative(LevelAssetRepresentation.BLOCK_TYPE_PROPERTY_NAME).intValue =
                (int)newBlockType;

            selectedLevelRepresentation.ApplyChanges();
            needUpdateLevelPreview = true;
            levelBlockPosition = position;
            needToSelectLevelBlock = true;

            Debug.Log($"Changed block type at {position} from {oldBlockType} to {newBlockType}");

            Repaint();
        }

        public void HandleBlockChange(Vector2Int oldPosition, Vector2Int newPosition)
        {
            if (oldPosition == newPosition)
            {
                return;
            }

            if (selectedBlockPosition == oldPosition)
            {
                IsBlockSelected = false;
                Debug.LogWarning("Block unselected to prevent data corruption.");
            }

            SerializedProperty oldProperty =
                selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(
                    selectedLevelRepresentation.GetIndex(oldPosition.x, oldPosition.y));
            SerializedProperty newProperty =
                selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(
                    selectedLevelRepresentation.GetIndex(newPosition.x, newPosition.y));

            if (LevelAssetRepresentation.GetElementType(newProperty) != ElementType.InnerTile)
            {
                Debug.LogWarning("Can place block only on inner tile.");
                needUpdateLevelPreview = true;
                levelBlockPosition = oldPosition;
                needToSelectLevelBlock = true;
                Repaint();
                return;
            }

            LevelElementData oldData = oldProperty.managedReferenceValue as LevelElementData;
            LevelElementData movedData = oldData?.Clone();
            if (movedData != null) movedData.SetPosition(newPosition);
            newProperty.managedReferenceValue = movedData;

            LevelElementData clearedData = new InnerTileLevelElementData();
            clearedData.SetPosition(oldPosition);
            oldProperty.managedReferenceValue = clearedData;
            selectedLevelRepresentation.ApplyChanges();
            selectedLevelRepresentation.RefreshSerializedObject();
            needUpdateLevelPreview = true;
            levelBlockPosition = newPosition;
            needToSelectLevelBlock = true;
            Repaint();
        }

        public void HandleBlockSpawn(Vector2Int oldPosition, Vector2Int newPosition, EditorSelectBlockData data)
        {
            SerializedProperty oldProperty =
                selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(
                    selectedLevelRepresentation.GetIndex(oldPosition.x, oldPosition.y));
            int index = selectedLevelRepresentation.GetIndex(newPosition.x, newPosition.y);

            if (index == -1)
            {
                Debug.LogError($"[Block spawn] newPosition: {newPosition} is outside field. Block not spawned.");
                return;
            }
            
            SerializedProperty newProperty = selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(index);
            if (LevelAssetRepresentation.GetElementType(newProperty) != ElementType.InnerTile)
            {
                Debug.LogError($"[Block spawn] newPosition: {newPosition} is not inner tile. Block not spawned.");
                return;
            }

            if (data.spawnFromFullCopy != null)
            {
                newProperty.managedReferenceValue = data.spawnFromFullCopy;
            }
            else
            {
                BlockColor color = data.blockColor;
                BlockColor srcColor = color != BlockColor.None ? color
                    : (BlockColor)(oldProperty.managedReferenceValue is BlockLevelElementData ob ? (int)ob.BlockColor : 0);

                var newBlock = new BlockLevelElementData();
                newBlock.SetBlockType(data.blockType);
                newBlock.SetBlockColor(srcColor);
                newBlock.SetPosition(newPosition);
                newProperty.managedReferenceValue = newBlock;
            }

            selectedLevelRepresentation.ApplyChanges();
            blockSceneInspectorPosition = new Vector2Int(-1, -1);
            needUpdateLevelPreview = true;
            levelBlockPosition = newPosition;
            needToSelectLevelBlock = true;
            cellTypeHandler.selectedCellTypeValue = (int)ElementType.InnerTile;
            Repaint();
        }

        public void HandleBlockDelete(Vector2Int oldPosition)
        {
            if (selectedBlockPosition == oldPosition)
            {
                IsBlockSelected = false;
                Debug.LogWarning("Block unselected to prevent data corruption.");
            }

            SerializedProperty oldProperty =
                selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(
                    selectedLevelRepresentation.GetIndex(oldPosition.x, oldPosition.y));
            var innerTile = new InnerTileLevelElementData();
            innerTile.SetPosition(oldPosition);
            oldProperty.managedReferenceValue = innerTile;
            selectedLevelRepresentation.ApplyChanges();
            needUpdateLevelPreview = true;
            Repaint();
        }

        /// <summary>
        /// Deletes a Border-mounted element (Gate or Generator) at <paramref name="position"/> by reverting the
        /// cell to a plain Border, the same vacated-cell state the move handlers leave behind.
        /// </summary>
        public void HandleBorderElementDelete(Vector2Int position)
        {
            int index = selectedLevelRepresentation.GetIndex(position.x, position.y);
            if (index == -1) return;

            SerializedProperty prop = selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(index);
            var border = new BorderLevelElementData();
            border.SetPosition(position);
            prop.managedReferenceValue = border;

            IsBlockSelected = false;
            selectedLevelRepresentation.ApplyChanges();
            needUpdateLevelPreview = true;
            Repaint();
        }

        public void HandleGateMove(Vector2Int[] oldPositions, Vector2Int[] newPositions)
        {
            if (oldPositions.Length != newPositions.Length || oldPositions.Length == 0)
            {
                Debug.LogWarning("Gate move: position arrays mismatch or empty.");
                return;
            }

            for (int i = 0; i < newPositions.Length; i++)
            {
                int newIndex = selectedLevelRepresentation.GetIndex(newPositions[i].x, newPositions[i].y);
                if (newIndex == -1)
                {
                    Debug.LogWarning($"Gate move: new position {newPositions[i]} is outside level bounds.");
                    needUpdateLevelPreview = true;
                    Repaint();
                    return;
                }

                SerializedProperty newProp = selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(newIndex);
                ElementType newType = LevelAssetRepresentation.GetElementType(newProp);
                if (newType != ElementType.Border)
                {
                    Debug.LogWarning($"Gate move: target position {newPositions[i]} is not a Border (type={newType}). Can only move gate to border positions.");
                    needUpdateLevelPreview = true;
                    Repaint();
                    return;
                }
            }

            for (int i = 0; i < oldPositions.Length; i++)
            {
                int oldIndex = selectedLevelRepresentation.GetIndex(oldPositions[i].x, oldPositions[i].y);
                int newIndex = selectedLevelRepresentation.GetIndex(newPositions[i].x, newPositions[i].y);
                if (oldIndex == -1 || newIndex == -1) continue;

                SerializedProperty oldProp = selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(oldIndex);
                SerializedProperty newProp = selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(newIndex);

                LevelElementData oldGateData = oldProp.managedReferenceValue as LevelElementData;
                LevelElementData movedGate = oldGateData?.Clone();
                if (movedGate != null) movedGate.SetPosition(newPositions[i]);
                newProp.managedReferenceValue = movedGate;

                var clearedBorder = new BorderLevelElementData();
                clearedBorder.SetPosition(oldPositions[i]);
                oldProp.managedReferenceValue = clearedBorder;
            }

            IsBlockSelected = false;
            selectedLevelRepresentation.ApplyChanges();
            needUpdateLevelPreview = true;
            needToSelectGate = true;
            Repaint();
        }

        /// <summary>
        /// Scene-view Ctrl+Click pie menu: switches the cell at <paramref name="position"/> to
        /// <paramref name="elementType"/> via the same paint path the grid uses, reusing the sidebar's selected
        /// color and extendable flag. Undo is already registered by the scene controller before this call.
        /// </summary>
        public void HandleSwitchCellElement(Vector2Int position, ElementType elementType)
        {
            if (selectedLevelRepresentation == null || selectedLevelRepresentation.NullLevel)
            {
                return;
            }

            if (selectedLevelRepresentation.GetIndex(position.x, position.y) == -1)
            {
                return;
            }

            int colorValue = Mathf.Clamp(cellColorHandler.selectedCellTypeValue, 0, int.MaxValue);
            selectedLevelRepresentation.SetItemsValue(position.x, position.y, (int)elementType, colorValue,
                drawBorderAsExtendable, selectedInteractableType);
            selectedLevelRepresentation.ApplyChanges();

            IsBlockSelected = false;
            needUpdateLevelPreview = true;
            Repaint();
        }

        public void HandleCreateGateEditor(Vector2Int position)
        {
            IsBlockSelected = false;

            if (gateEditorInspector)
            {
                DestroyImmediate(gateEditorInspector);
            }

            int index = selectedLevelRepresentation.GetIndex(position.x, position.y);
            if (index == -1) return;

            SerializedProperty gateProp = selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(index);
            EditorSceneController.Instance.SelectedBlockEditor.Data = gateProp.managedReferenceValue as LevelElementData;
            gateEditorInspector = (MonoBehaviorInspector)Editor.CreateEditor(
                EditorSceneController.Instance.SelectedBlockEditor, typeof(MonoBehaviorInspector));
            gateEditorInspector.SetScriptFieldState(false);
            EditorSceneController.Instance.GateHandlesEditor = gateEditorInspector;
        }

        public void HandleUpdateGateData(Vector2Int position, List<Vector2Int> unifiedCellPositions)
        {
            int primaryIndex = selectedLevelRepresentation.GetIndex(position.x, position.y);
            if (primaryIndex == -1) return;

            SerializedProperty primaryProp = selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(primaryIndex);
            primaryProp.managedReferenceValue = EditorSceneController.Instance.SelectedBlockEditor.Data;

            if (unifiedCellPositions != null)
            {
                foreach (var cellPos in unifiedCellPositions)
                {
                    if (cellPos == position) continue;

                    int cellIndex = selectedLevelRepresentation.GetIndex(cellPos.x, cellPos.y);
                    if (cellIndex == -1) continue;

                    SerializedProperty cellProp = selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(cellIndex);
                    CopyGateProperties(primaryProp, cellProp);
                }
            }

            selectedLevelRepresentation.ApplyChanges();
            needUpdateLevelPreview = true;
            gateSelectPosition = position;
            needToSelectGate = true;
            Repaint();
        }

        private void CopyGateProperties(SerializedProperty source, SerializedProperty dest)
        {
            LevelElementData srcData = source.managedReferenceValue as LevelElementData;
            if (srcData == null) return;
            LevelElementData copy = srcData.Clone();
            copy.SetPosition(dest.FindPropertyRelative(LevelAssetRepresentation.POSITION_PROPERTY_NAME).vector2IntValue);
            dest.managedReferenceValue = copy;
        }

        public void HandleCreateGeneratorEditor(Vector2Int position)
        {
            IsBlockSelected = false;

            if (generatorSceneInspectorPosition == position &&
                EditorSceneController.Instance.GeneratorHandlesEditor != null)
            {
                return;
            }

            generatorSceneInspectorPosition = position;

            if (generatorEditorInspector)
            {
                DestroyImmediate(generatorEditorInspector);
            }

            int index = selectedLevelRepresentation.GetIndex(position.x, position.y);
            if (index == -1) return;

            SerializedProperty generatorProp = selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(index);
            EditorSceneController.Instance.SelectedBlockEditor.Data = generatorProp.managedReferenceValue as LevelElementData;
            generatorEditorInspector = (MonoBehaviorInspector)Editor.CreateEditor(
                EditorSceneController.Instance.SelectedBlockEditor, typeof(MonoBehaviorInspector));
            generatorEditorInspector.SetScriptFieldState(false);
            EditorSceneController.Instance.GeneratorHandlesEditor = generatorEditorInspector;
        }

        public void HandleUpdateGeneratorData(Vector2Int position)
        {
            int index = selectedLevelRepresentation.GetIndex(position.x, position.y);
            if (index == -1) return;

            SerializedProperty generatorProp = selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(index);
            generatorProp.managedReferenceValue = EditorSceneController.Instance.SelectedBlockEditor.Data;

            selectedLevelRepresentation.ApplyChanges();
            RequestDebouncedLevelPreview();
            generatorSelectPosition = position;
            needToSelectGenerator = true;
            Repaint();
        }

        public void HandleGeneratorMove(Vector2Int oldPosition, Vector2Int newPosition)
        {
            if (oldPosition == newPosition)
            {
                return;
            }

            int oldIndex = selectedLevelRepresentation.GetIndex(oldPosition.x, oldPosition.y);
            int newIndex = selectedLevelRepresentation.GetIndex(newPosition.x, newPosition.y);
            if (oldIndex == -1 || newIndex == -1)
            {
                Debug.LogWarning("Generator move: old/new position outside level bounds.");
                needUpdateLevelPreview = true;
                Repaint();
                return;
            }

            // Reuse the same placement rules as painting a generator.
            if (!selectedLevelRepresentation.ValidateGeneratorCell(selectedLevelRepresentation, newPosition.x, newPosition.y))
            {
                needUpdateLevelPreview = true;
                Repaint();
                return;
            }

            SerializedProperty oldProp = selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(oldIndex);
            SerializedProperty newProp = selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(newIndex);

            ElementType oldType = LevelAssetRepresentation.GetElementType(oldProp);
            if (oldType != ElementType.Generator)
            {
                Debug.LogWarning($"Generator move: old position {oldPosition} is not a Generator (type={oldType}).");
                needUpdateLevelPreview = true;
                Repaint();
                return;
            }

            ElementType newType = LevelAssetRepresentation.GetElementType(newProp);
            if (newType != ElementType.Border)
            {
                Debug.LogWarning(
                    $"Generator move: target position {newPosition} is not a Border (type={newType}). Can only move Generator onto Border.");
                needUpdateLevelPreview = true;
                Repaint();
                return;
            }

            LevelElementData src = oldProp.managedReferenceValue as LevelElementData;
            if (src == null)
            {
                Debug.LogWarning("Generator move: source data is null.");
                needUpdateLevelPreview = true;
                Repaint();
                return;
            }

            LevelElementData moved = src.Clone();
            moved.SetPosition(newPosition);
            newProp.managedReferenceValue = moved;

            var clearedBorder = new BorderLevelElementData();
            clearedBorder.SetPosition(oldPosition);
            oldProp.managedReferenceValue = clearedBorder;

            IsBlockSelected = false;
            selectedLevelRepresentation.ApplyChanges();
            needUpdateLevelPreview = true;
            generatorSelectPosition = newPosition;
            needToSelectGenerator = true;
            Repaint();
        }

        public void HandleCreateInteractableEditor(Vector2Int position)
        {
            IsBlockSelected = false;

            if (interactableSceneInspectorPosition == position &&
                EditorSceneController.Instance.InteractableHandlesEditor != null)
            {
                return;
            }

            interactableSceneInspectorPosition = position;

            if (interactableEditorInspector)
            {
                DestroyImmediate(interactableEditorInspector);
            }

            int index = selectedLevelRepresentation.GetIndex(position.x, position.y);
            if (index == -1) return;

            SerializedProperty interactableProp = selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(index);
            EditorSceneController.Instance.SelectedBlockEditor.Data = interactableProp.managedReferenceValue as LevelElementData;
            interactableEditorInspector = (MonoBehaviorInspector)Editor.CreateEditor(
                EditorSceneController.Instance.SelectedBlockEditor, typeof(MonoBehaviorInspector));
            interactableEditorInspector.SetScriptFieldState(false);
            EditorSceneController.Instance.InteractableHandlesEditor = interactableEditorInspector;
        }

        public void HandleUpdateInteractableData(Vector2Int position)
        {
            int index = selectedLevelRepresentation.GetIndex(position.x, position.y);
            if (index == -1) return;

            SerializedProperty interactableProp = selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(index);
            interactableProp.managedReferenceValue = EditorSceneController.Instance.SelectedBlockEditor.Data;

            selectedLevelRepresentation.ApplyChanges();
            RequestDebouncedLevelPreview();
            interactableSelectPosition = position;
            needToSelectInteractable = true;
            Repaint();
        }

        public void HandleInteractableMove(Vector2Int oldPosition, Vector2Int newPosition)
        {
            CopyInteractableToCell(oldPosition, newPosition, clearSource: true, logLabel: "move");
        }

        public void HandleInteractableDuplicate(Vector2Int sourcePosition, Vector2Int newPosition)
        {
            CopyInteractableToCell(sourcePosition, newPosition, clearSource: false, logLabel: "duplicate");
        }

        /// <summary>
        /// Writes a clone of the interactable at <paramref name="oldPosition"/> into <paramref name="newPosition"/>,
        /// optionally clearing the source cell (move vs duplicate). Cloning the polymorphic
        /// <see cref="LevelElementData"/> keeps this type-agnostic: new <see cref="InteractableObjectType"/> values
        /// carry over without touching this code.
        /// </summary>
        private void CopyInteractableToCell(Vector2Int oldPosition, Vector2Int newPosition, bool clearSource,
            string logLabel)
        {
            if (oldPosition == newPosition)
            {
                return;
            }

            int oldIndex = selectedLevelRepresentation.GetIndex(oldPosition.x, oldPosition.y);
            int newIndex = selectedLevelRepresentation.GetIndex(newPosition.x, newPosition.y);
            if (oldIndex == -1 || newIndex == -1)
            {
                Debug.LogWarning($"Interactable {logLabel}: old/new position outside level bounds.");
                needUpdateLevelPreview = true;
                Repaint();
                return;
            }

            SerializedProperty oldProp = selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(oldIndex);
            SerializedProperty newProp = selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(newIndex);

            ElementType oldType = LevelAssetRepresentation.GetElementType(oldProp);
            if (oldType != ElementType.InteractableObject)
            {
                Debug.LogWarning($"Interactable {logLabel}: old position {oldPosition} is not an InteractableObject (type={oldType}).");
                needUpdateLevelPreview = true;
                Repaint();
                return;
            }

            // Interactable objects sit on InnerTile cells, so the target cell must be a free InnerTile.
            ElementType newType = LevelAssetRepresentation.GetElementType(newProp);
            if (newType != ElementType.InnerTile)
            {
                Debug.LogWarning(
                    $"Interactable {logLabel}: target position {newPosition} is not an InnerTile (type={newType}). Can only place onto InnerTile.");
                needUpdateLevelPreview = true;
                Repaint();
                return;
            }

            LevelElementData src = oldProp.managedReferenceValue as LevelElementData;
            if (src == null)
            {
                Debug.LogWarning($"Interactable {logLabel}: source data is null.");
                needUpdateLevelPreview = true;
                Repaint();
                return;
            }

            LevelElementData copy = src.Clone();
            copy.SetPosition(newPosition);
            newProp.managedReferenceValue = copy;

            if (clearSource)
            {
                var clearedInnerTile = new InnerTileLevelElementData();
                clearedInnerTile.SetPosition(oldPosition);
                oldProp.managedReferenceValue = clearedInnerTile;
            }

            IsBlockSelected = false;
            selectedLevelRepresentation.ApplyChanges();
            needUpdateLevelPreview = true;
            interactableSelectPosition = newPosition;
            needToSelectInteractable = true;
            Repaint();
        }

        private void DrawLevel()
        {
            // Keep SerializedProperty in sync with LevelData (e.g. after scene moves / AssignElementIds) so grid
            // and block color draws do not show cached blockColor until the next unrelated UI event.
            if (selectedLevelRepresentation != null && !selectedLevelRepresentation.NullLevel)
            {
                selectedLevelRepresentation.RefreshSerializedObject();
            }

            Vector2Int gridSize = selectedLevelRepresentation.sizeProperty.vector2IntValue;
            drawLevelGridSize = gridSize;

            drawRect = EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            xSize = Mathf.Floor(drawRect.width / gridSize.x);
            ySize = Mathf.Floor(drawRect.height / gridSize.y);
            elementSize = Mathf.Max(minGridCellSize, Mathf.Min(xSize, ySize));
            GUILayout.Space(elementSize * gridSize.y);
            currentEvent = Event.current;
            CellTypesHandler.CellType cellType;
            CellTypesHandler.CellType gateType;
            CellTypesHandler.ExtraProp extraProp;

            if (currentEvent.type == EventType.MouseUp)
            {
                if (positions.Count != 0)
                {
                    positions.Clear();
                }

                isPaintingLevelGrid = false;
            }

            //handle drag
            if ((!IsBlockSelected) && (cellTypeHandler.selectedCellTypeValue != CellTypesHandler.NO_SELECTION) &&
                (currentEvent.type == EventType.MouseDrag) && (currentEvent.button == 0) &&
                hasFocus && (FigureSelectorWindow.window == null))
            {
                elementUnderMouseIndex = (currentEvent.mousePosition - drawRect.position) / (elementSize);
                elementPosition = new Vector2Int(Mathf.FloorToInt(elementUnderMouseIndex.x),
                    gridSize.y - 1 - Mathf.FloorToInt(elementUnderMouseIndex.y));

                if ((elementPosition.x >= 0) &&
                    (elementPosition.x < gridSize.x) &&
                    (elementPosition.y >= 0) &&
                    (elementPosition.y < gridSize.y) &&
                    (!positions.Contains(elementPosition)))
                {
                    if (!isPaintingLevelGrid)
                    {
                        BeginLevelUndo("Paint Level Cells");
                        isPaintingLevelGrid = true;
                    }

                    positions.Add(elementPosition);
                    selectedLevelRepresentation.SetItemsValue(elementPosition.x, elementPosition.y,
                        cellTypeHandler.selectedCellTypeValue,
                        Mathf.Clamp(cellColorHandler.selectedCellTypeValue, 0, int.MaxValue),
                        drawBorderAsExtendable, selectedInteractableType);
                    needUpdateLevelPreview = true;
                    Repaint();
                }
            }

            //Handle  click
            if ((currentEvent.type == EventType.MouseDown) && (hasFocus) && (FigureSelectorWindow.window == null))
            {
                elementUnderMouseIndex = (currentEvent.mousePosition - drawRect.position) / (elementSize);

                elementPosition = new Vector2Int(Mathf.FloorToInt(elementUnderMouseIndex.x),
                    gridSize.y - 1 - Mathf.FloorToInt(elementUnderMouseIndex.y));

                if ((elementPosition.x >= 0) &&
                    (elementPosition.x < gridSize.x) &&
                    (elementPosition.y >= 0) &&
                    (elementPosition.y < gridSize.y))
                {
                    if ((!IsBlockSelected) && (currentEvent.button == 0) &&
                        (cellTypeHandler.selectedCellTypeValue != CellTypesHandler.NO_SELECTION))
                    {
                        if (!isPaintingLevelGrid)
                        {
                            BeginLevelUndo("Paint Level Cell");
                            isPaintingLevelGrid = true;
                        }

                        selectedLevelRepresentation.SetItemsValue(elementPosition.x, elementPosition.y,
                            cellTypeHandler.selectedCellTypeValue, cellColorHandler.selectedCellTypeValue,
                            drawBorderAsExtendable, selectedInteractableType);
                        positions.Add(elementPosition);
                        currentEvent.Use();
                        needUpdateLevelPreview = true;
                    }
                    else if ((currentEvent.button == 1) && (currentEvent.type == EventType.MouseDown))
                    {
                        int index = selectedLevelRepresentation.GetIndex(elementPosition.x, elementPosition.y);

                        // A cell missing from the layer (out-of-sync grid data) has nothing to select; it is
                        // treated as an empty cell and recovered by painting it. See RepairGridPositions.
                        tempCellProperty = index < 0
                            ? null
                            : selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(index);

                        ElementType tempCellType = LevelAssetRepresentation.GetElementType(tempCellProperty);
                        if ((tempCellType == ElementType.Block)
                            || (tempCellType == ElementType.Gate)
                            || (tempCellType == ElementType.Border)
                            || (tempCellType == ElementType.InteractableObject)
                            || (tempCellType == ElementType.Generator))
                        {
                            if (IsBlockSelected && (selectedBlockPosition.x == elementPosition.x) &&
                                (selectedBlockPosition.y == elementPosition.y))
                            {
                                IsBlockSelected = false;
                            }
                            else
                            {
                                selectedBlockPosition = new Vector2Int(elementPosition.x, elementPosition.y);
                                selectedBlockProperty =
                                    selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(index);
                                IsBlockSelected = true;
                                isSelectedBlockGate = (tempCellType == ElementType.Gate);

                                if (isSelectedBlockGate)
                                {
                                    CollectNearbyGates();
                                }

                                selectedBlockLabel =
                                    $"{GetSelectedElementLabel(tempCellProperty)} #{selectedBlockProperty.GetPropertyArrayIndex()} {selectedBlockPosition.ToString()}";
                            }
                        }
                        else // reset selection
                        {
                            IsBlockSelected = false;
                            selectedBlockPosition =
                                new Vector2Int(elementPosition.x,
                                    elementPosition.y); // we memorize block position anyway
                        }

                        Repaint();
                    }
                }
            }

            //draw
            for (int i = 0; i < selectedLevelRepresentation.itemsProperty.arraySize; i++)
            {
                tempCellProperty = selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(i);
                SerializedProperty cellPositionProperty =
                    tempCellProperty.FindPropertyRelative(LevelAssetRepresentation.POSITION_PROPERTY_NAME);
                if (cellPositionProperty == null)
                    continue;

                tempCellPosition = cellPositionProperty.vector2IntValue;
                cellType = cellTypeHandler.GetCellType((int)LevelAssetRepresentation.GetElementType(tempCellProperty));
                buttonRect = GetPositionRect(tempCellPosition);

                if (cellType.value == (int)ElementType.Gate)
                {
                    SerializedProperty gateDataArray = tempCellProperty.FindPropertyRelative(LevelAssetRepresentation.GATE_DATA_PROPERTY_NAME);
                    GeneratorPlacementRules.TryGetEdgeGateDirection(tempCellPosition, gridSize, out GateDirection.Type gateDir);
                    DrawGateBackground(buttonRect, gateDataArray, gateDir);
                }
                else if (cellType.value == (int)ElementType.Block)
                {
                    gateType = cellColorHandler.GetCellType(Mathf.Clamp(
                        tempCellProperty.FindPropertyRelative(LevelAssetRepresentation.BLOCK_COLOR_PROPERTY_NAME).intValue,
                        0, int.MaxValue));
                    DrawColorRect(buttonRect, gateType.color);
                }
                else if (cellType.value == (int)ElementType.InteractableObject)
                {
                    SerializedProperty ioData = tempCellProperty.FindPropertyRelative(
                        LevelAssetRepresentation.INTERACTABLE_OBJECT_DATA_PROPERTY_NAME);
                    int ioType = ioData.FindPropertyRelative(LevelAssetRepresentation.TYPE_PROPERTY_NAME).intValue;
                    if (InteractableObjectData.UsesObstacleColor((InteractableObjectType)ioType))
                    {
                        gateType = cellColorHandler.GetCellType(Mathf.Clamp(
                            ioData.FindPropertyRelative(LevelAssetRepresentation.INTERACTABLE_OBSTACLE_COLOR_PROPERTY_NAME)
                                .intValue,
                            0, int.MaxValue));
                        DrawColorRect(buttonRect, gateType != null ? gateType.color : cellType.color);
                    }
                    else if (ioType == (int)InteractableObjectType.Grinder)
                    {
                        DrawGrinderCell(buttonRect, ioData);
                    }
                    else
                    {
                        DrawColorRect(buttonRect, cellType.color);
                    }
                }
                else
                {
                    DrawColorRect(buttonRect, cellType.color);
                }

                tempTexture = GetCachedCellTypeTexture(cellType.value);

                if (tempTexture && cellType.value != (int)ElementType.Block)
                {
                    textureRect = new Rect(buttonRect);
                    textureRect.xMin += 2f;
                    textureRect.yMin += 2f;
                    GUI.DrawTexture(textureRect, tempTexture);
                }

                if (cellType.value == (int)ElementType.Generator)
                {
                    SerializedProperty generatorQueueProp =
                        tempCellProperty.FindPropertyRelative(LevelAssetRepresentation.GENERATOR_QUEUE_PROPERTY_NAME);
                    int queueCount = generatorQueueProp != null ? generatorQueueProp.arraySize : 0;
                    float queueStripHeight = Mathf.Clamp(buttonRect.height * 0.32f, 12f, 24f);
                    Rect queueCountRect = new Rect(buttonRect.x, buttonRect.yMax - queueStripHeight, buttonRect.width,
                        queueStripHeight);
                    GUI.Label(queueCountRect, queueCount.ToString(), generatorQueueLabelStyle);
                }

                if (cellType.value == (int)ElementType.Border &&
                    tempCellProperty.FindPropertyRelative(LevelAssetRepresentation.IS_EXTENDABLE_PROPERTY_NAME).boolValue)
                {
                    Color markerColor = new Color(0.2f, 1f, 0.2f, 0.95f);
                    float plusThickness = 5f;
                    float plusSize = Mathf.Min(buttonRect.width, buttonRect.height) * 0.4f;
                    Vector2 markerCenter = buttonRect.center;

                    Rect horizontalRect = new Rect(
                        markerCenter.x - plusSize * 0.5f,
                        markerCenter.y - plusThickness * 0.5f,
                        plusSize,
                        plusThickness);
                    Rect verticalRect = new Rect(
                        markerCenter.x - plusThickness * 0.5f,
                        markerCenter.y - plusSize * 0.5f,
                        plusThickness,
                        plusSize);

                    GUI.DrawTexture(horizontalRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0,
                        markerColor, 0f, 0f);
                    GUI.DrawTexture(verticalRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0,
                        markerColor, 0f, 0f);
                }
            }

            // After the per-element pass so the derived tape footprint paints over the plain cells it covers.
            DrawGrinderTapePreviews();

            figuresDictionary.Clear();

            //Second draw for block data
            Handles.BeginGUI();
            Handles.color = Color.white;
            for (int i = 0; i < selectedLevelRepresentation.itemsProperty.arraySize; i++)
            {
                tempCellProperty = selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(i);
                cellType = cellTypeHandler.GetCellType((int)LevelAssetRepresentation.GetElementType(tempCellProperty));

                if (cellType.value != (int)ElementType.Block)
                {
                    continue;
                }

                tempCellPosition = tempCellProperty.FindPropertyRelative(LevelAssetRepresentation.POSITION_PROPERTY_NAME)
                    .vector2IntValue;
                extraProp = cellTypeHandler.GetExtraProp(tempCellProperty
                    .FindPropertyRelative(LevelAssetRepresentation.BLOCK_TYPE_PROPERTY_NAME).intValue);
                gateType = cellColorHandler.GetCellType(Mathf.Clamp(
                    tempCellProperty.FindPropertyRelative(LevelAssetRepresentation.BLOCK_COLOR_PROPERTY_NAME).intValue, 0,
                    int.MaxValue));
                buttonRect = GetPositionRect(tempCellPosition);

                if (tempCellProperty.FindPropertyRelative(LevelAssetRepresentation.BLOCK_EFFECTS_PROPERTY_NAME).arraySize >
                    0)
                {
                    tempDrawBlockEffectLabel = true;
                    tempBlockEffectLabel = selectedLevelRepresentation.GetBlockEffectLabel(
                        tempCellProperty.FindPropertyRelative(LevelAssetRepresentation.BLOCK_EFFECTS_PROPERTY_NAME));
                }
                else
                {
                    tempDrawBlockEffectLabel = false;
                }

                tempBlockId = tempCellProperty.FindPropertyRelative(LevelAssetRepresentation.BLOCK_ID_PROPERTY_NAME).intValue;

                if (IsBlockSelected && (tempCellPosition == selectedBlockPosition) &&
                    BlockFigureGeometryCache.HasCachedFigure((BlockType)extraProp.value))
                {
                    GUI.DrawTexture(buttonRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, Color.white,
                        8, 0);
                    //draw circle for pivot
                    DrawCircle(buttonRect, 0.7f, Color.red);

                    if (tempDrawBlockEffectLabel)
                    {
                        GUI.Label(buttonRect, tempBlockEffectLabel, cellTypeHandler.GetLabelStyle(gateType.color));
                    }

                    // Draw block id
                    if (tempBlockId != 0)
                    {
                        GUI.Label(buttonRect, tempBlockId.ToString(), blockIdLabelStyle);
                    }

                    //draw neigbours
                    Vector2Int[] figureOffsets =
                        BlockFigureGeometryCache.GetOffsetsRelativeToPivot((BlockType)extraProp.value);
                    for (int j = 0; j < figureOffsets.Length; j++)
                    {
                        elementPosition = tempCellPosition + figureOffsets[j];

                        if ((elementPosition.x >= 0) &&
                            (elementPosition.x < gridSize.x) &&
                            (elementPosition.y >= 0) && (elementPosition.y < gridSize.y))
                        {
                            Rect tempElementRect = GetPositionRect(elementPosition);
                            DrawColorRect(tempElementRect, gateType.color);

                            if (tempDrawBlockEffectLabel)
                            {
                                GUI.Label(tempElementRect, tempBlockEffectLabel,
                                    cellTypeHandler.GetLabelStyle(gateType.color));
                            }

                            if (!figuresDictionary.TryAdd(elementPosition, 1))
                            {
                                Debug.LogError($"Figures are overlapping at {elementPosition.ToString()}.Check figure at : {tempCellPosition.ToString()}");
                                figuresDictionary[elementPosition]++;
                                DrawCircle(tempElementRect, 0.5f, Color.red, true);
                                GUI.Label(tempElementRect, figuresDictionary[elementPosition].ToString(),
                                    cellTypeHandler.GetLabelStyle(gateType.color));
                            }

                            //select all part of figure
                            GUI.DrawTexture(tempElementRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0,
                                Color.white, 8, 0);

                            Handles.DrawLine(tempElementRect.center, buttonRect.center);
                        }
                        else
                        {
                            Debug.LogWarning("Figure outside level bounds check figure at :" +
                                             tempCellPosition.ToString());
                        }
                    }
                }
                else
                {
                    //draw circle for pivot
                    DrawCircle(buttonRect, 0.7f, Color.red);

                    if (tempDrawBlockEffectLabel)
                    {
                        GUI.Label(buttonRect, tempBlockEffectLabel, cellTypeHandler.GetLabelStyle(gateType.color));
                    }

                    // Draw block id
                    if (tempBlockId != 0)
                    {
                        GUI.Label(buttonRect, tempBlockId.ToString(), blockIdLabelStyle);
                    }

                    //draw neigbours
                    if (BlockFigureGeometryCache.HasCachedFigure((BlockType)extraProp.value))
                    {
                        if (!figuresDictionary.TryAdd(tempCellPosition, 1))
                        {
                            Rect tempElementRect = GetPositionRect(tempCellPosition);
                            Debug.LogError($"Figures are overlapping at {tempCellPosition.ToString()}.");
                            figuresDictionary[tempCellPosition]++;
                            DrawCircle(tempElementRect, 0.5f, Color.red, true);
                            GUI.Label(tempElementRect, figuresDictionary[tempCellPosition].ToString(),
                                cellTypeHandler.GetLabelStyle(gateType.color));
                        }

                        Vector2Int[] figureOffsets =
                            BlockFigureGeometryCache.GetOffsetsRelativeToPivot((BlockType)extraProp.value);
                        for (int j = 0; j < figureOffsets.Length; j++)
                        {
                            elementPosition = tempCellPosition + figureOffsets[j];

                            if ((elementPosition.x >= 0) &&
                                (elementPosition.x < gridSize.x) &&
                                (elementPosition.y >= 0) && (elementPosition.y < gridSize.y))
                            {
                                if (selectedBlockPosition == elementPosition) // we select figure
                                {
                                    selectedBlockProperty = tempCellProperty.Copy();
                                    selectedBlockPosition = tempCellPosition;
                                    IsBlockSelected = true;
                                    isSelectedBlockGate = false;
                                    selectedBlockLabel =
                                        $"{GetSelectedElementLabel(tempCellProperty)} #{selectedBlockProperty.GetPropertyArrayIndex()} {selectedBlockPosition.ToString()}";
                                }


                                Rect tempElementRect = GetPositionRect(elementPosition);
                                DrawColorRect(tempElementRect, gateType.color);

                                if (tempDrawBlockEffectLabel)
                                {
                                    GUI.Label(tempElementRect, tempBlockEffectLabel,
                                        cellTypeHandler.GetLabelStyle(gateType.color));
                                }

                                if (!figuresDictionary.TryAdd(elementPosition, 1))
                                {
                                    Debug.LogError($"Figures are overlapping at {elementPosition.ToString()}.Check figure at : {tempCellPosition.ToString()}");
                                    figuresDictionary[elementPosition]++;
                                    DrawCircle(tempElementRect, 0.5f, Color.red, true);
                                    GUI.Label(tempElementRect, figuresDictionary[elementPosition].ToString(),
                                        cellTypeHandler.GetLabelStyle(gateType.color));
                                }

                                Handles.DrawLine(tempElementRect.center, buttonRect.center);
                            }
                            else
                            {
                                Debug.LogWarning("Figure outside level bounds check figure at :" +
                                                 tempCellPosition.ToString());
                            }
                        }
                    }
                }
            }

            Handles.EndGUI();

            // Third draw for gate IDs
            for (int i = 0; i < selectedLevelRepresentation.itemsProperty.arraySize; i++)
            {
                tempCellProperty = selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(i);
                cellType = cellTypeHandler.GetCellType((int)LevelAssetRepresentation.GetElementType(tempCellProperty));

                if (cellType.value != (int)ElementType.Gate)
                    continue;

                tempCellPosition = tempCellProperty.FindPropertyRelative(LevelAssetRepresentation.POSITION_PROPERTY_NAME)
                    .vector2IntValue;
                tempBlockId = tempCellProperty.FindPropertyRelative(LevelAssetRepresentation.BLOCK_ID_PROPERTY_NAME).intValue;
                buttonRect = GetPositionRect(tempCellPosition);

                if (tempBlockId != 0)
                {
                    GUI.Label(buttonRect, tempBlockId.ToString(), blockIdLabelStyle);
                }
            }

            //draw grid
            for (int x = 0; x < gridSize.x + 1; x++)
            {
                lineRect = new Rect(drawRect.x + (x * elementSize), drawRect.y, 2, gridSize.y * elementSize);
                DrawColorRect(lineRect, GRID_COLOR);
            }

            for (int y = 0; y < gridSize.y + 1; y++)
            {
                lineRect = new Rect(drawRect.x, drawRect.y + (y * elementSize), gridSize.x * elementSize, 2);
                DrawColorRect(lineRect, GRID_COLOR);
            }

            EditorGUILayout.Space();
            EditorGUILayout.EndVertical();
        }

        private void CollectNearbyGates()
        {
            selectedGateNeighbours.Clear();

            if (selectedBlockProperty == null)
                return;

            Vector2Int tempPosition = selectedBlockPosition;
            int colorValue = LevelAssetRepresentation.GetPrimaryColorValue(selectedBlockProperty);
            Vector2Int[] directions = { Vector2Int.left, Vector2Int.up, Vector2Int.down, Vector2Int.right };
            int directionIndex = 0;
            SerializedProperty blockProperty;

            while (directionIndex < directions.Length)
            {
                tempPosition += directions[directionIndex];

                if ((tempPosition.x < 0) ||
                    (tempPosition.x >= selectedLevelRepresentation.sizeProperty.vector2IntValue.x) ||
                    (tempPosition.y < 0) ||
                    (tempPosition.y >= selectedLevelRepresentation.sizeProperty.vector2IntValue.y))
                {
                    tempPosition = selectedBlockPosition;
                    directionIndex++;
                    continue;
                }

                blockProperty =
                    selectedLevelRepresentation.itemsProperty.GetArrayElementAtIndex(
                        selectedLevelRepresentation.GetIndex(tempPosition.x, tempPosition.y));

                if ((LevelAssetRepresentation.GetPrimaryColorValue(blockProperty) != colorValue) ||
                    (LevelAssetRepresentation.GetElementType(blockProperty) != ElementType.Gate))
                {
                    tempPosition = selectedBlockPosition;
                    directionIndex++;
                }
                else
                {
                    selectedGateNeighbours.Add(blockProperty.Copy());
                }
            }
        }

        private Rect GetPositionRect(Vector2Int position)
        {
            invertedY = drawLevelGridSize.y - 1 - position.y;

            buttonRectX = drawRect.position.x + position.x * elementSize;
            buttonRectY = drawRect.position.y + invertedY * elementSize;

            return new Rect(buttonRectX, buttonRectY, elementSize, elementSize);
        }

        private void DrawCircle(Rect parent, float radius, Color color, bool drawFullCircle = false)
        {
            textureRect = new Rect(parent);

            float size = parent.width * radius;

            float x = parent.x + (buttonRect.width - size) / 2f;
            float y = parent.y + (buttonRect.height - size) / 2f;

            textureRect = new Rect(x, y, size, size);

            if (drawFullCircle)
            {
                GUI.DrawTexture(textureRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, size, 30);
            }
            else
            {
                GUI.DrawTexture(textureRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 4, 30);
            }
        }

        private string GetSelectedElementLabel(SerializedProperty elementProperty)
        {
            ElementType type = LevelAssetRepresentation.GetElementType(elementProperty);
            if (type == ElementType.Gate)
                return "Gate";
            if (type == ElementType.Border)
                return "Border";
            if (type == ElementType.Generator)
                return "Generator";

            return "Block";
        }

        private static readonly Color GRINDER_MACHINE_COLOR = new Color(0.16f, 0.72f, 0.70f, 1f);
        private static readonly Color GRINDER_TAPE_COLOR = new Color(0.42f, 0.88f, 0.86f, 0.55f);
        private static readonly Color GRINDER_TAPE_INVALID_COLOR = new Color(0.95f, 0.25f, 0.25f, 0.6f);

        /// <summary>The machine cell shows its tape length per side, e.g. "3|3" or "0|5".</summary>
        private void DrawGrinderCell(Rect rect, SerializedProperty interactableData)
        {
            GrinderLayout layout = GrinderLayout.From(interactableData
                .FindPropertyRelative(LevelAssetRepresentation.INTERACTABLE_GRINDER_CONFIG_PROPERTY_NAME)
                .vector3IntValue);

            DrawColorRect(rect, GRINDER_MACHINE_COLOR);

            float labelHeight = Mathf.Clamp(rect.height * 0.32f, 12f, 24f);
            Rect labelRect = new Rect(rect.x, rect.yMax - labelHeight, rect.width, labelHeight);
            GUI.Label(labelRect, $"{layout.NegativeLength}|{layout.PositiveLength}", generatorQueueLabelStyle);
        }

        /// <summary>
        /// Tape cells are not authored — they are derived from the machine's config — so the grid has no
        /// element to colour for them. Overlay the derived footprint after the main cell pass so a
        /// designer sees the real reach while typing the counts; cells that fall outside the grid or on
        /// anything other than an InnerTile are flagged red (the same cases <see cref="GrinderShapeRule"/> errors on).
        /// </summary>
        private void DrawGrinderTapePreviews()
        {
            SerializedProperty items = selectedLevelRepresentation.itemsProperty;
            grinderCoreCellsBuffer.Clear();
            grinderElementTypesBuffer.Clear();

            for (int i = 0; i < items.arraySize; i++)
            {
                SerializedProperty element = items.GetArrayElementAtIndex(i);
                Vector2Int cell = element
                    .FindPropertyRelative(LevelAssetRepresentation.POSITION_PROPERTY_NAME).vector2IntValue;
                ElementType elementType = LevelAssetRepresentation.GetElementType(element);
                grinderElementTypesBuffer[cell] = elementType;

                if (elementType != ElementType.InteractableObject)
                    continue;

                SerializedProperty interactableData = element.FindPropertyRelative(
                    LevelAssetRepresentation.INTERACTABLE_OBJECT_DATA_PROPERTY_NAME);
                if (interactableData == null)
                    continue;

                if (interactableData.FindPropertyRelative(LevelAssetRepresentation.TYPE_PROPERTY_NAME).intValue
                    != (int)InteractableObjectType.Grinder)
                    continue;

                GrinderLayout layout = GrinderLayout.From(interactableData
                    .FindPropertyRelative(LevelAssetRepresentation.INTERACTABLE_GRINDER_CONFIG_PROPERTY_NAME)
                    .vector3IntValue);
                if (layout.HasTape)
                    grinderCoreCellsBuffer.Add((cell, layout));
            }

            if (grinderCoreCellsBuffer.Count > 0)
            {
                grinderBlockCoveredCellsBuffer.Clear();
                GrinderShapeRule.CollectBlockCoveredCells(items, grinderBlockCoveredCellsBuffer);
            }

            for (int i = 0; i < grinderCoreCellsBuffer.Count; i++)
            {
                (Vector2Int core, GrinderLayout layout) = grinderCoreCellsBuffer[i];

                grinderTapeCellsBuffer.Clear();
                layout.AppendTapeCells(core, grinderTapeCellsBuffer);

                for (int c = 0; c < grinderTapeCellsBuffer.Count; c++)
                {
                    Vector2Int cell = grinderTapeCellsBuffer[c];

                    // Off-grid cells have no rect to paint; GrinderShapeRule reports them instead.
                    if (!grinderElementTypesBuffer.TryGetValue(cell, out ElementType cellType))
                        continue;

                    bool isCellFree = GrinderShapeRule.IsTapeCellAllowed(cellType)
                                      && !grinderBlockCoveredCellsBuffer.Contains(cell);

                    DrawColorRect(GetPositionRect(cell),
                        isCellFree ? GRINDER_TAPE_COLOR : GRINDER_TAPE_INVALID_COLOR);
                }
            }
        }

        private void DrawGateBackground(Rect rect, SerializedProperty gateDataArray, GateDirection.Type direction)
        {
            if (gateDataArray == null || gateDataArray.arraySize == 0)
            {
                DrawColorRect(rect, Color.gray);
                return;
            }

            int colorCount = gateDataArray.arraySize;

            if (colorCount == 1)
            {
                BlockColor color = (BlockColor)gateDataArray.GetArrayElementAtIndex(0).FindPropertyRelative("color").intValue;
                DrawColorRect(rect, cellColorHandler.GetCellType((int)color).color);
                return;
            }

            bool isVerticalSplit = (direction == GateDirection.Type.Top || direction == GateDirection.Type.Bottom);
            bool reverseOrder = (direction == GateDirection.Type.Top || direction == GateDirection.Type.Left);

            for (int i = 0; i < colorCount; i++)
            {
                int colorIndex = reverseOrder ? (colorCount - 1 - i) : i;
                BlockColor color = (BlockColor)gateDataArray.GetArrayElementAtIndex(colorIndex).FindPropertyRelative("color").intValue;
                Color drawColor = cellColorHandler.GetCellType((int)color).color;

                Rect colorRect;
                if (isVerticalSplit)
                {
                    float segmentHeight = rect.height / colorCount;
                    colorRect = new Rect(rect.x, rect.y + i * segmentHeight, rect.width, segmentHeight);
                }
                else
                {
                    float segmentWidth = rect.width / colorCount;
                    colorRect = new Rect(rect.x + i * segmentWidth, rect.y, segmentWidth, rect.height);
                }

                DrawColorRect(colorRect, drawColor);
            }
        }

        private void DrawCurrentLevelValidationErrors()
        {
            if (selectedLevelRepresentation == null || selectedLevelRepresentation.NullLevel)
                return;

            LevelValidationResult result =
                GetOrComputeLevelValidation(selectedLevelRepresentation.EditedLevelObject);
            if (result == null || result.IsValid)
                return;

            for (int i = 0; i < result.Errors.Count; i++)
                EditorGUILayout.HelpBox(result.Errors[i], MessageType.Error, true);
        }

        private void DrawTipsAndWarnings()
        {
            // Min height reserved for level-analysis breakdown; validation-only rows should not pad empty space.
            if (isShowLevelAnalysis)
                infoRect = EditorGUILayout.BeginVertical(GUILayout.MinHeight(INFO_HEIGH));
            else
                EditorGUILayout.BeginVertical();

            DrawCurrentLevelValidationErrors();

            int selectedIndexForTips = showSpecialLevelsList
                ? specialLevelsHandler != null && specialLevelsHandler.HasSelection ? 1 : 0
                : levelsHandler.SelectedLevelIndex;
            if (selectedIndexForTips <= 0)
            {
                EditorGUILayout.HelpBox(LEVEL_INSTRUCTION, MessageType.Info);
                EditorGUILayout.HelpBox(RIGHT_CLICK_INSTRUCTION, MessageType.Info);
            }

            if (!isShowLevelAnalysis)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            if (levelStatisticsDirty || cachedLevelStatistics == null)
            {
                LevelData levelDataForStats = selectedLevelRepresentation?.EditedLevelObject as LevelData;
                bool isLift = levelDataForStats != null && levelDataForStats.HasExtraLayer &&
                              levelDataForStats.ExtraLayerType == ExtraLayerType.Lift;

                SerializedProperty mainProperty = isLift
                    ? selectedLevelRepresentation?.BaseItemsProperty
                    : selectedLevelRepresentation?.itemsProperty;

                SerializedProperty extraLayerForStats = isLift
                    ? selectedLevelRepresentation?.extraLayerItemsProperty
                    : null;

                cachedLevelStatistics = LevelStatisticsCalculator.Calculate(mainProperty, extraLayerForStats);
                levelStatisticsDirty = false;
            }

            LevelStatistics levelStats = cachedLevelStatistics;
            if (levelStats.totalGateColors > 0)
            {
                // Display summary
                StringBuilder summary = stringBuilder;
                summary.Clear();
                summary.Append("Total Gate Water: ");
                summary.Append(levelStats.totalGateColors);
                summary.Append(" | Total Blocks: ");
                summary.Append(levelStats.totalBlockColors);
                summary.Append(" | Difference: ");
                summary.Append(levelStats.totalGateColors - levelStats.totalBlockColors);

                EditorGUILayout.HelpBox(summary.ToString(), MessageType.Info);

                // Display detailed color breakdown
                EditorGUILayout.BeginVertical(GUI.skin.box);

                foreach (var kvp in levelStats.gateColorCounts)
                {
                    if (kvp.Value > 0 || levelStats.blockColorCounts[kvp.Key] > 0)
                    {
                        EditorGUILayout.BeginHorizontal();

                        // Color name with color indicator
                        Color guiColor = cellColorHandler.GetCellType((int)kvp.Key).color;
                        GUI.color = guiColor;
                        EditorGUILayout.LabelField("■", GUILayout.Width(20));
                        GUI.color = Color.white;

                        Rect colorLabelRect = EditorGUILayout.GetControlRect(
                            false,
                            EditorGUIUtility.singleLineHeight,
                            GUILayout.Width(80));
                        DrawReplaceColorLabel(colorLabelRect, kvp.Key);

                        // Gate count
                        EditorGUILayout.LabelField("Gate: " + kvp.Value, GUILayout.Width(70));

                        // Block count
                        EditorGUILayout.LabelField("Block: " + levelStats.blockColorCounts[kvp.Key],
                            GUILayout.Width(70));

                        // Missing count
                        int missing = levelStats.missingColorCounts[kvp.Key];
                        if (missing > 0)
                        {
                            GUI.color = Color.red;
                            EditorGUILayout.LabelField("Need: +" + missing, EditorStyles.boldLabel);
                            GUI.color = Color.white;
                        }
                        else if (missing < 0)
                        {
                            GUI.color = Color.orange;
                            EditorGUILayout.LabelField("Extra: " + (-missing), EditorStyles.boldLabel);
                            GUI.color = Color.white;
                        }
                        else
                        {
                            GUI.color = Color.green;
                            EditorGUILayout.LabelField("✓ Balanced", EditorStyles.boldLabel);
                            GUI.color = Color.white;
                        }

                        EditorGUILayout.EndHorizontal();
                    }
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawReplaceColorLabel(Rect rect, BlockColor sourceColor)
        {
            LevelColorReplacePopup.DrawLabel(rect, sourceColor, GetEditorColor, ReplaceLevelColor);
        }

        private void ReplaceLevelColor(BlockColor sourceColor, BlockColor targetColor)
        {
            if (sourceColor == targetColor || selectedLevelRepresentation == null || selectedLevelRepresentation.NullLevel)
                return;

            selectedLevelRepresentation.RefreshSerializedObject();
            BeginLevelUndo($"Replace {sourceColor} Color");

            // Pass the base elements property so the replace scope (base + Lift extra layer) matches
            // LevelStatisticsCalculator exactly, regardless of which layer is currently being edited.
            int changedCount = LevelColorReplacementUtility.ReplaceLevelColor(
                selectedLevelRepresentation.BaseItemsProperty,
                sourceColor,
                targetColor);
            if (changedCount <= 0)
                return;

            selectedLevelRepresentation.ApplyChanges();
            selectedLevelRepresentation.RefreshSerializedObject();
            RequestDebouncedLevelPreview();
            Repaint();
        }

        private Color GetEditorColor(BlockColor blockColor)
        {
            CellTypesHandler.CellType cellType = cellColorHandler?.GetCellType((int)blockColor);
            return cellType != null ? cellType.color : Color.gray;
        }

        private List<ILevelValidationRule> GetOrCreateValidationRules()
        {
            if (cachedValidationRules != null)
                return cachedValidationRules;

            cachedValidationRules = LevelValidator.CreateDefaultRules();

            return cachedValidationRules;
        }

        private void ApplyMultiColumnLayoutToLevelList()
        {
            if (levelsHandler?.CustomList == null)
            {
                return;
            }

            levelsHandler.CustomList.enableMultiColumnLayout = enableMultiColumnLevelList;
            if (specialLevelsHandler?.CustomList != null)
                specialLevelsHandler.CustomList.enableMultiColumnLayout = enableMultiColumnLevelList;
        }

        private void DrawMainSpecialToggle()
        {
            EditorGUILayout.BeginHorizontal();
            bool mainSelected = !showSpecialLevelsList;
            if (GUILayout.Toggle(mainSelected, "Main", EditorStyles.toolbarButton) != mainSelected)
            {
                showSpecialLevelsList = false;
                EditorPrefs.SetBool(PREFS_SHOW_SPECIAL_LEVELS, false);
                ReopenSelectionForActiveList();
                SaveEditorSessionState();
            }

            bool specialSelected = showSpecialLevelsList;
            if (GUILayout.Toggle(specialSelected, "Special", EditorStyles.toolbarButton) != specialSelected)
            {
                showSpecialLevelsList = true;
                EditorPrefs.SetBool(PREFS_SHOW_SPECIAL_LEVELS, true);
                ReopenSelectionForActiveList();
                SaveEditorSessionState();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2f);
        }

        private void ReopenSelectionForActiveList()
        {
            editingVariantAsset = null;

            if (showSpecialLevelsList)
            {
                Object specialSelection = specialLevelsHandler != null ? specialLevelsHandler.SelectedLevelObject : null;
                if (specialSelection)
                    OpenLevel(specialSelection, SPECIAL_LEVEL_TEST_SLOT);

                return;
            }

            if (levelsHandler != null && levelsHandler.SelectedLevelIndex >= 0)
                levelsHandler.ReopenLevel();
        }

        private void DisplayEditorTab()
        {
            if (BeginSection(PREFS_SECTION_EDITOR_SETTINGS, "Editor Settings"))
                DrawEditorSettingsSection();
            EndSection();

            if (BeginSection(PREFS_SECTION_GENERAL_CONFIG, "Level General Config"))
                DrawLevelGeneralConfigSection();
            EndSection();

            if (BeginSection(PREFS_SECTION_SPECIAL_MODE, "Special Level Mode Config"))
                DrawSpecialLevelModeSection();
            EndSection();

            if (BeginSection(PREFS_SECTION_LEVEL_CONFIGURE, "Level Editor Configure"))
                DrawLevelEditorConfigureSection();
            EndSection();

            if (BeginSection(PREFS_SECTION_EFFECTS, "Effects"))
                DrawEffectsSection();
            EndSection();

            if (BeginSection(PREFS_SECTION_OTHER, "Other"))
                DrawOtherSection();
            EndSection();
        }

        /// <summary>
        /// Draws a persistent collapsible header for the Editor tab. Always pair with <see cref="EndSection"/>.
        /// Returns true when the section is expanded and its body should be drawn.
        /// </summary>
        /// <remarks>
        /// Uses a plain <see cref="EditorGUILayout.Foldout(bool, string, bool, GUIStyle)"/> rather than
        /// BeginFoldoutHeaderGroup: section bodies draw array/generic PropertyFields that open their own
        /// foldout header groups internally, and Unity forbids nesting those.
        /// </remarks>
        private bool BeginSection(string prefsKey, string label)
        {
            sectionHeaderStyle ??= new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold
            };

            bool expanded = EditorPrefs.GetBool(prefsKey, true);
            bool next = EditorGUILayout.Foldout(expanded, label, true, sectionHeaderStyle);
            if (next != expanded)
                EditorPrefs.SetBool(prefsKey, next);

            sectionBodyIndented = next;
            if (next)
                EditorGUI.indentLevel++;

            return next;
        }

        private void EndSection()
        {
            if (sectionBodyIndented)
                EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }

        private void DrawEditorSettingsSection()
        {
            EditorGUI.BeginChangeCheck();
            minGridCellSize =
                EditorGUILayout.IntField(new GUIContent("minGridCellSize", "Minimum size of cell in level grid"),
                    minGridCellSize);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetInt(PREFS_MIN_ELEMENT_SIZE, minGridCellSize);
            }

            EditorGUI.BeginChangeCheck();
            bool disablePickingOnLoad = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "Disable picking on level load",
                    "When enabled (default), the loaded level GameObject hierarchy is excluded from Scene view picking, so blocks/gates/generators can only be selected through the editor handles."),
                EditorSceneController.DisablePickingOnLoad);
            if (EditorGUI.EndChangeCheck())
            {
                EditorSceneController.DisablePickingOnLoad = disablePickingOnLoad;
                if (SceneManager.GetActiveScene().name == EDITOR_SCENE_NAME)
                {
                    EditorSceneController.Instance.ApplyPickingSetting();
                }
            }

            EditorGUI.BeginChangeCheck();
            enableMultiColumnLevelList = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "Multi-column level list",
                    "When enabled, the level list in the Levels tab uses multiple columns when the sidebar is wide enough."),
                enableMultiColumnLevelList);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(PREFS_MULTI_COLUMN_LEVEL_LIST, enableMultiColumnLevelList);
                ApplyMultiColumnLayoutToLevelList();
            }

            EditorGUI.BeginChangeCheck();
            showCellPos = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "Show cell positions",
                    "When enabled, spawned inner tiles will display their grid coordinates in the Scene view (editor-only)."),
                showCellPos);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(PREFS_SHOW_CELL_POS, showCellPos);
                ApplyCellPosVisibility(showCellPos);
            }
        }

        private void DrawLevelGeneralConfigSection()
        {
            if (levelGeneralConfigDataSerializedProperty != null)
            {
                EditorGUILayout.PropertyField(levelGeneralConfigDataSerializedProperty, true);
            }
            else
            {
                EditorGUILayout.HelpBox("Missing levelGeneralConfigData on LevelDatabase.", MessageType.Warning);
            }

            EditorGUILayout.Space();
            DrawDatabaseProperty("blockEffectCompatibility", "Block Effect Compatibility");
        }

        private void DrawSpecialLevelModeSection()
        {
            if (specialModeLimitsSerializedProperty != null)
            {
                EditorGUILayout.PropertyField(specialModeLimitsSerializedProperty, true);
            }
            else
            {
                EditorGUILayout.HelpBox("Missing specialModeLimits on LevelDatabase.", MessageType.Warning);
            }
        }

        private void DrawLevelEditorConfigureSection()
        {
            EditorGUILayout.LabelField("Cell types:", EditorCustomStyles.labelLargeBold);

            EditorGUI.BeginChangeCheck();
            for (int i = 0; i < cellsSerializedProperty.arraySize; i++)
            {
                EditorGUILayout.LabelField(cellTypeHandler.GetCellType(i).label, EditorCustomStyles.labelBold);
                EditorGUI.indentLevel++;

                SerializedProperty serializedProperty = cellsSerializedProperty.GetArrayElementAtIndex(i);

                SerializedProperty iterator = cellsSerializedProperty.GetArrayElementAtIndex(i).Copy();

                while (iterator.NextVisible(true) && iterator.propertyPath.Contains(serializedProperty.propertyPath))
                {
                    if (iterator.name != TYPE_PROPERTY_NAME)
                    {
                        EditorGUILayout.PropertyField(iterator);
                    }
                }

                EditorGUI.indentLevel--;
            }

            if (EditorGUI.EndChangeCheck())
                RebuildCellTypeTextureCache();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Gate colors:", EditorCustomStyles.labelLargeBold);

            for (int i = 0; i < editorColorsDataSerializedProperty.arraySize; i++)
            {
                EditorGUILayout.PropertyField(
                    editorColorsDataSerializedProperty.GetArrayElementAtIndex(i)
                        .FindPropertyRelative(COLOR_PROPERTY_NAME),
                    new GUIContent(cellColorHandler.GetCellType(i).label));
            }
        }

        private void DrawEffectsSection()
        {
            DrawStaleReferenceResolver();

            DrawDatabaseProperty("effects", "Block Effects");
            DrawDatabaseProperty("gateEffects", "Gate Effects");
            DrawDatabaseProperty("interactableObjects", "Interactable Objects");
        }

        private void DrawStaleReferenceResolver()
        {
            LevelDatabase database = levelsDatabase as LevelDatabase;

            if (!LevelDatabaseReferenceHealer.HasStaleReferences(database))
                return;

            EditorGUILayout.HelpBox(
                "Some prefab references were broken by an asset reload (typically a git branch switch that deleted and restored prefabs). The asset file on disk is still correct.",
                MessageType.Warning);

            if (GUILayout.Button("Resolve Broken Prefab References"))
            {
                LevelDatabaseReferenceHealer.Heal(database);
                levelsDatabaseSerializedObject.Update();
            }

            EditorGUILayout.Space();
        }

        private void DrawOtherSection()
        {
            // Catch-all: every unmarked LevelDatabase field not already shown in a dedicated section above
            // (e.g. Obstacle Unlock Database, Special Level Schedule Config, and any field added later).
            bool drewAny = false;
            foreach (SerializedProperty item in unmarkedProperties)
            {
                if (EditorTabExplicitProperties.Contains(item.name))
                    continue;

                EditorGUILayout.PropertyField(item, true);
                drewAny = true;
            }

            if (!drewAny)
                EditorGUILayout.HelpBox("No additional database fields.", MessageType.Info);
        }

        private void DrawDatabaseProperty(string propertyName, string label)
        {
            SerializedProperty property = levelsDatabaseSerializedObject.FindProperty(propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label), true);
            }
            else
            {
                EditorGUILayout.HelpBox($"Missing {propertyName} on LevelDatabase.", MessageType.Warning);
            }
        }

        public override void OnBeforeAssemblyReload()
        {
            lastActiveLevelOpened = false;
        }


        public override bool WindowClosedInPlaymode()
        {
            return false;
        }

        private void Update()
        {
            playModeInspector?.OnUpdate();
        }

        private void OnDestroy()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= UnloadEditor;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            AssetDatabase.SaveAssets();

            try
            {
                UnloadEditor();
            }
            catch
            {
            }

            // if (!EditorApplication.isPlayingOrWillChangePlaymode)
            // {
            //     OpenScene(GAME_SCENE_PATH);
            // }
        }

    }
}
