using UnityEngine;

namespace WaterFlow.Game
{
    [CreateAssetMenu(menuName = "WaterFlow/Gate Effects/Moving Gate Lock Config")]
    public class MovingGateLockConfig : BaseGateEffectConfig
    {
        [Header("Jump Animation")]
        [SerializeField] private float jumpHeight = 0.4f;
        [SerializeField] private float anticipationDuration = 0.08f;
        [SerializeField] private float jumpDuration = 0.35f;
        [SerializeField] private float landDuration = 0.15f;

        public float JumpHeight => jumpHeight;
        public float AnticipationDuration => anticipationDuration;
        public float JumpDuration => jumpDuration;
        public float LandDuration => landDuration;
    }
}
