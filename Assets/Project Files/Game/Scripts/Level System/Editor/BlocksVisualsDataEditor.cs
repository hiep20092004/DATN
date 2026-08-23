using System;
using UnityEngine;
using UnityEditor;

namespace WaterFlow.Game
{
    /// <summary>
    /// Main editor window for managing BlocksVisualsData assets.
    /// Provides an intuitive interface for configuring block types and color variants.
    /// </summary>
    public class BlocksVisualsDataEditor : EditorWindow
    {
        #region Fields
        
        private BlocksVisualsData targetData;
        private Vector2 scrollPosition;
        private int selectedTab = 0;
        private readonly string[] tabNames = new string[] { "Blocks", "Colors", "Settings" };
        
        // Child UI components
        private BlocksTabUI blocksTab;
        private ColorsTabUI colorsTab;
        private SettingsTabUI settingsTab;
        
        // Styles manager
        private EditorStylesManager styles;
        
        // Data operations handler
        private DataOperationsHandler dataOps;
        
        // Preview manager
        private PreviewManager previewManager;
        
        private bool isInitialized = false;
        
        #endregion

        #region Window Setup
        
        [MenuItem("Tools/Blocks Visuals Data Editor")]
        public static void ShowWindow()
        {
            BlocksVisualsDataEditor window = GetWindow<BlocksVisualsDataEditor>("Blocks Visuals Editor");
            window.minSize = new Vector2(900, 650);
            window.Show();
        }

        private void Reset()
        {
            isInitialized = false;
        }

        private void OnEnable()
        {
            isInitialized = false;
            Initialize();
            LoadTargetData();
        }

        private void OnDisable()
        {
            Cleanup();
        }

        private void Initialize()
        {
            if (isInitialized) return;
            
            styles = new EditorStylesManager();
            previewManager = new PreviewManager();
            dataOps = new DataOperationsHandler();
            
            blocksTab = new BlocksTabUI(previewManager, dataOps, styles);
            colorsTab = new ColorsTabUI(dataOps, styles);
            settingsTab = new SettingsTabUI(styles);
            
            isInitialized = true;
        }

        private void Cleanup()
        {
            previewManager?.Cleanup();
            blocksTab?.Cleanup();
            colorsTab?.Cleanup();
        }
        
        #endregion

        #region GUI Drawing
        
        private void OnGUI()
        {
            if (!isInitialized)
            {
                Initialize();
            }
            
            styles.InitializeStyles();
            
            DrawHeader();
            
            if (targetData == null)
            {
                DrawNoDataWarning();
                return;
            }
            
            DrawTabs();
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.Space(10);
            
            // Draw selected tab content
            switch (selectedTab)
            {
                case 0:
                    blocksTab.Draw(targetData);
                    break;
                case 1:
                    colorsTab.Draw(targetData);
                    break;
                case 2:
                    settingsTab.Draw(targetData);
                    break;
            }
            
            EditorGUILayout.Space(20);
            EditorGUILayout.EndScrollView();
            
            DrawFooter();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(styles.HeaderBoxStyle);
            
            EditorGUILayout.Space(5);
            
            // Title with icon
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("🎨 Blocks Visuals Data Editor", styles.TitleStyle);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(8);
            
            // Target data selection
            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.LabelField("Target Asset:", GUILayout.Width(85));
            
            EditorGUI.BeginChangeCheck();
            targetData = (BlocksVisualsData)EditorGUILayout.ObjectField(
                targetData, 
                typeof(BlocksVisualsData), 
                false
            );
            if (EditorGUI.EndChangeCheck())
            {
                OnTargetDataChanged();
            }
            
            if (GUILayout.Button("Create New", styles.PrimaryButtonStyle, GUILayout.Width(100)))
            {
                CreateNewData();
            }
            
            if (GUILayout.Button("↻ Refresh", styles.ButtonStyle, GUILayout.Width(80)))
            {
                RefreshData();
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Statistics
            if (targetData != null)
            {
                EditorGUILayout.Space(5);
                DrawStatistics();
            }
            
            EditorGUILayout.Space(3);
            EditorGUILayout.EndVertical();
        }

        private void DrawStatistics()
        {
            EditorGUILayout.BeginHorizontal();
            
            int blocksCount = targetData.Blocks?.Length ?? 0;
            int colorsCount = targetData.Colors?.Length ?? 0;
            
            EditorGUILayout.LabelField($"📦 Blocks: {blocksCount}", styles.StatStyle, GUILayout.Width(100));
            EditorGUILayout.LabelField($"🎨 Colors: {colorsCount}", styles.StatStyle, GUILayout.Width(100));
            
            GUILayout.FlexibleSpace();
            
            // Validation status
            bool isValid = ValidateData();
            string statusIcon = isValid ? "✓" : "⚠";
            Color statusColor = isValid ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.9f, 0.7f, 0.2f);
            
            GUIStyle statusStyle = new GUIStyle(styles.StatStyle);
            statusStyle.normal.textColor = statusColor;
            
            EditorGUILayout.LabelField($"{statusIcon} {(isValid ? "Valid" : "Issues")}", statusStyle, GUILayout.Width(80));
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTabs()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            selectedTab = GUILayout.Toolbar(
                selectedTab, 
                tabNames, 
                styles.TabStyle,
                GUILayout.Height(32), 
                GUILayout.Width(400)
            );
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginVertical(styles.FooterStyle);
            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.LabelField($"Unity {Application.unityVersion}", styles.FooterTextStyle, GUILayout.Width(120));
            GUILayout.FlexibleSpace();
            
            if (targetData != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(targetData);
                EditorGUILayout.LabelField(assetPath, styles.FooterTextStyle);
            }
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawNoDataWarning()
        {
            EditorGUILayout.Space(100);
            
            EditorGUILayout.BeginVertical(styles.CenteredBoxStyle);
            
            EditorGUILayout.Space(30);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("⚠", styles.WarningIconStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("No Data Asset Selected", styles.WarningTitleStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Please select an existing asset or create a new one", styles.WarningMessageStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(20);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Create New Data Asset", styles.CreateButtonStyle, GUILayout.Width(200), GUILayout.Height(35)))
            {
                CreateNewData();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(30);
            EditorGUILayout.EndVertical();
        }
        
        #endregion

        #region Data Management
        
        private void CreateNewData()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Blocks Visuals Data",
                "BlocksVisualsData",
                "asset",
                "Create a new BlocksVisualsData asset"
            );
            
            if (!string.IsNullOrEmpty(path))
            {
                BlocksVisualsData newData = CreateInstance<BlocksVisualsData>();
                AssetDatabase.CreateAsset(newData, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                targetData = newData;
                SaveTargetData();
                OnTargetDataChanged();
                
                EditorUtility.DisplayDialog(
                    "Success", 
                    "New Blocks Visuals Data asset created successfully!", 
                    "OK"
                );
            }
        }

        private void LoadTargetData()
        {
            string guid = EditorPrefs.GetString(GetPrefsKey(), "");
            
            if (!string.IsNullOrEmpty(guid))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                targetData = AssetDatabase.LoadAssetAtPath<BlocksVisualsData>(path);
            }
        }

        private void SaveTargetData()
        {
            if (targetData != null)
            {
                string path = AssetDatabase.GetAssetPath(targetData);
                string guid = AssetDatabase.AssetPathToGUID(path);
                EditorPrefs.SetString(GetPrefsKey(), guid);
            }
            else
            {
                EditorPrefs.DeleteKey(GetPrefsKey());
            }
        }

        private void RefreshData()
        {
            if (targetData != null)
            {
                LoadTargetData();
                previewManager.Cleanup();
                blocksTab.Refresh();
                colorsTab.Refresh();
                Repaint();
                
                Debug.Log("✓ Data refreshed successfully");
            }
        }

        private void OnTargetDataChanged()
        {
            SaveTargetData();
            previewManager.Cleanup();
            blocksTab.Refresh();
            colorsTab.Refresh();
            Repaint();
        }

        private bool ValidateData()
        {
            if (targetData == null) return false;
            
            bool hasBlocks = targetData.Blocks != null && targetData.Blocks.Length > 0;
            bool hasColors = targetData.Colors != null && targetData.Colors.Length > 0;
            
            return hasBlocks && hasColors;
        }

        private string GetPrefsKey()
        {
            return $"{Application.productName}_BlocksVisualsDataEditor_TargetData";
        }
        
        #endregion
    }
}