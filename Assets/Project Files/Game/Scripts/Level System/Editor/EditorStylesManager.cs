using UnityEngine;
using UnityEditor;

namespace WaterFlow.Game
{
    /// <summary>
    /// Centralized style management for the Blocks Visuals Data Editor.
    /// Provides consistent, professional styling across all UI components.
    /// </summary>
    public class EditorStylesManager
    {
        private bool initialized = false;

        #region Style Properties
        
        // Header & Title
        public GUIStyle TitleStyle { get; private set; }
        public GUIStyle HeaderBoxStyle { get; private set; }
        public GUIStyle SubtitleStyle { get; private set; }
        
        // Boxes & Containers
        public GUIStyle BoxStyle { get; private set; }
        public GUIStyle CardStyle { get; private set; }
        public GUIStyle SectionHeaderStyle { get; private set; }
        public GUIStyle CenteredBoxStyle { get; private set; }
        
        // Buttons
        public GUIStyle ButtonStyle { get; private set; }
        public GUIStyle PrimaryButtonStyle { get; private set; }
        public GUIStyle AddButtonStyle { get; private set; }
        public GUIStyle RemoveButtonStyle { get; private set; }
        public GUIStyle CreateButtonStyle { get; private set; }
        
        // Text & Labels
        public GUIStyle LabelStyle { get; private set; }
        public GUIStyle BoldLabelStyle { get; private set; }
        public GUIStyle TypeLabelStyle { get; private set; }
        public GUIStyle StatStyle { get; private set; }
        public GUIStyle HintStyle { get; private set; }
        public GUIStyle ShaderInfoStyle { get; private set; }
        
        // Tabs
        public GUIStyle TabStyle { get; private set; }
        
        // Footer
        public GUIStyle FooterStyle { get; private set; }
        public GUIStyle FooterTextStyle { get; private set; }
        
        // Warning/Empty States
        public GUIStyle WarningIconStyle { get; private set; }
        public GUIStyle WarningTitleStyle { get; private set; }
        public GUIStyle WarningMessageStyle { get; private set; }
        
        // Field Labels
        public GUIStyle FieldLabelStyle { get; private set; }
        public GUIStyle RequiredFieldLabelStyle { get; private set; }
        public GUIStyle OptionalFieldLabelStyle { get; private set; }
        
        #endregion

        #region Colors
        
        private readonly Color primaryColor = new Color(0.3f, 0.7f, 1f);
        private readonly Color successColor = new Color(0.3f, 0.8f, 0.4f);
        private readonly Color warningColor = new Color(0.9f, 0.7f, 0.2f);
        private readonly Color dangerColor = new Color(1f, 0.4f, 0.4f);
        private readonly Color accentColor = new Color(1f, 0.7f, 0.3f);
        
        private readonly Color darkBg = new Color(0.22f, 0.22f, 0.22f);
        private readonly Color mediumBg = new Color(0.28f, 0.28f, 0.28f);
        private readonly Color lightBg = new Color(0.35f, 0.35f, 0.35f);
        
        #endregion

        public void InitializeStyles()
        {
            if (initialized) return;
            
            InitializeHeaderStyles();
            InitializeContainerStyles();
            InitializeButtonStyles();
            InitializeTextStyles();
            InitializeTabStyles();
            InitializeFooterStyles();
            InitializeWarningStyles();
            InitializeFieldLabelStyles();
            
            initialized = true;
        }

        private void InitializeHeaderStyles()
        {
            TitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(5, 5, 5, 5),
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
            };

            HeaderBoxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(15, 15, 12, 12),
                margin = new RectOffset(5, 5, 5, 5)
            };
            if (EditorGUIUtility.isProSkin)
            {
                HeaderBoxStyle.normal.background = MakeTex(2, 2, new Color(0.25f, 0.25f, 0.25f, 1f));
            }

            SubtitleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };
        }

        private void InitializeContainerStyles()
        {
            BoxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(15, 15, 12, 12),
                margin = new RectOffset(5, 5, 8, 8)
            };

            CardStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(12, 12, 12, 12),
                margin = new RectOffset(5, 5, 5, 5)
            };
            if (EditorGUIUtility.isProSkin)
            {
                CardStyle.normal.background = MakeTex(2, 2, new Color(0.28f, 0.28f, 0.28f, 1f));
            }

            SectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(5, 5, 8, 8),
                normal = { textColor = primaryColor }
            };

            CenteredBoxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(40, 40, 40, 40),
                margin = new RectOffset(40, 40, 20, 20),
                alignment = TextAnchor.MiddleCenter
            };
        }

        private void InitializeButtonStyles()
        {
            ButtonStyle = new GUIStyle(GUI.skin.button)
            {
                padding = new RectOffset(12, 12, 6, 6),
                fontSize = 11,
                fontStyle = FontStyle.Normal
            };

            PrimaryButtonStyle = new GUIStyle(GUI.skin.button)
            {
                padding = new RectOffset(15, 15, 7, 7),
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = primaryColor }
            };

            AddButtonStyle = new GUIStyle(GUI.skin.button)
            {
                padding = new RectOffset(15, 15, 7, 7),
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = successColor }
            };

            RemoveButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(8, 8, 4, 4)
            };

            CreateButtonStyle = new GUIStyle(GUI.skin.button)
            {
                padding = new RectOffset(20, 20, 10, 10),
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = successColor }
            };
        }

        private void InitializeTextStyles()
        {
            LabelStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft
            };

            BoldLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft
            };

            TypeLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = primaryColor }
            };

            StatStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };

            HintStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9,
                fontStyle = FontStyle.Italic,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) },
                wordWrap = true
            };

            ShaderInfoStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };
        }

        private void InitializeTabStyles()
        {
            TabStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                fixedHeight = 32
            };
        }

        private void InitializeFooterStyles()
        {
            FooterStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 6, 6),
                margin = new RectOffset(0, 0, 5, 0)
            };
            if (EditorGUIUtility.isProSkin)
            {
                FooterStyle.normal.background = MakeTex(2, 2, new Color(0.2f, 0.2f, 0.2f, 1f));
            }

            FooterTextStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9,
                normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
            };
        }

        private void InitializeWarningStyles()
        {
            WarningIconStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 48,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = warningColor }
            };

            WarningTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
            };

            WarningMessageStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };
        }

        private void InitializeFieldLabelStyles()
        {
            FieldLabelStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                padding = new RectOffset(0, 0, 2, 2)
            };

            RequiredFieldLabelStyle = new GUIStyle(FieldLabelStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
            };

            OptionalFieldLabelStyle = new GUIStyle(FieldLabelStyle)
            {
                fontStyle = FontStyle.Italic,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };
        }

        #region Utilities
        
        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
            {
                pix[i] = col;
            }
            
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            
            return result;
        }

        public Color GetPrimaryColor() => primaryColor;
        public Color GetSuccessColor() => successColor;
        public Color GetWarningColor() => warningColor;
        public Color GetDangerColor() => dangerColor;
        public Color GetAccentColor() => accentColor;
        
        #endregion
    }
}