using WaterFlow.Enums;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    public abstract class BasePowerUpConfig : ScriptableObject
    {
        [Header("General Settings")]
        [SerializeField] private PowerUpType type;
        [SerializeField] private string displayName;
        [SerializeField] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private Sprite resultIcon;
        [SerializeField] private Sprite tooltipBackground;
        [Tooltip("Secondary text-color key used to match the description/loading text to TooltipBackground.")]
        [SerializeField] private string tooltipTextSecondaryTerm;
        [SerializeField] private GameObject behaviorPrefab;
        [SerializeField] AudioClip activateSound;

        public PowerUpType Type => type;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        /// <summary>Preview icon shown after the arrow in the Home-to-Game transition tooltip (e.g. the resulting frozen/filled/expanded block).</summary>
        public Sprite ResultIcon => resultIcon;
        /// <summary>Full-screen background used behind this booster's Home-to-Game transition tooltip.</summary>
        public Sprite TooltipBackground => tooltipBackground;
        /// <summary>Secondary text-color key for the tooltip description/loading text, matched to TooltipBackground.</summary>
        public string TooltipTextSecondaryTerm => tooltipTextSecondaryTerm;
        public GameObject BehaviorPrefab => behaviorPrefab;
        public AudioClip ActivateSound => activateSound;

        public void Init()
        {
            OnInit();
        }

        protected abstract void OnInit();
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!behaviorPrefab)
                return;

            if (!behaviorPrefab.TryGetComponent<PowerUpBehavior>(out _))
            {
                Debug.LogError(
                    $"[PowerUpConfig] BehaviorPrefab '{behaviorPrefab.name}' " +
                    $"must have BasePowerUpBehaviour on ROOT GameObject",
                    behaviorPrefab
                );
            }
        }
#endif

    }
}