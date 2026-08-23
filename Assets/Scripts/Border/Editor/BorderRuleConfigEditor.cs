using BorderSpawnModule;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace WaterFlow.Game
{
    [CustomEditor(typeof(BorderRuleConfig))]
    public class BorderRuleConfigEditor : UnityEditor.Editor
    {
        private const string BORDER_RULES_PROPERTY = "borderRules";
        private const string RULE_NAME_PROP = "ruleName";
        private const string RULE_TYPE_PROP = "ruleType";
        private const string NEIGHBORS_PROP = "neighbors";
        private const string PREFAB_PROP = "prefab";
        private const string POSITION_OFFSET_PROP = "positionOffset";
        private const string ROTATION_OFFSET_PROP = "rotationOffset";

        private SerializedProperty borderRulesProperty;
        private ReorderableList ruleList;
        // private Vector2 rulesScrollPosition;
        private bool rulesFoldout = true;
        private int selectedRuleIndex = -1;
        private UnityEditor.Editor prefabPreviewEditor;

        private static readonly string[] NeighborLabels =
        {
            "\u2196", "\u2191", "\u2197",
            "\u2190", "\u25CF", "\u2192",
            "\u2199", "\u2193", "\u2198"
        };

        private static readonly string[] RuleTypeShortLabels = { "FIX", "ROT", "MX", "MY", "MXY" };
        private static readonly Color THIS_COLOR = new Color(0.3f, 0.8f, 0.3f);
        private static readonly Color NOT_THIS_COLOR = new Color(0.9f, 0.3f, 0.3f);
        private static readonly Color INNER_TILE_COLOR = new Color(0.3f, 0.8f, 1f);
        private static readonly Color OBSTACLE_COLOR = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color GATE_COLOR = new Color(1f, 0.75f, 0.3f);
        private static readonly Color DONT_CARE_COLOR = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color CENTER_COLOR = new Color(0.4f, 0.6f, 0.9f);

        private void OnEnable()
        {
            borderRulesProperty = serializedObject.FindProperty(BORDER_RULES_PROPERTY);
            BuildRuleList();
        }

        private void OnDisable()
        {
            if (!prefabPreviewEditor) return;
            DestroyImmediate(prefabPreviewEditor);
            prefabPreviewEditor = null;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawBorderRuleTilesSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawBorderRuleTilesSection()
        {
            EditorGUILayout.Space(10);

            rulesFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(rulesFoldout, "Border Rule Tiles");

            if (!rulesFoldout)
            {
                EditorGUILayout.EndFoldoutHeaderGroup();
                return;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(4);
            DrawRuleLegend();
            EditorGUILayout.Space(4);

            ruleList.DoLayoutList();

            if (borderRulesProperty.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "No border rules defined.\nAdd rules to enable rule-based border tile matching.",
                    MessageType.Info);
                return;
            }

            selectedRuleIndex = Mathf.Clamp(selectedRuleIndex, 0, borderRulesProperty.arraySize - 1);
            SerializedProperty selectedRule = borderRulesProperty.GetArrayElementAtIndex(selectedRuleIndex);

            // rulesScrollPosition = EditorGUILayout.BeginScrollView(rulesScrollPosition, GUILayout.MaxHeight(460));
            DrawSelectedRuleDetails(selectedRuleIndex, selectedRule);
            // EditorGUILayout.EndScrollView();
        }

        private void BuildRuleList()
        {
            ruleList = new ReorderableList(serializedObject, borderRulesProperty, true, true, true, true);
            ruleList.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, $"Rules (drag to reorder) - {borderRulesProperty.arraySize} item(s)");
            };

            ruleList.onAddCallback = list =>
            {
                AddNewRule();
                selectedRuleIndex = borderRulesProperty.arraySize - 1;
            };

            ruleList.onRemoveCallback = list =>
            {
                if (!EditorUtility.DisplayDialog("Delete Rule",
                        "Delete selected rule?", "Delete", "Cancel"))
                    return;

                if (list.index >= 0 && list.index < borderRulesProperty.arraySize)
                {
                    borderRulesProperty.DeleteArrayElementAtIndex(list.index);
                    selectedRuleIndex = Mathf.Clamp(list.index, 0, borderRulesProperty.arraySize - 1);
                }
            };

            ruleList.onSelectCallback = list =>
            {
                selectedRuleIndex = list.index;
            };

            ruleList.onReorderCallback = list =>
            {
                selectedRuleIndex = list.index;
            };

            ruleList.elementHeight = EditorGUIUtility.singleLineHeight + 8;
            ruleList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                SerializedProperty rule = borderRulesProperty.GetArrayElementAtIndex(index);
                SerializedProperty nameProp = rule.FindPropertyRelative(RULE_NAME_PROP);
                SerializedProperty typeProp = rule.FindPropertyRelative(RULE_TYPE_PROP);
                SerializedProperty prefabProp = rule.FindPropertyRelative(PREFAB_PROP);

                rect.y += 2;
                float indexWidth = 26f;
                float typeWidth = 40f;
                float prefabWidth = 160f;
                float spacing = 6f;

                Rect indexRect = new Rect(rect.x, rect.y, indexWidth, EditorGUIUtility.singleLineHeight);
                Rect typeRect = new Rect(indexRect.xMax + spacing, rect.y, typeWidth, EditorGUIUtility.singleLineHeight);
                Rect prefabRect = new Rect(rect.xMax - prefabWidth, rect.y, prefabWidth, EditorGUIUtility.singleLineHeight);
                Rect nameRect = new Rect(typeRect.xMax + spacing, rect.y,
                    Mathf.Max(60f, prefabRect.x - (typeRect.xMax + spacing * 2f)),
                    EditorGUIUtility.singleLineHeight);

                EditorGUI.LabelField(indexRect, $"#{index}");
                EditorGUI.LabelField(typeRect, RuleTypeShortLabels[typeProp.enumValueIndex], EditorStyles.miniBoldLabel);
                nameProp.stringValue = EditorGUI.TextField(nameRect, nameProp.stringValue);

                Object prefab = prefabProp.objectReferenceValue;
                string prefabName = prefab != null ? prefab.name : "No Prefab";
                EditorGUI.LabelField(prefabRect, prefabName, EditorStyles.miniLabel);
            };
        }

        private void AddNewRule()
        {
            borderRulesProperty.InsertArrayElementAtIndex(borderRulesProperty.arraySize);
            SerializedProperty newRule = borderRulesProperty.GetArrayElementAtIndex(borderRulesProperty.arraySize - 1);
            newRule.FindPropertyRelative(RULE_NAME_PROP).stringValue = $"Rule {borderRulesProperty.arraySize - 1}";
            newRule.FindPropertyRelative(RULE_TYPE_PROP).enumValueIndex = 0;
            newRule.FindPropertyRelative(PREFAB_PROP).objectReferenceValue = null;
            newRule.FindPropertyRelative(POSITION_OFFSET_PROP).vector3Value = Vector3.zero;
            newRule.FindPropertyRelative(ROTATION_OFFSET_PROP).vector3Value = Vector3.zero;

            SerializedProperty neighbors = newRule.FindPropertyRelative(NEIGHBORS_PROP);
            neighbors.arraySize = 9;
            for (int n = 0; n < 9; n++)
                neighbors.GetArrayElementAtIndex(n).enumValueIndex = 0;
        }

        private void DrawSelectedRuleDetails(int index, SerializedProperty ruleProperty)
        {
            SerializedProperty nameProp = ruleProperty.FindPropertyRelative(RULE_NAME_PROP);
            SerializedProperty typeProp = ruleProperty.FindPropertyRelative(RULE_TYPE_PROP);
            SerializedProperty neighborsProp = ruleProperty.FindPropertyRelative(NEIGHBORS_PROP);
            SerializedProperty prefabProp = ruleProperty.FindPropertyRelative(PREFAB_PROP);
            SerializedProperty posOffsetProp = ruleProperty.FindPropertyRelative(POSITION_OFFSET_PROP);
            SerializedProperty rotOffsetProp = ruleProperty.FindPropertyRelative(ROTATION_OFFSET_PROP);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"Selected Rule #{index}", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            nameProp.stringValue = EditorGUILayout.TextField("Name", nameProp.stringValue);
            if (GUILayout.Button("Duplicate", GUILayout.Width(80)))
            {
                DuplicateRule(index);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(typeProp, new GUIContent("Rule Type"));

            if (neighborsProp.arraySize != 9)
                neighborsProp.arraySize = 9;

            Draw3x3Grid(neighborsProp, typeProp);

            EditorGUILayout.PropertyField(prefabProp, new GUIContent("Prefab"));
            DrawPrefabPreview(prefabProp.objectReferenceValue);
            EditorGUILayout.PropertyField(posOffsetProp, new GUIContent("Position Offset"));
            EditorGUILayout.PropertyField(rotOffsetProp, new GUIContent("Rotation Offset"));
            EditorGUILayout.EndVertical();
        }

        private void DrawPrefabPreview(Object prefabObject)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Prefab Preview", EditorStyles.boldLabel);

            const float previewHeight = 180f;
            Rect previewRect = GUILayoutUtility.GetRect(10f, previewHeight, GUILayout.ExpandWidth(true));

            if (!prefabObject)
            {
                EditorGUI.HelpBox(previewRect, "Assign a Prefab to see preview.", MessageType.Info);
                return;
            }

            Editor.CreateCachedEditor(prefabObject, null, ref prefabPreviewEditor);
            if (prefabPreviewEditor && prefabPreviewEditor.HasPreviewGUI())
            {
                prefabPreviewEditor.OnInteractivePreviewGUI(previewRect, GUIStyle.none);
                return;
            }

            Texture2D previewTexture = AssetPreview.GetAssetPreview(prefabObject) as Texture2D;
            if (!previewTexture)
                previewTexture = AssetPreview.GetMiniThumbnail(prefabObject);

            if (previewTexture)
            {
                GUI.DrawTexture(previewRect, previewTexture, ScaleMode.ScaleToFit);
                return;
            }

            EditorGUI.HelpBox(previewRect, "Preview is loading...", MessageType.None);
            Repaint();
        }

        private void DuplicateRule(int sourceIndex)
        {
            if (sourceIndex < 0 || sourceIndex >= borderRulesProperty.arraySize)
                return;

            SerializedProperty source = borderRulesProperty.GetArrayElementAtIndex(sourceIndex);
            borderRulesProperty.InsertArrayElementAtIndex(sourceIndex + 1);
            SerializedProperty target = borderRulesProperty.GetArrayElementAtIndex(sourceIndex + 1);

            target.FindPropertyRelative(RULE_NAME_PROP).stringValue =
                source.FindPropertyRelative(RULE_NAME_PROP).stringValue + " Copy";
            target.FindPropertyRelative(RULE_TYPE_PROP).enumValueIndex =
                source.FindPropertyRelative(RULE_TYPE_PROP).enumValueIndex;
            target.FindPropertyRelative(PREFAB_PROP).objectReferenceValue =
                source.FindPropertyRelative(PREFAB_PROP).objectReferenceValue;
            target.FindPropertyRelative(POSITION_OFFSET_PROP).vector3Value =
                source.FindPropertyRelative(POSITION_OFFSET_PROP).vector3Value;
            target.FindPropertyRelative(ROTATION_OFFSET_PROP).vector3Value =
                source.FindPropertyRelative(ROTATION_OFFSET_PROP).vector3Value;

            SerializedProperty sourceNeighbors = source.FindPropertyRelative(NEIGHBORS_PROP);
            SerializedProperty targetNeighbors = target.FindPropertyRelative(NEIGHBORS_PROP);
            targetNeighbors.arraySize = 9;
            for (int i = 0; i < 9; i++)
            {
                targetNeighbors.GetArrayElementAtIndex(i).enumValueIndex =
                    sourceNeighbors.GetArrayElementAtIndex(i).enumValueIndex;
            }

            selectedRuleIndex = sourceIndex + 1;
        }

        private void DrawRuleLegend()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Legend", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            DrawLegendItem(DONT_CARE_COLOR, "Empty = Don't care");
            DrawLegendItem(THIS_COLOR, "Arrow = This");
            DrawLegendItem(NOT_THIS_COLOR, "X = Not This");
            DrawLegendItem(INNER_TILE_COLOR, "I = Inner Tile");
            DrawLegendItem(OBSTACLE_COLOR, "O = Obstacle");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("Tip: drag rules in list to set priority (top matches first).", MessageType.None);
            EditorGUILayout.EndVertical();
        }

        private static void DrawLegendItem(Color color, string label)
        {
            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUILayout.Box("", GUILayout.Width(14), GUILayout.Height(14));
            GUI.backgroundColor = prev;
            GUILayout.Label(label, EditorStyles.miniLabel, GUILayout.Width(120));
        }

        private void Draw3x3Grid(SerializedProperty neighborsProp, SerializedProperty ruleTypeProp)
        {
            const float cellSize = 36f;
            const float spacing = 2f;

            EditorGUILayout.LabelField("3x3 Neighbor Grid:");

            Color originalBg = GUI.backgroundColor;

            for (int row = 0; row < 3; row++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                for (int col = 0; col < 3; col++)
                {
                    int idx = row * 3 + col;

                    if (idx == 4)
                    {
                        DrawCenterCell(ruleTypeProp, cellSize);
                    }
                    else
                    {
                        DrawNeighborCell(neighborsProp, idx, cellSize);
                    }

                    if (col < 2) GUILayout.Space(spacing);
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                if (row < 2) GUILayout.Space(spacing);
            }

            GUI.backgroundColor = originalBg;
        }

        private void DrawNeighborCell(SerializedProperty neighborsProp, int idx, float cellSize)
        {
            SerializedProperty cellProp = neighborsProp.GetArrayElementAtIndex(idx);
            NeighborCondition condition = (NeighborCondition)cellProp.enumValueIndex;

            string label;
            switch (condition)
            {
                case NeighborCondition.This:
                    GUI.backgroundColor = THIS_COLOR;
                    label = NeighborLabels[idx];
                    break;
                case NeighborCondition.NotThis:
                    GUI.backgroundColor = NOT_THIS_COLOR;
                    label = "\u2715";
                    break;
                case NeighborCondition.InnerTile:
                    GUI.backgroundColor = INNER_TILE_COLOR;
                    label = "I";
                    break;
                case NeighborCondition.Gate:
                    GUI.backgroundColor = GATE_COLOR;
                    label = "G";
                    break;
                case NeighborCondition.Obstacle:
                    GUI.backgroundColor = OBSTACLE_COLOR;
                    label = "O";
                    break;
                default:
                    GUI.backgroundColor = DONT_CARE_COLOR;
                    label = "";
                    break;
            }

            if (GUILayout.Button(label, GUILayout.Width(cellSize), GUILayout.Height(cellSize)))
            {
                int conditionCount = System.Enum.GetValues(typeof(NeighborCondition)).Length;
                cellProp.enumValueIndex = ((int)condition + 1) % conditionCount;
            }
        }

        private void DrawCenterCell(SerializedProperty ruleTypeProp, float cellSize)
        {
            GUI.backgroundColor = CENTER_COLOR;
            BorderRuleType ruleType = (BorderRuleType)ruleTypeProp.enumValueIndex;
            string label = RuleTypeShortLabels[(int)ruleType];

            if (GUILayout.Button(label, GUILayout.Width(cellSize), GUILayout.Height(cellSize)))
            {
                ruleTypeProp.enumValueIndex = ((int)ruleType + 1) % RuleTypeShortLabels.Length;
            }
        }
    }
}
