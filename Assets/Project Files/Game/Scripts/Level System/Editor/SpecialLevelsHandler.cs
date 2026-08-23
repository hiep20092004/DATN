using System.Collections.Generic;
using System.IO;
using System.Reflection;
using WaterFlow.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WaterFlow.Game
{
    public enum SpecialLevelEditorModeFilter
    {
        Test = 0,
        GoldMode = 1,
        RescueColor = 2,
        RescueBlock = 3
    }

    public enum SpecialLevelEditorTestType
    {
        Normal = 0,
        GoldMode = 1,
        RescueColor = 2,
        RescueBlock = 3
    }

    public sealed class SpecialLevelsHandler
    {
        private const string HeaderPrefix = "Special levels amount: ";
        private const string NumberFormat = "000";
        private const string FilterPrefsKey = "WaterFlow.Game.LevelEditor.SpecialModeFilter";
        private const string TestTypePrefsKey = "WaterFlow.Game.LevelEditor.SpecialTestType";
        private const string TestLevelsFolder = LevelSystemUtils.SpecialLevelsSourceFolder + "/Test";
        private const string TestStoreAssetPath = LevelSystemUtils.SpecialLevelsSourceFolder + "/SpecialLevelsEditorStore.asset";

        private readonly SerializedObject levelDatabaseSerializedObject;
        private readonly SerializedProperty runtimeSpecialLevelsSerializedProperty;
        private readonly List<string> cachedLabels = new List<string>();
        private readonly List<SerializedProperty> filteredRuntimeProperties = new List<SerializedProperty>();
        private readonly List<int> filteredRuntimeIndices = new List<int>();

        private readonly SpecialLevelsEditorStore testStore;
        private readonly SerializedObject testStoreSerializedObject;
        private readonly SerializedProperty testLevelsSerializedProperty;

        private CustomList customList;
        private SpecialLevelEditorModeFilter currentFilter;
        private SpecialLevelEditorTestType currentTestType;

        public int SelectedLevelIndex => customList?.SelectedIndex ?? -1;
        public Object SelectedLevelObject => TryGetSelectedLevelObject();

        public bool HasSelection => TryGetSelectedLevelObject() != null;
        public bool IsTestFilterActive => currentFilter == SpecialLevelEditorModeFilter.Test;

        public bool IgnoreDragEvents
        {
            get => customList != null && customList.IgnoreDragEvents;
            set
            {
                if (customList != null)
                    customList.IgnoreDragEvents = value;
            }
        }

        public CustomList CustomList => customList;

        public SpecialLevelsHandler(
            SerializedObject levelDatabaseSerializedObject,
            SerializedProperty specialLevelsSerializedProperty)
        {
            this.levelDatabaseSerializedObject = levelDatabaseSerializedObject;
            runtimeSpecialLevelsSerializedProperty = specialLevelsSerializedProperty;

            currentFilter = (SpecialLevelEditorModeFilter)EditorPrefs.GetInt(
                FilterPrefsKey,
                (int)SpecialLevelEditorModeFilter.GoldMode);
            currentTestType = (SpecialLevelEditorTestType)EditorPrefs.GetInt(
                TestTypePrefsKey,
                (int)SpecialLevelEditorTestType.Normal);

            testStore = LoadOrCreateTestStore();
            testStoreSerializedObject = new SerializedObject(testStore);
            testLevelsSerializedProperty = testStoreSerializedObject.FindProperty("testLevels");

            RebuildListAndKeepSelection();
        }

        public void DisplayReorderableList()
        {
            DrawFilterToolbar();

            if (customList == null)
                return;

            customList.DrawJumpToIndexRow();
            customList.Display();
        }

        public void DrawSelectedModeConfig()
        {
            SpecialLevelData specialLevel = SelectedLevelObject as SpecialLevelData;
            if (!specialLevel)
                return;

            EditorGUILayout.Space();

            SerializedObject selectedLevelSo = new SerializedObject(specialLevel);
            foreach (SerializedProperty property in GetVisibleSpecialConfigProperties(selectedLevelSo, specialLevel))
                EditorGUILayout.PropertyField(property, true);

            selectedLevelSo.ApplyModifiedProperties();
        }

        private static IEnumerable<SerializedProperty> GetVisibleSpecialConfigProperties(
            SerializedObject serializedObject,
            SpecialLevelData specialLevel)
        {
            FieldInfo[] fields = typeof(SpecialLevelData).GetFields(ReflectionUtils.FLAGS_INSTANCE);
            foreach (FieldInfo field in fields)
            {
                if (field.GetCustomAttribute<LevelEditorSetting>() == null || field.Name == "mode")
                    continue;

                if (!CanDrawByShowIf(field, specialLevel))
                    continue;

                SerializedProperty property = serializedObject.FindProperty(field.Name);
                if (property != null)
                    yield return property;
            }
        }

        private static bool CanDrawByShowIf(FieldInfo field, SpecialLevelData specialLevel)
        {
            ShowIfAttribute showIf = field.GetCustomAttribute<ShowIfAttribute>();
            if (showIf == null)
                return true;

            BindingFlags flags = ReflectionUtils.FLAGS_INSTANCE;
            string conditionName = showIf.ConditionName;
            System.Type type = typeof(SpecialLevelData);

            FieldInfo conditionField = type.GetField(conditionName, flags);
            if (conditionField != null && conditionField.FieldType == typeof(bool))
                return (bool)conditionField.GetValue(specialLevel);

            PropertyInfo conditionProperty = type.GetProperty(conditionName, flags);
            if (conditionProperty != null && conditionProperty.PropertyType == typeof(bool))
                return (bool)conditionProperty.GetValue(specialLevel);

            MethodInfo conditionMethod = type.GetMethod(conditionName, flags);
            if (conditionMethod != null &&
                conditionMethod.ReturnType == typeof(bool) &&
                conditionMethod.GetParameters().Length == 0)
            {
                return (bool)conditionMethod.Invoke(specialLevel, null);
            }

            return true;
        }

        public void ClearSelection()
        {
            if (customList != null)
                customList.SelectedIndex = -1;
        }

        /// <summary>
        /// Selects (and opens via the editor) the given special level, switching the mode filter to the
        /// level's own mode so it becomes visible in the list. Returns true when the level was found and selected.
        /// </summary>
        public bool SelectLevel(Object levelObject)
        {
            if (!levelObject) return false;

            // Test levels (both plain LevelData and SpecialLevelData sub-types) live in the test store,
            // not in the runtime special levels list. Check there first so the Test filter is restored
            // correctly when re-opening the editor after play-mode exit.
            if (IsInTestStore(levelObject))
            {
                if (currentFilter != SpecialLevelEditorModeFilter.Test)
                {
                    currentFilter = SpecialLevelEditorModeFilter.Test;
                    EditorPrefs.SetInt(FilterPrefsKey, (int)currentFilter);
                }

                RebuildListAndKeepSelection(levelObject);
                return SelectedLevelObject == levelObject;
            }

            if (!(levelObject is SpecialLevelData special))
                return false;

            // Make sure the active filter shows this level's mode.
            SpecialLevelEditorModeFilter targetFilter = GetFilterForMode(special.Mode);
            if (currentFilter != targetFilter)
            {
                currentFilter = targetFilter;
                EditorPrefs.SetInt(FilterPrefsKey, (int)currentFilter);
            }

            // Rebuild the list under the (possibly new) filter and select the requested object.
            // RebuildListAndKeepSelection invokes OnSelectionChanged, which opens the level in the editor.
            RebuildListAndKeepSelection(levelObject);
            return SelectedLevelObject == levelObject;
        }

        private bool IsInTestStore(Object target)
        {
            if (!target || testLevelsSerializedProperty == null) return false;
            testStoreSerializedObject.Update();
            for (int i = 0; i < testLevelsSerializedProperty.arraySize; i++)
            {
                if (testLevelsSerializedProperty.GetArrayElementAtIndex(i).objectReferenceValue == target)
                    return true;
            }
            return false;
        }

        private static SpecialLevelEditorModeFilter GetFilterForMode(SpecialLevelMode mode)
        {
            switch (mode)
            {
                case SpecialLevelMode.RescueColor:
                    return SpecialLevelEditorModeFilter.RescueColor;
                case SpecialLevelMode.RescueBlock:
                    return SpecialLevelEditorModeFilter.RescueBlock;
                default:
                    return SpecialLevelEditorModeFilter.GoldMode;
            }
        }

        private void DrawFilterToolbar()
        {
            EditorGUI.BeginChangeCheck();
            SpecialLevelEditorModeFilter nextFilter =
                (SpecialLevelEditorModeFilter)EditorGUILayout.EnumPopup("Special Mode", currentFilter);
            if (EditorGUI.EndChangeCheck() && nextFilter != currentFilter)
            {
                currentFilter = nextFilter;
                EditorPrefs.SetInt(FilterPrefsKey, (int)currentFilter);
                RebuildListAndKeepSelection();
            }

            DrawTestTypeField();
        }

        private void DrawTestTypeField()
        {
            if (currentFilter != SpecialLevelEditorModeFilter.Test)
                return;

            EditorGUI.BeginChangeCheck();
            SpecialLevelEditorTestType nextTestType =
                (SpecialLevelEditorTestType)EditorGUILayout.EnumPopup("Special Test Type", currentTestType);
            if (!EditorGUI.EndChangeCheck() || nextTestType == currentTestType)
                return;

            currentTestType = nextTestType;
            EditorPrefs.SetInt(TestTypePrefsKey, (int)currentTestType);
        }

        private void RebuildListAndKeepSelection(Object preferredObject = null, int preferredVisibleIndex = -1)
        {
            Object rememberedSelection = preferredObject ? preferredObject : TryGetSelectedLevelObject();
            bool previousIgnoreDragEvents = IgnoreDragEvents;

            SetupCustomList();
            RefreshLabels();
            customList.IgnoreDragEvents = previousIgnoreDragEvents;

            if (rememberedSelection)
            {
                int restoredIndex = FindVisibleIndexByObject(rememberedSelection);
                if (restoredIndex >= 0)
                {
                    customList.SelectedIndex = restoredIndex;
                    OnSelectionChanged();
                    return;
                }
            }

            if (preferredVisibleIndex >= 0 && preferredVisibleIndex < customList.ArraySize())
            {
                customList.SelectedIndex = preferredVisibleIndex;
                OnSelectionChanged();
                return;
            }

            if (customList.ArraySize() > 0)
            {
                customList.SelectedIndex = 0;
                OnSelectionChanged();
            }
            else
            {
                customList.SelectedIndex = -1;
            }
        }

        private void SetupCustomList()
        {
            if (currentFilter == SpecialLevelEditorModeFilter.Test)
            {
                customList = new CustomList(testStoreSerializedObject, testLevelsSerializedProperty, GetLabel);
                customList.listReorderedCallback += OnTestListChanged;
            }
            else
            {
                BuildFilteredRuntimeProperties();
                customList = new CustomList(levelDatabaseSerializedObject, filteredRuntimeProperties, GetLabel);
                customList.listReorderedCallbackWithDetails += OnRuntimeListReordered;
            }

            customList.EnableHeader(GetHeader);
            customList.selectionChangedCallback += OnSelectionChanged;
            customList.addElementWithDropdownCallback += AddSpecialLevel;
            customList.removeElementCallback += RemoveSelectedSpecialLevel;
            customList.enableJumpToIndexRow = true;
            customList.jumpToIndexFieldLabel = "Jump to Special";
            customList.multiColumnMinElementWidth = 180f;
        }

        private void OnTestListChanged()
        {
            PersistTestStore();
            RefreshLabels();
        }

        /// <summary>
        /// The store lives outside version control and is the only link between the editor list and the
        /// test level assets, so it is written to disk immediately — a dirty-only store is lost on the
        /// next domain reload, leaving orphaned assets and an empty list.
        /// </summary>
        private void PersistTestStore()
        {
            testStoreSerializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(testStore);
            AssetDatabase.SaveAssets();
        }

        private void BuildFilteredRuntimeProperties()
        {
            filteredRuntimeProperties.Clear();
            filteredRuntimeIndices.Clear();

            if (runtimeSpecialLevelsSerializedProperty == null)
                return;

            SpecialLevelMode mode = GetRuntimeModeFilter(currentFilter);
            for (int i = 0; i < runtimeSpecialLevelsSerializedProperty.arraySize; i++)
            {
                SerializedProperty property = runtimeSpecialLevelsSerializedProperty.GetArrayElementAtIndex(i);
                if (!(property.objectReferenceValue is SpecialLevelData level) || level.Mode != mode)
                    continue;

                filteredRuntimeProperties.Add(property);
                filteredRuntimeIndices.Add(i);
            }
        }

        private void OnRuntimeListReordered(int sourceVisibleIndex, int destinationVisibleIndex)
        {
            if (runtimeSpecialLevelsSerializedProperty == null ||
                sourceVisibleIndex < 0 || sourceVisibleIndex >= filteredRuntimeIndices.Count ||
                destinationVisibleIndex < 0 || destinationVisibleIndex >= filteredRuntimeIndices.Count)
            {
                return;
            }

            int sourceRuntimeIndex = filteredRuntimeIndices[sourceVisibleIndex];
            int destinationRuntimeIndex = filteredRuntimeIndices[destinationVisibleIndex];
            runtimeSpecialLevelsSerializedProperty.MoveArrayElement(sourceRuntimeIndex, destinationRuntimeIndex);
            levelDatabaseSerializedObject.ApplyModifiedProperties();
            RebuildListAndKeepSelection();
        }

        private string GetLabel(SerializedProperty elementProperty, int elementIndex)
        {
            if (elementIndex < 0 || elementIndex >= cachedLabels.Count)
                return $"#{elementIndex + 1} | Missing";

            Object levelObject = elementProperty.objectReferenceValue;
            if (levelObject is SpecialLevelData specialLevel)
                return $"{cachedLabels[elementIndex]} [{specialLevel.Mode}]";

            return cachedLabels[elementIndex];
        }

        private string GetHeader()
        {
            return HeaderPrefix + (customList != null ? customList.ArraySize() : 0);
        }

        private void OnSelectionChanged()
        {
            Object levelObject = SelectedLevelObject;
            if (!levelObject)
                return;

            // Do not open/persist when the Main tab is active (handler init used to overwrite saved session).
            if (LevelEditorBase.Instance is LevelEditorWindow levelEditor && !levelEditor.ShowSpecialLevelsList)
                return;

            // Special levels are editor-only test targets; always open them as slot 0.
            LevelEditorBase.Instance?.OpenLevel(levelObject, 0);
        }

        private void AddSpecialLevel()
        {
            string targetFolder = currentFilter == SpecialLevelEditorModeFilter.Test
                ? TestLevelsFolder
                : LevelSystemUtils.SpecialActiveLevelsFolder;
            EnsureFolderExists(targetFolder);

            LevelData newAsset = CreateNewLevelAsset();
            SetDefaultModeForNewAsset(newAsset);
            string assetPath = BuildUniqueAssetPath(targetFolder, GetNewAssetNamePrefix());
            AssetDatabase.CreateAsset(newAsset, assetPath);

            if (currentFilter == SpecialLevelEditorModeFilter.Test)
            {
                int insertIndex = testLevelsSerializedProperty.arraySize;
                testLevelsSerializedProperty.arraySize++;
                testLevelsSerializedProperty.GetArrayElementAtIndex(insertIndex).objectReferenceValue = newAsset;
                PersistTestStore();
            }
            else
            {
                int insertIndex = runtimeSpecialLevelsSerializedProperty.arraySize;
                runtimeSpecialLevelsSerializedProperty.arraySize++;
                runtimeSpecialLevelsSerializedProperty.GetArrayElementAtIndex(insertIndex).objectReferenceValue = newAsset;
                levelDatabaseSerializedObject.ApplyModifiedProperties();
            }

            RebuildListAndKeepSelection(newAsset);
        }

        private LevelData CreateNewLevelAsset()
        {
            if (currentFilter != SpecialLevelEditorModeFilter.Test)
                return ScriptableObject.CreateInstance<SpecialLevelData>();

            return currentTestType == SpecialLevelEditorTestType.Normal
                ? ScriptableObject.CreateInstance<LevelData>()
                : ScriptableObject.CreateInstance<SpecialLevelData>();
        }

        private string GetNewAssetNamePrefix()
        {
            if (currentFilter != SpecialLevelEditorModeFilter.Test)
                return GetRuntimeModeFilter(currentFilter) + " ";

            return currentTestType == SpecialLevelEditorTestType.Normal
                ? "LevelTest "
                : currentTestType + "Test ";
        }

        private void SetDefaultModeForNewAsset(LevelData asset)
        {
            if (!(asset is SpecialLevelData))
                return;

            SpecialLevelMode defaultMode = currentFilter == SpecialLevelEditorModeFilter.Test
                ? GetSpecialModeFromTestType(currentTestType)
                : GetRuntimeModeFilter(currentFilter);

            SerializedObject assetSo = new SerializedObject(asset);
            SerializedProperty modeProp = assetSo.FindProperty("mode");
            if (modeProp != null)
                modeProp.intValue = (int)defaultMode;
            assetSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private void RemoveSelectedSpecialLevel()
        {
            int selectedIndex = SelectedLevelIndex;
            if (selectedIndex < 0)
                return;

            Object selectedObject = TryGetSelectedLevelObject();
            int countBefore = currentFilter == SpecialLevelEditorModeFilter.Test
                ? testLevelsSerializedProperty.arraySize
                : filteredRuntimeIndices.Count;

            if (customList != null)
                customList.SelectedIndex = -1;

            if (selectedObject)
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(selectedObject));

            if (currentFilter == SpecialLevelEditorModeFilter.Test)
            {
                if (selectedIndex >= testLevelsSerializedProperty.arraySize)
                    return;

                testLevelsSerializedProperty.GetArrayElementAtIndex(selectedIndex).objectReferenceValue = null;
                testLevelsSerializedProperty.DeleteArrayElementAtIndex(selectedIndex);
                PersistTestStore();
            }
            else
            {
                if (selectedIndex >= filteredRuntimeIndices.Count)
                    return;

                int runtimeIndex = filteredRuntimeIndices[selectedIndex];
                runtimeSpecialLevelsSerializedProperty.GetArrayElementAtIndex(runtimeIndex).objectReferenceValue = null;
                runtimeSpecialLevelsSerializedProperty.DeleteArrayElementAtIndex(runtimeIndex);
                levelDatabaseSerializedObject.ApplyModifiedProperties();
            }

            int preferredVisibleIndex = countBefore <= 1
                ? -1
                : (selectedIndex > 0 ? selectedIndex - 1 : 0);
            RebuildListAndKeepSelection(preferredVisibleIndex: preferredVisibleIndex);
        }

        private int FindVisibleIndexByObject(Object target)
        {
            if (!target || customList == null)
                return -1;

            int count = customList.ArraySize();
            for (int i = 0; i < count; i++)
            {
                SerializedProperty property = GetPropertyByVisibleIndex(i);
                if (property != null && property.objectReferenceValue == target)
                    return i;
            }

            return -1;
        }

        private void RefreshLabels()
        {
            cachedLabels.Clear();

            int count = customList != null ? customList.ArraySize() : 0;
            for (int i = 0; i < count; i++)
            {
                SerializedProperty property = GetPropertyByVisibleIndex(i);
                Object levelObject = property != null ? property.objectReferenceValue : null;
                cachedLabels.Add(LevelEditorBase.Instance != null
                    ? LevelEditorBase.Instance.GetLevelLabel(levelObject, i)
                    : $"#{i + 1}");
            }
        }

        private Object TryGetSelectedLevelObject()
        {
            SerializedProperty selectedProperty = GetSelectedProperty();
            if (selectedProperty == null)
                return null;

            if (selectedProperty.serializedObject == null ||
                selectedProperty.serializedObject.targetObject == null)
                return null;

            selectedProperty.serializedObject.Update();
            return selectedProperty.objectReferenceValue;
        }

        private SerializedProperty GetSelectedProperty()
        {
            return GetPropertyByVisibleIndex(SelectedLevelIndex);
        }

        private SerializedProperty GetPropertyByVisibleIndex(int visibleIndex)
        {
            if (visibleIndex < 0 || customList == null || visibleIndex >= customList.ArraySize())
                return null;

            if (currentFilter == SpecialLevelEditorModeFilter.Test)
                return testLevelsSerializedProperty.GetArrayElementAtIndex(visibleIndex);

            if (visibleIndex >= filteredRuntimeProperties.Count)
                return null;

            return filteredRuntimeProperties[visibleIndex];
        }

        private static SpecialLevelMode GetRuntimeModeFilter(SpecialLevelEditorModeFilter filter)
        {
            switch (filter)
            {
                case SpecialLevelEditorModeFilter.RescueColor:
                    return SpecialLevelMode.RescueColor;
                case SpecialLevelEditorModeFilter.RescueBlock:
                    return SpecialLevelMode.RescueBlock;
                default:
                    return SpecialLevelMode.GoldMode;
            }
        }

        private static SpecialLevelsEditorStore LoadOrCreateTestStore()
        {
            EnsureFolderExists(LevelSystemUtils.SpecialLevelsSourceFolder);

            SpecialLevelsEditorStore store = AssetDatabase.LoadAssetAtPath<SpecialLevelsEditorStore>(TestStoreAssetPath);
            if (store)
                return store;

            store = ScriptableObject.CreateInstance<SpecialLevelsEditorStore>();
            AssetDatabase.CreateAsset(store, TestStoreAssetPath);
            AssetDatabase.SaveAssets();
            return store;
        }

        private static void EnsureFolderExists(string assetFolder)
        {
            string fullPath = Path.Combine(
                Application.dataPath.Replace("Assets", string.Empty),
                assetFolder);
            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);

            AssetDatabase.Refresh();
        }

        private static string BuildUniqueAssetPath(string folderPath, string baseName)
        {
            int index = 1;
            while (true)
            {
                string path = $"{folderPath}/{baseName}{index.ToString(NumberFormat)}.asset";
                if (!AssetDatabase.LoadAssetAtPath<Object>(path))
                    return path;
                index++;
            }
        }

        private static SpecialLevelMode GetSpecialModeFromTestType(SpecialLevelEditorTestType testType)
        {
            switch (testType)
            {
                case SpecialLevelEditorTestType.RescueColor:
                    return SpecialLevelMode.RescueColor;
                case SpecialLevelEditorTestType.RescueBlock:
                    return SpecialLevelMode.RescueBlock;
                default:
                    return SpecialLevelMode.GoldMode;
            }
        }
    }
}
