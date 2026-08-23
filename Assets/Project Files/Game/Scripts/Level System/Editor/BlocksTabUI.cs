using UnityEngine;
using UnityEditor;

namespace WaterFlow.Game
{
    /// <summary>
    /// UI component for the Blocks tab.
    /// Handles block configuration, display, and management.
    /// </summary>
    public class BlocksTabUI
    {
        private readonly PreviewManager previewManager;
        private readonly DataOperationsHandler dataOps;
        private readonly EditorStylesManager styles;

        // Input fields
        private BlockType newBlockType;
        private GameObject newBlockPrefab;

        // UI state
        private Vector2 gridScrollPosition;

        public BlocksTabUI(PreviewManager previewManager, DataOperationsHandler dataOps, EditorStylesManager styles)
        {
            this.previewManager = previewManager;
            this.dataOps = dataOps;
            this.styles = styles;
        }

        public void Draw(BlocksVisualsData data)
        {
            DrawAddBlockSection(data);
            EditorGUILayout.Space(10);
            DrawBlocksGrid(data);
        }

        public void Refresh()
        {
            newBlockPrefab = null;
        }

        public void Cleanup()
        {
            // Cleanup handled by PreviewManager
        }

        #region Add Block Section
        
        private void DrawAddBlockSection(BlocksVisualsData data)
        {
            EditorGUILayout.BeginVertical(styles.BoxStyle);

            // Section header
            EditorGUILayout.LabelField("➕ Add New Block", styles.SectionHeaderStyle);
            EditorGUILayout.Space(8);

            // Main fields row
            EditorGUILayout.BeginHorizontal();

            // Block Type
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            EditorGUILayout.LabelField("Block Type *", styles.RequiredFieldLabelStyle);
            newBlockType = (BlockType)EditorGUILayout.EnumPopup(newBlockType);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Prefab
            EditorGUILayout.BeginVertical(GUILayout.Width(250));
            EditorGUILayout.LabelField("Prefab *", styles.RequiredFieldLabelStyle);
            newBlockPrefab = (GameObject)EditorGUILayout.ObjectField(
                newBlockPrefab,
                typeof(GameObject),
                false
            );
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            // Add button
            EditorGUILayout.BeginVertical();
            GUILayout.Space(20);

            bool canAdd = newBlockPrefab != null && !dataOps.IsBlockTypeExists(data, newBlockType);
            GUI.enabled = canAdd;

            if (GUILayout.Button("+ Add Block", styles.AddButtonStyle, GUILayout.Width(120), GUILayout.Height(28)))
            {
                if (dataOps.AddBlock(data, newBlockType, newBlockPrefab))
                {
                    newBlockPrefab = null;
                    GUI.FocusControl(null);
                }
            }

            GUI.enabled = true;
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            // Validation messages
            if (newBlockPrefab != null)
            {
                var validation = dataOps.ValidateBlock(newBlockPrefab);
                if (!validation.IsValid || validation.HasWarnings)
                {
                    EditorGUILayout.Space(5);
                    DrawValidationMessages(validation);
                }
            }

            // Help text
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(
                "* Required fields. Prefab must have LevelBlockBehavior component.",
                styles.HintStyle
            );

            EditorGUILayout.EndVertical();
        }

        private void DrawValidationMessages(ValidationResult validation)
        {
            foreach (var error in validation.Errors)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }

            foreach (var warning in validation.Warnings)
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }
        }
        
        #endregion

        #region Blocks Grid
        
        private void DrawBlocksGrid(BlocksVisualsData data)
        {
            BlockData[] blocks = data.Blocks ?? new BlockData[0];

            EditorGUILayout.BeginVertical(styles.BoxStyle);

            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"📦 Current Blocks ({blocks.Length})", styles.SectionHeaderStyle);

            GUILayout.FlexibleSpace();

            // Sort/Filter options could go here
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            if (blocks.Length == 0)
            {
                DrawEmptyState();
            }
            else
            {
                // Grid layout - 2 columns
                int columns = 2;
                int rows = Mathf.CeilToInt(blocks.Length / (float)columns);

                for (int row = 0; row < rows; row++)
                {
                    EditorGUILayout.BeginHorizontal();

                    for (int col = 0; col < columns; col++)
                    {
                        int index = row * columns + col;
                        if (index < blocks.Length)
                        {
                            DrawBlockCard(data, blocks[index], index);
                        }
                        else
                        {
                            GUILayout.FlexibleSpace();
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space(5);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawEmptyState()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.Space(20);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("📭", GUILayout.Width(40));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("No blocks configured yet", styles.SubtitleStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Add your first block above to get started", styles.HintStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(20);
            EditorGUILayout.EndVertical();
        }

        private void DrawBlockCard(BlocksVisualsData data, BlockData blockData, int index)
        {
            EditorGUILayout.BeginVertical(styles.CardStyle, GUILayout.Width(430), GUILayout.MinHeight(240));

            // Card Header
            DrawBlockCardHeader(data, blockData, index);

            EditorGUILayout.Space(8);

            // Card Content
            DrawBlockCardContent(data, blockData, index);

            EditorGUILayout.EndVertical();
        }

        private void DrawBlockCardHeader(BlocksVisualsData data, BlockData blockData, int index)
        {
            EditorGUILayout.BeginHorizontal();

            // Type badge
            GUIStyle typeStyle = new GUIStyle(styles.TypeLabelStyle)
            {
                normal = { textColor = styles.GetPrimaryColor() }
            };
            EditorGUILayout.LabelField($"🔷 {blockData.Type}", typeStyle);

            GUILayout.FlexibleSpace();

            // Remove button
            GUI.backgroundColor = styles.GetDangerColor();
            if (GUILayout.Button("✕ Remove", styles.RemoveButtonStyle, GUILayout.Width(85)))
            {
                if (EditorUtility.DisplayDialog(
                    "Remove Block",
                    $"Are you sure you want to remove '{blockData.Type}'?\n\nThis action cannot be undone.",
                    "Remove", "Cancel"))
                {
                    dataOps.RemoveBlock(data, index);
                    previewManager.RemoveBlockPreview(index);
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawBlockCardContent(BlocksVisualsData data, BlockData blockData, int index)
        {
            SerializedObject serializedObject = new SerializedObject(data);
            SerializedProperty blocksProperty = serializedObject.FindProperty("blocks");
            SerializedProperty blockProperty = blocksProperty.GetArrayElementAtIndex(index);

            // Fields
            EditorGUILayout.BeginVertical();

            // Block Type (read-only)
            GUI.enabled = false;
            EditorGUILayout.PropertyField(blockProperty.FindPropertyRelative("type"), new GUIContent("Block Type"));
            GUI.enabled = true;

            // Prefab
            EditorGUILayout.PropertyField(blockProperty.FindPropertyRelative("prefab"), new GUIContent("Prefab *"));

            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);

            // Preview section
            DrawBlockPreview(blockData, index);
        }

        private void DrawBlockPreview(BlockData blockData, int index)
        {
            EditorGUILayout.LabelField("Preview", styles.BoldLabelStyle);

            if (blockData.Prefab != null)
            {
                var behavior = blockData.Prefab.GetComponent<LevelBlockBehavior>();
                if (behavior != null)
                {
                    Rect previewRect = GUILayoutUtility.GetRect(400, 140);
                    
                    // Draw background
                    EditorGUI.DrawRect(previewRect, new Color(0.15f, 0.15f, 0.15f));
                    
                    // Draw preview
                    previewManager.DrawBlockPreview(blockData.Prefab, index, previewRect);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "⚠ Missing LevelBlockBehavior component on prefab!",
                        MessageType.Error
                    );
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No prefab assigned", MessageType.Warning);
            }
        }
        
        #endregion
    }
}