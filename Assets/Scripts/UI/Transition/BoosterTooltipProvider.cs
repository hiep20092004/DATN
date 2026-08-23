using System.Collections.Generic;
using Sirenix.OdinInspector;
using WaterFlow.Enums;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WaterFlow.Game
{
    /// <summary>
    /// Shows a random unlocked booster's tooltip during Home&lt;-&gt;Game loading transitions.
    /// Icon/title/description are read from the booster's BasePowerUpConfig, reusing the same
    /// data already authored for PowerUpUnlockNotifyPopup.
    /// </summary>
    public class BoosterTooltipProvider : TransitionTooltipProvider
    {
        [Header("References")]
        [SerializeField] private GameObject root;
        [SerializeField] private Image badgeIcon;
        [SerializeField] private Image resultIcon;
        [Tooltip("Multiple stacked TMP objects (outline/shadow/fill layers) that together render the styled title.")]
        [SerializeField] private List<TMP_Text> titleTexts;
        [SerializeField] private TMP_Text descriptionText;
        [Tooltip("Shared full-screen background behind the transition panel; swapped per booster and restored on Hide().")]
        [SerializeField] private Image background;
        [Tooltip("Localize component on the description text; its SecondaryTerm is swapped to match the background color.")]
        [SerializeField] private Localize descriptionLocalize;
        [Tooltip("Localize component on the shared \"Loading...\" text; its SecondaryTerm is swapped to match the background color.")]
        [SerializeField] private Localize loadingTextLocalize;

        private readonly List<BasePowerUpConfig> candidateBuffer = new();
        private Sprite defaultBackgroundSprite;
        private bool defaultBackgroundCached;
        private string defaultDescriptionSecondaryTerm;
        private string defaultLoadingSecondaryTerm;
        private bool defaultSecondaryTermsCached;

        public override bool TryShow(GamePlacement from, GamePlacement to)
        {
            var isHomeToGame = from == GamePlacement.Home && to == GamePlacement.Game;
            var isGameToHome = from == GamePlacement.Game && to == GamePlacement.Home;
            if (!isHomeToGame && !isGameToHome)
            {
                Hide();
                return false;
            }

            candidateBuffer.Clear();
            foreach (var boosterConfig in Services.BoosterService.GetAllBoosterConfigs())
            {
                if (!Services.BoosterService.IsBoosterUnlock(boosterConfig.type)) continue;

                var powerUpConfig = boosterConfig.GetPowerUpConfig();
                if (powerUpConfig) candidateBuffer.Add(powerUpConfig);
            }

            if (candidateBuffer.Count == 0)
            {
                Hide();
                return false;
            }

            var config = candidateBuffer[Random.Range(0, candidateBuffer.Count)];
            ApplyConfig(config);

            if (root) root.SetActive(true);
            return true;
        }

        public override void Hide()
        {
            if (root) root.SetActive(false);
            if (background && defaultBackgroundCached) background.sprite = defaultBackgroundSprite;

            if (defaultSecondaryTermsCached)
            {
                if (descriptionLocalize) descriptionLocalize.SecondaryTerm = defaultDescriptionSecondaryTerm;
                if (loadingTextLocalize) loadingTextLocalize.SecondaryTerm = defaultLoadingSecondaryTerm;
            }
        }

        private void ApplyConfig(BasePowerUpConfig config)
        {
            if (badgeIcon) badgeIcon.sprite = config.Icon;
            if (resultIcon) resultIcon.sprite = config.ResultIcon;

            if (background)
            {
                if (!defaultBackgroundCached)
                {
                    defaultBackgroundSprite = background.sprite;
                    defaultBackgroundCached = true;
                }
                if (config.TooltipBackground) background.sprite = config.TooltipBackground;
            }

            // Secondary term (font/material swap) must be applied before the text itself,
            // matching LevelPanel's convention — otherwise I2's re-localize pass on term
            // change can stomp the literal text we're about to set.
            if (!defaultSecondaryTermsCached)
            {
                if (descriptionLocalize) defaultDescriptionSecondaryTerm = descriptionLocalize.SecondaryTerm;
                if (loadingTextLocalize) defaultLoadingSecondaryTerm = loadingTextLocalize.SecondaryTerm;
                defaultSecondaryTermsCached = true;
            }
            if (!string.IsNullOrEmpty(config.TooltipTextSecondaryTerm))
            {
                if (descriptionLocalize) descriptionLocalize.SecondaryTerm = config.TooltipTextSecondaryTerm;
                if (loadingTextLocalize) loadingTextLocalize.SecondaryTerm = config.TooltipTextSecondaryTerm;
            }

            if (titleTexts != null)
            {
                foreach (var titleText in titleTexts)
                {
                    if (titleText) titleText.text = config.DisplayName;
                }
            }
            if (descriptionText) descriptionText.text = config.Description;
        }

#if UNITY_EDITOR
        [FoldoutGroup("Editor Testing")]
        [ShowInInspector, ReadOnly]
        private string previewBoosterName;

        private int previewIndex = -1;

        /// <summary>Cycles through every booster config (ignoring unlock state) to eyeball icon/text wiring in Play Mode.</summary>
        [FoldoutGroup("Editor Testing")]
        [Button("Preview Next Booster")]
        private void PreviewNextBooster()
        {
            var boosterConfigs = Services.BoosterService?.GetAllBoosterConfigs();
            if (boosterConfigs == null || boosterConfigs.Length == 0)
            {
                Debug.LogWarning("[BoosterTooltipProvider] No booster configs found — enter Play Mode so BoosterService is initialized.");
                return;
            }

            previewIndex = (previewIndex + 1) % boosterConfigs.Length;
            var boosterConfig = boosterConfigs[previewIndex];
            var config = boosterConfig.GetPowerUpConfig();
            if (!config)
            {
                Debug.LogWarning($"[BoosterTooltipProvider] {boosterConfig.type} has no PowerUpConfig assigned.");
                return;
            }

            previewBoosterName = boosterConfig.type.ToString();
            ApplyConfig(config);
            if (root) root.SetActive(true);
        }

        [FoldoutGroup("Editor Testing")]
        [Button("Hide Preview")]
        private void HidePreview() => Hide();
#endif
    }
}
