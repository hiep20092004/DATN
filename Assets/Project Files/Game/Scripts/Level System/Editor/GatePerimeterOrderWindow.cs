using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    public class GatePerimeterOrderWindow : EditorWindow
    {
        private int levelNumber = 3;
        private int blockId = -1;
        private int previewIndex;

        private List<int> lastOrderedIds;
        private string lastError;
        private Vector2 scroll;

        [MenuItem("Tools/Gate Perimeter Order")]
        public static void Open()
        {
            GatePerimeterOrderWindow win = GetWindow<GatePerimeterOrderWindow>();
            win.titleContent = new GUIContent("Gate Perimeter Order");
            win.minSize = new Vector2(360, 220);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            levelNumber = EditorGUILayout.IntField("Level", levelNumber);
            blockId = EditorGUILayout.IntField("Gate BlockId", blockId);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Compute", GUILayout.Height(28)))
                Compute();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            scroll = EditorGUILayout.BeginScrollView(scroll);

            if (!string.IsNullOrEmpty(lastError))
            {
                EditorGUILayout.HelpBox(lastError, MessageType.Error);
            }
            else if (lastOrderedIds != null && lastOrderedIds.Count > 0)
            {
                EditorGUILayout.LabelField("Ordered gate BlockIds", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(FormatIdList(lastOrderedIds), EditorStyles.wordWrappedLabel);

                EditorGUILayout.Space(6);
                int max = Mathf.Max(0, lastOrderedIds.Count - 1);
                previewIndex = EditorGUILayout.IntSlider("Preview index", Mathf.Clamp(previewIndex, 0, max), 0, max);
                if (lastOrderedIds.Count > 0)
                {
                    int id = lastOrderedIds[previewIndex];
                    EditorGUILayout.LabelField("Gate at index", $"{previewIndex}: {id}");
                }

                EditorGUILayout.HelpBox(
                    "Index +1 is the next id in the list (clockwise around the centroid of gate positions). Index −1 wraps the other way.",
                    MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        private void Compute()
        {
            lastError = null;
            lastOrderedIds = null;

            string path = LevelSystemUtils.GetSourceLevelAssetPath(levelNumber);
            LevelData levelData = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (!levelData)
            {
                lastError = $"Could not load level asset at path:\n{path}";
                return;
            }

            if (!TryCompute(levelData, blockId, out List<int> ordered, out string err))
            {
                lastError = err;
                return;
            }

            lastOrderedIds = ordered;
            previewIndex = 0;
        }

        private static bool TryCompute(LevelData levelData, int startBlockId, out List<int> orderedIds, out string errorMessage)
        {
            orderedIds = null;
            errorMessage = null;

            if (!levelData)
            {
                errorMessage = "Level asset is missing.";
                return false;
            }

            LevelElementData[] elements = levelData.Elements;
            if (!GatePerimeterOrdering.TryComputeGateBlockIdOrder(elements, out List<int> baseOrder, out errorMessage))
                return false;

            int startIndex = baseOrder.IndexOf(startBlockId);
            if (startIndex < 0)
            {
                errorMessage = $"BlockId {startBlockId} is not a Gate id in this level.";
                return false;
            }

            orderedIds = new List<int>(baseOrder.Count);
            for (int i = 0; i < baseOrder.Count; i++)
                orderedIds.Add(baseOrder[(startIndex + i) % baseOrder.Count]);

            return true;
        }
        
        private static string FormatIdList(IReadOnlyList<int> ids)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{ ");
            for (int i = 0; i < ids.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(ids[i]);
            }

            sb.Append(" }");
            return sb.ToString();
        }
    }
}
