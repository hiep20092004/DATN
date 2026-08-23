using DG.Tweening;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Shared tuning for every grinder: tape limits, retract feel, grind wobble and the explode
    /// sequence. Scene references (machine art, tape sides, VFX) stay on the prefab — only numbers
    /// live here so the feel can be tuned once for all levels without touching the prefab.
    ///
    /// Not to be confused with <see cref="InteractableObjectData.GrinderConfig"/>, which is the
    /// per-instance (axis, positiveCells, negativeCells) layout authored in the level editor.
    /// </summary>
    [CreateAssetMenu(fileName = "Grinder Tuning Config", menuName = "Data/Grinder Tuning Config")]
    public class GrinderTuningConfig : ScriptableObject
    {
        [Header("Tape layout")]
        [Tooltip("Giới hạn số ô tape mỗi bên khi spawn. Một tile ice gốc phủ ~9.5 ô; dài hơn thì phần " +
                 "giữa bị lặp và lộ seam, nên tape sẽ bị clamp về giá trị này.")]
        [SerializeField, Min(1)] private int maxTapeCells = 9;

        [Tooltip("Đầu trong của tape thụt vào dưới thân máy bao nhiêu ô. Dùng để giấu mặt cắt phẳng " +
                 "của tile ice. Tăng nếu thấy hở mép giữa ice và máy.")]
        [SerializeField, Min(0f)] private float iceMachineInset = 0.12f;

        [Header("Retract")]
        [Tooltip("Thời gian rút 1 cột tape (giây). Cũng là thời lượng rung nghiền của máy và là " +
                 "khoảng chờ trước khi grinder nổ.")]
        [SerializeField, Min(0f)] private float retractDuration = 0.2f;

        [Tooltip("Ease của chuyển động rút tape.")]
        [SerializeField] private Ease retractEase = Ease.OutQuad;

        [Tooltip("Biên độ giật của đầu ngoài tape khi bị kéo vào, tính theo ô. Giảm dần về 0 ở cuối " +
                 "nhịp rút. Đặt 0 để tắt. Chỉ ảnh hưởng hình ảnh, collider không giật theo.")]
        [SerializeField, Min(0f)] private float retractTipJitter = 0.05f;

        [Tooltip("Số radian của sóng sin trong 1 nhịp rút: 45 ≈ 7 lần giật. Cao hơn = giật nhanh và " +
                 "vụn hơn.")]
        [SerializeField, Min(0f)] private float retractTipJitterFrequency = 45f;

        [Header("Grind shake")]
        [Tooltip("Biên độ rung của thân máy trong lúc nghiền, chạy đúng bằng Retract Duration. " +
                 "Đặt 0 để tắt.")]
        [SerializeField, Min(0f)] private float grindShakeStrength = 0.035f;

        [Tooltip("Số nhịp rung nghiền. Cao hơn = rung dày và gắt hơn.")]
        [SerializeField, Min(0)] private int grindShakeVibrato = 18;

        [Header("Explode")]
        [Tooltip("Thời gian máy rung tại chỗ sau khi cột tape cuối rút xong, trước khi nổ. " +
                 "Đặt 0 để nổ ngay.")]
        [SerializeField, Min(0f)] private float explodeShakeDuration = 0.25f;

        [Tooltip("Biên độ rung ngay trước khi nổ. Nên lớn hơn Grind Shake Strength để tách biệt.")]
        [SerializeField, Min(0f)] private float explodeShakeStrength = 0.08f;

        [Tooltip("Số nhịp rung ngay trước khi nổ.")]
        [SerializeField, Min(0)] private int explodeShakeVibrato = 14;

        [Tooltip("Thời gian thân máy co về 0 khi nổ.")]
        [SerializeField, Min(0f)] private float machineShrinkDuration = 0.25f;

        [Tooltip("Chờ bao lâu rồi mới destroy grinder và VFX nổ. Phải dài hơn độ dài VFX nổ, " +
                 "nếu không VFX sẽ bị cắt giữa chừng.")]
        [SerializeField, Min(0f)] private float explodeDestroyDelay = 1f;

        public int MaxTapeCells => maxTapeCells;
        public float IceMachineInset => iceMachineInset;

        public float RetractDuration => retractDuration;
        public Ease RetractEase => retractEase;
        public float RetractTipJitter => retractTipJitter;
        public float RetractTipJitterFrequency => retractTipJitterFrequency;

        public float GrindShakeStrength => grindShakeStrength;
        public int GrindShakeVibrato => grindShakeVibrato;

        public float ExplodeShakeDuration => explodeShakeDuration;
        public float ExplodeShakeStrength => explodeShakeStrength;
        public int ExplodeShakeVibrato => explodeShakeVibrato;
        public float MachineShrinkDuration => machineShrinkDuration;
        public float ExplodeDestroyDelay => explodeDestroyDelay;
    }
}
