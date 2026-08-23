using DG.Tweening;
using WaterFlow.Enums;
using WaterFlow.Core;
using TMPro;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// One visual per linked PAIR (unlike Combines which spawns one per touching cell). Shows the link's
    /// remaining clear counter and plays a break FX when the link is destroyed. Placement/orientation and
    /// the per-block materials are driven by the reused <see cref="CombinesEffectBehavior.ConnectedBlocks"/>.
    /// </summary>
    public class BreakableLinkVisuals : MonoBehaviour
    {
        private static readonly AudioId[] LinkBreakAudio =
        {
            AudioId.Obstacle_TieBlock_01,
            AudioId.Obstacle_TieBlock_02,
            AudioId.Obstacle_TieBlock_03,
        };

        [SerializeField] TMP_Text countText;
        [SerializeField] Transform textVisualRoot;
        [SerializeField] ParticleSystem breakFx;
        [SerializeField] GameObject linkVertical;
        [SerializeField] GameObject linkHorizontal;

        private CombinesEffectBehavior.ConnectedBlocks connectedBlocks;
        private Tweener punchTween;

        public void Init(CombinesEffectBehavior.ConnectedBlocks connection, int count)
        {
            connectedBlocks = connection;

            var isVertical = connection.Direction == CombinesEffectBehavior.Direction.Down || connection.Direction == CombinesEffectBehavior.Direction.Up;

            linkVertical.SetActive(isVertical);
            linkHorizontal.SetActive(!isVertical);

            if (breakFx) breakFx.gameObject.SetActive(false);

            SetCount(count);
        }

        public void SetCount(int count)
        {
            if (countText)
                countText.text = count.ToString();
        }

        public void PlayPunch(float scale, float duration)
        {
            if (!textVisualRoot) return;

            punchTween?.Kill();
            textVisualRoot.localScale = Vector3.one;
            punchTween = textVisualRoot.DOPunchScale(Vector3.one * scale, duration)
                .SetEase(DG.Tweening.Ease.OutSine);
        }

        public bool InvolvesBlock(LevelBlockBehavior block)
        {
            if (!block || connectedBlocks == null)
                return false;

            return connectedBlocks.BlockA == block || connectedBlocks.BlockB == block;
        }

        public void Hide(bool playBreakFx)
        {
            punchTween?.Kill();
            punchTween = null;

            if (playBreakFx && breakFx)
            {
                PlayBreakAudio();

                var fxTransform = breakFx.transform;
                var localPosition = fxTransform.localPosition;
                localPosition.y = 0.2f;
                fxTransform.localPosition = localPosition;
                fxTransform.SetParent(null, true);
                fxTransform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                breakFx.gameObject.SetActive(true);
                Destroy(breakFx.gameObject, 1f);
            }

            Destroy(gameObject);
        }

        private void PlayBreakAudio()
        {
            AudioId pickedAudio = LinkBreakAudio[Random.Range(0, LinkBreakAudio.Length)];
            Services.AudioService.PlaySound(pickedAudio);
        }
    }
}
