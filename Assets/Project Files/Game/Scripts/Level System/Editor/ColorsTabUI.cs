using UnityEngine;
using UnityEditor;

namespace WaterFlow.Game
{
    /// <summary>
    /// UI component for the Colors tab.
    /// Handles color configuration with multiple material types.
    /// </summary>
    public class ColorsTabUI
    {
        private readonly DataOperationsHandler dataOps;
        private readonly EditorStylesManager styles;

        // Input fields
        private BlockColor newColorType;
        private Material newMaterial;
        private Material newPipeMaterial;
        private Material newGlassMaterial;
        private Material newWaterMaterial;
        private Material newFlowMaterial;
        private Color newColor = Color.white;

        // UI state
        private bool showAllMaterials = true;

        public ColorsTabUI(DataOperationsHandler dataOps, EditorStylesManager styles)
        {
            this.dataOps = dataOps;
            this.styles = styles;
        }

        public void Draw(BlocksVisualsData data)
        {
            DrawAddColorSection(data);
            EditorGUILayout.Space(10);
            DrawColorsGrid(data);
        }

        public void Refresh()
        {
            newMaterial = null;
            newPipeMaterial = null;
            newGlassMaterial = null;
            newWaterMaterial = null;
            newFlowMaterial = null;
            newColor = Color.white;
        }

        public void Cleanup()
        {
            // No cleanup needed
        }

        #region Add Color Section
        
        private void DrawAddColorSection(BlocksVisualsData data)
        {
            EditorGUILayout.BeginVertical(styles.BoxStyle);

            // Section header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("🎨 Add New Color Configuration", styles.SectionHeaderStyle);
            GUILayout.FlexibleSpace();
            
            // Toggle for showing all materials
            showAllMaterials = EditorGUILayout.ToggleLeft(
                "Show All Materials", 
                showAllMaterials, 
                GUILayout.Width(130)
            );
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(8);

            // Color Type and Base Color row
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical(GUILayout.Width(180));
            EditorGUILayout.LabelField("Color Type *", styles.RequiredFieldLabelStyle);
            newColorType = (BlockColor)EditorGUILayout.EnumPopup(newColorType);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginVertical(GUILayout.Width(180));
            EditorGUILayout.LabelField("Base Color *", styles.RequiredFieldLabelStyle);
            newColor = EditorGUILayout.ColorField(newColor);
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Materials section
            EditorGUILayout.LabelField("Materials", styles.BoldLabelStyle);
            EditorGUILayout.Space(5);

            if (showAllMaterials)
            {
                DrawAllMaterialFields();
            }
            else
            {
                DrawEssentialMaterialFields();
            }

            EditorGUILayout.Space(10);

            // Add button row
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            bool canAdd = !dataOps.IsColorTypeExists(data, newColorType);
            GUI.enabled = canAdd;

            if (GUILayout.Button("+ Add Color Configuration", styles.AddButtonStyle, GUILayout.Width(200), GUILayout.Height(30)))
            {
                if (dataOps.AddColor(
                    data, 
                    newColorType, 
                    newMaterial,
                    newPipeMaterial,
                    newGlassMaterial,
                    newWaterMaterial,
                    newFlowMaterial,
                    newColor))
                {
                    Refresh();
                    GUI.FocusControl(null);
                }
            }

            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            // Help text
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(
                "* Required fields. All material fields are optional and can be assigned later.",
                styles.HintStyle
            );

            EditorGUILayout.EndVertical();
        }

        private void DrawEssentialMaterialFields()
        {
            // Show only main material in compact mode
            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Main Material", styles.FieldLabelStyle, GUILayout.Width(100));
            newMaterial = (Material)EditorGUILayout.ObjectField(
                newMaterial,
                typeof(Material),
                false,
                GUILayout.Width(250)
            );
            EditorGUILayout.EndVertical();

            if (newMaterial != null)
            {
                PreviewManager.DrawMaterialPreviewCompact(newMaterial, 60);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawAllMaterialFields()
        {
            // Row 1: Main and Pipe
            EditorGUILayout.BeginHorizontal();
            DrawMaterialField("Main Material", ref newMaterial, 200);
            EditorGUILayout.Space(10);
            DrawMaterialField("Pipe Material", ref newPipeMaterial, 200);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Row 2: Glass and Water
            EditorGUILayout.BeginHorizontal();
            DrawMaterialField("Glass Material", ref newGlassMaterial, 200);
            EditorGUILayout.Space(10);
            DrawMaterialField("Water Material", ref newWaterMaterial, 200);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Row 3: Flow
            EditorGUILayout.BeginHorizontal();
            DrawMaterialField("Flow Material", ref newFlowMaterial, 200);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawMaterialField(string label, ref Material material, float width)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(width));
            EditorGUILayout.LabelField(label, styles.OptionalFieldLabelStyle);
            material = (Material)EditorGUILayout.ObjectField(
                material,
                typeof(Material),
                false
            );
            EditorGUILayout.EndVertical();
        }
        
        #endregion

        #region Colors Grid
        
        private void DrawColorsGrid(BlocksVisualsData data)
        {
            BlockColorData[] colors = data.Colors ?? new BlockColorData[0];

            EditorGUILayout.BeginVertical(styles.BoxStyle);

            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"🎨 Current Colors ({colors.Length})", styles.SectionHeaderStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            if (colors.Length == 0)
            {
                DrawEmptyState();
            }
            else
            {
                // Grid layout - 2 columns
                int columns = 2;
                int rows = Mathf.CeilToInt(colors.Length / (float)columns);

                for (int row = 0; row < rows; row++)
                {
                    EditorGUILayout.BeginHorizontal();

                    for (int col = 0; col < columns; col++)
                    {
                        int index = row * columns + col;
                        if (index < colors.Length)
                        {
                            DrawColorCard(data, colors[index], index);
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
            EditorGUILayout.LabelField("🎨", GUILayout.Width(40));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("No colors configured yet", styles.SubtitleStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Add your first color configuration above", styles.HintStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(20);
            EditorGUILayout.EndVertical();
        }

        private void DrawColorCard(BlocksVisualsData data, BlockColorData colorData, int index)
        {
            EditorGUILayout.BeginVertical(styles.CardStyle, GUILayout.Width(430));

            // Card Header
            DrawColorCardHeader(data, colorData, index);

            EditorGUILayout.Space(8);

            // Card Content
            DrawColorCardContent(data, colorData, index);

            EditorGUILayout.Space(8);
            EditorGUILayout.EndVertical();
        }

        private void DrawColorCardHeader(BlocksVisualsData data, BlockColorData colorData, int index)
        {
            EditorGUILayout.BeginHorizontal();

            // Color preview square
            PreviewManager.DrawColorPreview(colorData.Color, 28);

            GUILayout.Space(8);

            // Type label
            GUIStyle typeStyle = new GUIStyle(styles.TypeLabelStyle)
            {
                normal = { textColor = styles.GetAccentColor() }
            };
            EditorGUILayout.LabelField($"⬤ {colorData.Type}", typeStyle);

            GUILayout.FlexibleSpace();

            // Remove button
            GUI.backgroundColor = styles.GetDangerColor();
            if (GUILayout.Button("✕ Remove", styles.RemoveButtonStyle, GUILayout.Width(85)))
            {
                if (EditorUtility.DisplayDialog(
                    "Remove Color",
                    $"Are you sure you want to remove '{colorData.Type}'?\n\nThis action cannot be undone.",
                    "Remove", "Cancel"))
                {
                    dataOps.RemoveColor(data, index);
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawColorCardContent(BlocksVisualsData data, BlockColorData colorData, int index)
        {
            SerializedObject serializedObject = new SerializedObject(data);
            SerializedProperty colorsProperty = serializedObject.FindProperty("colors");
            SerializedProperty colorProperty = colorsProperty.GetArrayElementAtIndex(index);

            // Color Type (read-only)
            GUI.enabled = false;
            EditorGUILayout.PropertyField(colorProperty.FindPropertyRelative("type"), new GUIContent("Color Type"));
            GUI.enabled = true;

            // Base Color
            EditorGUILayout.PropertyField(colorProperty.FindPropertyRelative("color"), new GUIContent("Base Color"));

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Materials", styles.BoldLabelStyle);
            EditorGUILayout.Space(3);

            // Materials in a compact grid
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            
            EditorGUILayout.PropertyField(colorProperty.FindPropertyRelative("material"), new GUIContent("Main"));
            EditorGUILayout.PropertyField(colorProperty.FindPropertyRelative("pipeMaterial"), new GUIContent("Pipe"));
            EditorGUILayout.PropertyField(colorProperty.FindPropertyRelative("glassMaterial"), new GUIContent("Glass"));
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.BeginVertical();
            
            EditorGUILayout.PropertyField(colorProperty.FindPropertyRelative("waterMaterial"), new GUIContent("Water"));
            EditorGUILayout.PropertyField(colorProperty.FindPropertyRelative("flowMaterial"), new GUIContent("Flow"));
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();

            // Material preview for main material
            if (colorData.Material != null)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Main Material Preview", styles.BoldLabelStyle);
                PreviewManager.DrawMaterialPreviewCompact(colorData.Material, 80);

                EditorGUILayout.LabelField(
                    $"Shader: {colorData.Material.shader.name}",
                    styles.ShaderInfoStyle
                );
            }

            // Summary of assigned materials
            int assignedCount = 0;
            if (colorData.Material != null) assignedCount++;
            if (colorData.PipeMaterial != null) assignedCount++;
            if (colorData.GlassMaterial != null) assignedCount++;
            if (colorData.WaterMaterial != null) assignedCount++;
            if (colorData.FlowMaterial != null) assignedCount++;

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(
                $"✓ {assignedCount}/5 materials assigned",
                styles.HintStyle
            );
        }
        
        #endregion
    }
}