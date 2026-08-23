using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    public class LevelDatabaseEditorWindow : EditorWindow
    {
        // References
        private LevelDatabase levelDatabase;
        private LevelData[] cachedLevels;
        
        // UI State
        private Vector2 scrollPosition;
        private int selectedTab;
        private string[] tabNames = { "Overview", "Statistics", "Color Tools", "Clone Tool", "Search" };
        
        // Statistics cache
        private Dictionary<BlockColor, int> colorUsageStats = new Dictionary<BlockColor, int>();
        private Dictionary<BlockEffectType, int> blockEffectStats = new Dictionary<BlockEffectType, int>();
        private Dictionary<GateEffectType, int> gateEffectStats = new Dictionary<GateEffectType, int>();
        private Dictionary<LevelType, int> levelTypeStats = new Dictionary<LevelType, int>();
        private Dictionary<Vector2Int, int> sizeStats = new Dictionary<Vector2Int, int>();
        private bool statsCalculated;
        
        // Color swap settings (single level)
        private LevelData colorSwapTargetLevel;
        private BlockColor swapFromColor = BlockColor.Red;
        private BlockColor swapToColor = BlockColor.Blue;
        private bool includeGateData = true;
        private bool includeBlockEffects = true;
        private int colorSwapAffectedElements;
        
        // Clone settings
        private LevelData levelToClone;
        private string cloneName = "Level 999";
        private string clonePath = LevelSystemUtils.SourceLevelsFolder + "/";
        
        // Clone options - what to include
        private ElementType[] cloneElementTypes = Enum.GetValues(typeof(ElementType)).Cast<ElementType>().ToArray();
        private bool[] cloneElementTypeToggles;
        private bool cloneBlockEffects = true;
        private bool cloneGateEffects = true;
        private bool cloneGateData = true;
        
        // Clone color swap list
        [Serializable]
        private class ColorSwapEntry
        {
            public BlockColor fromColor;
            public BlockColor toColor;
            public bool enabled = true;
        }
        private List<ColorSwapEntry> cloneColorSwaps = new List<ColorSwapEntry>();
        private Vector2 cloneColorSwapScrollPos;
        
        // Search settings
        private string searchQuery = "";
        private BlockColor searchColor = BlockColor.None;
        private BlockEffectType searchBlockEffect;
        private GateEffectType searchGateEffect;
        private bool searchByColor;
        private bool searchByBlockEffect;
        private bool searchByGateEffect;
        private bool searchBySize;
        private Vector2Int searchSize = new Vector2Int(8, 8);
        private bool searchBySizeExact = true;
        private bool searchByLevelType;
        private LevelType searchLevelType;
        private List<LevelData> searchResults = new List<LevelData>();
        
        // Pagination for large databases
        private int currentPage;
        private int itemsPerPage = 50;
        private int totalPages;
        
        // Styles
        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle boxStyle;
        private bool stylesInitialized;

        [MenuItem("Tools/Level Extension/Level Database Statistics")]
        public static void ShowWindow()
        {
            var window = GetWindow<LevelDatabaseEditorWindow>("Level DB Stats");
            window.minSize = new Vector2(550, 650);
        }

        private void OnEnable()
        {
            // Try to auto-find LevelDatabase
            string[] guids = AssetDatabase.FindAssets("t:LevelDatabase");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                levelDatabase = AssetDatabase.LoadAssetAtPath<LevelDatabase>(path);
                RefreshCache();
            }
            cloneElementTypeToggles = new bool[cloneElementTypes.Length];
            for (int i = 0; i < cloneElementTypeToggles.Length; i++)
            {
                cloneElementTypeToggles[i] = true;
            }
            
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;
            
            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(0, 0, 10, 5)
            };
            
            subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                margin = new RectOffset(0, 0, 8, 4)
            };
            
            boxStyle = new GUIStyle("box")
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(5, 5, 5, 5)
            };
            
            stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitStyles();
            
            EditorGUILayout.Space(5);
            
            // Database selection
            EditorGUI.BeginChangeCheck();
            levelDatabase = (LevelDatabase)EditorGUILayout.ObjectField("Level Database", levelDatabase, typeof(LevelDatabase), false);
            if (EditorGUI.EndChangeCheck())
            {
                RefreshCache();
            }

            if (levelDatabase == null)
            {
                EditorGUILayout.HelpBox("Please assign a LevelDatabase to begin.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(5);
            
            // Refresh button
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Data", GUILayout.Width(100)))
            {
                RefreshCache();
            }
            EditorGUILayout.LabelField($"Total Levels: {cachedLevels?.Length ?? 0}", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Tabs
            selectedTab = GUILayout.Toolbar(selectedTab, tabNames);

            EditorGUILayout.Space(10);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            switch (selectedTab)
            {
                case 0: DrawOverviewTab(); break;
                case 1: DrawStatisticsTab(); break;
                case 2: DrawColorToolsTab(); break;
                case 3: DrawCloneToolTab(); break;
                case 4: DrawSearchTab(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        #region Overview Tab
        
        private void DrawOverviewTab()
        {
            if (cachedLevels == null) return;

            EditorGUILayout.LabelField("Database Overview", headerStyle);
            
            EditorGUILayout.BeginVertical(boxStyle);
            
            EditorGUILayout.LabelField($"Total Levels: {cachedLevels.Length}");
            
            int randomizableLevels = cachedLevels.Count(l => l != null && l.UseInRandomizer);
            EditorGUILayout.LabelField($"Randomizable Levels: {randomizableLevels}");
            EditorGUILayout.LabelField($"Non-Randomizable Levels: {cachedLevels.Length - randomizableLevels}");
            
            float avgDuration = cachedLevels.Where(l => l != null).Average(l => l.Duration);
            EditorGUILayout.LabelField($"Average Duration: {avgDuration:F1}s");
            
            int totalElements = cachedLevels.Where(l => l != null).Sum(l => l.Elements?.Length ?? 0);
            EditorGUILayout.LabelField($"Total Level Elements: {totalElements}");
            
            EditorGUILayout.EndVertical();
            
            // Quick level list with pagination
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Level List (Quick View)", headerStyle);
            
            totalPages = Mathf.CeilToInt((float)cachedLevels.Length / itemsPerPage);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(currentPage <= 0);
            if (GUILayout.Button("◀ Prev", GUILayout.Width(70)))
                currentPage--;
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.LabelField($"Page {currentPage + 1} / {totalPages}", EditorStyles.centeredGreyMiniLabel);
            
            EditorGUI.BeginDisabledGroup(currentPage >= totalPages - 1);
            if (GUILayout.Button("Next ▶", GUILayout.Width(70)))
                currentPage++;
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            int startIndex = currentPage * itemsPerPage;
            int endIndex = Mathf.Min(startIndex + itemsPerPage, cachedLevels.Length);
            
            for (int i = startIndex; i < endIndex; i++)
            {
                if (cachedLevels[i] == null) continue;
                
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                
                EditorGUILayout.LabelField($"#{i}", GUILayout.Width(50));
                
                if (GUILayout.Button(cachedLevels[i].name, EditorStyles.linkLabel))
                {
                    Selection.activeObject = cachedLevels[i];
                    EditorGUIUtility.PingObject(cachedLevels[i]);
                }
                
                EditorGUILayout.LabelField($"{cachedLevels[i].Size.x}x{cachedLevels[i].Size.y}", GUILayout.Width(60));
                EditorGUILayout.LabelField($"{cachedLevels[i].Duration}s", GUILayout.Width(50));
                EditorGUILayout.LabelField(cachedLevels[i].UseInRandomizer ? "✓" : "✗", GUILayout.Width(20));
                
                EditorGUILayout.EndHorizontal();
            }
        }
        
        #endregion

        #region Statistics Tab
        
        private void DrawStatisticsTab()
        {
            if (cachedLevels == null) return;

            if (!statsCalculated)
            {
                if (GUILayout.Button("Calculate Statistics"))
                {
                    CalculateStatistics();
                }
                EditorGUILayout.HelpBox("Click to calculate statistics. This may take a moment for large databases.", MessageType.Info);
                return;
            }

            // Color Usage
            EditorGUILayout.LabelField("Color Usage Statistics", headerStyle);
            EditorGUILayout.BeginVertical(boxStyle);
            foreach (var kvp in colorUsageStats.OrderByDescending(x => x.Value))
            {
                if (kvp.Value == 0) continue;
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(kvp.Key.ToString(), GUILayout.Width(120));
                
                float ratio = kvp.Value / (float)colorUsageStats.Values.Max();
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.Width(200)), ratio, kvp.Value.ToString());
                
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Level Type Usage
            EditorGUILayout.LabelField("Level Type Distribution", headerStyle);
            EditorGUILayout.BeginVertical(boxStyle);
            foreach (var kvp in levelTypeStats.OrderByDescending(x => x.Value))
            {
                if (kvp.Value == 0) continue;
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(kvp.Key.ToString(), GUILayout.Width(120));
                
                float ratio = kvp.Value / (float)cachedLevels.Length;
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.Width(200)), ratio, $"{kvp.Value} ({ratio:P0})");
                
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Size Distribution
            EditorGUILayout.LabelField("Grid Size Distribution", headerStyle);
            EditorGUILayout.BeginVertical(boxStyle);
            foreach (var kvp in sizeStats.OrderByDescending(x => x.Value))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{kvp.Key.x}x{kvp.Key.y}", GUILayout.Width(80));
                
                float ratio = kvp.Value / (float)cachedLevels.Length;
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.Width(200)), ratio, $"{kvp.Value} ({ratio:P0})");
                
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Block Effects Usage
            if (blockEffectStats.Any(x => x.Value > 0))
            {
                EditorGUILayout.LabelField("Block Effect Usage", headerStyle);
                EditorGUILayout.BeginVertical(boxStyle);
                foreach (var kvp in blockEffectStats.Where(x => x.Value > 0).OrderByDescending(x => x.Value))
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(kvp.Key.ToString(), GUILayout.Width(120));
                    EditorGUILayout.LabelField(kvp.Value.ToString());
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(10);

            // Gate Effects Usage
            if (gateEffectStats.Any(x => x.Value > 0))
            {
                EditorGUILayout.LabelField("Gate Effect Usage", headerStyle);
                EditorGUILayout.BeginVertical(boxStyle);
                foreach (var kvp in gateEffectStats.Where(x => x.Value > 0).OrderByDescending(x => x.Value))
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(kvp.Key.ToString(), GUILayout.Width(120));
                    EditorGUILayout.LabelField(kvp.Value.ToString());
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(10);
            
            if (GUILayout.Button("Recalculate Statistics"))
            {
                CalculateStatistics();
            }
        }

        private void CalculateStatistics()
        {
            colorUsageStats.Clear();
            blockEffectStats.Clear();
            gateEffectStats.Clear();
            levelTypeStats.Clear();
            sizeStats.Clear();

            foreach (BlockColor color in Enum.GetValues(typeof(BlockColor)))
                colorUsageStats[color] = 0;

            foreach (BlockEffectType effect in Enum.GetValues(typeof(BlockEffectType)))
                blockEffectStats[effect] = 0;

            foreach (GateEffectType effect in Enum.GetValues(typeof(GateEffectType)))
                gateEffectStats[effect] = 0;

            foreach (LevelType type in Enum.GetValues(typeof(LevelType)))
                levelTypeStats[type] = 0;

            int processed = 0;
            foreach (var level in cachedLevels)
            {
                if (level == null) continue;

                // Level type
                levelTypeStats[level.Type]++;

                // Size
                if (!sizeStats.ContainsKey(level.Size))
                    sizeStats[level.Size] = 0;
                sizeStats[level.Size]++;

                // Elements
                if (level.Elements == null) continue;
                
                foreach (var element in level.Elements)
                {
                    if (element == null) continue;

                    if (element is BlockLevelElementData block)
                    {
                        colorUsageStats[block.BlockColor]++;

                        if (block.BlockEffects != null)
                        {
                            foreach (var effect in block.BlockEffects)
                            {
                                if (effect == null) continue;
                                blockEffectStats[effect.Type]++;

                                if (effect is LayeredBlockEffectData layered)
                                    colorUsageStats[layered.layeredBlockColor]++;
                                else if (effect is DualBlockEffectData dual)
                                    colorUsageStats[dual.secondDualColor]++;
                                else if (effect is KeyColorBlockEffectData kc)
                                    colorUsageStats[kc.keyColor]++;
                            }
                        }
                    }

                    if (element is GateLevelElementData gate)
                    {
                        if (gate.GateData != null)
                        {
                            foreach (var gateColor in gate.GateData)
                                colorUsageStats[gateColor.color] += gateColor.colorCount;
                        }

                        if (gate.GateEffects != null)
                        {
                            foreach (var effect in gate.GateEffects)
                            {
                                if (effect == null) continue;
                                gateEffectStats[effect.Type]++;

                                if (effect is LockedColorGateEffectData locked)
                                    colorUsageStats[locked.lockColor]++;
                            }
                        }
                    }
                }

                processed++;
                if (processed % 100 == 0)
                {
                    EditorUtility.DisplayProgressBar("Calculating Statistics", 
                        $"Processing level {processed}/{cachedLevels.Length}", 
                        (float)processed / cachedLevels.Length);
                }
            }

            EditorUtility.ClearProgressBar();
            statsCalculated = true;
        }
        
        #endregion

        #region Color Tools Tab
        
        private void DrawColorToolsTab()
        {
            EditorGUILayout.LabelField("Color Swap Tool (Single Level)", headerStyle);
            EditorGUILayout.HelpBox(
                "This tool will replace all instances of one color with another in the selected level.\n" +
                "Affects: BlockColor, GateData colors, and optionally Block/Gate Effect colors.",
                MessageType.Info);

            EditorGUILayout.Space(10);
            
            EditorGUILayout.BeginVertical(boxStyle);
            
            // Target level selection
            EditorGUI.BeginChangeCheck();
            colorSwapTargetLevel = (LevelData)EditorGUILayout.ObjectField("Target Level", colorSwapTargetLevel, typeof(LevelData), false);
            if (EditorGUI.EndChangeCheck())
            {
                colorSwapAffectedElements = 0;
            }
            
            if (colorSwapTargetLevel != null)
            {
                EditorGUILayout.LabelField($"  Size: {colorSwapTargetLevel.Size.x}x{colorSwapTargetLevel.Size.y}, Elements: {colorSwapTargetLevel.Elements?.Length ?? 0}", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(10);
            
            swapFromColor = (BlockColor)EditorGUILayout.EnumPopup("From Color", swapFromColor);
            swapToColor = (BlockColor)EditorGUILayout.EnumPopup("To Color", swapToColor);

            EditorGUILayout.Space(5);
            
            includeGateData = EditorGUILayout.Toggle("Include Gate Data", includeGateData);
            includeBlockEffects = EditorGUILayout.Toggle("Include Block/Gate Effects", includeBlockEffects);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            EditorGUI.BeginDisabledGroup(colorSwapTargetLevel == null);
            
            if (GUILayout.Button("Preview Changes"))
            {
                PreviewColorSwapSingleLevel();
            }

            if (colorSwapAffectedElements > 0)
            {
                EditorGUILayout.HelpBox(
                    $"This will affect {colorSwapAffectedElements} element(s) in '{colorSwapTargetLevel?.name}'.",
                    MessageType.Warning);
            }
            else if (colorSwapTargetLevel != null && colorSwapAffectedElements == 0)
            {
                EditorGUILayout.HelpBox("No elements found with the selected color.", MessageType.Info);
            }

            EditorGUILayout.Space(5);

            EditorGUI.BeginDisabledGroup(swapFromColor == swapToColor || colorSwapAffectedElements == 0);
            
            GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
            if (GUILayout.Button("Execute Color Swap", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Confirm Color Swap",
                    $"Are you sure you want to replace all {swapFromColor} with {swapToColor} in '{colorSwapTargetLevel.name}'?\n\n" +
                    $"This will affect {colorSwapAffectedElements} element(s).\n\n" +
                    "This action cannot be undone easily!",
                    "Yes, Execute", "Cancel"))
                {
                    ExecuteColorSwapSingleLevel();
                }
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUI.EndDisabledGroup();
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(20);

            // Color analysis for selected level
            EditorGUILayout.LabelField("Level Color Analysis", headerStyle);
            
            if (colorSwapTargetLevel != null && GUILayout.Button("Analyze Colors in Level"))
            {
                AnalyzeLevelColors(colorSwapTargetLevel);
            }
        }

        private void PreviewColorSwapSingleLevel()
        {
            colorSwapAffectedElements = 0;

            if (colorSwapTargetLevel?.Elements == null) return;

            foreach (var element in colorSwapTargetLevel.Elements)
            {
                if (element == null) continue;

                if (element is BlockLevelElementData block)
                {
                    if (block.BlockColor == swapFromColor)
                        colorSwapAffectedElements++;

                    if (includeBlockEffects && block.BlockEffects != null)
                    {
                        foreach (var effect in block.BlockEffects)
                        {
                            if (effect is LayeredBlockEffectData layered && layered.layeredBlockColor == swapFromColor) colorSwapAffectedElements++;
                            else if (effect is DualBlockEffectData dual && dual.secondDualColor == swapFromColor) colorSwapAffectedElements++;
                            else if (effect is KeyColorBlockEffectData kc && kc.keyColor == swapFromColor) colorSwapAffectedElements++;
                        }
                    }
                }

                if (element is GateLevelElementData gate)
                {
                    if (includeGateData && gate.GateData != null)
                    {
                        foreach (var gateColor in gate.GateData)
                            if (gateColor.color == swapFromColor) colorSwapAffectedElements++;
                    }

                    if (includeBlockEffects && gate.GateEffects != null)
                    {
                        foreach (var effect in gate.GateEffects)
                            if (effect is LockedColorGateEffectData locked && locked.lockColor == swapFromColor) colorSwapAffectedElements++;
                    }
                }
            }
        }

        private void ExecuteColorSwapSingleLevel()
        {
            if (colorSwapTargetLevel == null) return;

            int changedElements = 0;

            SerializedObject so = new SerializedObject(colorSwapTargetLevel);
            SerializedProperty elementsProperty = so.FindProperty("elements");

            for (int j = 0; j < elementsProperty.arraySize; j++)
            {
                var elementProp = elementsProperty.GetArrayElementAtIndex(j);
                var elementData = elementProp.managedReferenceValue as LevelElementData;
                if (elementData == null) continue;

                if (elementData is BlockLevelElementData)
                {
                    // BlockColor
                    var blockColorProp = elementProp.FindPropertyRelative("blockColor");
                    if (blockColorProp != null && (BlockColor)blockColorProp.enumValueIndex == swapFromColor)
                    {
                        blockColorProp.enumValueIndex = (int)swapToColor;
                        changedElements++;
                    }

                    // Block Effects
                    if (includeBlockEffects)
                    {
                        var blockEffectsProp = elementProp.FindPropertyRelative("blockEffects");
                        if (blockEffectsProp != null)
                        {
                            for (int k = 0; k < blockEffectsProp.arraySize; k++)
                            {
                                var effectProp = blockEffectsProp.GetArrayElementAtIndex(k);
                                var effectData = effectProp.managedReferenceValue as BlockEffectData;
                                if (effectData == null) continue;

                                string colorFieldName = null;
                                if (effectData is LayeredBlockEffectData) colorFieldName = "layeredBlockColor";
                                else if (effectData is DualBlockEffectData) colorFieldName = "secondDualColor";
                                else if (effectData is KeyColorBlockEffectData) colorFieldName = "keyColor";

                                if (colorFieldName != null)
                                {
                                    var colorProp = effectProp.FindPropertyRelative(colorFieldName);
                                    if (colorProp != null && (BlockColor)colorProp.enumValueIndex == swapFromColor)
                                    {
                                        colorProp.enumValueIndex = (int)swapToColor;
                                        changedElements++;
                                    }
                                }
                            }
                        }
                    }
                }

                if (elementData is GateLevelElementData)
                {
                    // GateData
                    if (includeGateData)
                    {
                        var gateDataProp = elementProp.FindPropertyRelative("gateData");
                        if (gateDataProp != null)
                        {
                            for (int k = 0; k < gateDataProp.arraySize; k++)
                            {
                                var gateColorProp = gateDataProp.GetArrayElementAtIndex(k).FindPropertyRelative("color");
                                if (gateColorProp != null && (BlockColor)gateColorProp.enumValueIndex == swapFromColor)
                                {
                                    gateColorProp.enumValueIndex = (int)swapToColor;
                                    changedElements++;
                                }
                            }
                        }
                    }

                    // Gate Effects
                    if (includeBlockEffects)
                    {
                        var gateEffectsProp = elementProp.FindPropertyRelative("gateEffects");
                        if (gateEffectsProp != null)
                        {
                            for (int k = 0; k < gateEffectsProp.arraySize; k++)
                            {
                                var effectProp = gateEffectsProp.GetArrayElementAtIndex(k);
                                if (effectProp.managedReferenceValue is LockedColorGateEffectData)
                                {
                                    var colorProp = effectProp.FindPropertyRelative("lockColor");
                                    if (colorProp != null && (BlockColor)colorProp.enumValueIndex == swapFromColor)
                                    {
                                        colorProp.enumValueIndex = (int)swapToColor;
                                        changedElements++;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(colorSwapTargetLevel);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Color Swap Complete",
                $"Successfully changed {changedElements} element(s) in '{colorSwapTargetLevel.name}'.",
                "OK");

            colorSwapAffectedElements = 0;
        }

        private void AnalyzeLevelColors(LevelData level)
        {
            Dictionary<BlockColor, int> colorCount = new Dictionary<BlockColor, int>();
            
            foreach (BlockColor color in Enum.GetValues(typeof(BlockColor)))
                colorCount[color] = 0;

            if (level.Elements != null)
            {
                foreach (var element in level.Elements)
                {
                    if (element == null) continue;

                    if (element is BlockLevelElementData block)
                    {
                        colorCount[block.BlockColor]++;

                        if (block.BlockEffects != null)
                        {
                            foreach (var effect in block.BlockEffects)
                            {
                                if (effect is LayeredBlockEffectData layered) colorCount[layered.layeredBlockColor]++;
                                else if (effect is DualBlockEffectData dual) colorCount[dual.secondDualColor]++;
                                else if (effect is KeyColorBlockEffectData kc) colorCount[kc.keyColor]++;
                            }
                        }
                    }

                    if (element is GateLevelElementData gate)
                    {
                        if (gate.GateData != null)
                        {
                            foreach (var gateColor in gate.GateData)
                                colorCount[gateColor.color] += gateColor.colorCount;
                        }

                        if (gate.GateEffects != null)
                        {
                            foreach (var effect in gate.GateEffects)
                                if (effect is LockedColorGateEffectData locked) colorCount[locked.lockColor]++;
                        }
                    }
                }
            }

            string message = $"Colors in '{level.name}':\n\n";
            foreach (var kvp in colorCount.Where(x => x.Value > 0).OrderByDescending(x => x.Value))
            {
                message += $"• {kvp.Key}: {kvp.Value}\n";
            }

            EditorUtility.DisplayDialog("Level Color Analysis", message, "OK");
        }
        
        #endregion

        #region Clone Tool Tab
        
        private void DrawCloneToolTab()
        {
            EditorGUILayout.LabelField("Clone Level Tool", headerStyle);
            EditorGUILayout.HelpBox(
                "Clone an existing level with options to select what data to include and swap colors.",
                MessageType.Info);

            EditorGUILayout.Space(10);
            
            // Source level
            EditorGUILayout.BeginVertical(boxStyle);
            EditorGUILayout.LabelField("Source", subHeaderStyle);
            
            levelToClone = (LevelData)EditorGUILayout.ObjectField("Level to Clone", levelToClone, typeof(LevelData), false);
            
            if (levelToClone != null)
            {
                EditorGUILayout.LabelField($"  Size: {levelToClone.Size.x}x{levelToClone.Size.y}, Duration: {levelToClone.Duration}s", EditorStyles.miniLabel);
                
                int blockCount = 0, gateCount = 0, objectCount = 0;
                if (levelToClone.Elements != null)
                {
                    foreach (var el in levelToClone.Elements)
                    {
                        if (el == null) continue;
                        if (el.Type == ElementType.Block) blockCount++;
                        else if (el.Type == ElementType.Gate) gateCount++;
                        else if (el.Type == ElementType.InteractableObject) objectCount++;
                    }
                }
                EditorGUILayout.LabelField($"  Blocks: {blockCount}, Gates: {gateCount}, Objects: {objectCount}", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(5);
            
            // Output settings
            EditorGUILayout.BeginVertical(boxStyle);
            EditorGUILayout.LabelField("Output", subHeaderStyle);
            
            cloneName = EditorGUILayout.TextField("New Level Name", cloneName);
            
            EditorGUILayout.BeginHorizontal();
            clonePath = EditorGUILayout.TextField("Save Path", clonePath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string selectedPath = EditorUtility.OpenFolderPanel("Select Folder", "Assets", "");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    clonePath = "Assets" + selectedPath.Substring(Application.dataPath.Length) + "/";
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);
            
            // Clone options - Element Types
            EditorGUILayout.BeginVertical(boxStyle);
            EditorGUILayout.LabelField("Element Types to Clone", subHeaderStyle);
            // Element type toggles
            for (int i = 0; i < cloneElementTypes.Length; i++)
            {
                ElementType elementType = cloneElementTypes[i];
                bool currentValue = cloneElementTypeToggles[i];
                bool newValue = EditorGUILayout.Toggle(elementType.ToString(), currentValue);
                cloneElementTypeToggles[i] = newValue;
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(5);
            
            // Clone options - Data
            EditorGUILayout.BeginVertical(boxStyle);
            EditorGUILayout.LabelField("Data to Clone", subHeaderStyle);
            
            cloneBlockEffects = EditorGUILayout.Toggle("Block Effects", cloneBlockEffects);
            cloneGateData = EditorGUILayout.Toggle("Gate Data (Colors)", cloneGateData);
            cloneGateEffects = EditorGUILayout.Toggle("Gate Effects", cloneGateEffects);
            
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);
            
            // Color swap list
            EditorGUILayout.BeginVertical(boxStyle);
            EditorGUILayout.LabelField("Color Swaps (Applied During Clone)", subHeaderStyle);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Add Color Swap", GUILayout.Width(120)))
            {
                cloneColorSwaps.Add(new ColorSwapEntry());
            }
            if (GUILayout.Button("Clear All", GUILayout.Width(80)))
            {
                cloneColorSwaps.Clear();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            if (cloneColorSwaps.Count > 0)
            {
                cloneColorSwapScrollPos = EditorGUILayout.BeginScrollView(cloneColorSwapScrollPos, GUILayout.MaxHeight(150));
                
                for (int i = cloneColorSwaps.Count - 1; i >= 0; i--)
                {
                    var swap = cloneColorSwaps[i];
                    
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    
                    swap.enabled = EditorGUILayout.Toggle(swap.enabled, GUILayout.Width(20));
                    
                    EditorGUILayout.LabelField("From:", GUILayout.Width(35));
                    swap.fromColor = (BlockColor)EditorGUILayout.EnumPopup(swap.fromColor, GUILayout.Width(90));
                    
                    EditorGUILayout.LabelField("→", GUILayout.Width(15));
                    
                    EditorGUILayout.LabelField("To:", GUILayout.Width(20));
                    swap.toColor = (BlockColor)EditorGUILayout.EnumPopup(swap.toColor, GUILayout.Width(90));
                    
                    if (GUILayout.Button("✕", GUILayout.Width(25)))
                    {
                        cloneColorSwaps.RemoveAt(i);
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.LabelField("No color swaps defined.", EditorStyles.miniLabel);
            }
            
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Clone button
            EditorGUI.BeginDisabledGroup(levelToClone == null || string.IsNullOrEmpty(cloneName));
            
            GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
            if (GUILayout.Button("Clone Level", GUILayout.Height(35)))
            {
                CloneLevelWithOptions();
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUI.EndDisabledGroup();
        }

        private void CloneLevelWithOptions()
        {
            if (levelToClone == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a level to clone.", "OK");
                return;
            }

            // Ensure path exists
            if (!Directory.Exists(clonePath))
            {
                Directory.CreateDirectory(clonePath);
            }

            string fullPath = clonePath + cloneName + ".asset";
            
            // Check if file exists
            if (File.Exists(fullPath))
            {
                if (!EditorUtility.DisplayDialog("File Exists",
                    $"A file named '{cloneName}.asset' already exists. Overwrite?",
                    "Yes", "No"))
                {
                    return;
                }
                AssetDatabase.DeleteAsset(fullPath);
            }

            // Create clone
            LevelData clone = ScriptableObject.CreateInstance<LevelData>();
            
            // Copy basic properties using SerializedObject
            SerializedObject srcSO = new SerializedObject(levelToClone);
            SerializedObject dstSO = new SerializedObject(clone);
            
            // Copy size, duration, type, useInRandomizer
            dstSO.FindProperty("size").vector2IntValue = srcSO.FindProperty("size").vector2IntValue;
            dstSO.FindProperty("duration").floatValue = srcSO.FindProperty("duration").floatValue;
            dstSO.FindProperty("type").enumValueIndex = srcSO.FindProperty("type").enumValueIndex;
            dstSO.FindProperty("useInRandomizer").boolValue = srcSO.FindProperty("useInRandomizer").boolValue;
            
            // Filter and copy elements
            List<int> elementIndicesToCopy = new List<int>();
            
            if (levelToClone.Elements != null)
            {
                for (int i = 0; i < levelToClone.Elements.Length; i++)
                {
                    var element = levelToClone.Elements[i];
                    if (element == null) continue;
                    
                    bool shouldInclude = cloneElementTypeToggles[(int)element.Type];
                    if (shouldInclude)
                    {
                        elementIndicesToCopy.Add(i);
                    }
                }
            }
            
            // Copy filtered elements
            SerializedProperty srcElements = srcSO.FindProperty("elements");
            SerializedProperty dstElements = dstSO.FindProperty("elements");
            
            dstElements.arraySize = elementIndicesToCopy.Count;
            
            for (int i = 0; i < elementIndicesToCopy.Count; i++)
            {
                int srcIndex = elementIndicesToCopy[i];
                CopyElementWithOptions(srcElements.GetArrayElementAtIndex(srcIndex), dstElements.GetArrayElementAtIndex(i));
            }
            
            dstSO.ApplyModifiedPropertiesWithoutUndo();
            
            // Save asset
            AssetDatabase.CreateAsset(clone, fullPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = clone;
            EditorGUIUtility.PingObject(clone);

            EditorUtility.DisplayDialog("Success", 
                $"Level '{cloneName}' has been created at:\n{fullPath}\n\n" +
                $"Elements copied: {elementIndicesToCopy.Count}\n" +
                $"Color swaps applied: {cloneColorSwaps.Count(s => s.enabled)}", 
                "OK");
        }

        private void CopyElementWithOptions(SerializedProperty src, SerializedProperty dst)
        {
            var srcElement = src.managedReferenceValue as LevelElementData;
            if (srcElement == null) return;

            // Deep-clone the element and apply color swaps
            LevelElementData cloned = srcElement.Clone();
            ApplyColorSwapsToElement(cloned);

            dst.managedReferenceValue = cloned;

            // Handle selective data inclusion by clearing fields we don't want
            if (cloned is BlockLevelElementData clonedBlock)
            {
                if (!cloneBlockEffects)
                    clonedBlock.SetBlockEffects(null);
            }
            else if (cloned is GateLevelElementData clonedGate)
            {
                if (!cloneGateData)
                    clonedGate.SetGateData(null);
                if (!cloneGateEffects)
                    clonedGate.SetGateEffects(null);
            }
        }

        private void ApplyColorSwapsToElement(LevelElementData element)
        {
            if (cloneColorSwaps == null || cloneColorSwaps.Count == 0) return;

            if (element is BlockLevelElementData block)
            {
                block.SetBlockColor((BlockColor)ApplyColorSwaps((int)block.BlockColor));

                if (block.BlockEffects != null)
                {
                    foreach (var effect in block.BlockEffects)
                    {
                        if (effect is LayeredBlockEffectData layered)
                            layered.layeredBlockColor = (BlockColor)ApplyColorSwaps((int)layered.layeredBlockColor);
                        else if (effect is DualBlockEffectData dual)
                            dual.secondDualColor = (BlockColor)ApplyColorSwaps((int)dual.secondDualColor);
                        else if (effect is KeyColorBlockEffectData kc)
                            kc.keyColor = (BlockColor)ApplyColorSwaps((int)kc.keyColor);
                        else if (effect is RopesBlockEffectData rope && rope.ropesColors != null)
                        {
                            for (int i = 0; i < rope.ropesColors.Length; i++)
                                rope.ropesColors[i] = (BlockColor)ApplyColorSwaps((int)rope.ropesColors[i]);
                        }
                    }
                }
            }
            else if (element is GateLevelElementData gate)
            {
                if (gate.GateData != null)
                {
                    for (int i = 0; i < gate.GateData.Count; i++)
                    {
                        var gd = gate.GateData[i];
                        gd.color = (BlockColor)ApplyColorSwaps((int)gd.color);
                        gate.GateData[i] = gd;
                    }
                }

                if (gate.GateEffects != null)
                {
                    foreach (var effect in gate.GateEffects)
                        if (effect is LockedColorGateEffectData locked)
                            locked.lockColor = (BlockColor)ApplyColorSwaps((int)locked.lockColor);
                }
            }
        }

        private static bool IsSerializedField(FieldInfo field)
        {
            if (field.IsStatic) return false;
            if (Attribute.IsDefined(field, typeof(NonSerializedAttribute))) return false;
            if (field.IsPublic) return true;
            return Attribute.IsDefined(field, typeof(SerializeField));
        }

        private int ApplyColorSwaps(int colorEnumIndex)
        {
            BlockColor color = (BlockColor)colorEnumIndex;
            
            foreach (var swap in cloneColorSwaps)
            {
                if (swap.enabled && swap.fromColor == color)
                {
                    return (int)swap.toColor;
                }
            }
            
            return colorEnumIndex;
        }
        
        #endregion

        #region Search Tab
        
        private void DrawSearchTab()
        {
            EditorGUILayout.LabelField("Search Levels", headerStyle);

            EditorGUILayout.BeginVertical(boxStyle);
            
            searchQuery = EditorGUILayout.TextField("Name Contains", searchQuery);

            EditorGUILayout.Space(5);
            
            // Search by Size
            searchBySize = EditorGUILayout.Toggle("Search by Size", searchBySize);
            if (searchBySize)
            {
                EditorGUI.indentLevel++;
                searchSize = EditorGUILayout.Vector2IntField("Size", searchSize);
                searchBySizeExact = EditorGUILayout.Toggle("Exact Match", searchBySizeExact);
                if (!searchBySizeExact)
                {
                    EditorGUILayout.HelpBox("Will find sizes >= specified value", MessageType.None);
                }
                EditorGUI.indentLevel--;
            }
            
            // Search by Level Type
            searchByLevelType = EditorGUILayout.Toggle("Search by Level Type", searchByLevelType);
            if (searchByLevelType)
            {
                EditorGUI.indentLevel++;
                searchLevelType = (LevelType)EditorGUILayout.EnumPopup("Level Type", searchLevelType);
                EditorGUI.indentLevel--;
            }
            
            // Search by Color
            searchByColor = EditorGUILayout.Toggle("Search by Color", searchByColor);
            if (searchByColor)
            {
                EditorGUI.indentLevel++;
                searchColor = (BlockColor)EditorGUILayout.EnumPopup("Color", searchColor);
                EditorGUI.indentLevel--;
            }

            // Search by Block Effect
            searchByBlockEffect = EditorGUILayout.Toggle("Search by Block Effect", searchByBlockEffect);
            if (searchByBlockEffect)
            {
                EditorGUI.indentLevel++;
                searchBlockEffect = (BlockEffectType)EditorGUILayout.EnumPopup("Effect Type", searchBlockEffect);
                EditorGUI.indentLevel--;
            }

            // Search by Gate Effect
            searchByGateEffect = EditorGUILayout.Toggle("Search by Gate Effect", searchByGateEffect);
            if (searchByGateEffect)
            {
                EditorGUI.indentLevel++;
                searchGateEffect = (GateEffectType)EditorGUILayout.EnumPopup("Effect Type", searchGateEffect);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Search", GUILayout.Height(25)))
            {
                PerformSearch();
            }

            EditorGUILayout.Space(10);

            // Results
            if (searchResults.Count > 0)
            {
                EditorGUILayout.LabelField($"Found {searchResults.Count} levels:", headerStyle);
                
                int displayCount = Mathf.Min(searchResults.Count, 100);
                for (int i = 0; i < displayCount; i++)
                {
                    var level = searchResults[i];
                    if (level == null) continue;
                    
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    
                    if (GUILayout.Button(level.name, EditorStyles.linkLabel))
                    {
                        Selection.activeObject = level;
                        EditorGUIUtility.PingObject(level);
                    }
                    
                    EditorGUILayout.LabelField($"{level.Size.x}x{level.Size.y}", GUILayout.Width(50));
                    EditorGUILayout.LabelField(level.Type.ToString(), GUILayout.Width(70));
                    EditorGUILayout.LabelField($"{level.Elements?.Length ?? 0} el.", GUILayout.Width(50));
                    
                    EditorGUILayout.EndHorizontal();
                }
                
                if (searchResults.Count > 100)
                {
                    EditorGUILayout.HelpBox($"Showing first 100 of {searchResults.Count} results.", MessageType.Info);
                }

                EditorGUILayout.Space(5);
                
                if (GUILayout.Button("Select All Results in Project"))
                {
                    Selection.objects = searchResults.Take(100).ToArray();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No search results. Adjust criteria and click Search.", MessageType.Info);
            }
        }

        private void PerformSearch()
        {
            searchResults.Clear();

            if (cachedLevels == null) return;

            foreach (var level in cachedLevels)
            {
                if (level == null) continue;

                bool matches = true;

                // Name filter
                if (!string.IsNullOrEmpty(searchQuery))
                {
                    if (!level.name.ToLower().Contains(searchQuery.ToLower()))
                        matches = false;
                }

                // Size filter
                if (matches && searchBySize)
                {
                    if (searchBySizeExact)
                    {
                        if (level.Size != searchSize)
                            matches = false;
                    }
                    else
                    {
                        if (level.Size.x < searchSize.x || level.Size.y < searchSize.y)
                            matches = false;
                    }
                }
                
                // Level Type filter
                if (matches && searchByLevelType)
                {
                    if (level.Type != searchLevelType)
                        matches = false;
                }

                // Color filter
                if (matches && searchByColor && searchColor != BlockColor.None)
                {
                    bool hasColor = false;
                    if (level.Elements != null)
                    {
                        hasColor = level.Elements.Any(e => e is BlockLevelElementData b && b.BlockColor == searchColor);
                        if (!hasColor)
                        {
                            hasColor = level.Elements.Any(e =>
                                e is GateLevelElementData g && g.GateData != null && g.GateData.Any(gd => gd.color == searchColor));
                        }
                    }
                    if (!hasColor) matches = false;
                }

                // Block effect filter
                if (matches && searchByBlockEffect)
                {
                    bool hasEffect = false;
                    if (level.Elements != null)
                    {
                        hasEffect = level.Elements.Any(e =>
                            e is BlockLevelElementData b && b.BlockEffects != null && b.BlockEffects.Any(ef => ef != null && ef.Type == searchBlockEffect));
                    }
                    if (!hasEffect) matches = false;
                }

                // Gate effect filter
                if (matches && searchByGateEffect)
                {
                    bool hasEffect = false;
                    if (level.Elements != null)
                    {
                        hasEffect = level.Elements.Any(e =>
                            e is GateLevelElementData g && g.GateEffects != null && g.GateEffects.Any(ef => ef != null && ef.Type == searchGateEffect));
                    }
                    if (!hasEffect) matches = false;
                }

                if (matches)
                    searchResults.Add(level);
            }
        }
        
        #endregion

        #region Utility Methods
        
        private void RefreshCache()
        {
            if (levelDatabase == null)
            {
                cachedLevels = null;
                return;
            }

            // Use reflection to get private levels array
            var field = typeof(LevelDatabase).GetField("levels", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (field != null)
            {
                cachedLevels = field.GetValue(levelDatabase) as LevelData[];
            }
            
            statsCalculated = false;
            colorSwapAffectedElements = 0;
            searchResults.Clear();
            currentPage = 0;
            
            Repaint();
        }
        
        #endregion
    }
}