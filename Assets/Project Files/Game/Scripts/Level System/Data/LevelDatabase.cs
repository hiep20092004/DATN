using System;
using WaterFlow.Core;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Random = UnityEngine.Random;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WaterFlow.Game
{
    [CreateAssetMenu(menuName = "Data/Level/Level Database", fileName = "Level Database")]
    public class LevelDatabase : ScriptableObject
    {
#if UNITY_EDITOR
        [SerializeField, LevelEditorSetting] LevelData[] levels;

        [Space]
        [Tooltip("Per-base-level variants and which asset is used when building active level assets.")]
        [SerializeField, LevelEditorSetting] LevelVariantEntry[] variantEntries = Array.Empty<LevelVariantEntry>();
#endif
        
        [Space]
        [SerializeField, LevelEditorSetting] ElementTypeEditorData[] cells;
        [SerializeField, LevelEditorSetting] EditorColorData[] editorColorData;
        
        [Space]
        [SerializeField] LevelBlockEffectData[] effects;
        [SerializeField] LevelGateEffectData[] gateEffects;
        [SerializeField] LevelInteractableObjectData[] interactableObjects;

        [Space]
        [Tooltip("Pairwise rules for which block effects may be combined on a single block. Empty = all combinations allowed.")]
        [SerializeField] BlockEffectCompatibilityMatrix blockEffectCompatibility = new BlockEffectCompatibilityMatrix();
        
        [Space]
        [SerializeField] LevelGeneralConfigData levelGeneralConfigData;

        [SerializeField, HideInInspector] LevelRuntimeInfo[] runtimeLevelInfos = Array.Empty<LevelRuntimeInfo>();
        

        public int AmountOfLevels
        {
            get
            {
#if UNITY_EDITOR
                return levels?.Length ?? 0;
#else
                return GetMaxLevel();
#endif
            }
        }
        public LevelBlockEffectData[] Effects => effects;
        public LevelGateEffectData[] GateEffects => gateEffects;
        public LevelInteractableObjectData[] InteractableObjects => interactableObjects;
        public BlockEffectCompatibilityMatrix BlockEffectCompatibility => blockEffectCompatibility;
        
        /// <summary>Editor-only visual (icon + color) configured per <see cref="ElementType"/>; null if unset.</summary>
        public ElementTypeEditorData GetCellEditorData(ElementType type)
        {
            if (cells == null)
                return null;

            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] != null && cells[i].Type == type)
                    return cells[i];
            }

            return null;
        }

        public LevelGeneralConfigData LevelGeneralConfigData => levelGeneralConfigData;

#if UNITY_EDITOR
        private const string PREFS_TEST_VARIANT_ASSET_PATH = "editor_test_variant_asset_path";
        private const string PREFS_TEST_VARIANT_LEVEL_INDEX = "editor_test_variant_level_index";
        private const string PREFS_SPECIAL_TEST_PLAY = "editor_special_test_play";

        // Sentinel values instead of DeleteKey/HasKey: EditorPrefs.HasKey reports false for keys written
        // before the domain reload that enters play mode, which made every override check read as "inactive"
        // exactly when the tested level had to be resolved.
        private const int NO_OVERRIDE_SLOT = -1;

        /// <summary>
        /// Marks the current play session as a test play launched from the Level Editor, which force-unlocks
        /// power-ups and makes them free. Cleared on play exit via <see cref="ClearEditorPlayModeLevelOverride"/>,
        /// so it never leaks into a normal Play session.
        /// </summary>
        public static void SetEditorTestPlay(bool active)
        {
            EditorPrefs.SetBool(PREFS_SPECIAL_TEST_PLAY, active);
        }

        public static bool IsEditorTestPlay => EditorPrefs.GetBool(PREFS_SPECIAL_TEST_PLAY, false);

        public static void SetEditorPlayModeLevelOverride(int slotIndex, LevelData levelData)
        {
            if (!levelData || slotIndex < 0)
            {
                ClearEditorPlayModeLevelOverride();
                return;
            }

            EditorPrefs.SetString(PREFS_TEST_VARIANT_ASSET_PATH, AssetDatabase.GetAssetPath(levelData));
            EditorPrefs.SetInt(PREFS_TEST_VARIANT_LEVEL_INDEX, slotIndex);
        }

        public static void ClearEditorPlayModeLevelOverride()
        {
            EditorPrefs.SetString(PREFS_TEST_VARIANT_ASSET_PATH, string.Empty);
            EditorPrefs.SetInt(PREFS_TEST_VARIANT_LEVEL_INDEX, NO_OVERRIDE_SLOT);
            EditorPrefs.SetBool(PREFS_SPECIAL_TEST_PLAY, false);
        }

        /// <summary>
        /// True while play mode was entered via the Level Editor "Test" button. Used to skip onboarding
        /// flows (e.g. first-level tutorial) that must not fire when a developer is testing a single level.
        /// </summary>
        public static bool IsEditorPlayModeLevelOverrideActive =>
            EditorPlayModeLevelOverrideSlot >= 0 && !string.IsNullOrEmpty(EditorPlayModeLevelOverridePath);

        public static int EditorPlayModeLevelOverrideSlot =>
            EditorPrefs.GetInt(PREFS_TEST_VARIANT_LEVEL_INDEX, NO_OVERRIDE_SLOT);

        public static string EditorPlayModeLevelOverridePath =>
            EditorPrefs.GetString(PREFS_TEST_VARIANT_ASSET_PATH, string.Empty);

        private static LevelData TryGetEditorPlayModeLevelOverride(int slotIndex)
        {
            if (slotIndex < 0 || EditorPlayModeLevelOverrideSlot != slotIndex)
                return null;

            string assetPath = EditorPlayModeLevelOverridePath;
            if (string.IsNullOrEmpty(assetPath))
                return null;

            return UnityEditor.AssetDatabase.LoadAssetAtPath<LevelData>(assetPath);
        }
#endif

#if UNITY_EDITOR
        public LevelVariantEntry[] VariantEntries => variantEntries;

        /// <summary>Resolves which <see cref="LevelData"/> is used for active build / editor play for this base slot.</summary>
        public LevelData GetActiveVariantForLevel(LevelData baseLevelData)
        {
            if (!baseLevelData)
                return baseLevelData;

            if (variantEntries == null || variantEntries.Length == 0)
                return baseLevelData;

            for (int i = 0; i < variantEntries.Length; i++)
            {
                LevelVariantEntry entry = variantEntries[i];
                if (entry == null || entry.BaseLevel != baseLevelData)
                    continue;

                int idx = entry.ActiveVariantIndex;
                if (idx < 0)
                    return baseLevelData;

                LevelData[] v = entry.Variants;
                if (v == null || idx >= v.Length)
                    return baseLevelData;

                LevelData picked = v[idx];
                return picked ? picked : baseLevelData;
            }

            return baseLevelData;
        }

        /// <summary>Finds variant metadata for a base level, or null.</summary>
        public LevelVariantEntry GetVariantEntry(LevelData baseLevelData)
        {
            if (!baseLevelData || variantEntries == null)
                return null;

            for (int i = 0; i < variantEntries.Length; i++)
            {
                LevelVariantEntry entry = variantEntries[i];
                if (entry != null && entry.BaseLevel == baseLevelData)
                    return entry;
            }

            return null;
        }

        /// <summary>
        /// Build plan used by <see cref="LevelJsonAddressableBuilder"/>: for each slot up to
        /// <see cref="GetMaxLevel"/>, which <see cref="LevelData"/> is copied into
        /// <see cref="LevelSystemUtils.ActiveLevelsFolder"/>.
        /// </summary>
        public LevelActiveBuildSlotInfo[] ComputeActiveBuildSlots()
        {
            if (levels == null || levels.Length == 0)
                return Array.Empty<LevelActiveBuildSlotInfo>();

            int includedCount = Mathf.Min(GetMaxLevel(), levels.Length);
            var slots = new List<LevelActiveBuildSlotInfo>(includedCount);

            for (int i = 0; i < includedCount; i++)
            {
                LevelData baseLevel = levels[i];
                if (!baseLevel)
                    continue;

                int slotNumber = i + 1;
                LevelData sourceLevel = GetActiveVariantForLevel(baseLevel);
                if (!sourceLevel)
                    sourceLevel = baseLevel;

                LevelVariantEntry entry = GetVariantEntry(baseLevel);
                int activeVariantIndex = entry != null ? entry.ActiveVariantIndex : -1;

                slots.Add(new LevelActiveBuildSlotInfo(
                    slotNumber,
                    baseLevel,
                    sourceLevel,
                    LevelSystemUtils.GetActiveLevelSlotAssetPath(slotNumber),
                    activeVariantIndex));
            }

            return slots.ToArray();
        }

        /// <summary>Short label for list UI, e.g. " [V1]" when the first variant is active for build. Matches Level Editor toolbar (V1, V2, …); empty for base (V0).</summary>
        public string Editor_GetVariantListSuffix(LevelData baseLevelData)
        {
            LevelVariantEntry entry = GetVariantEntry(baseLevelData);
            if (entry == null || entry.Variants == null || entry.Variants.Length == 0)
                return string.Empty;

            int idx = entry.ActiveVariantIndex;
            if (idx < 0 || idx >= entry.Variants.Length)
                return string.Empty;

            LevelData variant = entry.Variants[idx];
            if (!variant)
                return string.Empty;

            return $" [V{idx + 1}]";
        }

        [Button]
        private void Validate()
        {
            foreach (LevelData level in levels)
            {
                level.Validate();
            }

            RuntimeEditorUtils.SetDirty(this);
        }
#endif
        
        /// <summary>
        /// Is called when LevelController is initialized
        /// </summary>
        public void Init()
        {
            ChainManager.Init();
            ColorManager.Init();
        }
        
        public int GetRandomLevelIndex(int displayLevelNumber, IReadOnlyList<int> recentLevelIndexes, bool forceRandom)
        {
            int levelCount = GetMaxLevel();
            if (!forceRandom && displayLevelNumber >= 0 && displayLevelNumber < levelCount)
                return displayLevelNumber;

            return PickRandomizerLevelIndex(levelCount, recentLevelIndexes);
        }

        public LevelData GetRandomLevel()
        {
            int randomLevelIndex = PickRandomizerLevelIndex(GetMaxLevel(), null);
#if UNITY_EDITOR
            if (randomLevelIndex >= 0 && randomLevelIndex < levels.Length)
                return levels[randomLevelIndex];
            return null;
#else
            return LevelActiveLevelLoader.Load(randomLevelIndex);
#endif
        }

        /// <summary>
        /// Picks a random level index eligible for the randomizer (UseInRandomizer, none of the
        /// recently played ones). Reads baked <see cref="LevelRuntimeInfo"/> only — never loads level
        /// assets. Gives up after 100 attempts and returns the last pick so a misconfigured database
        /// (no randomizer levels) cannot hang the game.
        /// </summary>
        private int PickRandomizerLevelIndex(int levelCount, IReadOnlyList<int> recentLevelIndexes)
        {
            int excludeCount = recentLevelIndexes == null
                ? 0
                : Mathf.Clamp(recentLevelIndexes.Count, 0, Mathf.Max(0, levelCount - 1));

            int randomLevelIndex;
            int attempts = 0;

            do
            {
                randomLevelIndex = Random.Range(0, levelCount);

                attempts++;
                if (attempts > 100)
                    return randomLevelIndex;
            }
            while (IsRecentlyPlayed(randomLevelIndex, recentLevelIndexes, excludeCount)
                   || !IsUsableInRandomizer(randomLevelIndex));

            return randomLevelIndex;
        }

        private static bool IsRecentlyPlayed(int levelIndex, IReadOnlyList<int> recentLevelIndexes, int excludeCount)
        {
            if (excludeCount <= 0)
                return false;

            for (int i = recentLevelIndexes.Count - excludeCount; i < recentLevelIndexes.Count; i++)
            {
                if (recentLevelIndexes[i] == levelIndex)
                    return true;
            }

            return false;
        }

        private bool IsUsableInRandomizer(int index)
        {
            return TryGetLevelRuntimeInfo(index, out _, out bool useInRandomizer) && useInRandomizer;
        }

        /// <summary>
        /// Resolves per-level facts without loading the full LevelData asset. Priority: remote
        /// bundle index (remote levels override local metadata) → array baked at build time →
        /// legacy synchronous Addressables load (only when the baked array is missing/stale).
        /// </summary>
        public bool TryGetLevelRuntimeInfo(int index, out LevelType type, out bool useInRandomizer)
        {
            type = LevelType.Normal;
            useInRandomizer = false;

            if (index < 0)
                return false;

#if UNITY_EDITOR
            if (levels != null && index < levels.Length && levels[index])
            {
                type = levels[index].Type;
                useInRandomizer = levels[index].UseInRandomizer;
                return true;
            }
#endif

            if (runtimeLevelInfos != null && index < runtimeLevelInfos.Length)
            {
                type = runtimeLevelInfos[index].Type;
                useInRandomizer = runtimeLevelInfos[index].UseInRandomizer;
                return true;
            }

            LevelData level = LevelActiveLevelLoader.Load(index);
            if (!level)
                return false;

            type = level.Type;
            useInRandomizer = level.UseInRandomizer;
            return true;
        }

#if UNITY_EDITOR
        public void Editor_SetRuntimeLevelInfos(LevelRuntimeInfo[] infos)
        {
            runtimeLevelInfos = infos ?? Array.Empty<LevelRuntimeInfo>();
        }
#endif

        public int GetMaxLevel()
        {
            return levelGeneralConfigData.MaxLevel;
        }
        
        public LevelType GetLevelType(int index)
        {
            return TryGetLevelRuntimeInfo(index, out LevelType type, out _) ? type : LevelType.Normal;
        }

#if UNITY_EDITOR
        public LevelData GetLevelDirectly(int index)
        {
            if (index < 0 || index >= levels.Length)
            {
                Debug.LogError($"Invalid level index {index}. Total levels: {levels.Length}");
                return null;
            }

            LevelData baseLevel = levels[index];
            if (Application.isPlaying)
            {
                LevelData overrideLevel = TryGetEditorPlayModeLevelOverride(index);
                if (overrideLevel)
                    return overrideLevel;
            }

            return GetActiveVariantForLevel(baseLevel);
        }
#endif

        public LevelData GetLevel(int index)
        {
#if UNITY_EDITOR
            return GetLevelDirectly(index);
#else
            return LevelActiveLevelLoader.Load(index);
#endif
        }
        
        public LevelBlockEffectData GetEffectData(BlockEffectType effectType)
        {
            foreach (LevelBlockEffectData effect in effects)
            {
#if UNITY_EDITOR
               if (!effect.Behavior)
               {
                   Debug.LogError($"Block effect behavior for {effect.Type} is missing or destroyed. Assign a prefab on the LevelDatabase asset.", this);
                   continue;
               }
#endif
                if (effect.Type == effectType)
                    return effect;
            }
            Debug.LogError($"Effect data for {effectType} not found in level database. Please check the LevelDatabase asset.", this);
            return null;
        }

        public LevelGateEffectData GetGateEffectData(GateEffectType effectType)
        {
            foreach (LevelGateEffectData effect in gateEffects)
            {
#if UNITY_EDITOR
               if (!effect.Behavior)
               {
                   Debug.LogError($"Gate effect behavior for {effect.Type} is missing or destroyed. Assign a prefab on the LevelDatabase asset.", this);
                   continue;
               }
#endif
                if (effect.Type == effectType)
                    return effect;
            }
            
            Debug.LogError($"Gate effect data for {effectType} not found in level database. Please check the LevelDatabase asset.", this);
            return null;
        }

        public LevelInteractableObjectData GetInteractableObjectData(InteractableObjectType objectType)
        {
            foreach (LevelInteractableObjectData interactableObject in interactableObjects)
            {
#if UNITY_EDITOR
                if (!interactableObject.Behavior)
                {
                    Debug.LogError($"Interactable object for {interactableObject.Type} is missing or destroyed. Assign a prefab on the LevelDatabase asset.", this);
                    continue;
                }
#endif
                if (interactableObject.Type == objectType)
                    return interactableObject;
            }

            Debug.LogError($"Interactable object data for {objectType} not found in level database. Please check the LevelDatabase asset.", this);

            return null;
        }

        /// <summary>Returns true if the level at <paramref name="index"/> contains at least one Generator element.</summary>
        public bool LevelHasGenerator(int index)
        {
#if UNITY_EDITOR
            LevelData level = GetLevelDirectly(index);
#else
            LevelData level = LevelActiveLevelLoader.Load(index);
#endif
            if (level?.Elements == null) return false;
            foreach (LevelElementData element in level.Elements)
            {
                if (element is GeneratorLevelElementData) return true;
            }
            return false;
        }

#if UNITY_EDITOR
        private const string LEVEL_PREFIX = "Level ";
        private const string ASSET_SUFFIX = ".asset";
        private const string PATH_SEPARATOR = "/";

        /// <summary>
        /// Quét thư mục levels, load tất cả level asset và gán vào mảng levels (chỉ Editor).
        /// </summary>
        public void Editor_PopulateLevels(string levelsFolderPath)
        {
            string searchPattern = LEVEL_PREFIX + "*" + ASSET_SUFFIX;
            string fullPath = Path.Combine(Application.dataPath.Replace("Assets", ""), levelsFolderPath);

            if (!Directory.Exists(fullPath))
            {
                Debug.LogError("Levels folder not found: " + fullPath);
                return;
            }

            string[] files = Directory.GetFiles(fullPath, searchPattern);
            Dictionary<int, string> levelPaths = new Dictionary<int, string>();

            foreach (string filePath in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                if (!fileName.StartsWith(LEVEL_PREFIX)) continue;

                string numberPart = fileName.Substring(LEVEL_PREFIX.Length);
                if (!int.TryParse(numberPart, out int levelNumber)) continue;

                string relativePath = levelsFolderPath + PATH_SEPARATOR + Path.GetFileName(filePath);
                levelPaths[levelNumber] = relativePath;
            }

            var sortedPaths = levelPaths.OrderBy(kvp => kvp.Key).ToList();
            SerializedObject so = new SerializedObject(this);
            SerializedProperty levelsProp = so.FindProperty("levels");
            if (levelsProp == null) return;

            List<LevelData> loadedLevels = new List<LevelData>();
            levelsProp.arraySize = sortedPaths.Count;
            for (int i = 0; i < sortedPaths.Count; i++)
            {
                LevelData levelAsset = AssetDatabase.LoadAssetAtPath<LevelData>(sortedPaths[i].Value);
                levelsProp.GetArrayElementAtIndex(i).objectReferenceValue = levelAsset;
                loadedLevels.Add(levelAsset);
            }
            so.ApplyModifiedProperties();

            Editor_PruneVariantEntries();

            Validate();

            EditorUtility.SetDirty(this);
        }

        /// <summary>Removes variant entries whose base level is no longer in the <see cref="levels"/> array.</summary>
        public void Editor_PruneVariantEntries()
        {
            if (variantEntries == null || variantEntries.Length == 0 || levels == null)
                return;

            HashSet<LevelData> validBases = new HashSet<LevelData>();
            foreach (LevelData l in levels)
            {
                if (l)
                    validBases.Add(l);
            }

            List<LevelVariantEntry> kept = new List<LevelVariantEntry>();
            foreach (LevelVariantEntry e in variantEntries)
            {
                if (e != null && e.BaseLevel && validBases.Contains(e.BaseLevel))
                    kept.Add(e);
            }

            if (kept.Count == variantEntries.Length)
                return;

            variantEntries = kept.ToArray();
            EditorUtility.SetDirty(this);
        }

        /// <summary>Removes variant metadata for a base level (does not delete variant asset files).</summary>
        public void Editor_RemoveVariantEntryForBase(LevelData baseLevelData)
        {
            if (!baseLevelData || variantEntries == null || variantEntries.Length == 0)
                return;

            List<LevelVariantEntry> kept = new List<LevelVariantEntry>();
            foreach (LevelVariantEntry e in variantEntries)
            {
                if (e == null || e.BaseLevel == baseLevelData)
                    continue;
                kept.Add(e);
            }

            if (kept.Count == variantEntries.Length)
                return;

            variantEntries = kept.ToArray();
            EditorUtility.SetDirty(this);
        }

        /// <summary>Palette color from <see cref="editorColorData"/> for level editor UI (inspector pickers).</summary>
        public Color Editor_GetBlockEditorPaletteColor(BlockColor blockColor)
        {
            if (blockColor == BlockColor.None)
                return new Color(0.38f, 0.38f, 0.38f, 1f);

            if (editorColorData == null)
                return new Color(0.55f, 0.55f, 0.55f, 1f);

            for (int i = 0; i < editorColorData.Length; i++)
            {
                EditorColorData entry = editorColorData[i];
                if (entry == null || entry.Type != blockColor)
                    continue;
                return entry.PaletteColor;
            }

            return new Color(0.55f, 0.55f, 0.55f, 1f);
        }
#endif
    }
}
