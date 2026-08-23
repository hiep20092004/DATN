using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    public class BlockLevelFigureLogWindow : EditorWindow
    {
        private const string BlocksFolder = "Assets/Project Files/Game/Prefabs/Blocks";

        private readonly List<BlockFigureEntry> entries = new List<BlockFigureEntry>();
        private Vector2 scroll;
        private string logText = string.Empty;
        private bool expandAll = true;

        [MenuItem("Tools/Block Level Figure Log")]
        public static void Open()
        {
            var window = GetWindow<BlockLevelFigureLogWindow>();
            window.titleContent = new GUIContent("Block Figure Log");
            window.minSize = new Vector2(480, 360);
            window.ScanBlocks();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scan Blocks", GUILayout.Height(28)))
                ScanBlocks();
            if (GUILayout.Button("Copy All", GUILayout.Height(28), GUILayout.Width(80)))
                EditorGUIUtility.systemCopyBuffer = logText;
            expandAll = EditorGUILayout.ToggleLeft("Expand All", expandAll, GUILayout.Width(90));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"Folder: {BlocksFolder}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Blocks: {entries.Count}", EditorStyles.boldLabel);

            EditorGUILayout.Space(4);
            scroll = EditorGUILayout.BeginScrollView(scroll);

            if (entries.Count == 0)
            {
                EditorGUILayout.HelpBox("No block prefabs found. Click Scan Blocks.", MessageType.Info);
            }
            else
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    DrawEntry(entries[i], i);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawEntry(BlockFigureEntry entry, int index)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            bool expanded = expandAll || EditorGUILayout.Foldout(entry.Expanded, entry.PrefabName, true);
            entry.Expanded = expanded;

            if (expanded)
            {
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.BeginVertical();
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField("Prefab", entry.Prefab, typeof(GameObject), false);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.SelectableLabel(entry.Detail, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(80));

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select Prefab", GUILayout.Width(100)) && entry.Prefab)
                {
                    Selection.activeObject = entry.Prefab;
                    EditorGUIUtility.PingObject(entry.Prefab);
                }
                if (GUILayout.Button("Copy", GUILayout.Width(60)))
                    EditorGUIUtility.systemCopyBuffer = entry.Detail;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();

                if (entry.Figure != null)
                {
                    Vector2 previewSize = LevelFigureDrawer.GetPreviewDimensions(entry.Figure.Size);
                    GUILayout.Space(8);
                    Rect previewRect = GUILayoutUtility.GetRect(previewSize.x, previewSize.y, GUILayout.Width(previewSize.x));
                    LevelFigureDrawer.DrawFigurePreview(previewRect, entry.Figure);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            if (index < entries.Count - 1)
                EditorGUILayout.Space(2);
        }

        private void ScanBlocks()
        {
            entries.Clear();
            var sb = new StringBuilder();

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { BlocksFolder });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab)
                    continue;

                var behavior = prefab.GetComponent<LevelBlockBehavior>();
                if (!behavior)
                    continue;

                LevelFigure figure = behavior.Figure;
                string detail = FormatFigure(prefab.name, figure);
                entries.Add(new BlockFigureEntry
                {
                    PrefabName = prefab.name,
                    Prefab = prefab,
                    AssetPath = path,
                    Figure = figure,
                    Detail = detail,
                    Expanded = true
                });
            }

            entries.Sort((a, b) => string.Compare(a.PrefabName, b.PrefabName, StringComparison.OrdinalIgnoreCase));

            foreach (BlockFigureEntry entry in entries)
            {
                sb.AppendLine($"=== {entry.PrefabName} ===");
                sb.Append(entry.Detail);
                sb.AppendLine();
            }

            logText = sb.ToString();
            Repaint();
        }

        private static string FormatFigure(string prefabName, LevelFigure figure)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"prefab: {prefabName}");

            if (figure == null)
            {
                sb.AppendLine("figure: null");
                return sb.ToString();
            }

            Vector2Int size = figure.Size;
            Vector2Int pivot = figure.PivotPoint;
            int activePoints = figure.ActivePoints;
            PointData[] points = figure.Points;

            sb.AppendLine($"size: ({size.x}, {size.y})");
            sb.AppendLine($"pivotPoint: ({pivot.x}, {pivot.y})");
            sb.AppendLine($"activePoints: {activePoints}");
            sb.AppendLine($"blockPieceCount: {figure.GetBlockPieceCount()}");

            Vector2Int[] offsets = figure.GetOffsetsRelativeToPivot();
            sb.Append("offsetsRelativeToPivot: ");
            AppendVector2IntArray(sb, offsets);
            sb.AppendLine();

            if (figure.TryGetFigureFilledBoundsCenterXZ(out Vector2 centerXZ))
                sb.AppendLine($"filledBoundsCenterXZ: ({centerXZ.x:F2}, {centerXZ.y:F2})");
            else
                sb.AppendLine("filledBoundsCenterXZ: (none)");

            Bounds horBounds = figure.GetHorizontalCenterBounds();
            Bounds verBounds = figure.GetVerticalCenterBounds();
            sb.AppendLine($"horizontalCenterBounds: center={horBounds.center}, size={horBounds.size}");
            sb.AppendLine($"verticalCenterBounds: center={verBounds.center}, size={verBounds.size}");

            sb.AppendLine();
            sb.AppendLine("visual grid (P=pivot, #=filled, .=empty):");
            sb.Append(FormatVisualGrid(size, pivot, points));

            sb.AppendLine();
            sb.AppendLine("points:");
            int expectedCount = size.x * size.y;
            if (points == null)
            {
                sb.AppendLine("  (null)");
            }
            else
            {
                if (points.Length != expectedCount)
                    sb.AppendLine($"  [warning] array length {points.Length} != size.x*size.y ({expectedCount})");

                for (int i = 0; i < points.Length; i++)
                {
                    int x = i % size.x;
                    int y = i / size.x;
                    PointData p = points[i];
                    if (p == null)
                    {
                        sb.AppendLine($"  [{i}] ({x},{y}): null");
                        continue;
                    }

                    bool isPivot = pivot.x == x && pivot.y == y;
                    sb.AppendLine(
                        $"  [{i}] ({x},{y}) filled={p.IsFilled} horBounds={p.UseInHorizontalCenteredBounds} verBounds={p.UseInVerticalCenteredBounds}" +
                        (isPivot ? " [PIVOT]" : string.Empty));
                }
            }

            return sb.ToString();
        }

        private static string FormatVisualGrid(Vector2Int size, Vector2Int pivot, PointData[] points)
        {
            var sb = new StringBuilder();
            for (int y = size.y - 1; y >= 0; y--)
            {
                sb.Append("  ");
                for (int x = 0; x < size.x; x++)
                {
                    int index = x + y * size.x;
                    char c = '.';
                    if (points != null && index < points.Length && points[index] != null && points[index].IsFilled)
                        c = '#';
                    if (pivot.x == x && pivot.y == y)
                        c = 'P';
                    sb.Append(c);
                    if (x < size.x - 1)
                        sb.Append(' ');
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static void AppendVector2IntArray(StringBuilder sb, Vector2Int[] values)
        {
            if (values == null || values.Length == 0)
            {
                sb.Append("[]");
                return;
            }

            sb.Append('[');
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append($"({values[i].x},{values[i].y})");
            }

            sb.Append(']');
        }

        private class BlockFigureEntry
        {
            public string PrefabName;
            public GameObject Prefab;
            public string AssetPath;
            public LevelFigure Figure;
            public string Detail;
            public bool Expanded = true;
        }
    }
}
