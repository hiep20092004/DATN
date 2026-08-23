using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using WaterFlow.Core;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Displays runtime debug information for Blocks and Gates during Play Mode.
    /// Supports multi-select to monitor multiple objects simultaneously.
    /// Uses EditorCustomStyles/EditorGUILayoutCustom for consistent UI.
    /// </summary>
    public class PlayModeInspectorModule
    {
        #region Nested Types

        private class CardState
        {
            public bool infoExpanded = true;
            public bool effectsExpanded = true;
            public bool privateExpanded;
            public readonly Dictionary<int, bool> effectFoldouts = new();
        }

        #endregion

        #region Constants

        private const float LIST_PANEL_WIDTH = 200f;
        private const float TOGGLE_WIDTH = 18f;
        private const float PANEL_GAP = 3f;

        private const float CARD_HEADER_HEIGHT = 34f;
        private const float CARD_HEADER_ACCENT_WIDTH = 4f;
        private const float CARD_HEADER_ICON_SIZE = 20f;
        private const float CARD_HEADER_BTN_SIZE = 20f;

        private const float SECTION_HEADER_HEIGHT = 22f;
        private const float SECTION_ACCENT_WIDTH = 3f;
        private const float SECTION_ICON_SIZE = 14f;

        #endregion

        #region Colors

        private static readonly Color BlockAccentColor = new(0.25f, 0.25f, 1f);
        private static readonly Color GateAccentColor = new(0.25f, 0.85f, 0.55f);
        private static readonly Color CardHeaderBgColor = new(0.14f, 0.14f, 0.14f, 1f);

        private static readonly Color InfoAccentColor = new(0.3f, 0.55f, 0.95f);
        private static readonly Color EffectsAccentColor = new(0.95f, 0.62f, 0.2f);
        private static readonly Color PrivateAccentColor = new(0.9f, 0.35f, 0.35f);
        private static readonly Color SectionBgColor = new(0.21f, 0.21f, 0.21f, 1f);
        private static readonly Color SectionBgHoverColor = new(0.26f, 0.26f, 0.26f, 1f);

        #endregion

        #region State

        private bool isEnabled;
        private Vector2 listScrollPos;
        private Vector2 inspectorScrollPos;

        private readonly HashSet<int> selectedIds = new();
        private readonly Dictionary<int, CardState> cardStates = new();
        private int lastSceneSelectionHash;

        private readonly Action repaintCallback;
        private readonly StringBuilder sb = new();

        #endregion

        #region Styles & Icons

        private bool stylesReady;
        private GUIStyle cardTitleStyle;
        private GUIStyle sectionTitleStyle;
        private GUIStyle sectionContentStyle;
        private GUIStyle effectFoldoutStyle;

        // Icons — cached lazily
        private GUIContent iconBlock;
        private GUIContent iconGate;
        private GUIContent iconInfo;
        private GUIContent iconEffects;
        private GUIContent iconPrivate;
        private GUIContent iconPing;
        private GUIContent iconClose;

        #endregion

        #region Reflection

        private static readonly BindingFlags DeclaredInstanceFields =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly;

        private static readonly Dictionary<Type, FieldInfo[]> ReflectionCache = new();

        #endregion

        public bool IsEnabled => isEnabled;

        public PlayModeInspectorModule(Action repaintCallback)
        {
            this.repaintCallback = repaintCallback;
        }

        #region Public API

        public void Draw()
        {
            EditorCustomStyles.CheckStyles();
            EnsureStyles();

            isEnabled = EditorGUILayout.Toggle("Debug", isEnabled);
            if (!isEnabled || !EditorApplication.isPlaying) return;

            var levelController = LevelController.Instance;
            if (levelController == null || levelController.LevelRepresentation == null)
            {
                EditorGUILayout.HelpBox("LevelController not available.", MessageType.Info);
                return;
            }

            SyncSceneSelection(levelController);

            // Outer horizontal: list | gap | inspector
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            DrawListPanel(levelController);
            GUILayout.Space(PANEL_GAP);
            DrawInspectorPanel(levelController);
            EditorGUILayout.EndHorizontal();
        }

        public void OnUpdate()
        {
            if (isEnabled && EditorApplication.isPlaying)
                repaintCallback?.Invoke();
        }

        #endregion

        #region Scene Selection Sync

        private void SyncSceneSelection(LevelController lc)
        {
            int hash = ComputeSelectionHash();
            if (hash == lastSceneSelectionHash) return;
            lastSceneSelectionHash = hash;

            foreach (var go in Selection.gameObjects)
            {
                if (!go) continue;
                var block = go.GetComponent<LevelBlockBehavior>();
                if (block) { selectedIds.Add(block.GetInstanceID()); continue; }
                var gate = go.GetComponent<GateBehavior>();
                if (gate) selectedIds.Add(gate.GetInstanceID());
            }
        }

        private static int ComputeSelectionHash()
        {
            int hash = 0;
            foreach (var obj in Selection.objects)
                if (obj) hash ^= obj.GetInstanceID();
            return hash;
        }

        #endregion

        /// <summary>
        /// Prefer project asset file name when <see cref="LevelData"/> is an on-disk asset (Editor + SO reference).
        /// Runtime instances created from JSON have no asset path — use <see cref="Object.name"/> or a level index fallback.
        /// </summary>
        private static string BuildBlockGateListPanelHeader(LevelController lc)
        {
            LevelData data = lc?.LevelRepresentation?.LevelData;
            if (!data)
                return "Block And Gate Lists";

            string assetPath = AssetDatabase.GetAssetPath(data);
            if (!string.IsNullOrEmpty(assetPath))
                return Path.GetFileNameWithoutExtension(assetPath);

            if (!string.IsNullOrEmpty(data.name) && data.name != nameof(LevelData))
                return data.name;

            try
            {
                int idx = ActiveSession.Current.DisplayLevelIndex;
                if (idx >= 0)
                    return "Level " + (idx + 1).ToString("000");
            }
            catch
            {
                // ignore — show generic label below
            }

            return "Block And Gate Lists";
        }

        #region List Panel

        private void DrawListPanel(LevelController lc)
        {
            EditorGUILayout.BeginVertical(EditorCustomStyles.box,
                GUILayout.Width(LIST_PANEL_WIDTH), GUILayout.ExpandHeight(true));

            EditorGUILayoutCustom.Header(BuildBlockGateListPanelHeader(lc));

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All", EditorCustomStyles.buttonMini)) SelectAll(lc);
            if (GUILayout.Button("Clear All", EditorCustomStyles.buttonMini)) ClearAll();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            listScrollPos = EditorGUILayout.BeginScrollView(listScrollPos);
            DrawBlockList(lc);
            EditorGUILayout.Space(4);
            EditorGUILayoutCustom.LineSpacer();
            DrawGateList(lc);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private void DrawBlockList(LevelController lc)
        {
            var blocks = lc.LevelRepresentation.ActiveBlocks;
            if (blocks == null) return;

            EditorGUILayout.LabelField($"Blocks ({blocks.Count})", EditorCustomStyles.labelBold);
            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                if (!block) continue;
                DrawListItem(block.GetInstanceID(), block.gameObject,
                    BuildBlockLabel(block));
            }
        }

        private void DrawGateList(LevelController lc)
        {
            var gates = lc.LevelRepresentation.EnvironmentSpawner?.Gates;
            if (gates == null) return;

            EditorGUILayout.LabelField($"Gates ({gates.Count})", EditorCustomStyles.labelBold);
            for (int i = 0; i < gates.Count; i++)
            {
                var gate = gates[i];
                if (!gate) continue;
                DrawListItem(gate.GetInstanceID(), gate.gameObject,
                    BuildGateLabel(gate, i));
            }
        }

        private void DrawListItem(int instanceId, GameObject go, string label)
        {
            bool wasSelected = selectedIds.Contains(instanceId);

            EditorGUILayout.BeginHorizontal();
            bool toggled = EditorGUILayout.Toggle(wasSelected, GUILayout.Width(TOGGLE_WIDTH));
            if (GUILayout.Button(label, EditorStyles.miniButton))
            {
                toggled = !wasSelected;
                Selection.activeGameObject = go;
            }
            EditorGUILayout.EndHorizontal();

            if (toggled == wasSelected) return;
            if (toggled) selectedIds.Add(instanceId);
            else RemoveSelection(instanceId);
        }

        private string BuildBlockLabel(LevelBlockBehavior block)
        {
            sb.Clear();
            sb.Append('#').Append(block.BlockId).Append(' ');
            sb.Append(block.BlockConfig != null ? block.BlockConfig.Type.ToString() : "?");
            sb.Append(" (").Append(block.GetActiveBlockColor()).Append(')');
            return sb.ToString();
        }

        private string BuildGateLabel(GateBehavior gate, int index)
        {
            sb.Clear();
            sb.Append("Gate ").Append(index);
            if (gate.Data != null) sb.Append(" (").Append(gate.Data.GateDirectionType).Append(')');
            sb.Append(" [").Append(gate.GetActiveColor()).Append(']');
            return sb.ToString();
        }

        private void SelectAll(LevelController lc)
        {
            var blocks = lc.LevelRepresentation.ActiveBlocks;
            if (blocks != null)
                foreach (var b in blocks)
                    if (b) selectedIds.Add(b.GetInstanceID());

            var gates = lc.LevelRepresentation.EnvironmentSpawner?.Gates;
            if (gates != null)
                foreach (var g in gates)
                    if (g) selectedIds.Add(g.GetInstanceID());
        }

        private void ClearAll()
        {
            selectedIds.Clear();
            cardStates.Clear();
        }

        private void RemoveSelection(int instanceId)
        {
            selectedIds.Remove(instanceId);
            cardStates.Remove(instanceId);
        }

        #endregion

        #region Inspector Panel

        private void DrawInspectorPanel(LevelController lc)
        {
            // ExpandWidth(true) + MinWidth(0) ensures the panel takes remaining space
            // without overflowing the window bounds.
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.MinWidth(0));

            if (selectedIds.Count == 0)
            {
                EditorGUILayout.BeginVertical(EditorCustomStyles.box);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Select blocks or gates from the list to inspect.",
                    EditorCustomStyles.labelCentered);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
            }
            else
            {
                inspectorScrollPos = EditorGUILayout.BeginScrollView(inspectorScrollPos,
                    GUILayout.ExpandWidth(true));
                DrawSelectedCards(lc);
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSelectedCards(LevelController lc)
        {
            var blocks = lc.LevelRepresentation.ActiveBlocks;
            if (blocks != null)
                for (int i = 0; i < blocks.Count; i++)
                {
                    var block = blocks[i];
                    if (block && selectedIds.Contains(block.GetInstanceID()))
                        DrawBlockCard(block);
                }

            var gates = lc.LevelRepresentation.EnvironmentSpawner?.Gates;
            if (gates != null)
                for (int i = 0; i < gates.Count; i++)
                {
                    var gate = gates[i];
                    if (gate && selectedIds.Contains(gate.GetInstanceID()))
                        DrawGateCard(gate, i);
                }
        }

        #endregion

        #region Block Card

        private void DrawBlockCard(LevelBlockBehavior block)
        {
            int id = block.GetInstanceID();
            var state = GetOrCreateState(id);

            EditorGUILayout.BeginVertical(EditorCustomStyles.box);

            DrawCardHeader(BuildBlockLabel(block), id, block.gameObject, BlockAccentColor, iconBlock);

            // Info
            state.infoExpanded = DrawSectionHeader(state.infoExpanded, "Info", iconInfo, InfoAccentColor);
            if (state.infoExpanded)
            {
                EditorGUILayout.BeginVertical(sectionContentStyle);
                DrawBlockInfo(block);
                EditorGUILayout.EndVertical();
            }

            // Effects
            int effectCount = block.Effects?.Count ?? 0;
            state.effectsExpanded = DrawSectionHeader(state.effectsExpanded,
                $"Effects ({effectCount})", iconEffects, EffectsAccentColor);
            if (state.effectsExpanded)
            {
                EditorGUILayout.BeginVertical(sectionContentStyle);
                DrawBlockEffects(block, state);
                EditorGUILayout.EndVertical();
            }

            // Private Fields
            state.privateExpanded = DrawSectionHeader(state.privateExpanded,
                "Private Fields", iconPrivate, PrivateAccentColor);
            if (state.privateExpanded)
            {
                EditorGUILayout.BeginVertical(sectionContentStyle);
                DrawPrivateFields(block, typeof(LevelBlockBehavior));
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(4);
        }

        private void DrawBlockInfo(LevelBlockBehavior block)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Block ID", block.BlockId);
                EditorGUILayout.Vector2IntField("Position", block.MatrixPosition);

                if (block.BlockConfig != null)
                    EditorGUILayout.EnumPopup("Block Type", block.BlockConfig.Type);

                if (block.OriginColorConfig != null)
                    EditorGUILayout.EnumPopup("Origin Color", block.OriginColorConfig.Type);

                EditorGUILayout.EnumPopup("Active Color", block.GetActiveBlockColor());
                EditorGUILayout.EnumPopup("Secondary Color", block.GetSecondaryBlockColor());
            }

            EditorGUILayoutCustom.LineSpacer();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Is Full Fill", block.IsFullFill);
                EditorGUILayout.IntField("Available Point", block.AvailablePoint);
                EditorGUILayout.Toggle("Block Collectable", block.BlockCollectable);
                EditorGUILayout.Toggle("Can Collect", block.CanCollectBlock());
                EditorGUILayout.Toggle("Has Active Effect", block.HasActiveEffect());
            }
        }

        #endregion

        #region Gate Card

        private void DrawGateCard(GateBehavior gate, int listIndex)
        {
            int id = gate.GetInstanceID();
            var state = GetOrCreateState(id);

            EditorGUILayout.BeginVertical(EditorCustomStyles.box);

            DrawCardHeader(BuildGateLabel(gate, listIndex), id, gate.gameObject, GateAccentColor, iconGate);

            // Info
            state.infoExpanded = DrawSectionHeader(state.infoExpanded, "Info", iconInfo, InfoAccentColor);
            if (state.infoExpanded)
            {
                EditorGUILayout.BeginVertical(sectionContentStyle);
                DrawGateInfo(gate);
                EditorGUILayout.EndVertical();
            }

            // Effects
            int effectCount = gate.Effects?.Count ?? 0;
            state.effectsExpanded = DrawSectionHeader(state.effectsExpanded,
                $"Effects ({effectCount})", iconEffects, EffectsAccentColor);
            if (state.effectsExpanded)
            {
                EditorGUILayout.BeginVertical(sectionContentStyle);
                DrawGateEffects(gate, state);
                EditorGUILayout.EndVertical();
            }

            // Private Fields
            state.privateExpanded = DrawSectionHeader(state.privateExpanded,
                "Private Fields", iconPrivate, PrivateAccentColor);
            if (state.privateExpanded)
            {
                EditorGUILayout.BeginVertical(sectionContentStyle);
                DrawPrivateFields(gate, typeof(GateBehavior));
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(4);
        }

        private void DrawGateInfo(GateBehavior gate)
        {
            var data = gate.Data;
            using (new EditorGUI.DisabledScope(true))
            {
                if (data != null)
                {
                    EditorGUILayout.Vector2IntField("Position", data.Position);
                    EditorGUILayout.EnumPopup("Direction", data.GateDirectionType);
                }

                EditorGUILayout.EnumPopup("Active Color", gate.GetActiveColor());
                EditorGUILayout.IntField("Active Point", gate.GetActivePoint());
                EditorGUILayout.Toggle("Is Out Of Water", gate.IsOutOfWater());
            }
        }

        #endregion

        #region Card Header

        private void DrawCardHeader(string title, int instanceId, GameObject go,
            Color accentColor, GUIContent typeIcon)
        {
            // Reserve layout space first, then draw manually at the returned rect
            Rect headerRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.Height(CARD_HEADER_HEIGHT), GUILayout.ExpandWidth(true));

            // Background
            EditorGUI.DrawRect(headerRect, CardHeaderBgColor);

            // Left accent bar
            EditorGUI.DrawRect(
                new Rect(headerRect.x, headerRect.y, CARD_HEADER_ACCENT_WIDTH, headerRect.height),
                accentColor);

            // Type icon
            float iconX = headerRect.x + CARD_HEADER_ACCENT_WIDTH + 6f;
            float iconY = headerRect.y + (CARD_HEADER_HEIGHT - CARD_HEADER_ICON_SIZE) / 2f;
            GUI.Label(new Rect(iconX, iconY, CARD_HEADER_ICON_SIZE, CARD_HEADER_ICON_SIZE), typeIcon);

            // Title
            float titleX = iconX + CARD_HEADER_ICON_SIZE + 4f;
            float buttonsWidth = CARD_HEADER_BTN_SIZE * 2 + 8f;
            GUI.Label(new Rect(titleX, headerRect.y, headerRect.width - titleX + headerRect.x - buttonsWidth,
                CARD_HEADER_HEIGHT), title, cardTitleStyle);

            // Buttons
            float btnY = headerRect.y + (CARD_HEADER_HEIGHT - CARD_HEADER_BTN_SIZE) / 2f;
            Rect pingRect = new(headerRect.xMax - CARD_HEADER_BTN_SIZE * 2 - 4f, btnY,
                CARD_HEADER_BTN_SIZE, CARD_HEADER_BTN_SIZE);
            Rect closeRect = new(headerRect.xMax - CARD_HEADER_BTN_SIZE - 2f, btnY,
                CARD_HEADER_BTN_SIZE, CARD_HEADER_BTN_SIZE);

            if (GUI.Button(pingRect, iconPing, EditorStyles.iconButton))
            {
                Selection.activeGameObject = go;
                EditorGUIUtility.PingObject(go);
            }

            if (GUI.Button(closeRect, iconClose, EditorStyles.iconButton))
                RemoveSelection(instanceId);
        }

        #endregion

        #region Section Header

        /// <summary>
        /// Compact collapsible section header (22 px) with left accent bar and icon.
        /// Replaces BeginExpandBoxGroup which reserves 30 px.
        /// </summary>
        private bool DrawSectionHeader(bool expanded, string title, GUIContent icon, Color accentColor)
        {
            Rect headerRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.Height(SECTION_HEADER_HEIGHT), GUILayout.ExpandWidth(true));

            // Hover tint
            bool isHovered = headerRect.Contains(Event.current.mousePosition);
            EditorGUI.DrawRect(headerRect, isHovered ? SectionBgHoverColor : SectionBgColor);

            // Left accent
            EditorGUI.DrawRect(
                new Rect(headerRect.x, headerRect.y, SECTION_ACCENT_WIDTH, headerRect.height),
                accentColor);

            // Icon
            float iconY = headerRect.y + (SECTION_HEADER_HEIGHT - SECTION_ICON_SIZE) / 2f;
            GUI.Label(new Rect(headerRect.x + SECTION_ACCENT_WIDTH + 4f, iconY,
                SECTION_ICON_SIZE, SECTION_ICON_SIZE), icon);

            // Title
            float textX = headerRect.x + SECTION_ACCENT_WIDTH + SECTION_ICON_SIZE + 8f;
            GUI.Label(new Rect(textX, headerRect.y, headerRect.width - textX + headerRect.x - 20f,
                SECTION_HEADER_HEIGHT), title, sectionTitleStyle);

            // Click to toggle
            if (Event.current.type == EventType.MouseDown && headerRect.Contains(Event.current.mousePosition))
            {
                expanded = !expanded;
                Event.current.Use();
                repaintCallback?.Invoke();
            }

            return expanded;
        }

        #endregion

        #region Effects Drawing

        private void DrawBlockEffects(LevelBlockBehavior block, CardState state)
        {
            var effects = block.Effects;
            if (effects == null || effects.Count == 0)
            {
                EditorGUILayout.LabelField("No effects attached.", EditorCustomStyles.labelSmall);
                return;
            }

            for (int i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (!effect) continue;

                string label = BuildEffectLabel(effect, i, showOrder: true);
                state.effectFoldouts.TryAdd(i, false);
                state.effectFoldouts[i] = DrawEffectFoldout(state.effectFoldouts[i], label);

                if (state.effectFoldouts[i])
                    DrawFieldsHierarchy(effect, effect.GetType(), typeof(BlockEffectBehavior));
            }
        }

        private void DrawGateEffects(GateBehavior gate, CardState state)
        {
            var effects = gate.Effects;
            if (effects == null || effects.Count == 0)
            {
                EditorGUILayout.LabelField("No effects attached.", EditorCustomStyles.labelSmall);
                return;
            }

            for (int i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (!effect) continue;

                string label = BuildEffectLabel(effect, i, showOrder: false);
                state.effectFoldouts.TryAdd(i, false);
                state.effectFoldouts[i] = DrawEffectFoldout(state.effectFoldouts[i], label);

                if (state.effectFoldouts[i])
                    DrawFieldsHierarchy(effect, effect.GetType(), typeof(GateEffectBehavior));
            }
        }

        private string BuildEffectLabel(MonoBehaviour effect, int index, bool showOrder)
        {
            sb.Clear();
            sb.Append('[').Append(index).Append("] ");
            sb.Append(effect.GetType().Name);
            if (showOrder && effect is BlockEffectBehavior bfx)
                sb.Append(" (Order: ").Append(bfx.OrderID).Append(')');
            sb.Append(GetActiveLabel(effect is BlockEffectBehavior b2 ? b2.IsActive
                : effect is GateEffectBehavior g ? g.IsActive : false));
            return sb.ToString();
        }

        private static string GetActiveLabel(bool isActive) =>
            isActive ? " ✓ Active" : " ✗ Inactive";

        private bool DrawEffectFoldout(bool expanded, string label)
        {
            Rect r = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.Height(20f), GUILayout.ExpandWidth(true));

            EditorGUI.DrawRect(r, new Color(0.18f, 0.18f, 0.18f, 1f));
            GUI.Label(new Rect(r.x + 4f, r.y, r.width - 20f, r.height), label, effectFoldoutStyle);

            var arrowContent = expanded ? EditorCustomStyles.foldoutArrowDown : EditorCustomStyles.foldoutArrowRight;
            GUI.Label(new Rect(r.xMax - 18f, r.y + 3f, 14f, 14f), arrowContent);

            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            {
                expanded = !expanded;
                Event.current.Use();
                repaintCallback?.Invoke();
            }

            return expanded;
        }

        #endregion

        #region Reflection

        private void DrawPrivateFields(object target, Type declaredType)
        {
            if (target == null) return;

            var fields = GetCachedFields(declaredType);
            foreach (var field in fields)
            {
                if (IsSkippableField(field)) continue;
                DrawReflectedField(target, field);
            }
        }

        private void DrawFieldsHierarchy(object target, Type actualType, Type stopType)
        {
            var typeChain = BuildTypeChain(actualType, stopType);

            foreach (var type in typeChain)
            {
                if (typeChain.Count > 1)
                    EditorGUILayout.LabelField(type.Name, EditorCustomStyles.labelBold);

                var fields = GetCachedFields(type);
                foreach (var field in fields)
                {
                    if (IsSkippableField(field)) continue;
                    DrawReflectedField(target, field);
                }

                if (typeChain.Count > 1) GUILayout.Space(2);
            }
        }

        private void DrawReflectedField(object target, FieldInfo field)
        {
            object value;
            try { value = field.GetValue(target); }
            catch
            {
                EditorGUILayout.LabelField(PrettifyFieldName(field.Name), "<error>");
                return;
            }

            var content = new GUIContent(PrettifyFieldName(field.Name));

            if (value != null && EditorGUILayoutCustom.DrawLayoutField(value, content))
                return;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(content);
            EditorGUILayout.LabelField(FormatValue(value), EditorCustomStyles.label);
            EditorGUILayout.EndHorizontal();
        }

        private static List<Type> BuildTypeChain(Type actualType, Type stopType)
        {
            var chain = new List<Type>();
            var t = actualType;
            while (t != null && t != typeof(MonoBehaviour))
            {
                chain.Add(t);
                if (t == stopType) break;
                t = t.BaseType;
            }

            chain.Reverse();
            return chain;
        }

        private static FieldInfo[] GetCachedFields(Type type)
        {
            if (ReflectionCache.TryGetValue(type, out var cached)) return cached;
            var fields = type.GetFields(DeclaredInstanceFields);
            ReflectionCache[type] = fields;
            return fields;
        }

        private static bool IsSkippableField(FieldInfo field) =>
            field.Name.Contains("__BackingField") || field.Name.StartsWith("m_");

        private static string PrettifyFieldName(string name)
        {
            if (name.Length == 0) return name;

            int start = 0;
            while (start < name.Length && name[start] == '_') start++;
            if (start >= name.Length) return name;

            var result = new StringBuilder(name.Length + 4);
            result.Append(char.ToUpper(name[start]));
            for (int i = start + 1; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsUpper(c) && i > start + 1 && !char.IsUpper(name[i - 1]))
                    result.Append(' ');
                result.Append(c);
            }

            return result.ToString();
        }

        private static string FormatValue(object value)
        {
            if (value == null) return "null";
            if (value is UnityEngine.Object uObj)
                return uObj ? $"{uObj.name} ({uObj.GetType().Name})" : "null (destroyed)";
            if (value is IList list) return $"List [{list.Count}]";
            if (value is IDictionary dict) return $"Dict [{dict.Count}]";
            if (value is Vector2Int v2i) return $"({v2i.x}, {v2i.y})";
            if (value is Vector3 v3) return $"({v3.x:F2}, {v3.y:F2}, {v3.z:F2})";
            return value.ToString();
        }

        #endregion

        #region Styles & Icons Init

        private void EnsureStyles()
        {
            if (stylesReady) return;
            stylesReady = true;

            cardTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Ellipsis,
                padding = new RectOffset(0, 0, 0, 0)
            };

            sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 0, 0, 0)
            };

            sectionContentStyle = new GUIStyle
            {
                padding = new RectOffset(6, 4, 4, 4)
            };

            effectFoldoutStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(2, 0, 0, 0)
            };

            // --- Icons ---
            // Block  → Cube-like object icon
            iconBlock = EditorGUIUtility.IconContent("d_GameObject Icon");
            iconBlock.tooltip = "Block";

            // Gate  → Linked / connection icon
            iconGate = EditorGUIUtility.IconContent("d_SkinnedMeshRenderer Icon");
            iconGate.tooltip = "Gate";

            // Info  → Inspector window icon
            iconInfo = EditorGUIUtility.IconContent("d_console.infoicon");
            iconInfo.tooltip = "Info";

            // Effects  → Particle system icon
            iconEffects = EditorGUIUtility.IconContent("d_Particle Effect");
            iconEffects.tooltip = "Effects";

            // Private Fields  → Script / code icon
            iconPrivate = EditorGUIUtility.IconContent("d_cs Script Icon");
            iconPrivate.tooltip = "Private Fields";

            // Ping button  → Locate / focus icon
            iconPing = EditorGUIUtility.IconContent("d_Search Icon");
            iconPing.tooltip = "Select & Ping in Hierarchy";

            // Close button  → X icon
            iconClose = EditorGUIUtility.IconContent("d_winbtn_mac_close_a@2x");
            iconClose.tooltip = "Remove from inspector";
        }

        #endregion

        #region State Helpers

        private CardState GetOrCreateState(int instanceId)
        {
            if (!cardStates.TryGetValue(instanceId, out var state))
            {
                state = new CardState();
                cardStates[instanceId] = state;
            }

            return state;
        }

        #endregion
    }
}
