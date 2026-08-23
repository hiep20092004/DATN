using System;
using DG.Tweening;
using UnityEngine;

namespace WaterFlow.Game
{
    public abstract class BaseContainerBoxEffectConfig : BaseBlockEffectConfig
    {
        [Tooltip("Prefab with a pre-configured 1x1 MeshCollider (including its physics material). One " +
                 "instance is spawned on each empty perimeter cell of the group bound to fill the gaps " +
                 "the container box visual spans.")]
        [SerializeField] private GameObject cellColliderPrefab;
        [SerializeReference] private BaseContainerBoxVisualConfig visualConfig;

        public GameObject CellColliderPrefab => cellColliderPrefab;
        public BaseContainerBoxVisualConfig VisualConfig => visualConfig;
    }
    
    [Serializable]
    public abstract class BaseContainerBoxVisualConfig
    {
        [Tooltip("Edit mode only. If true: alpha=1 and show wood lines. If false: alpha=0.25 and hide wood lines. Play mode always uses full alpha and wood lines. Also gates hiding obstacles under the box in edit mode.")]
        [SerializeField] private bool showFullAlpha = false;
        
        [SerializeField] private float localHeightOffset = 0.6f;
        
        [Header("Shadow")]
        [Tooltip("Extra width added on top of bounds width (in cell units).")]
        [SerializeField] private float shadowWidthPadding = 0f;
        [Tooltip("Local Y padding above the visual bottom edge (-height/2).")]
        [SerializeField] private float shadowBottomPadding = 0.2f;

        [Header("Line")]
        [Tooltip("Base width of the  line when the group width is 1 cell.")]
        [SerializeField] private float lineBaseWidth = 0.765f;
        [Tooltip("Extra width added per additional horizontal cell (beyond the first).")]
        [SerializeField] private float lineWidthPerExtraCell = 1f;
        [Tooltip("Padding from top/bottom (in cell units).")]
        [SerializeField] private float lineVerticalPadding = 0.5f;
        [Tooltip("Local height of a single line (before Transform scale).")]
        [SerializeField] private float lineLocalHeight = 1.5f;
        
        [Header("Remaining Counter Punch")]
        [SerializeField] private float turnPunchScale = 0.3f;
        [SerializeField] private float turnPunchDuration = 0.3f;
        [Tooltip("When counter hits 0, start box clear after this fraction of the counter punch (0-1).")]
        [SerializeField] private float clearStartAfterTurnPunchNormalized = 0.5f;

        [Header("Visual/Clear Animation")]
        [SerializeField] private ClearAnimationSettings clearAnimation = new();
        
        public float LocalHeightOffset => localHeightOffset;
        public float ShadowWidthPadding => shadowWidthPadding;
        public float ShadowBottomPadding => shadowBottomPadding;
        public float LineBaseWidth => lineBaseWidth;
        public float LineWidthPerExtraCell => lineWidthPerExtraCell;
        public float LineVerticalPadding => lineVerticalPadding;
        public float LineLocalHeight => lineLocalHeight;
        public ClearAnimationSettings ClearAnimation => clearAnimation;
        public bool ShowFullAlpha => showFullAlpha;
        public float TurnPunchScale => turnPunchScale;
        public float TurnPunchDuration => turnPunchDuration;
        public float ClearStartAfterTurnPunchNormalized => clearStartAfterTurnPunchNormalized;
    }
    
    /// <summary>Tuning for the "pop &amp; dissolve" disappear animation played when a group is cleared.</summary>
    [Serializable]
    public class ClearAnimationSettings
    {
        [Tooltip("World-space height the clear VFX spawns at. If <= 0, falls back to the visual local height.")]
        public float VfxHeightOffset = 1.6f;

        [Tooltip("Duration of the initial squash punch (box grows slightly before collapsing).")]
        public float PunchDuration = 0.11f;
        [Tooltip("Extra scale added during the punch (0.16 = +16%).")]
        public float PunchScale = 0.14f;
        [Tooltip("How much punch scale decays per extra occupied cell (area-based).")]
        public float PunchScaleDecayPerExtraCell = 0.045f;
        [Tooltip("Minimum multiplier applied to PunchScale after size-based decay.")]
        public float PunchScaleMinMultiplier = 0.35f;
        public Ease PunchEase = Ease.OutBack;
        [Tooltip("Local Y applied to the visual right before the punch phase.")]
        public float PunchLocalY = 1f;
        [Tooltip("Offset added to current local Z before the punch phase (e.g. -0.2 moves it back).")]
        public float PunchLocalZOffset = -0.2f;

        [Tooltip("Duration of the shrink + fade phase.")]
        public float CollapseDuration = 0.22f;
        [Tooltip("Scale the box shrinks to while fading out (0 = fully collapsed).")]
        public float CollapseScale = 0.5f;
        public Ease CollapseEase = Ease.InBack;
        public Ease FadeEase = Ease.InCubic;
    }
}
