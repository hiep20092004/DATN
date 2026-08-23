using UnityEngine;

namespace WaterFlow.Game
{
    [CreateAssetMenu(menuName = "WaterFlow/Block Effects/TNT Block Effect Config")]
    public class TntBlockEffectConfig : BaseBlockEffectConfig
    {
        [Header("Turn counter punch")]
        [SerializeField] private float turnPunchScale = 0.3f;
        [SerializeField] private float turnPunchDuration = 0.3f;

        [Header("Warning (2 turns left)")]
        [SerializeField] private Color warningColor2 = new Color(1f, 0.3f, 0.3f, 1f);
        [SerializeField] private float warningBlinkScale2 = 0.25f;
        [SerializeField] private float warningBlinkDuration2 = 0.5f;

        [Header("Warning (1 turn left)")]
        [SerializeField] private Color warningColor1 = new Color(1f, 0f, 0f, 1f);
        [SerializeField] private float warningBlinkScale1 = 0.4f;
        [SerializeField] private float warningBlinkDuration1 = 0.3f;

        [Header("Explosion pre-animation")]
        [SerializeField] private float explodeMoveUpY = 1.2f;
        [SerializeField] private float explodeScaleMultiplier = 2.5f;
        [SerializeField] private float explodeMoveDuration = 0.4f;
        [SerializeField] private float explodeHideTextDuration = 0.2f;
        [SerializeField] private float explodeFinalShakeDuration = 0.15f;
        [SerializeField] private float explodeFinalShakeStrength = 0.15f;

        [Header("Revive")]
        [SerializeField] private int extraTurnsAfterRevive = 3;

        public float TurnPunchScale => turnPunchScale;
        public float TurnPunchDuration => turnPunchDuration;

        public Color WarningColor2 => warningColor2;
        public float WarningBlinkScale2 => warningBlinkScale2;
        public float WarningBlinkDuration2 => warningBlinkDuration2;

        public Color WarningColor1 => warningColor1;
        public float WarningBlinkScale1 => warningBlinkScale1;
        public float WarningBlinkDuration1 => warningBlinkDuration1;

        public float ExplodeMoveUpY => explodeMoveUpY;
        public float ExplodeScaleMultiplier => explodeScaleMultiplier;
        public float ExplodeMoveDuration => explodeMoveDuration;
        public float ExplodeHideTextDuration => explodeHideTextDuration;
        public float ExplodeFinalShakeDuration => explodeFinalShakeDuration;
        public float ExplodeFinalShakeStrength => explodeFinalShakeStrength;

        public int ExtraTurnsAfterRevive => extraTurnsAfterRevive;
    }
}

