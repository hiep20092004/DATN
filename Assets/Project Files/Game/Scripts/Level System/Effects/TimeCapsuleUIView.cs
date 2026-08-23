using System;
using Coffee.UIExtensions;
using DG.Tweening;
using WaterFlow.Enums;
using TMPro;
using UnityEngine;

namespace WaterFlow.Game
{
    public class TimeCapsuleUIView : MonoBehaviour
    {
        private enum ArcDirection
        {
            TowardCenter,
            AwayFromCenter
        }

        [SerializeField] RectTransform rectTransform;
        [SerializeField] RectTransform visualTransform;
        [SerializeField] TextMeshProUGUI timeText;
        [SerializeField] UIParticle trailParticles;
        
        [Header("Fly Animation")]
        [SerializeField] float flyDuration = 0.6f;
        [SerializeField] Ease moveEase = Ease.InCubic;
        [Tooltip("How far the arc bulges away from the straight line to target, as a fraction of the travel distance.")]
        [SerializeField] float arcHeightRatio = 0.35f;
        [Tooltip("TowardCenter curves the arc in toward the middle of the screen; AwayFromCenter curves it out toward the screen edge.")]
        [SerializeField] ArcDirection arcDirection = ArcDirection.TowardCenter;

        [Header("Scale Punch")]
        [SerializeField] float peakScale = 1.3f;
        [SerializeField] float arrivalScale = 0.4f;
        [Tooltip("Fraction of flyDuration spent scaling up to peakScale before scaling back down to arrivalScale.")]
        [SerializeField] [Range(0.1f, 0.9f)] float scaleUpRatio = 0.35f;

        [Tooltip("Extra time to keep trailParticles alive after arrival, so the trail fades out instead of cutting off.")]
        [SerializeField] float trailLingerDuration = 0.5f;

        private Sequence flySequence;

        public Vector2 BaseSize => rectTransform.rect.size;

        public void SetBonusText(int bonus)
        {
            if (timeText)
                timeText.text = $"+{bonus}";
        }

        public void PlayFlyTo(Vector2 startLocalPos, Vector2 targetLocalPos, Action onArrived)
        {
            trailParticles.Play();
            rectTransform.anchoredPosition = startLocalPos;
            visualTransform.localScale = Vector3.one;

            Vector2 controlPos = GetArcControlPoint(startLocalPos, targetLocalPos);

            flySequence?.Kill();
            flySequence = DOTween.Sequence();
            flySequence.Insert(0f, DOVirtual.Float(0f, 1f, flyDuration, t =>
                    rectTransform.anchoredPosition = EvaluateQuadraticBezier(startLocalPos, controlPos, targetLocalPos, t))
                .SetEase(moveEase));
            flySequence.Insert(0f, visualTransform.DOScale(peakScale, flyDuration * scaleUpRatio).SetEase(Ease.OutQuad));
            flySequence.Insert(flyDuration * scaleUpRatio, visualTransform.DOScale(arrivalScale, flyDuration * (1f - scaleUpRatio)).SetEase(Ease.InQuad));
            flySequence.OnComplete(() =>
            {
                Services.AudioService.PlaySound(AudioId.Obstacle_TimeCapsule);
                onArrived?.Invoke();
                visualTransform.gameObject.SetActive(false);
                Destroy(gameObject, trailLingerDuration);
            });
        }

        private Vector2 GetArcControlPoint(Vector2 start, Vector2 target)
        {
            Vector2 mid = (start + target) * 0.5f;
            Vector2 toTarget = target - start;
            Vector2 perpendicular = new Vector2(-toTarget.y, toTarget.x).normalized;

            // ItemOverlays is a fullscreen, center-pivoted RectTransform, so local (0,0) is the screen center.
            // Orient the perpendicular so it points toward or away from that center, per arcDirection, instead
            // of an arbitrary rotation side - otherwise the bulge direction would flip randomly depending on
            // which way start/target happen to be relative to each other.
            if (mid.sqrMagnitude > 0.0001f)
            {
                Vector2 towardCenter = -mid.normalized;
                bool pointsTowardCenter = Vector2.Dot(perpendicular, towardCenter) >= 0f;
                bool shouldPointTowardCenter = arcDirection == ArcDirection.TowardCenter;
                if (pointsTowardCenter != shouldPointTowardCenter)
                    perpendicular = -perpendicular;
            }

            float arcHeight = toTarget.magnitude * arcHeightRatio;
            return mid + perpendicular * arcHeight;
        }

        private static Vector2 EvaluateQuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float t)
        {
            float u = 1f - t;
            return (u * u * start) + (2f * u * t * control) + (t * t * end);
        }

        private void OnDestroy()
        {
            flySequence?.Kill();
        }

        private void Reset()
        {
            rectTransform = transform as RectTransform;
        }
    }
}
