using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WaterFlow.Core;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    public class LevelsHandler
    {

        //strings
        public const string LEVEL_PREFIX = "Level ";
        private const string ASSET_SUFFIX = ".asset";
        private const string OLD_PREFIX = "Old ";
        private const string REMOVE_LEVEL = "Are you sure you want to remove ";
        private const string BRACKET = "\"";
        private const string QUESTION_MARK = "?";
        private const string REMOVING_LEVEL_TITLE = "Removing level";
        private const string YES = "Yes";
        private const string CANCEL = "Cancel";
        private const string FORMAT_TYPE = "000";
        private const string PATH_SEPARATOR = "/";
        private const string DEFAULT_LEVEL_LIST_HEADER = "Levels amount: ";
        private const string REMOVE_SELECTION = "Unselect";
        private const string RENAME_LEVEL_LABEL = "Rename Level in Index #{0}";
        private const string RENAME_LEVELS_LABEL = "Populate Levels";
        private const string GLOBAL_VALIDATION_LABEL = "Global Validation";
        private const string REMOVE_ELEMENT_CALLBACK = "Remove element";
        private const string ON_ENABLE_OVERRIDEN_ERROR = "LevelEditorBase.Instance == null. OnEnable() overriden without base.OnEnable() call";
        private const string SET_POSITION_LABEL = "Set position";
        private const string INDEX_CHANGE_WINDOW = "Index change window";
        private readonly Vector2 INDEX_CHANGE_WINDOW_SIZE = new Vector2(300, 64);

        #region delegates

        public delegate void AddElementCallbackDelegate();
        public delegate void RemoveElementCallbackDelegate();
        public delegate void DisplayContextMenuCallbackDelegate(GenericMenu genericMenu);
        public delegate void OnClearSelectionCallbackDelegate();
        public delegate void OnRenameAllCallbackDelegate();

        public AddElementCallbackDelegate addElementCallback;
        public RemoveElementCallbackDelegate removeElementCallback;
        public DisplayContextMenuCallbackDelegate displayContextMenuCallback;
        public OnClearSelectionCallbackDelegate onClearSelectionCallback;
        public OnRenameAllCallbackDelegate onRenameAllCallback;
        #endregion

        private List<string> levelLabels;
        private SerializedObject levelsDatabaseSerializedObject;
        private SerializedProperty levelsSerializedProperty;
        private CustomList customList;

        public int SelectedLevelIndex => customList.SelectedIndex;
        public SerializedProperty SelectedLevelProperty { get => levelsSerializedProperty.GetArrayElementAtIndex(SelectedLevelIndex); set => levelsSerializedProperty.GetArrayElementAtIndex(SelectedLevelIndex).objectReferenceValue = value.objectReferenceValue; }
        public bool IgnoreDragEvents { get => customList.IgnoreDragEvents; set => customList.IgnoreDragEvents = value; }
        public CustomList CustomList => customList;

        // Tag filter: while active, the list shows a read-only filtered view (selectable rows that map back
        // to the real array index) and structural edits are blocked to avoid index drift. selectedTagMask == 0
        // means "not filtering".
        private const string TAG_FILTER_ACTIVE_TITLE = "Tag filter active";
        private const string TAG_FILTER_ACTIVE_MESSAGE =
            "Clear the tag filter before adding, deleting, reordering, or populating levels.";
        private ulong selectedTagMask;
        private readonly List<int> filteredRealIndices = new List<int>();
        private Vector2 filteredScroll;
        private GUIStyle filteredRowStyle;
        private int pendingScrollToRealIndex = -1;
        private static readonly Color FilteredRowSelectedTint = new Color(0.30f, 0.55f, 0.92f, 1f);

        public bool IsTagFiltering => selectedTagMask != 0UL;
        public ulong SelectedTagMask => selectedTagMask;

        /// <summary>Sets the AND-match tag mask. Pass 0 (or call <see cref="ClearTagFilter"/>) to leave filter mode.</summary>
        public void ApplyTagFilter(ulong mask)
        {
            selectedTagMask = mask;
        }

        public void ClearTagFilter()
        {
            selectedTagMask = 0UL;
        }

        /// <summary>Drops a level's cached tag mask so the filtered view re-scans it after its content changed.</summary>
        public void InvalidateTagMask(Object levelObject)
        {
            LevelTagScanner.Invalidate(levelObject as LevelData);
        }

        public LevelsHandler(SerializedObject levelsDatabaseSerializedObject, SerializedProperty levelsSerializedProperty)
        {
            this.levelsDatabaseSerializedObject = levelsDatabaseSerializedObject;
            this.levelsSerializedProperty = levelsSerializedProperty;
            this.levelLabels = new List<string>();

            SetLevelLabels();
            SetCustomList();
        }

        #region Reordable list

        private void SetCustomList()
        {
            //customList = new CustomList(levelsDatabaseSerializedObject, levelsSerializedProperty);
            customList = new CustomList(levelsDatabaseSerializedObject, levelsSerializedProperty,  GetLabel);

            customList.EnableHeader(GetHeaderCallback);
            customList.selectionChangedCallback += SelectionChangedCallback;
            customList.listReorderedCallback += ListReorderedCallback;
            customList.addElementWithDropdownCallback += AddElementCallback;
            customList.removeElementCallback += RemoveElementCallback;
            customList.displayContextMenuCallback += DisplayContextMenuCallback;
            customList.ReorderConfirmationEnabled = true;
            customList.drawElementElementHeaderExtraCallback = DrawLevelHeaderExtra;
            // customList.AddCustomField(DrawLevelBodyIsEnableInBuild, GetLevelBodyIsEnableInBuildHeight);
            
            customList.enableJumpToIndexRow = true;
            customList.jumpToIndexFieldLabel = "Jump to Level";
            customList.jumpToIndexLabelWidth = 82f;
            customList.MinWidth = 140;
            customList.multiColumnMinElementWidth = 135f;
        }

        private void DrawLevelHeaderExtra(SerializedProperty elementProperty, int index, Rect rect)
        {
            // var levelData = elementProperty.objectReferenceValue as LevelData;
            // if (!levelData) return;
            // var so = new SerializedObject(levelData);
            // var prop = so.FindProperty("isEnableInBuild");
            // if (prop == null) return;
            // EditorGUI.BeginChangeCheck();
            // bool value = EditorGUI.Toggle(rect, GUIContent.none, prop.boolValue);
            // if (EditorGUI.EndChangeCheck())
            // {
            //     prop.boolValue = value;
            //     so.ApplyModifiedProperties();
            //     EditorUtility.SetDirty(levelData);
            // }
        }

        // private void DrawLevelBodyIsEnableInBuild(SerializedProperty elementProperty, Rect rect, CustomListStyle style)
        // {
        //     var levelData = elementProperty.objectReferenceValue as LevelData;
        //     if (levelData == null) return;
        //     var so = new SerializedObject(levelData);
        //     var prop = so.FindProperty("isEnableInBuild");
        //     if (prop == null) return;
        //     EditorGUI.BeginChangeCheck();
        //     EditorGUI.PropertyField(rect, prop, new GUIContent("Enable In Build"));
        //     if (EditorGUI.EndChangeCheck())
        //     {
        //         so.ApplyModifiedProperties();
        //         EditorUtility.SetDirty(levelData);
        //     }
        // }
        //
        // private float GetLevelBodyIsEnableInBuildHeight(SerializedProperty elementProperty, CustomListStyle style)
        // {
        //     return EditorGUIUtility.singleLineHeight;
        // }

        private string GetLabel(SerializedProperty elementProperty, int elementIndex)
        {
            if (elementIndex >= 0 && elementIndex < levelLabels.Count)
            {
                return levelLabels[elementIndex];
            }

            return BuildDisplayLabel(elementIndex);
        }

        private string BuildDisplayLabel(int index)
        {
            if (index < 0 || index >= levelsSerializedProperty.arraySize)
            {
                return string.Empty;
            }

            Object levelObject = levelsSerializedProperty.GetArrayElementAtIndex(index).objectReferenceValue;

            if (!levelObject || LevelEditorBase.Instance == null)
            {
                return LevelEditorBase.Instance != null
                    ? LevelEditorBase.Instance.GetLevelLabel(levelObject, index)
                    : LEVEL_PREFIX + (index + 1);
            }

            string validationSuffix = LevelEditorBase.Instance.GetLevelLabelSuffix(levelObject);
            if (!string.IsNullOrEmpty(validationSuffix))
            {
                return LevelEditorBase.Instance.FormatLevelListRowLabel(index, string.Empty, validationSuffix);
            }

            string label = LevelEditorBase.Instance.GetLevelLabel(levelObject, index);

            if (levelsDatabaseSerializedObject.targetObject is LevelDatabase flowDb &&
                levelObject is LevelData slotLevel)
            {
                LevelData resolvedBase = LevelDataUtilities.ResolveBaseForSlotAsset(flowDb, slotLevel);
                label += flowDb.Editor_GetVariantListSuffix(resolvedBase);
            }

            return label;
        }

        /// <summary>Rebuilds cached list row labels (variant suffix, validation suffix, etc.).</summary>
        public void RefreshDisplayLabels()
        {
            SetLevelLabels();
        }

        private void DisplayContextMenuCallback()
        {
            GenericMenu genericMenu = new GenericMenu();
            genericMenu.AddItem(new GUIContent(SET_POSITION_LABEL), false, OpenSetIndexModalWindow);
            genericMenu.AddItem(new GUIContent(REMOVE_SELECTION), false, ClearSelection);
            genericMenu.AddItem(new GUIContent(REMOVE_ELEMENT_CALLBACK), false, RemoveElementCallback);
            displayContextMenuCallback?.Invoke(genericMenu);
            genericMenu.ShowAsContext();
        }

        private void RemoveElementCallback()
        {
            DeleteLevel(SelectedLevelIndex);
            removeElementCallback?.Invoke();
        }

        private void AddElementCallback()
        {
            AddLevel();
            customList.ListChangedCallback();
            addElementCallback?.Invoke();
        }

        private void ListReorderedCallback()
        {
            SetLevelLabels();
        }

        private void SelectionChangedCallback()
        {
            OpenLevel(SelectedLevelIndex);
        }

        private string GetHeaderCallback()
        {
            return DEFAULT_LEVEL_LIST_HEADER + levelsSerializedProperty.arraySize;
        }

        public void ClearSelection()
        {
            customList.SelectedIndex = -1;
            onClearSelectionCallback?.Invoke();
        }

        public void UpdateCurrentLevelLabel(string label)
        {
            if (SelectedLevelIndex == -1)
            {
                return;
            }

            while (levelLabels.Count < levelsSerializedProperty.arraySize)
            {
                levelLabels.Add(string.Empty);
            }

            if (SelectedLevelIndex < levelLabels.Count)
            {
                levelLabels[SelectedLevelIndex] = BuildDisplayLabel(SelectedLevelIndex);
            }
        }

        public void DisplayReorderableList()
        {
            EditorGUILayout.BeginVertical();

            if (IsTagFiltering)
            {
                DrawFilteredList();
            }
            else
            {
                customList.DrawJumpToIndexRow();
                customList.Display();
            }

            EditorGUILayout.EndVertical();
        }

        // Read-only filtered view. Rebuilt every repaint (mask lookups are cached, so this is a cheap
        // bitwise scan even across ~1000 levels) so it always reflects live edits to the open level.
        private void DrawFilteredList()
        {
            RebuildFilteredIndices();

            EnsureFilteredRowStyle();

            EditorGUILayout.LabelField(
                $"Filtered: {filteredRealIndices.Count} / {levelsSerializedProperty.arraySize}",
                EditorStyles.boldLabel);

            if (filteredRealIndices.Count == 0)
            {
                EditorGUILayout.HelpBox("No level matches the selected tags.", MessageType.Info);
                return;
            }

            HandleFilteredListArrowKeyNavigation();

            filteredScroll = EditorGUILayout.BeginScrollView(filteredScroll);

            int selected = customList.SelectedIndex;
            Color previousBackground = GUI.backgroundColor;

            for (int i = 0; i < filteredRealIndices.Count; i++)
            {
                int realIndex = filteredRealIndices[i];
                bool isSelected = realIndex == selected;

                string label = realIndex < levelLabels.Count ? levelLabels[realIndex] : BuildDisplayLabel(realIndex);

                GUI.backgroundColor = isSelected ? FilteredRowSelectedTint : previousBackground;
                if (GUILayout.Button(label, filteredRowStyle))
                {
                    customList.SetSelectedIndexAndNotify(realIndex);
                }

                // GetLastRect() only holds real coordinates outside the Layout pass; ScrollTo needs those.
                if (realIndex == pendingScrollToRealIndex && Event.current.type == EventType.Repaint)
                {
                    GUI.ScrollTo(GUILayoutUtility.GetLastRect());
                    pendingScrollToRealIndex = -1;
                }
            }

            GUI.backgroundColor = previousBackground;
            EditorGUILayout.EndScrollView();
        }

        /// <summary>Up/Down navigates the filtered view, wrapping at either end, and queues an auto-scroll to the new row.</summary>
        private void HandleFilteredListArrowKeyNavigation()
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown ||
                (current.keyCode != KeyCode.UpArrow && current.keyCode != KeyCode.DownArrow))
            {
                return;
            }

            int step = current.keyCode == KeyCode.DownArrow ? 1 : -1;
            int count = filteredRealIndices.Count;
            int currentPos = filteredRealIndices.IndexOf(customList.SelectedIndex);

            int newPos = currentPos < 0
                ? (step > 0 ? 0 : count - 1)
                : (currentPos + step + count) % count;

            int newRealIndex = filteredRealIndices[newPos];
            customList.SetSelectedIndexAndNotify(newRealIndex);
            pendingScrollToRealIndex = newRealIndex;

            current.Use();

            if (LevelEditorBase.Instance)
            {
                LevelEditorBase.Instance.Repaint();
            }
        }

        private void RebuildFilteredIndices()
        {
            filteredRealIndices.Clear();

            for (int i = 0; i < levelsSerializedProperty.arraySize; i++)
            {
                LevelData level = levelsSerializedProperty.GetArrayElementAtIndex(i).objectReferenceValue as LevelData;
                if (level == null)
                    continue;

                if (LevelTagScanner.Matches(LevelTagScanner.GetMask(level), selectedTagMask))
                    filteredRealIndices.Add(i);
            }
        }

        private void EnsureFilteredRowStyle()
        {
            if (filteredRowStyle != null)
                return;

            filteredRowStyle = new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleLeft,
                richText = true
            };
        }

        /// <summary>Blocks (and notifies about) array-structure edits while the tag filter is active.</summary>
        private bool IsStructuralChangeBlockedByFilter()
        {
            if (!IsTagFiltering)
                return false;

            EditorUtility.DisplayDialog(TAG_FILTER_ACTIVE_TITLE, TAG_FILTER_ACTIVE_MESSAGE, "OK");
            return true;
        }

        #endregion

        public void OpenLevel(int index)
        {
            if (!LevelEditorBase.Instance)
            {
                Debug.LogError(ON_ENABLE_OVERRIDEN_ERROR);
                return;
            }

            Object slotRef = levelsSerializedProperty.GetArrayElementAtIndex(index).objectReferenceValue;
            if (!slotRef)
            {
                LevelEditorBase.Instance.OpenLevel(null, index);
                return;
            }

            Object toOpen = slotRef;
            if (levelsDatabaseSerializedObject.targetObject is LevelDatabase db &&
                slotRef is LevelData slotLevel)
            {
                LevelData canonicalBase = LevelDataUtilities.ResolveBaseForSlotAsset(db, slotLevel);
                LevelData ship = db.GetActiveVariantForLevel(canonicalBase);
                if (ship)
                    toOpen = ship;
            }

            LevelEditorBase.Instance.OpenLevel(toOpen, index);
        }

        public void ReopenLevel()
        {
            OpenLevel(SelectedLevelIndex);
        }


        public void AddLevel()
        {
            if (IsStructuralChangeBlockedByFilter())
                return;

            if (LevelEditorBase.Instance == null)
            {
                Debug.LogError(ON_ENABLE_OVERRIDEN_ERROR);
                return;
            }

            levelsSerializedProperty.arraySize++;
            int newLevelIndex = levelsSerializedProperty.arraySize - 1;

            var level = CreateNewLevel();
            
            levelLabels.Add(BuildDisplayLabel(newLevelIndex));
            levelsSerializedProperty.GetArrayElementAtIndex(newLevelIndex).objectReferenceValue = level;

            levelsDatabaseSerializedObject.ApplyModifiedProperties();
            
            AssetDatabase.SaveAssets();

            customList.SelectedIndex = newLevelIndex;

            OpenLevel(newLevelIndex);
        }

        private Object CreateNewLevel()
        {
            UnityEngine.Object level = ScriptableObject.CreateInstance(LevelEditorBase.Instance.GetLevelType());
            AssetDatabase.CreateAsset(level, GetRelativeLevelAssetPathByNumber(GetLevelNumber(levelsSerializedProperty.arraySize)));
            LevelEditorBase.Instance.ClearLevel(level);
            return level;
        }
        public void CreateNewLevelInIndex(int targetIndex, bool isReplacing)
        {
            if (IsStructuralChangeBlockedByFilter())
                return;

            if (!LevelEditorBase.Instance)
            {
                Debug.LogError(ON_ENABLE_OVERRIDEN_ERROR);
                return;
            }

            if (isReplacing)
            {
                var oldLevel = levelsSerializedProperty.GetArrayElementAtIndex(targetIndex).objectReferenceValue;
                
                if (oldLevel)
                {
                    AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(oldLevel));
                    AssetDatabase.Refresh();
                }
                var newLevel = CreateNewLevel();
                
                levelLabels[targetIndex] = BuildDisplayLabel(targetIndex);
                levelsSerializedProperty.GetArrayElementAtIndex(targetIndex).objectReferenceValue = newLevel;
            }
            else
            {
                levelsSerializedProperty.arraySize++;
                
                for (int i = levelsSerializedProperty.arraySize - 1; i > targetIndex; i--)
                {
                    levelsSerializedProperty.GetArrayElementAtIndex(i).objectReferenceValue = 
                        levelsSerializedProperty.GetArrayElementAtIndex(i - 1).objectReferenceValue;
                }
                Object newLevel =  CreateNewLevel();
                
                levelsSerializedProperty.GetArrayElementAtIndex(targetIndex).objectReferenceValue = newLevel;
                SetLevelLabels();
            }
            
            AssetDatabase.SaveAssets();
                
            customList.SelectedIndex = targetIndex;
            OpenLevel(targetIndex);
        }


        private string GetLevelNumber(int arraySize)
        {
            int levelNumber = arraySize - 1;

            do
            {
                levelNumber++;
            }
            while (File.Exists(LevelEditorBase.GetProjectPath() + GetRelativeLevelAssetPathByNumber(FormatNumber(levelNumber))));

            return FormatNumber(levelNumber);
        }

        private static string GetRelativeLevelAssetPathByNumber(string levelNumber)
        {
            return LevelEditorBase.Instance.LEVELS_FOLDER_PATH + PATH_SEPARATOR + LEVEL_PREFIX + levelNumber + ASSET_SUFFIX;
        }

        private static string FormatNumber(int maxIndex)
        {
            return maxIndex.ToString(FORMAT_TYPE);
        }


        public void DeleteLevel(int levelIndex)
        {
            if (IsStructuralChangeBlockedByFilter())
                return;

            StringBuilder stringBuilder = LevelEditorBase.Instance.stringBuilder;
            stringBuilder.Clear();
            stringBuilder.Append(REMOVE_LEVEL);
            stringBuilder.Append(BRACKET);
            stringBuilder.Append(levelLabels[levelIndex]);
            stringBuilder.Append(BRACKET);
            stringBuilder.Append(QUESTION_MARK);

            if (EditorUtility.DisplayDialog(REMOVING_LEVEL_TITLE, stringBuilder.ToString(), YES, CANCEL))
            {
                HandleDeleteLevel(levelIndex);
            }
        }

        private void HandleDeleteLevel(int levelIndex)
        {
            UnityEngine.Object tempObject = levelsSerializedProperty.GetArrayElementAtIndex(levelIndex).objectReferenceValue;
            LevelData baseLevelData = tempObject as LevelData;

            if (baseLevelData &&
                LevelDataUtilities.FindEntryIndex(
                    LevelDataUtilities.FindVariantEntriesProperty(levelsDatabaseSerializedObject),
                    baseLevelData) >= 0)
            {
                bool deleteVariantFiles = EditorUtility.DisplayDialog(
                    "Delete level variants?",
                    "This level has variants. Delete variant asset files from disk as well?",
                    "Delete variants + files",
                    "Keep variant files");

                LevelDataUtilities.OnBaseLevelDeletedFromDatabase(
                    levelsDatabaseSerializedObject,
                    baseLevelData,
                    deleteVariantFiles);
            }

            if (tempObject)
            {
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(tempObject));
                AssetDatabase.Refresh();
            }

            if (customList != null)
            {
                if (SelectedLevelIndex == levelIndex)
                {
                    customList.SelectedIndex = levelIndex - 1;
                }
                else
                {
                    customList.SelectedIndex = -1;
                }
            }
            OpenLevel(SelectedLevelIndex);
            levelLabels.RemoveAt(levelIndex);
            levelsSerializedProperty.GetArrayElementAtIndex(levelIndex).objectReferenceValue = null;
            levelsSerializedProperty.DeleteArrayElementAtIndex(levelIndex);
            SetLevelLabels();
        }

        public void SetLevelLabels()
        {
            if (LevelEditorBase.Instance is LevelEditorWindow levelEditorWindow)
            {
                levelEditorWindow.InvalidateLevelLabelSuffixCache();
            }

            levelLabels.Clear();

            if (LevelEditorBase.Instance == null)
            {
                Debug.LogError(ON_ENABLE_OVERRIDEN_ERROR);
                return;
            }

            for (int i = 0; i < levelsSerializedProperty.arraySize; i++)
            {
                levelLabels.Add(BuildDisplayLabel(i));
            }
        }

        /// <summary>
        /// Updates only the row label(s) whose slot resolves to <paramref name="baseLevel"/>, e.g. after
        /// toggling the active build variant. Avoids <see cref="SetLevelLabels"/>'s full validation-cache
        /// clear + O(all levels) rebuild, which froze the UI on every toggle click in a database with
        /// hundreds of levels even though variant metadata doesn't affect level content/validation.
        /// </summary>
        public void RefreshLabelsForBase(LevelData baseLevel)
        {
            if (!baseLevel || LevelEditorBase.Instance == null)
                return;

            if (!(levelsDatabaseSerializedObject.targetObject is LevelDatabase flowDb))
            {
                SetLevelLabels();
                return;
            }

            while (levelLabels.Count < levelsSerializedProperty.arraySize)
            {
                levelLabels.Add(string.Empty);
            }

            for (int i = 0; i < levelsSerializedProperty.arraySize; i++)
            {
                LevelData slotLevel = levelsSerializedProperty.GetArrayElementAtIndex(i).objectReferenceValue as LevelData;
                if (!slotLevel)
                    continue;

                if (LevelDataUtilities.ResolveBaseForSlotAsset(flowDb, slotLevel) != baseLevel)
                    continue;

                levelLabels[i] = BuildDisplayLabel(i);
            }
        }

        public void PopulateLevels()
        {
            if (IsStructuralChangeBlockedByFilter())
                return;

            var database = levelsDatabaseSerializedObject.targetObject as LevelDatabase;
            if (database == null) return;

            database.Editor_PopulateLevels(LevelEditorBase.Instance.LEVELS_FOLDER_PATH);
            levelsDatabaseSerializedObject.Update();
            LevelDataUtilities.PruneNullVariantReferences(levelsDatabaseSerializedObject);
            levelsDatabaseSerializedObject.Update();

            PopulateAllLevelVariants();
            levelsDatabaseSerializedObject.Update();
            // Merge is append-only and matches by base name, so it cannot drop variants left mis-owned
            // by an earlier level renumber/reorder. Prune those before the null pass re-indexes actives.
            LevelDataUtilities.PruneMisownedVariantReferences(levelsDatabaseSerializedObject);
            levelsDatabaseSerializedObject.Update();
            LevelDataUtilities.PruneNullVariantReferences(levelsDatabaseSerializedObject);
            levelsDatabaseSerializedObject.Update();

            List<ILevelValidationRule> validationRules = LevelValidator.CreateDefaultRules();
            HashSet<LevelData> validatedLevels = new HashSet<LevelData>();
            ValidatePopulatedLevels(validationRules, validatedLevels);
            ValidatePopulatedVariants(validationRules, validatedLevels);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SetLevelLabels();
            onRenameAllCallback?.Invoke();
        }

        private void PopulateAllLevelVariants()
        {
            for (int i = 0; i < levelsSerializedProperty.arraySize; i++)
            {
                LevelData baseLevel =
                    levelsSerializedProperty.GetArrayElementAtIndex(i).objectReferenceValue as LevelData;
                if (!baseLevel)
                    continue;

                if (LevelDataUtilities.TryMergeVariantAssetsFromDisk(
                        levelsDatabaseSerializedObject,
                        baseLevel,
                        LevelEditorBase.Instance.LEVELS_FOLDER_PATH))
                {
                    levelsDatabaseSerializedObject.Update();
                }
            }
        }

        private void ValidatePopulatedLevels(
            List<ILevelValidationRule> validationRules,
            HashSet<LevelData> validatedLevels)
        {
            for (int i = 0; i < levelsSerializedProperty.arraySize; i++)
            {
                LevelData levelData =
                    levelsSerializedProperty.GetArrayElementAtIndex(i).objectReferenceValue as LevelData;
                ValidatePopulatedLevel(levelData, i + 1, validationRules, validatedLevels);
            }
        }

        private void ValidatePopulatedVariants(
            List<ILevelValidationRule> validationRules,
            HashSet<LevelData> validatedLevels)
        {
            SerializedProperty entries = LevelDataUtilities.FindVariantEntriesProperty(levelsDatabaseSerializedObject);
            if (entries == null)
                return;

            for (int entryIndex = 0; entryIndex < entries.arraySize; entryIndex++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(entryIndex);
                SerializedProperty baseProp = entry.FindPropertyRelative("baseLevel");
                SerializedProperty variantsProp = entry.FindPropertyRelative("variants");
                LevelData baseLevel = baseProp?.objectReferenceValue as LevelData;
                int levelNumber = GetLevelNumberForValidation(baseLevel);

                if (variantsProp == null)
                    continue;

                for (int variantIndex = 0; variantIndex < variantsProp.arraySize; variantIndex++)
                {
                    LevelData variant =
                        variantsProp.GetArrayElementAtIndex(variantIndex).objectReferenceValue as LevelData;
                    if (variant)
                        variant.Validate();
                    ValidatePopulatedLevel(variant, levelNumber, validationRules, validatedLevels);
                }
            }
        }

        private int GetLevelNumberForValidation(LevelData baseLevel)
        {
            if (!baseLevel)
                return 0;

            for (int i = 0; i < levelsSerializedProperty.arraySize; i++)
            {
                if (levelsSerializedProperty.GetArrayElementAtIndex(i).objectReferenceValue == baseLevel)
                    return i + 1;
            }

            return 0;
        }

        private void ValidatePopulatedLevel(
            LevelData levelData,
            int levelNumber,
            List<ILevelValidationRule> validationRules,
            HashSet<LevelData> validatedLevels)
        {
            if (!levelData || !validatedLevels.Add(levelData))
                return;

            SerializedObject levelSo = new SerializedObject(levelData);
            SerializedProperty itemsProp = levelSo.FindProperty("elements");
            SerializedProperty sizeProp = levelSo.FindProperty("size");
            Vector2Int gridSize = sizeProp != null ? sizeProp.vector2IntValue : Vector2Int.zero;

            LevelValidator.ValidateAndLog(
                levelData.name,
                levelNumber,
                itemsProp,
                gridSize,
                validationRules,
                logPassedLevel: false);
        }
        
        private void RenameIncorrectLevels()
        {
            
        }

        #region draw buttons

        public void DrawRenameLevelsButton()
        {
            if (GUILayout.Button(RENAME_LEVELS_LABEL, EditorCustomStyles.button))
            {
                PopulateLevels();
            }
        }

        public void DrawClearSelectionButton()
        {
            if (GUILayout.Button(REMOVE_SELECTION, EditorCustomStyles.button))
            {
                ClearSelection();
            }
        }

        public void DrawGlobalValidationButton()
        {
            if (GUILayout.Button(GLOBAL_VALIDATION_LABEL, EditorCustomStyles.button))
            {
                Debug.Log("Global validation log begins");

                for (int i = 0; i < levelsSerializedProperty.arraySize; i++)
                {
                    LevelEditorBase.Instance.LogErrorsForGlobalValidation(levelsSerializedProperty.GetArrayElementAtIndex(i).objectReferenceValue, i);
                }

                Debug.Log("Global validation log ends");

                SetLevelLabels();
            }
        }

        #endregion

        #region Set index modal window

        private void OpenSetIndexModalWindow()
        {
            SetIndexModalWindow window = ScriptableObject.CreateInstance<SetIndexModalWindow>();
            window.SetData(SelectedLevelIndex, levelsSerializedProperty.arraySize, this);
            window.minSize = INDEX_CHANGE_WINDOW_SIZE;
            window.maxSize = INDEX_CHANGE_WINDOW_SIZE;
            window.titleContent = new GUIContent(INDEX_CHANGE_WINDOW);

            window.ShowModal();
        }

        private void ModalWindowProcessChange(int originalIndex, int newIndex)
        {
            if (IsStructuralChangeBlockedByFilter())
                return;

            levelsSerializedProperty.MoveArrayElement(originalIndex, newIndex);
            levelsDatabaseSerializedObject.ApplyModifiedProperties();
            customList.SelectedIndex = newIndex;
            ListReorderedCallback();
        }

        private class SetIndexModalWindow : EditorWindow
        {
            private const string INT_FIELD_LABEL = "target element new #";
            private const string CANCEL_BUTTON_LABEL = "Cancel";
            private const string CHANGE_BUTTON_LABEL = "Change";
            private const string DEFAULT_LABEL = "target element #";
            public int elementOrinialIndex;
            public int arraySize;
            public LevelsHandler levelsHandler;
            private string label;
            private int newPositionNumber;

            public void SetData(int elementOrinialIndex,int arraySize, LevelsHandler levelsHandler)
            {
                this.elementOrinialIndex = elementOrinialIndex;
                this.levelsHandler = levelsHandler;
                this.arraySize = arraySize;
                label = DEFAULT_LABEL + (elementOrinialIndex + 1);
                newPositionNumber = elementOrinialIndex + 1;
            }

            void OnGUI()
            {
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(label);
                newPositionNumber = EditorGUILayout.IntField(INT_FIELD_LABEL, newPositionNumber);
                newPositionNumber = Mathf.Clamp(newPositionNumber, 1, arraySize);

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button(CANCEL_BUTTON_LABEL))
                {
                    this.Close();
                }

                if (GUILayout.Button(CHANGE_BUTTON_LABEL))
                {
                    levelsHandler.ModalWindowProcessChange(elementOrinialIndex, newPositionNumber - 1);
                    this.Close();
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }
        }

        #endregion
    }
}