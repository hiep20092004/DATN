using UnityEngine;
using UnityEditor;
using System.Linq;

namespace WaterFlow.Game
{
    /// <summary>
    /// UI component for the Settings/Info tab.
    /// Provides utility functions, validation, and information about the data asset.
    /// </summary>
    public class SettingsTabUI
    {
        private readonly EditorStylesManager styles;
        private Vector2 scrollPosition;

        public SettingsTabUI(EditorStylesManager styles)
        {
            this.styles = styles;
        }

        public void Draw(BlocksVisualsData data)
        {
            DrawAssetInfo(data);
            EditorGUILayout.Space(10);
            DrawValidationSection(data);
            EditorGUILayout.Space(10);
            DrawUtilities(data);
            EditorGUILayout.Space(10);
            DrawStatistics(data);
        }

        #region Asset Info
        
        private void DrawAssetInfo(BlocksVisualsData data)
        {
            EditorGUILayout.BeginVertical(styles.BoxStyle);

            EditorGUILayout.LabelField("ℹ Asset Information", styles.SectionHeaderStyle);
            EditorGUILayout.Space(8);

            string assetPath = AssetDatabase.GetAssetPath(data);
            string assetGuid = AssetDatabase.AssetPathToGUID(assetPath);

            DrawInfoRow("Asset Name:", data.name);
            DrawInfoRow("Asset Path:", assetPath);
            DrawInfoRow("GUID:", assetGuid);

            EditorGUILayout.Space(5);

            // Quick actions
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("📋 Copy Path", GUILayout.Width(120)))
            {
                EditorGUIUtility.systemCopyBuffer = assetPath;
                Debug.Log($"Copied to clipboard: {assetPath}");
            }

            if (GUILayout.Button("📂 Show in Project", GUILayout.Width(140)))
            {
                EditorGUIUtility.PingObject(data);
                Selection.activeObject = data;
            }

            if (GUILayout.Button("🔄 Reload Asset", GUILayout.Width(120)))
            {
                AssetDatabase.Refresh();
                data.Init();
                Debug.Log("Asset reloaded and initialized");
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawInfoRow(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, styles.BoldLabelStyle, GUILayout.Width(100));
            EditorGUILayout.SelectableLabel(value, GUILayout.Height(16));
            EditorGUILayout.EndHorizontal();
        }
        
        #endregion

        #region Validation
        
        private void DrawValidationSection(BlocksVisualsData data)
        {
            EditorGUILayout.BeginVertical(styles.BoxStyle);

            EditorGUILayout.LabelField("✓ Validation", styles.SectionHeaderStyle);
            EditorGUILayout.Space(8);

            var issues = ValidateData(data);

            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox("✓ No issues found. All configurations are valid!", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox($"Found {issues.Count} issue(s) that need attention:", MessageType.Warning);

                EditorGUILayout.Space(5);

                foreach (var issue in issues)
                {
                    EditorGUILayout.BeginHorizontal(styles.CardStyle);
                    EditorGUILayout.LabelField(issue.Icon, GUILayout.Width(30));
                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField(issue.Title, styles.BoldLabelStyle);
                    EditorGUILayout.LabelField(issue.Description, styles.HintStyle);
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space(3);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private System.Collections.Generic.List<ValidationIssue> ValidateData(BlocksVisualsData data)
        {
            var issues = new System.Collections.Generic.List<ValidationIssue>();

            // Check blocks
            if (data.Blocks == null || data.Blocks.Length == 0)
            {
                issues.Add(new ValidationIssue
                {
                    Icon = "📦",
                    Title = "No blocks configured",
                    Description = "Add at least one block to the configuration"
                });
            }
            else
            {
                for (int i = 0; i < data.Blocks.Length; i++)
                {
                    var block = data.Blocks[i];
                    
                    if (block.Prefab == null)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Icon = "⚠",
                            Title = $"Block '{block.Type}' missing prefab",
                            Description = "Assign a prefab to this block configuration"
                        });
                    }
                    else if (block.Prefab.GetComponent<LevelBlockBehavior>() == null)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Icon = "⚠",
                            Title = $"Block '{block.Type}' prefab missing component",
                            Description = "Prefab must have LevelBlockBehavior component"
                        });
                    }
                }

                // Check for duplicate block types
                var duplicates = data.Blocks
                    .GroupBy(b => b.Type)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key);

                foreach (var dup in duplicates)
                {
                    issues.Add(new ValidationIssue
                    {
                        Icon = "⚠",
                        Title = $"Duplicate block type: {dup}",
                        Description = "Each block type should only appear once"
                    });
                }
            }

            // Check colors
            if (data.Colors == null || data.Colors.Length == 0)
            {
                issues.Add(new ValidationIssue
                {
                    Icon = "🎨",
                    Title = "No colors configured",
                    Description = "Add at least one color to the configuration"
                });
            }
            else
            {
                // Check for duplicate color types
                var duplicates = data.Colors
                    .GroupBy(c => c.Type)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key);

                foreach (var dup in duplicates)
                {
                    issues.Add(new ValidationIssue
                    {
                        Icon = "⚠",
                        Title = $"Duplicate color type: {dup}",
                        Description = "Each color type should only appear once"
                    });
                }

                // Check for colors without materials
                for (int i = 0; i < data.Colors.Length; i++)
                {
                    var color = data.Colors[i];
                    int materialCount = 0;
                    if (color.Material != null) materialCount++;
                    if (color.PipeMaterial != null) materialCount++;
                    if (color.GlassMaterial != null) materialCount++;
                    if (color.WaterMaterial != null) materialCount++;
                    if (color.FlowMaterial != null) materialCount++;

                    if (materialCount == 0)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Icon = "ℹ",
                            Title = $"Color '{color.Type}' has no materials",
                            Description = "Consider assigning at least one material"
                        });
                    }
                }
            }

            return issues;
        }

        private struct ValidationIssue
        {
            public string Icon;
            public string Title;
            public string Description;
        }
        
        #endregion

        #region Utilities
        
        private void DrawUtilities(BlocksVisualsData data)
        {
            EditorGUILayout.BeginVertical(styles.BoxStyle);

            EditorGUILayout.LabelField("🔧 Utilities", styles.SectionHeaderStyle);
            EditorGUILayout.Space(8);

            // Initialize button
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("⚡ Initialize Data", GUILayout.Height(30), GUILayout.Width(150)))
            {
                data.Init();
                Debug.Log("✓ Data initialized successfully");
                EditorUtility.DisplayDialog("Success", "Data initialized successfully!", "OK");
            }

            EditorGUILayout.LabelField(
                "Initializes all block behaviors and figures",
                styles.HintStyle
            );
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Clear all button
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = styles.GetDangerColor();
            if (GUILayout.Button("🗑 Clear All Data", GUILayout.Height(30), GUILayout.Width(150)))
            {
                if (EditorUtility.DisplayDialog(
                    "Clear All Data",
                    "Are you sure you want to clear ALL blocks and colors?\n\nThis action cannot be undone!",
                    "Clear All", "Cancel"))
                {
                    ClearAllData(data);
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.LabelField(
                "Removes all blocks and colors (cannot be undone!)",
                styles.HintStyle
            );
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Export summary button
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📄 Export Summary", GUILayout.Height(30), GUILayout.Width(150)))
            {
                ExportSummary(data);
            }

            EditorGUILayout.LabelField(
                "Export a text summary of the configuration",
                styles.HintStyle
            );
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void ClearAllData(BlocksVisualsData data)
        {
            SerializedObject serializedObject = new SerializedObject(data);
            
            SerializedProperty blocksProperty = serializedObject.FindProperty("blocks");
            blocksProperty.ClearArray();
            
            SerializedProperty colorsProperty = serializedObject.FindProperty("colors");
            colorsProperty.ClearArray();
            
            serializedObject.ApplyModifiedProperties();
            
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            
            Debug.Log("✓ All data cleared");
        }

        private void ExportSummary(BlocksVisualsData data)
        {
            string summary = GenerateSummary(data);
            
            string path = EditorUtility.SaveFilePanel(
                "Export Configuration Summary",
                "",
                $"{data.name}_Summary.txt",
                "txt"
            );

            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, summary);
                Debug.Log($"✓ Summary exported to: {path}");
                EditorUtility.DisplayDialog("Export Complete", $"Summary exported successfully to:\n{path}", "OK");
            }
        }

        private string GenerateSummary(BlocksVisualsData data)
        {
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine("=".PadRight(60, '='));
            sb.AppendLine($"Blocks Visuals Data Summary: {data.name}");
            sb.AppendLine($"Generated: {System.DateTime.Now}");
            sb.AppendLine("=".PadRight(60, '='));
            sb.AppendLine();

            // Blocks
            sb.AppendLine($"BLOCKS ({data.Blocks?.Length ?? 0})");
            sb.AppendLine("-".PadRight(60, '-'));
            if (data.Blocks != null)
            {
                foreach (var block in data.Blocks)
                {
                    sb.AppendLine($"• {block.Type}");
                    sb.AppendLine($"  Prefab: {(block.Prefab ? block.Prefab.name : "None")}");
                    sb.AppendLine();
                }
            }

            // Colors
            sb.AppendLine($"COLORS ({data.Colors?.Length ?? 0})");
            sb.AppendLine("-".PadRight(60, '-'));
            if (data.Colors != null)
            {
                foreach (var color in data.Colors)
                {
                    sb.AppendLine($"• {color.Type}");
                    sb.AppendLine($"  Color: {color.Color}");
                    sb.AppendLine($"  Main Material: {(color.Material ? color.Material.name : "None")}");
                    sb.AppendLine($"  Pipe Material: {(color.PipeMaterial ? color.PipeMaterial.name : "None")}");
                    sb.AppendLine($"  Glass Material: {(color.GlassMaterial ? color.GlassMaterial.name : "None")}");
                    sb.AppendLine($"  Water Material: {(color.WaterMaterial ? color.WaterMaterial.name : "None")}");
                    sb.AppendLine($"  Flow Material: {(color.FlowMaterial ? color.FlowMaterial.name : "None")}");
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }
        
        #endregion

        #region Statistics
        
        private void DrawStatistics(BlocksVisualsData data)
        {
            EditorGUILayout.BeginVertical(styles.BoxStyle);

            EditorGUILayout.LabelField("📊 Statistics", styles.SectionHeaderStyle);
            EditorGUILayout.Space(8);

            int totalBlocks = data.Blocks?.Length ?? 0;
            int totalColors = data.Colors?.Length ?? 0;
            int totalMaterials = 0;

            if (data.Colors != null)
            {
                foreach (var color in data.Colors)
                {
                    if (color.Material != null) totalMaterials++;
                    if (color.PipeMaterial != null) totalMaterials++;
                    if (color.GlassMaterial != null) totalMaterials++;
                    if (color.WaterMaterial != null) totalMaterials++;
                    if (color.FlowMaterial != null) totalMaterials++;
                }
            }

            EditorGUILayout.BeginHorizontal();
            DrawStatBox("Total Blocks", totalBlocks.ToString(), "📦");
            DrawStatBox("Total Colors", totalColors.ToString(), "🎨");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            DrawStatBox("Total Materials", totalMaterials.ToString(), "🖼");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawStatBox(string label, string value, string icon)
        {
            EditorGUILayout.BeginVertical(styles.CardStyle, GUILayout.Width(205));
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(icon, GUILayout.Width(25));
            EditorGUILayout.LabelField(label, styles.LabelStyle);
            EditorGUILayout.EndHorizontal();
            
            GUIStyle valueStyle = new GUIStyle(styles.TypeLabelStyle)
            {
                fontSize = 24,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField(value, valueStyle, GUILayout.Height(35));
            
            EditorGUILayout.EndVertical();
        }
        
        #endregion
    }
}