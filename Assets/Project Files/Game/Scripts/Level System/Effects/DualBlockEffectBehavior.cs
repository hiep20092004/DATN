using UnityEngine;

namespace WaterFlow.Game
{
    public class DualBlockEffectBehavior : BlockEffectBehavior<DualBlockEffectData>
    {
        private DualBlockVisualBehavior visualsBehavior;
        private DualBlockEffectConfig config;
        
        public override void OnCreated(LevelBlockBehavior blockBehavior)
        {
            if (!ValidateDataOrDisable())
                return;

            config = GetConfig<DualBlockEffectConfig>();
            BlockType blockType = blockBehavior.BlockConfig.Type;
            var blockTheme = blockBehavior.BlockTheme;
            if (config == null || !config.TryGetDualVisualsData(blockTheme, blockType, out DualVisualsData visualsData))
            {
                Debug.LogError($"Dual visuals data for block type {blockType} (theme {blockTheme}) not found.", this);
                DisableEffect();
                return;
            }
            
            GameObject visuals = Instantiate(visualsData.VisualsPrefab, linkedBlock.MeshRenderer.transform.parent);
            
            visualsBehavior = visuals.GetComponent<DualBlockVisualBehavior>();
            visualsBehavior.Init(linkedBlock.OriginColorConfig.Type, Data.secondDualColor);
            
            // Set secondary color and dual visual reference on the block
            linkedBlock.SetSecondaryColor(Data.secondDualColor);
            linkedBlock.SetDualVisual(visualsBehavior);
            
            linkedBlock.SetVisible(false, false);
        }

        public override void OnDisabled(LevelBlockBehavior blockBehavior, DisableSource disableSource)
        {
            blockBehavior.ClearSecondaryColor();
            blockBehavior.SetDualVisual(null);
            
            if (visualsBehavior)
            {
                Destroy(visualsBehavior.gameObject);
                visualsBehavior = null;
            }
        }

        protected override void ApplyVisualState(bool visible)
        {
            base.ApplyVisualState(visible);
            if (visualsBehavior)
                visualsBehavior.gameObject.SetActive(visible);

            // The dual visual replaces the block's base body+glass. Show the base body ONLY when the dual
            // visual is not showing AND the block region is still present (not hidden inside a Container).
            // Derived from the resolved set so layering is correct: while Dual is suppressed by Ice the base
            // (ice) glass stays visible — including after a Container clears — and the base glass turns off
            // only once Ice releases and the dual visual takes over.
            bool hiddenByContainer = hiddenVisualSources.Contains(ToggleVisualSource.Container);
            bool baseBodyVisible = !visible && !hiddenByContainer;
            linkedBlock.SetVisible(baseBodyVisible, false);
        }
    }
    
    public static class DualBlockDataExtensions
    {
        /// <summary>
        /// Gets the fill amount for the secondary color based on block type
        /// </summary>
        public static int GetSecondColorFillAmount(this BlockType blockData)
        {
            BlockGroupType groupType = blockData.ToBlockGroup();
            switch (groupType)
            {
                case BlockGroupType.Double:
                    return 1;
                case BlockGroupType.LType:
                case BlockGroupType.LTypeReverse:
                case BlockGroupType.Square:
                    return 2;
                default:
                    Debug.LogError($"Please config for block type {blockData} in Dual Block Config");
                    return 0;
            }
        }
    }
}
