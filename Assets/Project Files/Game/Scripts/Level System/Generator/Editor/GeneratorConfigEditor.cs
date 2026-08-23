#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace WaterFlow.Game
{
    [CustomEditor(typeof(GeneratorConfig))]
    public class GeneratorConfigEditor : UnityEditor.Editor
    {
        private const string BlockSpawnPresetsProperty = "blockSpawnPresets";
        private const string BlockTypeProperty         = "blockType";
        private const string SpawnCellsProperty        = "spawnCellPerDirection";

        // ── Labels ──────────────────────────────────────────────────────────────
        private static readonly string[] DirectionLabels = { "Left", "Right", "Top", "Bottom" };
        private static readonly string[] RowLabels       = { "Near", "Mid ", "Far " };
        private static readonly string[] ColLabels       = { " A ", "Ctr", " B " };

        // ── Colours ─────────────────────────────────────────────────────────────
        // Spawn cell (where pivot lands) = green, figure cells = blue, outside = dark
        private static readonly Color ColSpawn     = new Color(0.25f, 0.80f, 0.25f);
        private static readonly Color ColFigure    = new Color(0.22f, 0.52f, 0.95f, 0.95f);
        private static readonly Color ColEmpty     = new Color(0.28f, 0.28f, 0.28f);
        private static readonly Color ColHeaderBg  = new Color(0.20f, 0.20f, 0.22f);

        // ── List + selection ─────────────────────────────────────────────────────
        private SerializedProperty blockSpawnPresetsProp;
        private ReorderableList    presetList;
        private int                selectedPresetIndex;

        private readonly Dictionary<BlockType, LevelFigure> figCache = new Dictionary<BlockType, LevelFigure>();
        private BlocksVisualsData visualsData;

        // ── Lifecycle ────────────────────────────────────────────────────────────
        private void OnEnable()
        {
            figCache.Clear();
            blockSpawnPresetsProp = serializedObject.FindProperty(BlockSpawnPresetsProperty);
            BuildPresetList();

            string[] guids = AssetDatabase.FindAssets("t:BlocksVisualsData");
            if (guids.Length > 0)
                visualsData = AssetDatabase.LoadAssetAtPath<BlocksVisualsData>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));

            selectedPresetIndex =
                blockSpawnPresetsProp != null && blockSpawnPresetsProp.isArray &&
                blockSpawnPresetsProp.arraySize > 0
                    ? 0
                    : -1;
        }

        private void OnDisable() => figCache.Clear();

        // ── ReorderableList ──────────────────────────────────────────────────────
        private void BuildPresetList()
        {
            presetList = new ReorderableList(serializedObject, blockSpawnPresetsProp,
                draggable: true, displayHeader: true, displayAddButton: false, displayRemoveButton: false);

            presetList.drawHeaderCallback = rect =>
                EditorGUI.LabelField(rect,
                    $"Block presets (drag to reorder) — {blockSpawnPresetsProp.arraySize} item(s)");

            presetList.onSelectCallback  = list => selectedPresetIndex = list.index;
            presetList.onReorderCallback = list => selectedPresetIndex = list.index;

            presetList.elementHeight = EditorGUIUtility.singleLineHeight + 8f;
            presetList.drawElementCallback = DrawPresetListElement;
        }

        private void DrawPresetListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty preset      = blockSpawnPresetsProp.GetArrayElementAtIndex(index);
            SerializedProperty blockTypeProp = preset.FindPropertyRelative(BlockTypeProperty);

            BlockType blockType   = (BlockType)blockTypeProp.intValue;
            string    displayName = blockType.ToString().Replace('_', ' ');
            string    tag         = GetShortTypeTag(blockType);
            string    prefabName  = GetPrefabDisplayName(blockType);

            rect.y += 2f;
            const float indexW  = 30f;
            const float tagW    = 40f;
            const float prefabW = 160f;
            const float sp      = 6f;

            Rect idxR    = new Rect(rect.x, rect.y, indexW, EditorGUIUtility.singleLineHeight);
            Rect tagR    = new Rect(idxR.xMax + sp, rect.y, tagW, EditorGUIUtility.singleLineHeight);
            Rect prefabR = new Rect(rect.xMax - prefabW, rect.y, prefabW, EditorGUIUtility.singleLineHeight);
            Rect nameR   = new Rect(tagR.xMax + sp, rect.y,
                Mathf.Max(60f, prefabR.x - (tagR.xMax + sp * 2f)),
                EditorGUIUtility.singleLineHeight);

            EditorGUI.LabelField(idxR, $"#{index}");
            EditorGUI.LabelField(tagR, tag, EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(nameR, displayName);
            EditorGUI.LabelField(prefabR, prefabName, EditorStyles.miniLabel);
        }

        // ── Inspector root ───────────────────────────────────────────────────────
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty prop = serializedObject.GetIterator();
            bool enterChildren = true;

            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (prop.propertyPath == "m_Script")
                    continue;

                if (prop.propertyPath == BlockSpawnPresetsProperty)
                {
                    EditorGUILayout.Space(4f);
                    DrawSectionHeader("Block Spawn Presets");
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.Space(2f);
                        DrawBlockSpawnSection();
                        EditorGUILayout.Space(2f);
                    }
                    continue;
                }

                if (prop.propertyPath == "allowedBlockEffectsForQueue")
                {
                    EditorGUILayout.Space(4f);
                    DrawSectionHeader("Generator Queue Block Effects");
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.Space(2f);
                        EditorGUILayout.HelpBox(
                            "Empty = all block effect types are allowed on generator queue entries.",
                            MessageType.None);
                        EditorGUILayout.PropertyField(prop, true);
                        EditorGUILayout.Space(2f);
                    }
                    continue;
                }

                EditorGUILayout.PropertyField(prop, true);
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ── Section header ───────────────────────────────────────────────────────
        private static void DrawSectionHeader(string title)
        {
            Rect r = GUILayoutUtility.GetRect(0f, 22f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, ColHeaderBg);
            GUI.Label(new Rect(r.x + 8f, r.y + 3f, r.width, r.height), title, EditorStyles.boldLabel);
        }

        // ── Block spawn section ──────────────────────────────────────────────────
        private void DrawBlockSpawnSection()
        {
            EditorGUILayout.HelpBox(
                "Each cell = (col, row).  Col: 0=A · 1=Ctr · 2=B.  Row: 0=Near · 1=Mid · 2=Far.  " +
                "Index = col + row×3.  Shape overlay shows which watched cells the block would occupy.",
                MessageType.None);

            EditorGUILayout.Space(4f);

            if (GUILayout.Button("Initialize All Block Presets", GUILayout.Height(26f)))
            {
                var cfg = (GeneratorConfig)target;
                Undo.RecordObject(cfg, "Initialize All Block Presets");
                cfg.InitializeAllBlockPresets();
                EditorUtility.SetDirty(cfg);
                serializedObject.Update();
                blockSpawnPresetsProp = serializedObject.FindProperty(BlockSpawnPresetsProperty);
                BuildPresetList();
                selectedPresetIndex = blockSpawnPresetsProp.arraySize > 0 ? 0 : -1;
            }

            if (blockSpawnPresetsProp == null || !blockSpawnPresetsProp.isArray) return;

            if (blockSpawnPresetsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "No presets yet. Click the button above to create one entry per BlockType.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.Space(6f);
            presetList.DoLayoutList();

            selectedPresetIndex = Mathf.Clamp(selectedPresetIndex, 0, blockSpawnPresetsProp.arraySize - 1);
            DrawSelectedPresetDetails(selectedPresetIndex,
                blockSpawnPresetsProp.GetArrayElementAtIndex(selectedPresetIndex));
        }

        // ── Selected preset detail ───────────────────────────────────────────────
        private void DrawSelectedPresetDetails(int index, SerializedProperty preset)
        {
            SerializedProperty blockTypeProp = preset.FindPropertyRelative(BlockTypeProperty);
            SerializedProperty cellsProp     = preset.FindPropertyRelative(SpawnCellsProperty);
            BlockType          blockType     = (BlockType)blockTypeProp.intValue;

            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"Selected preset #{index}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Block type", blockType.ToString().Replace('_', ' '));
            EditorGUILayout.LabelField("Prefab", GetPrefabDisplayName(blockType), EditorStyles.miniLabel);

            // ── Legend ──────────────────────────────────────────────────────────
            EditorGUILayout.Space(4f);
            DrawLegend();
            EditorGUILayout.Space(6f);

            bool validCells = cellsProp != null && cellsProp.isArray && cellsProp.arraySize >= 4;
            if (!validCells)
            {
                EditorGUILayout.HelpBox(
                    "spawnCellPerDirection must have length 4. Re-run Initialize.",
                    MessageType.Warning);
            }
            else
            {
                LevelFigure figure = GetFigureCached(blockType);

                for (int d = 0; d < 4; d++)
                {
                    if (d > 0)
                    {
                        EditorGUILayout.Space(4f);
                        DrawHorizontalLine(new Color(0.3f, 0.3f, 0.3f));
                        EditorGUILayout.Space(2f);
                    }
                    DrawDirectionRow(d, DirectionLabels[d], cellsProp.GetArrayElementAtIndex(d), figure);
                }
            }

            EditorGUILayout.EndVertical();
        }

        // ── Legend ──────────────────────────────────────────────────────────────
        private static void DrawLegend()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(4f);
            DrawLegendSwatch(ColSpawn,  "= spawn (pivot)");
            GUILayout.Space(10f);
            DrawLegendSwatch(ColFigure, "= figure cell");
            GUILayout.Space(10f);
            DrawLegendSwatch(ColEmpty,  "= empty");
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawLegendSwatch(Color color, string label)
        {
            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUILayout.Box("", GUILayout.Width(14), GUILayout.Height(14));
            GUI.backgroundColor = prev;
            GUILayout.Label(label, EditorStyles.miniLabel);
        }

        // ── Direction row ────────────────────────────────────────────────────────
        private void DrawDirectionRow(int dirIndex, string label, SerializedProperty cellProp, LevelFigure figure)
        {
            // Label + grid on one inspector row; grid itself must be a vertical stack of horizontal
            // rows. Wrapping DrawGridWithFigure only in HorizontalScope nests every inner row as
            // siblings on one line and breaks A/Ctr/B alignment.
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel, GUILayout.Width(58f));
                GUILayout.Space(6f);
                using (new EditorGUILayout.VerticalScope())
                {
                    DrawGridWithFigure(dirIndex, cellProp, figure);
                }
            }
        }

        // ── 3×3 grid with embedded figure overlay ────────────────────────────────
        /// <remarks>
        /// Axis mapping per direction (figX/Y offsets from pivot → grid row/col offsets from spawn cell):
        ///   Left  : dRow = +dFigX,  dCol = +dFigY
        ///   Right : dRow = -dFigX,  dCol = +dFigY   (X axis reversed: block enters from right)
        ///   Top   : dRow = -dFigY,  dCol = +dFigX   (axes swapped + Y reversed: block enters from top)
        ///   Bottom: dRow = +dFigY,  dCol = +dFigX   (axes swapped: block enters from bottom)
        ///
        /// This matches BuildWatchedCells which builds watchedCells in order:
        ///   index = col + row * 3   (col=0 flankA, col=1 center, col=2 flankB; row=0 near, row=2 far)
        /// </remarks>
        private void DrawGridWithFigure(int dirIndex, SerializedProperty cellProp, LevelFigure figure)
        {
            const float cellW     = 36f;
            const float cellH     = 36f;
            const float gap       = 2f;
            const float rowLabelW = 34f;

            Vector2Int spawnCell = cellProp.vector2IntValue; // (col, row)
            Vector2Int pivot     = figure != null ? figure.PivotPoint : Vector2Int.zero;

            // ── Column header ────────────────────────────────────────────────────
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(rowLabelW);
                for (int col = 0; col < 3; col++)
                {
                    GUILayout.Label(ColLabels[col], EditorStyles.centeredGreyMiniLabel, GUILayout.Width(cellW));
                    if (col < 2) GUILayout.Space(gap);
                }
            }

            // ── Rows (top = Far/row2, bottom = Near/row0) ───────────────────────
            for (int row = 2; row >= 0; row--)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(RowLabels[row], EditorStyles.miniLabel, GUILayout.Width(rowLabelW));

                    for (int col = 0; col < 3; col++)
                    {
                        int  watchIdx   = col + row * 3;
                        bool isSpawn    = spawnCell.x == col && spawnCell.y == row;
                        bool isFigure   = IsFilledFigureCell(figure, dirIndex, spawnCell, pivot, col, row);

                        Color bgColor;
                        FontStyle fontStyle;
                        if (isSpawn)
                        {
                            bgColor   = ColSpawn;
                            fontStyle = FontStyle.Bold;
                        }
                        else if (isFigure)
                        {
                            bgColor   = ColFigure;
                            fontStyle = FontStyle.Normal;
                        }
                        else
                        {
                            bgColor   = ColEmpty;
                            fontStyle = FontStyle.Normal;
                        }

                        Color prev = GUI.backgroundColor;
                        GUI.backgroundColor = bgColor;

                        GUIStyle style = new GUIStyle(GUI.skin.button) { fontStyle = fontStyle };

                        if (GUILayout.Button(watchIdx.ToString(), style,
                                GUILayout.Width(cellW), GUILayout.Height(cellH)))
                        {
                            Undo.RecordObject(target, "Set generator spawn cell");
                            cellProp.vector2IntValue = new Vector2Int(col, row);
                        }

                        GUI.backgroundColor = prev;
                        if (col < 2) GUILayout.Space(gap);
                    }
                }
            }
        }

        // ── Figure → grid mapping ─────────────────────────────────────────────────
        /// <summary>
        /// Returns true if the figure has a filled cell that maps to grid position (col, row)
        /// given <paramref name="spawnCell"/> as the pivot landing point for
        /// generator direction <paramref name="dirIndex"/> (0=Left 1=Right 2=Top 3=Bottom).
        /// </summary>
        private static bool IsFilledFigureCell(
            LevelFigure figure, int dirIndex,
            Vector2Int spawnCell, Vector2Int pivot,
            int col, int row)
        {
            if (figure == null || figure.Points == null || figure.Points.Length == 0)
                return false;

            int sizeX = figure.Size.x;
            int sizeY = figure.Size.y;

            GateDirection.Type dir = (GateDirection.Type)dirIndex;

            for (int fy = 0; fy < sizeY; fy++)
            {
                for (int fx = 0; fx < sizeX; fx++)
                {
                    int idx = fy * sizeX + fx;
                    if (idx >= figure.Points.Length || !figure.Points[idx].IsFilled)
                        continue;

                    GeneratorSpawnGeometry.FigureOffsetToGridOffset(dir,
                        fx - pivot.x, fy - pivot.y,
                        out int dCol, out int dRow);

                    if (spawnCell.x + dCol == col && spawnCell.y + dRow == row)
                        return true;
                }
            }

            return false;
        }

        // ── Figure cache ─────────────────────────────────────────────────────────
        private LevelFigure GetFigureCached(BlockType blockType)
        {
            if (figCache.TryGetValue(blockType, out LevelFigure cached))
                return cached;
            LevelFigure fig = LoadFigure(blockType);
            figCache[blockType] = fig;
            return fig;
        }

        private LevelFigure LoadFigure(BlockType blockType)
        {
            if (visualsData == null) return null;
            BlockData[] blocks = visualsData.Blocks;
            if (blocks == null) return null;
            for (int i = 0; i < blocks.Length; i++)
            {
                BlockData bd = blocks[i];
                if (bd == null || bd.Type != blockType) continue;
                if (bd.Figure == null) bd.Init();
                return bd.Figure;
            }
            return null;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────
        private static string GetShortTypeTag(BlockType t)
        {
            string raw = t.ToString().Replace("_", string.Empty);
            if (string.IsNullOrEmpty(raw)) return "---";
            return raw.Length <= 3 ? raw.ToUpperInvariant() : raw.Substring(0, 3).ToUpperInvariant();
        }

        private string GetPrefabDisplayName(BlockType blockType)
        {
            if (visualsData == null) return "—";
            BlockData[] blocks = visualsData.Blocks;
            if (blocks == null) return "—";
            for (int i = 0; i < blocks.Length; i++)
            {
                BlockData bd = blocks[i];
                if (bd == null || bd.Type != blockType) continue;
                if (bd.Prefab == null) bd.Init();
                return bd.Prefab != null ? bd.Prefab.name : "No Prefab";
            }
            return "—";
        }

        private static void DrawHorizontalLine(Color color)
        {
            Rect r = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, color);
        }
    }
}
#endif
