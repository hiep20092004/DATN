using System;
using System.Collections.Generic;
using WaterFlow.Core;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
#if UNITY_EDITOR
    /// <summary>
    /// Property drawer for [SerializeReference] BlockEffectData (the new polymorphic type).
    /// Shows a numeric-enum preview + visual picker popup (aligned with <see cref="BlockEffectTypeDrawer"/>)
    /// followed by the default inspector for the selected concrete type.
    /// </summary>
    [CustomPropertyDrawer(typeof(BlockEffectData))]
    public class BlockEffectDataReferenceDrawer : PropertyDrawer
    {
        private static readonly Dictionary<Type, BlockEffectType> EffectTypeByManagedType = new Dictionary<Type, BlockEffectType>();
        private static List<Type> s_orderedConcreteTypes;
        private static GeneratorConfig s_config;
        private static LevelDatabase s_levelDatabase;

        [InitializeOnLoadMethod]
        private static void RegisterCacheInvalidation()
        {
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.projectChanged += OnProjectChanged;
        }

        private static void OnProjectChanged()
        {
            s_config = null;
            s_levelDatabase = null;
        }

        private static void DrawEffectPreview(Rect rect, BlockEffectType effectType)
        {
            EffectIconPreviewUtility.DrawEffectPreview(rect, effectType.ToString(), (int)effectType, effectType == BlockEffectType.None);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            BlockEffectType currentType = (property.managedReferenceValue as BlockEffectData)?.Type ?? BlockEffectType.None;

            EditorGUI.BeginProperty(position, label, property);

            Rect typeLineRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            DrawTypePicker(typeLineRect, property, currentType);

            if (property.managedReferenceValue != null)
            {
                float yOffset = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                Rect childRect = new Rect(position.x, position.y + yOffset, position.width, position.height - yOffset);
                SerializeReferenceDrawerUtility.DrawChildren(childRect, property);
            }

            EditorGUI.EndProperty();
        }

        // The type-picker filter (restriction + sibling duplicate/compatibility rules) is only consumed when the
        // popup opens on MouseDown. Building it eagerly per OnGUI was ~1ms + ~19KB GC per pass when a generator
        // queue draws dozens of effect slots — recomputed on every scene-view repaint, which caused editor lag.
        private static Func<Type, bool> BuildTypeFilter(SerializedProperty property, BlockEffectType currentType)
        {
            if (s_config == null)
                s_config = EditorUtils.GetAsset<GeneratorConfig>();
            GeneratorConfig config = s_config;

            bool restricted = IsGeneratorQueueBlockEffectPath(property.propertyPath) &&
                              config != null &&
                              config.RestrictsGeneratorBlockEffects;

            // Sibling effects already on this block: used to forbid duplicate types (always) and to apply the
            // pairwise compatibility matrix (only when configured). Skipped during multi-edit (ambiguous set).
            BlockEffectCompatibilityMatrix matrix = GetCompatibilityMatrix();
            List<BlockEffectType> siblings = property.hasMultipleDifferentValues
                ? null
                : CollectSiblingEffectTypes(property);
            bool hasSiblings = siblings != null && siblings.Count > 0;
            bool hasCompatibilityFilter = hasSiblings && matrix != null && matrix.IsConfigured;

            if (!restricted && !hasSiblings)
                return null;

            return t => PassesFilters(t, currentType, restricted, config, hasSiblings, hasCompatibilityFilter, matrix, siblings);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return SerializeReferenceDrawerUtility.GetPropertyHeight<BlockEffectData>(property, label);
        }

        private static void DrawTypePicker(Rect lineRect, SerializedProperty property, BlockEffectType currentType)
        {
            Rect rowRect = EditorGUI.PrefixLabel(lineRect, GUIUtility.GetControlID(FocusType.Passive), new GUIContent("Type"));

            if (property.hasMultipleDifferentValues)
            {
                if (Event.current.type == EventType.Repaint)
                {
                    EditorStyles.popup.Draw(rowRect, false, false, false, false);
                    Rect mixedPreview = new Rect(rowRect.x + 4f, rowRect.y + (rowRect.height - EffectIconPreviewUtility.PreviewSide) * 0.5f, EffectIconPreviewUtility.PreviewSide, EffectIconPreviewUtility.PreviewSide);
                    EditorGUI.DrawRect(mixedPreview, new Color(0.35f, 0.35f, 0.35f, 1f));
                    Rect mixedText = new Rect(mixedPreview.xMax + 8f, rowRect.y, rowRect.xMax - mixedPreview.xMax - 22f, rowRect.height);
                    GUI.Label(mixedText, "—", EditorStyles.label);
                }
                return;
            }

            if (Event.current.type == EventType.Repaint)
            {
                EditorStyles.popup.Draw(rowRect, false, false, false, false);
                Rect previewOuter = new Rect(rowRect.x + 4f, rowRect.y + (rowRect.height - EffectIconPreviewUtility.PreviewSide) * 0.5f, EffectIconPreviewUtility.PreviewSide, EffectIconPreviewUtility.PreviewSide);
                DrawEffectPreview(previewOuter, currentType);
                Rect textRect = new Rect(previewOuter.xMax + 8f, rowRect.y, rowRect.xMax - previewOuter.xMax - 22f, rowRect.height);
                GUI.Label(textRect, currentType.ToString(), EditorStyles.label);
            }

            if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
            {
                Func<Type, bool> typeFilter = BuildTypeFilter(property, currentType);
                PopupWindow.Show(rowRect, new BlockEffectDataPickerPopup(property, typeFilter));
                Event.current.Use();
            }
        }

        private static bool IsGeneratorQueueBlockEffectPath(string propertyPath)
        {
            return propertyPath.Contains($".{LevelAssetRepresentation.GENERATOR_QUEUE_PROPERTY_NAME}.") &&
                   propertyPath.Contains($".{GeneratorBlockEntry.BlockEffectsPropertyName}");
        }

        private static bool IsAllowedGeneratorEffectType(Type managedType, GeneratorConfig config, BlockEffectType currentType)
        {
            if (!TryGetEffectType(managedType, out BlockEffectType effectType))
                return false;

            if (effectType == currentType)
                return true;

            BlockEffectType[] allowed = config.AllowedBlockEffectsForQueue;
            if (allowed == null)
                return false;

            for (int i = 0; i < allowed.Length; i++)
            {
                if (allowed[i] == effectType)
                    return true;
            }

            return false;
        }

        private static bool PassesFilters(
            Type managedType,
            BlockEffectType currentType,
            bool restricted,
            GeneratorConfig config,
            bool hasDuplicateFilter,
            bool hasCompatibilityFilter,
            BlockEffectCompatibilityMatrix matrix,
            List<BlockEffectType> siblings)
        {
            if (!TryGetEffectType(managedType, out BlockEffectType effectType))
                return false;

            // Always allow keeping/re-selecting the current value so a populated slot is never stranded.
            if (effectType == currentType)
                return true;

            // A block may not carry two effects of the same type (siblings excludes the edited slot).
            if (hasDuplicateFilter && siblings.Contains(effectType))
                return false;

            if (restricted && !IsAllowedGeneratorEffectType(managedType, config, currentType))
                return false;

            if (hasCompatibilityFilter && !matrix.CanAddToBlock(effectType, siblings))
                return false;

            return true;
        }

        private static BlockEffectCompatibilityMatrix GetCompatibilityMatrix()
        {
            if (s_levelDatabase == null)
                s_levelDatabase = EditorUtils.GetAsset<LevelDatabase>();
            return s_levelDatabase != null ? s_levelDatabase.BlockEffectCompatibility : null;
        }

        /// <summary>Effect types already present in the same blockEffects array (excluding the edited slot).</summary>
        private static List<BlockEffectType> CollectSiblingEffectTypes(SerializedProperty property)
        {
            const string marker = ".Array.data[";
            string path = property.propertyPath;
            int arrIdx = path.LastIndexOf(marker, StringComparison.Ordinal);
            if (arrIdx < 0)
                return null;

            int numStart = arrIdx + marker.Length;
            int numEnd = path.IndexOf(']', numStart);
            if (numEnd < 0 || !int.TryParse(path.Substring(numStart, numEnd - numStart), out int currentIndex))
                return null;

            string arrayPath = path.Substring(0, arrIdx);
            SerializedProperty arrayProp = property.serializedObject.FindProperty(arrayPath);
            if (arrayProp == null || !arrayProp.isArray)
                return null;

            List<BlockEffectType> result = null;
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                if (i == currentIndex)
                    continue;
                if (arrayProp.GetArrayElementAtIndex(i).managedReferenceValue is BlockEffectData effect)
                    (result ??= new List<BlockEffectType>()).Add(effect.Type);
            }

            return result;
        }

        private static bool TryGetEffectType(Type managedType, out BlockEffectType effectType)
        {
            if (EffectTypeByManagedType.TryGetValue(managedType, out effectType))
                return true;

            if (Activator.CreateInstance(managedType) is not BlockEffectData effect)
            {
                effectType = BlockEffectType.None;
                return false;
            }

            effectType = effect.Type;
            EffectTypeByManagedType[managedType] = effectType;
            return true;
        }

        /// <summary>Concrete <see cref="BlockEffectData"/> types ordered by their <see cref="BlockEffectType"/> enum value.</summary>
        internal static List<Type> GetOrderedConcreteTypes()
        {
            if (s_orderedConcreteTypes != null)
                return s_orderedConcreteTypes;

            List<Type> result = new List<Type>();
            foreach (Type t in TypeCache.GetTypesDerivedFrom<BlockEffectData>())
            {
                if (t.IsAbstract || t.IsInterface)
                    continue;
                result.Add(t);
            }

            result.Sort((a, b) =>
            {
                TryGetEffectType(a, out BlockEffectType ea);
                TryGetEffectType(b, out BlockEffectType eb);
                int cmp = ((int)ea).CompareTo((int)eb);
                return cmp != 0 ? cmp : string.Compare(a.Name, b.Name, StringComparison.Ordinal);
            });

            s_orderedConcreteTypes = result;
            return s_orderedConcreteTypes;
        }

        private sealed class BlockEffectDataPickerPopup : PopupWindowContent
        {
            private const float RowHeight = 28f;
            private const float RowPreview = 24f;
            private const float RowPadding = 4f;

            private static GUIStyle s_rowLabel;
            private static GUIStyle s_rowLabelBold;

            private readonly SerializedObject serializedObject;
            private readonly string propertyPath;
            private readonly List<Type> rowTypes;
            private Vector2 scroll;

            private static void EnsureRowLabelStyles()
            {
                if (s_rowLabel != null)
                    return;

                s_rowLabel = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleLeft
                };
                s_rowLabelBold = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontStyle = FontStyle.Bold
                };
            }

            public BlockEffectDataPickerPopup(SerializedProperty property, Func<Type, bool> typeFilter)
            {
                serializedObject = property.serializedObject;
                propertyPath = property.propertyPath;

                List<Type> all = GetOrderedConcreteTypes();
                if (typeFilter == null)
                {
                    rowTypes = new List<Type>(all);
                }
                else
                {
                    rowTypes = new List<Type>(all.Count);
                    for (int i = 0; i < all.Count; i++)
                    {
                        if (typeFilter(all[i]))
                            rowTypes.Add(all[i]);
                    }
                }
            }

            public override Vector2 GetWindowSize()
            {
                int rowCount = rowTypes.Count + 1; // +1 for "(none)"
                float h = Mathf.Min(520f, 16f + rowCount * RowHeight + 16f);
                return new Vector2(320f, h);
            }

            public override void OnGUI(Rect rect)
            {
                // The captured SerializedObject can be invalidated while the popup is open (domain reload /
                // recompile / target destroyed). The C# wrapper stays non-null but its native backing is gone,
                // so even the targetObject getter throws NullReferenceException — guard the native access.
                if (!IsSerializedObjectAlive())
                {
                    editorWindow?.Close();
                    return;
                }

                serializedObject.Update();
                SerializedProperty prop = serializedObject.FindProperty(propertyPath);
                if (prop == null)
                {
                    editorWindow?.Close();
                    return;
                }

                Type currentType = prop.managedReferenceValue?.GetType();

                scroll = EditorGUILayout.BeginScrollView(scroll);

                DrawNoneRow(prop, currentType == null);
                for (int i = 0; i < rowTypes.Count; i++)
                {
                    Type t = rowTypes[i];
                    DrawSelectableRow(t, currentType, prop);
                }

                EditorGUILayout.EndScrollView();
            }

            private bool IsSerializedObjectAlive()
            {
                if (serializedObject == null)
                    return false;

                // targetObject is a native getter that throws when the backing object was destroyed/reloaded.
                try
                {
                    return serializedObject.targetObject != null;
                }
                catch (NullReferenceException)
                {
                    return false;
                }
            }

            private void DrawNoneRow(SerializedProperty prop, bool isSelected)
            {
                Rect rowRect = EditorGUILayout.GetControlRect(false, RowHeight, GUILayout.ExpandWidth(true));

                if (Event.current.type == EventType.Repaint && isSelected)
                    EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.48f, 0.90f, 0.35f));

                Rect previewRect = new Rect(rowRect.x + RowPadding, rowRect.y + (rowRect.height - RowPreview) * 0.5f, RowPreview, RowPreview);
                DrawEffectPreview(previewRect, BlockEffectType.None);

                Rect labelRect = new Rect(previewRect.xMax + 10f, rowRect.y, rowRect.width - RowPreview - RowPadding - 12f, rowRect.height);
                EnsureRowLabelStyles();
                GUI.Label(labelRect, "(none)", isSelected ? s_rowLabelBold : s_rowLabel);

                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    prop.managedReferenceValue = null;
                    serializedObject.ApplyModifiedProperties();
                    SelectedBlockEditorCommitEvents.Raise(serializedObject);
                    GUI.changed = true;
                    editorWindow.Close();
                    Event.current.Use();
                }
            }

            private void DrawSelectableRow(Type type, Type selectedType, SerializedProperty prop)
            {
                Rect rowRect = EditorGUILayout.GetControlRect(false, RowHeight, GUILayout.ExpandWidth(true));

                bool isSelected = type == selectedType;
                if (Event.current.type == EventType.Repaint && isSelected)
                    EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.48f, 0.90f, 0.35f));

                TryGetEffectType(type, out BlockEffectType effectType);

                Rect previewRect = new Rect(rowRect.x + RowPadding, rowRect.y + (rowRect.height - RowPreview) * 0.5f, RowPreview, RowPreview);
                DrawEffectPreview(previewRect, effectType);

                Rect labelRect = new Rect(previewRect.xMax + 10f, rowRect.y, rowRect.width - RowPreview - RowPadding - 12f, rowRect.height);
                EnsureRowLabelStyles();
                GUI.Label(labelRect, effectType.ToString(), isSelected ? s_rowLabelBold : s_rowLabel);

                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    prop.managedReferenceValue = Activator.CreateInstance(type);
                    serializedObject.ApplyModifiedProperties();
                    SelectedBlockEditorCommitEvents.Raise(serializedObject);
                    GUI.changed = true;
                    editorWindow.Close();
                    Event.current.Use();
                }
            }
        }
    }
#endif
}
