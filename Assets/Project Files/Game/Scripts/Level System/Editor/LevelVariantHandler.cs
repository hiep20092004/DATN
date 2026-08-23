using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WaterFlow.Game
{
    /// <summary>Editor CRUD for <see cref="LevelDatabase"/> level variants (separate <see cref="LevelData"/> assets under Levels/Variants).</summary>
    public static class LevelDataUtilities
    {
        private const string VARIANT_ENTRIES_PROPERTY = "variantEntries";
        private const string BASE_LEVEL_PROPERTY = "baseLevel";
        private const string VARIANTS_PROPERTY = "variants";
        private const string ACTIVE_INDEX_PROPERTY = "activeVariantIndex";
        private const string VARIANTS_FOLDER = "Variants";
        private const string LEVEL_PREFIX = "Level ";

        public static SerializedProperty FindVariantEntriesProperty(SerializedObject databaseSo)
        {
            return databaseSo.FindProperty(VARIANT_ENTRIES_PROPERTY);
        }

        /// <summary>Finds array index of entry for <paramref name="baseLevel"/>, or -1.</summary>
        public static int FindEntryIndex(SerializedProperty entriesProp, LevelData baseLevel)
        {
            if (entriesProp == null || !baseLevel)
                return -1;

            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                SerializedProperty el = entriesProp.GetArrayElementAtIndex(i);
                SerializedProperty b = el.FindPropertyRelative(BASE_LEVEL_PROPERTY);
                if (b != null && b.objectReferenceValue == baseLevel)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// If <paramref name="slotAsset"/> is a registered base level, returns it; if it appears as a variant of some base, returns that base.
        /// Otherwise returns <paramref name="slotAsset"/> (keeps variant UI working when <see cref="LevelDatabase.levels"/> slot points at a variant asset).
        /// </summary>
        public static LevelData ResolveBaseForSlotAsset(LevelDatabase database, LevelData slotAsset)
        {
            if (!database || !slotAsset)
                return slotAsset;

            if (database.GetVariantEntry(slotAsset) != null)
                return slotAsset;

            LevelVariantEntry[] raw = database.VariantEntries;
            if (raw == null)
                return slotAsset;

            foreach (LevelVariantEntry e in raw)
            {
                if (e?.BaseLevel == null || e.Variants == null)
                    continue;

                foreach (LevelData v in e.Variants)
                {
                    if (v == slotAsset)
                        return e.BaseLevel;
                }
            }

            return slotAsset;
        }

        /// <summary>
        /// If variant <c>.asset</c> files exist under <c>…/Variants/</c> for this base level but are missing from
        /// <see cref="LevelDatabase"/>, append them to <see cref="LevelVariantEntry.Variants"/> (sorted by V number).
        /// Fixes editor UI when assets were copied or the entry was removed while files remain.
        /// </summary>
        public static bool TryMergeVariantAssetsFromDisk(
            SerializedObject databaseSo,
            LevelData baseLevel,
            string levelsFolderPath)
        {
            if (!baseLevel || databaseSo == null || string.IsNullOrEmpty(levelsFolderPath))
                return false;

            string variantsFolder = levelsFolderPath.TrimEnd('/', '\\').Replace('\\', '/') + "/" + VARIANTS_FOLDER;
            string variantsFolderFull = ToFullProjectPath(variantsFolder);
            if (!Directory.Exists(variantsFolderFull))
                return false;

            string baseName = baseLevel.name;
            if (!baseName.StartsWith(LEVEL_PREFIX, StringComparison.Ordinal))
                baseName = LEVEL_PREFIX + baseName;

            Regex re = new Regex("^" + Regex.Escape(baseName) + @" V(\d+)$", RegexOptions.IgnoreCase);

            var ordered = new List<(int num, string relPath)>();
            foreach (string file in Directory.GetFiles(variantsFolderFull, "*.asset", SearchOption.TopDirectoryOnly))
            {
                string fn = Path.GetFileNameWithoutExtension(file);
                Match m = re.Match(fn);
                if (!m.Success || !int.TryParse(m.Groups[1].Value, out int num))
                    continue;

                string relPath = (variantsFolder + "/" + Path.GetFileName(file)).Replace('\\', '/');
                if (relPath.StartsWith("Assets/", StringComparison.Ordinal))
                    ordered.Add((num, relPath));
            }

            if (ordered.Count == 0)
                return false;

            ordered.Sort((a, b) => a.num.CompareTo(b.num));

            SerializedProperty entries = FindVariantEntriesProperty(databaseSo);
            if (entries == null)
                return false;

            bool recordedUndo = false;
            bool appended = false;

            foreach (var (_, relPath) in ordered)
            {
                LevelData asset = AssetDatabase.LoadAssetAtPath<LevelData>(relPath);
                if (!asset)
                    continue;

                int entryIdx = FindEntryIndex(entries, baseLevel);
                if (entryIdx >= 0)
                {
                    SerializedProperty variantsProp =
                        entries.GetArrayElementAtIndex(entryIdx).FindPropertyRelative(VARIANTS_PROPERTY);
                    if (VariantArrayContains(variantsProp, asset))
                        continue;
                }

                if (!recordedUndo)
                {
                    Undo.RecordObject(databaseSo.targetObject, "Link level variants from folder");
                    recordedUndo = true;
                }

                SerializedProperty entry = GetOrCreateEntryProperty(databaseSo, baseLevel);
                SerializedProperty variantsProp2 = entry.FindPropertyRelative(VARIANTS_PROPERTY);
                if (variantsProp2 == null)
                    break;

                if (VariantArrayContains(variantsProp2, asset))
                    continue;

                int v = variantsProp2.arraySize;
                variantsProp2.InsertArrayElementAtIndex(v);
                variantsProp2.GetArrayElementAtIndex(v).objectReferenceValue = asset;
                appended = true;
            }

            if (appended)
            {
                databaseSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(databaseSo.targetObject);
            }

            return appended;
        }

        private static bool VariantArrayContains(SerializedProperty variantsProp, LevelData asset)
        {
            if (variantsProp == null || !asset)
                return false;

            for (int i = 0; i < variantsProp.arraySize; i++)
            {
                if (variantsProp.GetArrayElementAtIndex(i).objectReferenceValue == asset)
                    return true;
            }

            return false;
        }

        public static SerializedProperty GetOrCreateEntryProperty(SerializedObject databaseSo, LevelData baseLevel)
        {
            SerializedProperty entries = FindVariantEntriesProperty(databaseSo);
            if (entries == null)
                return null;

            int idx = FindEntryIndex(entries, baseLevel);
            if (idx >= 0)
                return entries.GetArrayElementAtIndex(idx);

            int newIndex = entries.arraySize;
            entries.InsertArrayElementAtIndex(newIndex);
            SerializedProperty newEl = entries.GetArrayElementAtIndex(newIndex);
            SerializedProperty baseProp = newEl.FindPropertyRelative(BASE_LEVEL_PROPERTY);
            if (baseProp != null)
                baseProp.objectReferenceValue = baseLevel;
            SerializedProperty variantsProp = newEl.FindPropertyRelative(VARIANTS_PROPERTY);
            if (variantsProp != null)
                variantsProp.arraySize = 0;
            SerializedProperty activeProp = newEl.FindPropertyRelative(ACTIVE_INDEX_PROPERTY);
            if (activeProp != null)
                activeProp.intValue = -1;

            return newEl;
        }

        public static void SetActiveVariantIndex(SerializedObject databaseSo, LevelData baseLevel, int activeIndex)
        {
            SerializedProperty entries = FindVariantEntriesProperty(databaseSo);
            if (entries == null)
                return;

            int idx = FindEntryIndex(entries, baseLevel);
            if (idx < 0)
                return;

            SerializedProperty el = entries.GetArrayElementAtIndex(idx);
            SerializedProperty activeProp = el.FindPropertyRelative(ACTIVE_INDEX_PROPERTY);
            SerializedProperty variantsProp = el.FindPropertyRelative(VARIANTS_PROPERTY);
            if (activeProp == null)
                return;

            if (activeIndex < -1)
                activeIndex = -1;
            if (variantsProp != null && activeIndex >= variantsProp.arraySize)
                activeIndex = variantsProp.arraySize > 0 ? variantsProp.arraySize - 1 : -1;

            activeProp.intValue = activeIndex;
            databaseSo.ApplyModifiedProperties();
        }

        /// <summary>
        /// Sets the build-active variant index on every level that has variant metadata.
        /// When <paramref name="targetVariantArrayIndex"/> is &gt;= 0 and a level has no variant at that index, uses base (V0, -1).
        /// When <paramref name="targetVariantArrayIndex"/> is -1, sets all to base (V0).
        /// </summary>
        public static bool ApplyActiveVariantIndexToAllLevels(
            SerializedObject databaseSo,
            LevelDatabase database,
            SerializedProperty levelsProp,
            int targetVariantArrayIndex)
        {
            SerializedProperty entries = FindVariantEntriesProperty(databaseSo);
            if (entries == null || !database || levelsProp == null)
                return false;

            bool changed = false;
            Undo.RecordObject(databaseSo.targetObject, "Apply variant to all levels");

            for (int slot = 0; slot < levelsProp.arraySize; slot++)
            {
                LevelData slotAsset = levelsProp.GetArrayElementAtIndex(slot).objectReferenceValue as LevelData;
                if (!slotAsset)
                    continue;

                LevelData baseLevel = ResolveBaseForSlotAsset(database, slotAsset);
                int entryIdx = FindEntryIndex(entries, baseLevel);
                if (entryIdx < 0)
                    continue;

                SerializedProperty entry = entries.GetArrayElementAtIndex(entryIdx);
                SerializedProperty variantsProp = entry.FindPropertyRelative(VARIANTS_PROPERTY);
                SerializedProperty activeProp = entry.FindPropertyRelative(ACTIVE_INDEX_PROPERTY);
                if (activeProp == null)
                    continue;

                int newIndex;
                if (targetVariantArrayIndex < 0)
                {
                    newIndex = -1;
                }
                else if (variantsProp != null
                         && targetVariantArrayIndex < variantsProp.arraySize
                         && variantsProp.GetArrayElementAtIndex(targetVariantArrayIndex).objectReferenceValue != null)
                {
                    newIndex = targetVariantArrayIndex;
                }
                else
                {
                    newIndex = -1;
                }

                if (variantsProp != null && newIndex >= variantsProp.arraySize)
                    newIndex = variantsProp.arraySize > 0 ? variantsProp.arraySize - 1 : -1;
                if (newIndex < -1)
                    newIndex = -1;

                if (activeProp.intValue == newIndex)
                    continue;

                activeProp.intValue = newIndex;
                changed = true;
            }

            if (changed)
            {
                databaseSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(databaseSo.targetObject);
            }

            return changed;
        }

        /// <summary>Creates a new variant asset (deep copy of <paramref name="sourceForCopy"/>), appends to entry, does not change active index.</summary>
        public static LevelData AddVariantAsset(
            SerializedObject databaseSo,
            LevelData baseLevel,
            LevelData sourceForCopy,
            string levelsFolderPath)
        {
            if (!baseLevel || !sourceForCopy || string.IsNullOrEmpty(levelsFolderPath))
                return null;

            string variantsFolder = levelsFolderPath.TrimEnd('/') + "/" + VARIANTS_FOLDER;
            string variantsFolderFull = ToFullProjectPath(variantsFolder);
            if (!Directory.Exists(variantsFolderFull))
                Directory.CreateDirectory(variantsFolderFull);

            string baseName = baseLevel.name;
            if (!baseName.StartsWith(LEVEL_PREFIX, StringComparison.Ordinal))
                baseName = LEVEL_PREFIX + baseName;

            string nextAssetName = GetNextVariantAssetName(variantsFolder, baseName);
            string assetPath = variantsFolder + "/" + nextAssetName + ".asset";

            Undo.RecordObject(databaseSo.targetObject, "Add Level Variant");

            LevelData newLevel = ScriptableObject.CreateInstance<LevelData>();
            string json = JsonUtility.ToJson(sourceForCopy, false);
            JsonUtility.FromJsonOverwrite(json, newLevel);
            AssetDatabase.CreateAsset(newLevel, assetPath);
            newLevel.Validate();
            EditorUtility.SetDirty(newLevel);

            SerializedProperty entry = GetOrCreateEntryProperty(databaseSo, baseLevel);
            SerializedProperty variantsProp = entry.FindPropertyRelative(VARIANTS_PROPERTY);
            if (variantsProp == null)
            {
                AssetDatabase.DeleteAsset(assetPath);
                return null;
            }

            int v = variantsProp.arraySize;
            variantsProp.InsertArrayElementAtIndex(v);
            variantsProp.GetArrayElementAtIndex(v).objectReferenceValue = newLevel;

            databaseSo.Update();
            databaseSo.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            return newLevel;
        }

        public static void CopyLevelDataToClipboard(LevelData data)
        {
            if (!data)
                return;

            EditorGUIUtility.systemCopyBuffer = LevelDataSerializationUtilities.ToCompressedString(data);
        }

        public static bool PasteClipboardOntoLevelData(LevelData target, out string error)
        {
            error = null;
            if (!target)
            {
                error = "No level asset.";
                return false;
            }

            string clipboard = EditorGUIUtility.systemCopyBuffer;
            if (string.IsNullOrWhiteSpace(clipboard))
            {
                error = "Clipboard is empty.";
                return false;
            }

            Undo.RecordObject(target, "Paste Level Data");
            if (!LevelDataSerializationUtilities.TryApplyString(clipboard, target, out error))
                return false;

            EditorUtility.SetDirty(target);
            return true;
        }

        public static void RemoveVariantAt(
            SerializedObject databaseSo,
            LevelData baseLevel,
            int variantArrayIndex,
            bool deleteAsset)
        {
            SerializedProperty entries = FindVariantEntriesProperty(databaseSo);
            if (entries == null || !baseLevel)
                return;

            int entryIdx = FindEntryIndex(entries, baseLevel);
            if (entryIdx < 0)
                return;

            SerializedProperty entry = entries.GetArrayElementAtIndex(entryIdx);
            SerializedProperty variantsProp = entry.FindPropertyRelative(VARIANTS_PROPERTY);
            SerializedProperty activeProp = entry.FindPropertyRelative(ACTIVE_INDEX_PROPERTY);
            if (variantsProp == null || variantArrayIndex < 0 || variantArrayIndex >= variantsProp.arraySize)
                return;

            Object toRemove = variantsProp.GetArrayElementAtIndex(variantArrayIndex).objectReferenceValue;
            string path = toRemove ? AssetDatabase.GetAssetPath(toRemove) : null;

            Undo.RecordObject(databaseSo.targetObject, "Remove Level Variant");

            // Unity SerializedProperty quirk: deleting a non-last ObjectReference array element does NOT shrink
            // the array on the first call — it replaces that slot with null (fileID: 0). A second delete at the
            // same index removes the placeholder. See Unity docs for DeleteArrayElementAtIndex.
            int sizeBefore = variantsProp.arraySize;
            variantsProp.DeleteArrayElementAtIndex(variantArrayIndex);
            if (variantsProp.arraySize == sizeBefore)
                variantsProp.DeleteArrayElementAtIndex(variantArrayIndex);

            if (activeProp != null)
            {
                if (activeProp.intValue == variantArrayIndex)
                    activeProp.intValue = -1;
                else if (activeProp.intValue > variantArrayIndex)
                    activeProp.intValue--;
            }

            databaseSo.ApplyModifiedProperties();

            if (deleteAsset && !string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.SaveAssets();
            }

            databaseSo.Update();
            SerializedProperty entriesAfter = FindVariantEntriesProperty(databaseSo);
            int idxAfter = FindEntryIndex(entriesAfter, baseLevel);
            if (idxAfter >= 0)
            {
                SerializedProperty variantsAfter =
                    entriesAfter.GetArrayElementAtIndex(idxAfter).FindPropertyRelative(VARIANTS_PROPERTY);
                if (variantsAfter == null || variantsAfter.arraySize == 0)
                    RemoveEntryAt(databaseSo, idxAfter);
            }
        }

        /// <summary>
        /// Removes null / missing references from every <c>variants</c> array and fixes <c>activeVariantIndex</c>.
        /// Use after legacy deletes or hand-edited YAML left <c>{fileID: 0}</c> gaps.
        /// </summary>
        public static bool PruneNullVariantReferences(SerializedObject databaseSo)
        {
            SerializedProperty entries = FindVariantEntriesProperty(databaseSo);
            if (entries == null)
                return false;

            bool changed = false;

            for (int e = 0; e < entries.arraySize; e++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(e);
                SerializedProperty variantsProp = entry.FindPropertyRelative(VARIANTS_PROPERTY);
                SerializedProperty activeProp = entry.FindPropertyRelative(ACTIVE_INDEX_PROPERTY);
                if (variantsProp == null)
                    continue;

                int oldSize = variantsProp.arraySize;
                int oldActive = activeProp != null ? activeProp.intValue : -1;
                LevelData oldActiveRef = null;
                if (oldActive >= 0 && oldActive < oldSize)
                    oldActiveRef = variantsProp.GetArrayElementAtIndex(oldActive).objectReferenceValue as LevelData;

                var keep = new List<Object>(oldSize);
                for (int i = 0; i < oldSize; i++)
                {
                    Object o = variantsProp.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (o)
                        keep.Add(o);
                }

                if (keep.Count == oldSize)
                    continue;

                variantsProp.arraySize = keep.Count;
                for (int i = 0; i < keep.Count; i++)
                    variantsProp.GetArrayElementAtIndex(i).objectReferenceValue = keep[i];

                if (activeProp != null)
                {
                    if (oldActive < 0)
                        activeProp.intValue = -1;
                    else if (oldActiveRef)
                    {
                        int ni = keep.IndexOf(oldActiveRef);
                        activeProp.intValue = ni >= 0 ? ni : (keep.Count > 0 ? 0 : -1);
                    }
                    else
                        activeProp.intValue = keep.Count > 0 ? 0 : -1;
                }

                changed = true;
            }

            if (changed)
            {
                databaseSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(databaseSo.targetObject);
            }

            return changed;
        }

        /// <summary>
        /// Removes variants whose asset name does not belong to their entry's base level — the name
        /// must match <c>^&lt;baseLevel.name&gt; V\d+$</c>. Fixes entries left mis-owned after level
        /// renumbering/reordering: <see cref="TryMergeVariantAssetsFromDisk"/> is append-only and only
        /// matches by the base name, so it stacks correct variants on top of stale foreign ones without
        /// ever dropping them. Null refs are left for <see cref="PruneNullVariantReferences"/>. Fixes
        /// <c>activeVariantIndex</c> to follow the kept entries.
        /// </summary>
        public static bool PruneMisownedVariantReferences(SerializedObject databaseSo)
        {
            SerializedProperty entries = FindVariantEntriesProperty(databaseSo);
            if (entries == null)
                return false;

            bool changed = false;

            for (int e = 0; e < entries.arraySize; e++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(e);
                SerializedProperty baseProp = entry.FindPropertyRelative(BASE_LEVEL_PROPERTY);
                SerializedProperty variantsProp = entry.FindPropertyRelative(VARIANTS_PROPERTY);
                SerializedProperty activeProp = entry.FindPropertyRelative(ACTIVE_INDEX_PROPERTY);
                if (variantsProp == null)
                    continue;

                LevelData baseLevel = baseProp?.objectReferenceValue as LevelData;
                if (!baseLevel)
                    continue;

                string baseName = baseLevel.name;
                if (!baseName.StartsWith(LEVEL_PREFIX, StringComparison.Ordinal))
                    baseName = LEVEL_PREFIX + baseName;
                Regex re = new Regex("^" + Regex.Escape(baseName) + @" V(\d+)$", RegexOptions.IgnoreCase);

                int oldSize = variantsProp.arraySize;
                int oldActive = activeProp != null ? activeProp.intValue : -1;
                LevelData oldActiveRef = null;
                if (oldActive >= 0 && oldActive < oldSize)
                    oldActiveRef = variantsProp.GetArrayElementAtIndex(oldActive).objectReferenceValue as LevelData;

                var keep = new List<Object>(oldSize);
                for (int i = 0; i < oldSize; i++)
                {
                    Object o = variantsProp.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (!o || re.IsMatch(o.name))
                        keep.Add(o);
                }

                if (keep.Count == oldSize)
                    continue;

                variantsProp.arraySize = keep.Count;
                for (int i = 0; i < keep.Count; i++)
                    variantsProp.GetArrayElementAtIndex(i).objectReferenceValue = keep[i];

                if (activeProp != null)
                {
                    if (oldActive < 0)
                        activeProp.intValue = -1;
                    else if (oldActiveRef)
                    {
                        int ni = keep.IndexOf(oldActiveRef);
                        activeProp.intValue = ni >= 0 ? ni : (keep.Count > 0 ? 0 : -1);
                    }
                    else
                        activeProp.intValue = keep.Count > 0 ? 0 : -1;
                }

                changed = true;
            }

            if (changed)
            {
                databaseSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(databaseSo.targetObject);
            }

            return changed;
        }

        private static void RemoveEntryAt(SerializedObject databaseSo, int entryIndex)
        {
            SerializedProperty entries = FindVariantEntriesProperty(databaseSo);
            if (entries == null || entryIndex < 0 || entryIndex >= entries.arraySize)
                return;

            entries.DeleteArrayElementAtIndex(entryIndex);
            databaseSo.ApplyModifiedProperties();
        }

        /// <summary>When deleting a base level from the database, optionally delete all variant assets and remove the entry.</summary>
        public static void OnBaseLevelDeletedFromDatabase(SerializedObject databaseSo, LevelData baseLevel,
            bool deleteVariantAssets)
        {
            databaseSo.Update();
            SerializedProperty entries = FindVariantEntriesProperty(databaseSo);
            if (entries == null || !baseLevel)
                return;

            int entryIdx = FindEntryIndex(entries, baseLevel);
            if (entryIdx < 0)
                return;

            SerializedProperty entry = entries.GetArrayElementAtIndex(entryIdx);
            SerializedProperty variantsProp = entry.FindPropertyRelative(VARIANTS_PROPERTY);

            if (deleteVariantAssets && variantsProp != null)
            {
                for (int i = variantsProp.arraySize - 1; i >= 0; i--)
                {
                    Object o = variantsProp.GetArrayElementAtIndex(i).objectReferenceValue;
                    string path = o ? AssetDatabase.GetAssetPath(o) : null;
                    if (!string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.Ordinal))
                        AssetDatabase.DeleteAsset(path);
                }
            }

            Undo.RecordObject(databaseSo.targetObject, "Remove Level Variant Entry");
            entries.DeleteArrayElementAtIndex(entryIdx);
            databaseSo.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        private static string GetNextVariantAssetName(string variantsFolderProject, string baseLevelAssetName)
        {
            string folderFull = ToFullProjectPath(variantsFolderProject);
            int max = 0;
            if (Directory.Exists(folderFull))
            {
                string pattern = "^" + Regex.Escape(baseLevelAssetName) + @" V(\d+)$";
                Regex re = new Regex(pattern, RegexOptions.IgnoreCase);
                foreach (string file in Directory.GetFiles(folderFull, "*.asset", SearchOption.TopDirectoryOnly))
                {
                    string fn = Path.GetFileNameWithoutExtension(file);
                    Match m = re.Match(fn);
                    if (m.Success && int.TryParse(m.Groups[1].Value, out int n))
                        max = Mathf.Max(max, n);
                }
            }

            int next = max + 1;
            return baseLevelAssetName + " V" + next.ToString("D2");
        }

        private static string ToFullProjectPath(string projectPath)
        {
            return Path.Combine(Application.dataPath.Replace("Assets", ""),
                projectPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}