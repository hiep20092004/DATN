using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace WaterFlow.Game
{
    [Serializable]
    public class ObstacleUnlockEntry
    {
        [BoxGroup("Info", showLabel: false)]
        [HorizontalGroup("Info/Row")]
        [SerializeField, LabelText("Category"), LabelWidth(70)] private ObstacleCategory category;

        [BoxGroup("Info", showLabel: false)]
        [ShowIf(nameof(category), ObstacleCategory.Block)]
        [SerializeField] private BlockEffectType blockEffectType;

        [BoxGroup("Info", showLabel: false)]
        [ShowIf(nameof(category), ObstacleCategory.Gate)]
        [SerializeField] private GateEffectType gateEffectType;

        [BoxGroup("Info", showLabel: false)]
        [ShowIf(nameof(category), ObstacleCategory.InteractableObject)]
        [SerializeField] private InteractableObjectType interactableObjectType;

        [BoxGroup("Info", showLabel: false)]
        [ShowIf(nameof(category), ObstacleCategory.ExtraLayer)]
        [SerializeField] private ExtraLayerType extraLayerType;

        [BoxGroup("Display", showLabel: false)]
        [HorizontalGroup("Display/Row", width: 64)]
        [PreviewField(60, ObjectFieldAlignment.Left), HideLabel]
        [SerializeField] private Sprite icon;

        [VerticalGroup("Display/Row/Text")]
        [SerializeField, LabelWidth(80)] private string title;

        [VerticalGroup("Display/Row/Text")]
        [SerializeField, LabelWidth(80)] private string description;

        // Wide/tall sprites (e.g. Grinder) need a different icon rect than the popup default.
        // When disabled the popup keeps the width authored on the prefab.
        [VerticalGroup("Display/Row/Text")]
        [SerializeField, LabelWidth(80), LabelText("Override W")] private bool overrideIconWidth;

        [VerticalGroup("Display/Row/Text")]
        [ShowIf(nameof(overrideIconWidth))]
        [SerializeField, LabelWidth(80), LabelText("Icon Width")] private float iconWidth = 225f;

        // Optional per-theme icon overrides. Configured manually per entry (not auto-generated
        // from BlockEffectType) so any category can opt into a themed icon. When the active
        // BlockTheme has no override here, GetIcon falls back to the default icon above.
        [BoxGroup("Display", showLabel: false)]
        [TableList(AlwaysExpanded = true, ShowIndexLabels = false)]
        [SerializeField] private List<ThemeIcon> themeIcons = new List<ThemeIcon>();

        public ObstacleCategory Category => category;
        public BlockEffectType BlockEffectType => blockEffectType;
        public GateEffectType GateEffectType => gateEffectType;
        public InteractableObjectType InteractableObjectType => interactableObjectType;
        public ExtraLayerType ExtraLayerType => extraLayerType;
        public Sprite Icon => icon;
        public string Title => title;
        public string Description => description;
        public bool OverrideIconWidth => overrideIconWidth;
        public float IconWidth => iconWidth;

        public Sprite GetIcon(BlockTheme theme)
        {
            if (themeIcons != null)
            {
                foreach (ThemeIcon themeIcon in themeIcons)
                {
                    if (themeIcon.Theme == theme && themeIcon.Icon != null)
                        return themeIcon.Icon;
                }
            }

            return icon;
        }

        [Serializable]
        private struct ThemeIcon
        {
            [VerticalGroup("Theme"), HideLabel]
            [SerializeField] private BlockTheme theme;

            [TableColumnWidth(70, resizable: false)]
            [PreviewField(50, ObjectFieldAlignment.Center), HideLabel]
            [SerializeField] private Sprite icon;

            public BlockTheme Theme => theme;
            public Sprite Icon => icon;
        }
        
#if UNITY_EDITOR
        // Used by Odin's ListDrawerSettings to label each collapsed list element with its effect key.
        private string EditorLabel => GetEffectKey();
#endif

        public ObstacleUnlockEntry() { }

        public ObstacleUnlockEntry(ObstacleCategory category, BlockEffectType blockEffectType,
            GateEffectType gateEffectType, InteractableObjectType interactableObjectType,
            ExtraLayerType extraLayerType)
        {
            this.category = category;
            this.blockEffectType = blockEffectType;
            this.gateEffectType = gateEffectType;
            this.interactableObjectType = interactableObjectType;
            this.extraLayerType = extraLayerType;
            this.title = GetDefaultTitle(category, blockEffectType, gateEffectType, interactableObjectType, extraLayerType);
            this.description = "";
        }

        private static string GetDefaultTitle(
            ObstacleCategory category,
            BlockEffectType blockEffectType,
            GateEffectType gateEffectType,
            InteractableObjectType interactableObjectType,
            ExtraLayerType extraLayerType)
        {
            return category switch
            {
                ObstacleCategory.Block => blockEffectType.ToString(),
                ObstacleCategory.Gate => gateEffectType.ToString(),
                ObstacleCategory.InteractableObject => interactableObjectType.ToString(),
                ObstacleCategory.Generator => "Generator",
                ObstacleCategory.ExtraLayer => extraLayerType.ToString(),
                _ => ""
            };
        }

        /// <summary>
        /// Copy display info (icon, title, description) from another entry.
        /// Used to preserve manually-set values when re-generating.
        /// </summary>
        public void CopyDisplayInfoFrom(ObstacleUnlockEntry other)
        {
            if (other == null) return;
            icon = other.icon;
            themeIcons = other.themeIcons != null
                ? new List<ThemeIcon>(other.themeIcons)
                : new List<ThemeIcon>();
            title = other.title;
            description = other.description;
            overrideIconWidth = other.overrideIconWidth;
            iconWidth = other.iconWidth;
        }

        public bool MatchesEffect(ObstacleCategory cat, BlockEffectType bet, GateEffectType get,
            InteractableObjectType iot, ExtraLayerType elt)
        {
            if (category != cat) return false;
            return cat switch
            {
                ObstacleCategory.Block => blockEffectType == bet,
                ObstacleCategory.Gate => gateEffectType == get,
                ObstacleCategory.InteractableObject => interactableObjectType == iot,
                ObstacleCategory.Generator => true,
                ObstacleCategory.ExtraLayer => extraLayerType == elt,
                _ => false
            };
        }

        /// <summary>
        /// Returns a unique key for this obstacle entry, used for tracking notification state.
        /// </summary>
        public string GetEffectKey() =>
            ComputeEffectKey(category, blockEffectType, gateEffectType, interactableObjectType, extraLayerType);

        /// <summary>
        /// Canonical effect key for any (category, effect-fields) tuple. Kept static so
        /// scanners can derive the key without allocating a temporary entry.
        /// </summary>
        public static string ComputeEffectKey(
            ObstacleCategory category,
            BlockEffectType blockEffectType,
            GateEffectType gateEffectType,
            InteractableObjectType interactableObjectType,
            ExtraLayerType extraLayerType)
        {
            return category switch
            {
                ObstacleCategory.Block => $"Block_{blockEffectType}",
                ObstacleCategory.Gate => $"Gate_{gateEffectType}",
                ObstacleCategory.InteractableObject => $"Interactable_{interactableObjectType}",
                ObstacleCategory.Generator => "Generator",
                ObstacleCategory.ExtraLayer => $"ExtraLayer_{extraLayerType}",
                _ => category.ToString()
            };
        }
    }
}