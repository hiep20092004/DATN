using System;
using DG.Tweening;
using WaterFlow.Enums;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WaterFlow.Game
{
    /// <summary>
    /// Grinder machine: a 1-cell-wide obstacle laid out along one axis with blocking ice tape on one
    /// or both sides. Every block cleared from the board retracts the outermost tape column on each
    /// side; when the last column is gone the machine explodes and frees its cell.
    ///
    /// Only the machine cell exists in <see cref="LevelData"/> — the tape length comes from
    /// <see cref="InteractableObjectData.GrinderConfig"/> (see <see cref="GrinderLayout"/>), so tape
    /// cells stay plain inner tiles in the level grid.
    ///
    /// Each side's tape is a single 9-sliced ice sprite resized to the cells it covers, so the rounded
    /// caps keep their shape at any length and retracting is a resize, not a rebuild. Deliberately no
    /// <see cref="SpriteMask"/>: a sprite stencil is global, so one grinder's mask window would also
    /// reveal every other grinder's ice — and, within a grinder, each side's window would reveal the
    /// opposite side's tape. Blocking is physics-driven — each side's collider is resized with the
    /// tape, so a freed cell stops blocking the same frame.
    /// </summary>
    public class GrinderInteractableObjectBehavior : InteractableObjectBehavior
    {
        /// <summary>One tape side. Everything is authored along the side root's local +X, in cells.</summary>
        [Serializable]
        public class TapeSide
        {
            [Tooltip("Rotated by code to point along this side's direction.")]
            public Transform root;

            [Tooltip("Holds the active ice variant and is centred on the span the tape covers.")]
            public Transform ice;

            [Tooltip("Ice art for a horizontal grinder. Uniform scale; 9-sliced along its long axis.")]
            public SpriteRenderer iceHorizontal;

            [Tooltip("Ice art for a vertical grinder. Uniform scale; 9-sliced along its long axis.")]
            public SpriteRenderer iceVertical;

            [Tooltip("Blocks blocks over the covered cells. Resized together with the tape.")]
            public BoxCollider blocker;

            [Tooltip("Blade art facing this side on the horizontal machine. Hidden when the side has no tape.")]
            public GameObject bladeHorizontal;

            [Tooltip("Blade art facing this side on the vertical machine. Hidden when the side has no tape.")]
            public GameObject bladeVertical;

            private Vector3 iceBaseLocalPosition;
            private bool cached;

            private SpriteRenderer activeIce;
            private bool lengthIsSpriteY;
            private float iceLengthScale;
            private float iceCrossSize;

            public void CacheAuthoredValues()
            {
                if (cached) return;

                if (ice) iceBaseLocalPosition = ice.localPosition;
                cached = true;
            }

            public void SetOrientation(bool isVertical, bool hasCells)
            {
                if (iceHorizontal) iceHorizontal.gameObject.SetActive(!isVertical);
                if (iceVertical) iceVertical.gameObject.SetActive(isVertical);

                if (bladeHorizontal) bladeHorizontal.SetActive(hasCells && !isVertical);
                if (bladeVertical) bladeVertical.SetActive(hasCells && isVertical);

                activeIce = isVertical ? iceVertical : iceHorizontal;
                if (!activeIce || !activeIce.sprite) return;

                // The art's long axis is the tape direction by definition, so nothing has to author it.
                Vector3 nativeSize = activeIce.sprite.bounds.size;
                lengthIsSpriteY = nativeSize.y > nativeSize.x;

                // Read the scale of whichever axis is the length: the cross axis is free for art to
                // narrow the bar to the machine's port, so the two are not interchangeable.
                Vector3 localScale = activeIce.transform.localScale;
                iceLengthScale = lengthIsSpriteY ? localScale.y : localScale.x;

                // Kept at the sprite's native size so exactly one tile spans the bar's width.
                iceCrossSize = lengthIsSpriteY ? nativeSize.x : nativeSize.y;
            }

            /// <summary>
            /// <paramref name="visibleCells"/> is fractional while retracting so the tape can animate.
            /// Cell k (1-based) is centred at local X = k, so the covered span is [0.5, cells + 0.5].
            /// <paramref name="machineInset"/> extends the inner end back under the machine art.
            /// <paramref name="outerJitter"/> shakes only the outer tip while it is being dragged in;
            /// the blocker ignores it so physics never follows the cosmetic stutter.
            /// </summary>
            public void Apply(float visibleCells, float machineInset, float outerJitter = 0f)
            {
                bool visible = visibleCells > 0.001f;
                if (root) root.gameObject.SetActive(visible);
                if (!visible) return;

                float innerEdge = 0.5f - machineInset;
                float outerEdge = Mathf.Max(innerEdge + 0.01f, visibleCells + 0.5f + outerJitter);

                if (ice)
                {
                    Vector3 icePosition = iceBaseLocalPosition;
                    icePosition.x = (innerEdge + outerEdge) * 0.5f;
                    ice.localPosition = icePosition;
                }

                if (activeIce && iceLengthScale > 0.0001f)
                {
                    float localLength = (outerEdge - innerEdge) / iceLengthScale;
                    activeIce.size = lengthIsSpriteY
                        ? new Vector2(iceCrossSize, localLength)
                        : new Vector2(localLength, iceCrossSize);
                }

                if (blocker)
                {
                    Vector3 size = blocker.size;
                    size.x = visibleCells;
                    blocker.size = size;

                    Vector3 centre = blocker.center;
                    centre.x = (visibleCells + 1f) * 0.5f;
                    blocker.center = centre;
                }
            }
        }

        [Tooltip("Shrunk on explode. Not rotated by code — each orientation has its own authored art.")]
        [SerializeField] Transform machineRoot;

        [Tooltip("Machine art for a horizontal grinder.")]
        [SerializeField] GameObject centerSpriteHorizontal;

        [Tooltip("Machine art for a vertical grinder.")]
        [SerializeField] GameObject centerSpriteVertical;

        [Space]
        [Tooltip("Side along +axis: right when ngang, up when dọc.")]
        [SerializeField] TapeSide positiveSide = new TapeSide();

        [Tooltip("Side along -axis: left when ngang, down when dọc.")]
        [SerializeField] TapeSide negativeSide = new TapeSide();

        [Space]
        [Tooltip("Burst played every time a tape column retracts.")]
        [SerializeField] ParticleSystem pieceBreakVfx;

        [Tooltip("Played once when the last column is gone. Detached before the machine shrinks.")]
        [SerializeField] ParticleSystem explosionVfx;

        [Space]
        [Tooltip("Shared tuning for every grinder: tape limits, retract feel, shakes and explode timings.")]
        [SerializeField] GrinderTuningConfig tuning;

        private static readonly AudioId[] BreakAudioIds =
        {
            AudioId.Obstacle_Grinder_break_01,
            AudioId.Obstacle_Grinder_break_02,
            AudioId.Obstacle_Grinder_break_03,
        };

        // Shared across every grinder instance so two machines ticking the same frame don't stack SFX.
        private static int lastAudioFrame = -1;

        private static GrinderTuningConfig fallbackTuning;

        private GrinderLayout layout;
        private bool hasTape;
        private bool isExploding;

        private int positiveCells;
        private int negativeCells;
        private Tween positiveTween;
        private Tween negativeTween;
        private Tween grindShakeTween;
        private Sequence explodeSequence;
        private Vector3 machineBaseLocalPosition;

        public GrinderLayout Layout => layout;

        private GrinderTuningConfig Tuning
        {
            get
            {
                if (tuning) return tuning;

                // Defaults keep a misconfigured prefab playable instead of throwing on every clear.
                if (!fallbackTuning)
                {
                    Debug.LogError($"[Grinder] at {position}: no {nameof(GrinderTuningConfig)} assigned; " +
                                   "falling back to script defaults.", this);
                    fallbackTuning = ScriptableObject.CreateInstance<GrinderTuningConfig>();
                }

                return fallbackTuning;
            }
        }

        /// <summary>Block clears still needed before the machine explodes.</summary>
        public int RemainingSteps => Mathf.Max(positiveCells, negativeCells);

        public override void OnCreated()
        {
            if (pieceBreakVfx)
                pieceBreakVfx.gameObject.SetActive(false);

            if (explosionVfx)
                explosionVfx.gameObject.SetActive(false);

            if (machineRoot)
                machineBaseLocalPosition = machineRoot.localPosition;

            if (data == null) return;

            layout = GrinderLayout.From(data.GrinderConfig);
            hasTape = layout.HasTape;

            positiveSide.CacheAuthoredValues();
            negativeSide.CacheAuthoredValues();
            ApplyOrientation();

            if (!hasTape)
            {
                Debug.LogError($"[Grinder] at {position}: config {data.GrinderConfig} has no tape; " +
                               "it needs at least 1 cell on one side.", this);
                positiveSide.Apply(0f, Tuning.IceMachineInset);
                negativeSide.Apply(0f, Tuning.IceMachineInset);
                return;
            }

            WarnIfExceedsMaxTapeCells();

            positiveCells = Mathf.Min(layout.PositiveLength, Tuning.MaxTapeCells);
            negativeCells = Mathf.Min(layout.NegativeLength, Tuning.MaxTapeCells);

            positiveSide.Apply(positiveCells, Tuning.IceMachineInset);
            negativeSide.Apply(negativeCells, Tuning.IceMachineInset);
        }

        public override void OnBlockDestructed(LevelBlockBehavior levelBlockBehavior)
        {
            if (isExploding || !hasTape) return;

            int previousPositiveCells = positiveCells;
            int previousNegativeCells = negativeCells;

            positiveCells = RetractSide(positiveSide, positiveCells, ref positiveTween);
            negativeCells = RetractSide(negativeSide, negativeCells, ref negativeTween);

            bool retracted = positiveCells != previousPositiveCells || negativeCells != previousNegativeCells;

            if (Application.isPlaying && retracted)
            {
                PlayAudio(BreakAudioIds[Random.Range(0, BreakAudioIds.Length)]);
                PlayPieceBreakVfx();
                PlayGrindShake();
            }

            if (positiveCells == 0 && negativeCells == 0)
                Explode();
        }

        private float GetTipJitter(float progress)
        {
            float amplitude = Tuning.RetractTipJitter;
            if (amplitude <= 0f) return 0f;

            float fade = Mathf.Clamp01(1f - progress);
            return Mathf.Sin(progress * Tuning.RetractTipJitterFrequency) * amplitude * fade;
        }

        private void PlayGrindShake()
        {
            if (!machineRoot || Tuning.GrindShakeStrength <= 0f) return;

            StopGrindShake();
            grindShakeTween = machineRoot
                .DOShakePosition(Tuning.RetractDuration, Tuning.GrindShakeStrength, Tuning.GrindShakeVibrato)
                .SetLink(gameObject);
        }

        private void StopGrindShake()
        {
            grindShakeTween?.Kill();
            grindShakeTween = null;

            if (machineRoot)
                machineRoot.localPosition = machineBaseLocalPosition;
        }

        private void PlayPieceBreakVfx()
        {
            if (!pieceBreakVfx) return;

            pieceBreakVfx.gameObject.SetActive(true);
            pieceBreakVfx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            pieceBreakVfx.Play(true);
        }

        private static void PlayAudio(AudioId audioId)
        {
            if (Time.frameCount == lastAudioFrame) return;
            lastAudioFrame = Time.frameCount;
            Services.AudioService.PlaySound(audioId);
        }

        private void WarnIfExceedsMaxTapeCells()
        {
            int maxCells = Tuning.MaxTapeCells;
            if (layout.PositiveLength <= maxCells && layout.NegativeLength <= maxCells) return;

            Debug.LogWarning($"[Grinder] at {position}: config {data.GrinderConfig} exceeds the " +
                             $"{maxCells}-cell limit; the tape is clamped.", this);
        }

        private void ApplyOrientation()
        {
            bool isVertical = layout.IsVertical;

            if (centerSpriteHorizontal) centerSpriteHorizontal.SetActive(!isVertical);
            if (centerSpriteVertical) centerSpriteVertical.SetActive(isVertical);

            positiveSide.SetOrientation(isVertical, layout.PositiveLength > 0);
            negativeSide.SetOrientation(isVertical, layout.NegativeLength > 0);

            // Each side root's local +X points outward, so both sides share the same cell math.
            if (positiveSide.root)
            {
                positiveSide.root.localRotation =
                    isVertical ? Quaternion.Euler(0f, -90f, 0f) : Quaternion.identity;
            }

            if (negativeSide.root)
            {
                negativeSide.root.localRotation =
                    isVertical ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.Euler(0f, 180f, 0f);
            }
        }

        private int RetractSide(TapeSide side, int currentCells, ref Tween sideTween)
        {
            if (currentCells <= 0) return 0;

            int targetCells = currentCells - 1;

            // The collider shrinks with the animated tape, so the freed cell only opens as the ice
            // visually clears it — matching what the player sees.
            sideTween?.Kill();

            float inset = Tuning.IceMachineInset;

            if (!Application.isPlaying)
            {
                side.Apply(targetCells, inset);
                return targetCells;
            }

            float animated = currentCells;
            int startCells = currentCells;
            sideTween = DOTween.To(() => animated, value =>
                {
                    animated = value;
                    side.Apply(value, inset, GetTipJitter(startCells - value));
                }, targetCells, Tuning.RetractDuration)
                .SetEase(Tuning.RetractEase)
                .OnComplete(() => side.Apply(targetCells, inset))
                .SetLink(gameObject);

            return targetCells;
        }

        private void Explode()
        {
            isExploding = true;

            if (!Application.isPlaying)
            {
                FreeCell();
                DestroyImmediate(gameObject);
                return;
            }

            explodeSequence = DOTween.Sequence().SetLink(gameObject);
            explodeSequence.AppendInterval(Tuning.RetractDuration);
            explodeSequence.AppendCallback(StopGrindShake);

            if (machineRoot && Tuning.ExplodeShakeDuration > 0f)
            {
                explodeSequence.Append(machineRoot.DOShakePosition(
                    Tuning.ExplodeShakeDuration, Tuning.ExplodeShakeStrength, Tuning.ExplodeShakeVibrato));
            }

            explodeSequence.AppendCallback(Detonate);
        }

        private void Detonate()
        {
            PlayAudio(AudioId.Obstacle_Grinder_destroy);
            FreeCell();
            PlayExplosionVfx();

            if (machineRoot)
                machineRoot.DOScale(Vector3.zero, Tuning.MachineShrinkDuration).SetEase(Ease.InBack);

            Destroy(gameObject, Tuning.ExplodeDestroyDelay);
        }

        private void FreeCell()
        {
            OwnerLevel?.ConvertInteractableCellToInnerTile(position);
            OwnerLevel?.EnvironmentSpawner?.RemoveInteractableObject(this);

            foreach (Collider machineCollider in GetComponentsInChildren<Collider>())
                machineCollider.enabled = false;
        }

        private void PlayExplosionVfx()
        {
            if (!explosionVfx) return;

            GameObject vfxObject = explosionVfx.gameObject;
            explosionVfx.transform.SetParent(null, true);
            vfxObject.SetActive(true);
            explosionVfx.Play(true);

            Destroy(vfxObject, Tuning.ExplodeDestroyDelay);
        }

        private void OnDestroy()
        {
            positiveTween?.Kill();
            negativeTween?.Kill();
            grindShakeTween?.Kill();
            explodeSequence?.Kill();

            if (machineRoot)
                machineRoot.DOKill();
        }
    }
}
