using UnityEngine;
using DG.Tweening;

namespace WaterFlow.Game
{
    public class MovingGateLockVisual : MonoBehaviour
    {
        [SerializeField] private GameObject visualContainer;
        [SerializeField] private GameObject arrow;
        [SerializeField] private GameObject lockGate;

        private MovingGateLockConfig config;
        private GateBehavior currentGateBehavior;
        private Sequence moveSequence;

        public void Init(GateBehavior gateBehavior, bool isClockwise, MovingGateLockConfig movingConfig,
            bool affectGateVisuals = true)
        {
            config = movingConfig;
            this.currentGateBehavior = gateBehavior;
            if (arrow)
                arrow.transform.localScale = new Vector3(1, 1, isClockwise ? 1 : -1);

            if (affectGateVisuals && currentGateBehavior)
            {
                currentGateBehavior.SetActiveArrow(false);
            }

            Transform anchor = GetAnchorTransform(currentGateBehavior);
            if (anchor)
            {
                transform.position = anchor.position;
                transform.rotation = anchor.rotation;
            }
        }

        public void ReleaseGateVisuals()
        {
            if (currentGateBehavior)
                currentGateBehavior.SetActiveArrow(true);
        }

        public void SnapToGate(GateBehavior gateBehavior, bool affectGateVisuals)
        {
            currentGateBehavior = gateBehavior;
            if (affectGateVisuals && currentGateBehavior)
                currentGateBehavior.SetActiveArrow(false);

            Transform anchor = GetAnchorTransform(currentGateBehavior);
            if (anchor)
            {
                transform.position = anchor.position;
                transform.rotation = anchor.rotation;
            }
        }

        public void ChangeToNewGate(GateBehavior newGateBehavior)
        {
            if (!newGateBehavior || newGateBehavior == currentGateBehavior)
                return;

            GateBehavior oldGate = currentGateBehavior;
            currentGateBehavior = newGateBehavior;

            if (oldGate)
            {
                oldGate.SetActiveArrow(true);
            }

            newGateBehavior.SetActiveArrow(false);

            Transform newAnchor = GetAnchorTransform(newGateBehavior);
            if (!newAnchor)
                return;

            moveSequence?.Kill();
            var visualContainerTransform = visualContainer.transform;
            if (!Application.isPlaying)
            {
                transform.position = newAnchor.position;
                transform.rotation = newAnchor.rotation;
                visualContainerTransform.localScale = Vector3.one;
                transform.SetParent(newAnchor, true);
                return;
            }

            visualContainerTransform.localScale = Vector3.one;

            float jumpHeight = config ? config.JumpHeight : 0.4f;
            float anticipationDuration = config != null ? config.AnticipationDuration : 0.08f;
            float jumpDuration = config ? config.JumpDuration : 0.35f;
            float landDuration = config ? config.LandDuration : 0.15f;

            var seq = DOTween.Sequence();

            // Phase 1 — anticipation squash
            seq.Append(visualContainerTransform.DOScale(new Vector3(1.15f, 0.75f, 1.15f), anticipationDuration)
                .SetEase(Ease.OutQuad));

            // Phase 2 — parabolic jump
            seq.Append(transform.DOJump(newAnchor.position, jumpHeight, 1, jumpDuration)
                .SetEase(Ease.InOutQuad));

            // concurrent: scale stretch back to normal
            seq.Join(transform.DOScale(Vector3.one, jumpDuration)
                .SetEase(Ease.OutCubic));

            // concurrent: rotate toward target gate during flight
            seq.Join(transform.DORotateQuaternion(newAnchor.rotation, jumpDuration)
                .SetEase(Ease.InOutSine));

            // Phase 3 — landing bounce
            seq.Append(visualContainerTransform.DOPunchScale(new Vector3(0.1f, -0.15f, 0.1f), landDuration, 1, 0.5f));

            seq.OnComplete(() =>
            {
                if (!this)
                    return;

                transform.position = newAnchor.position;
                transform.rotation = newAnchor.rotation;
                visualContainerTransform.localScale = Vector3.one;
                transform.SetParent(newAnchor, true);
            });

            moveSequence = seq;
        }

        private static Transform GetAnchorTransform(GateBehavior gateBehavior)
        {
            if (!gateBehavior)
                return null;

            return gateBehavior.PipeTransform ? gateBehavior.PipeTransform : gateBehavior.transform;
        }

        private void OnDisable()
        {
            moveSequence?.Kill();
            moveSequence = null;
        }
    }
}
