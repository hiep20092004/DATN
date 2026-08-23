using DG.Tweening;
using WaterFlow.Enums;
using WaterFlow.Core;
using TMPro;
using UnityEngine;

namespace WaterFlow.Game
{
    public class TntBlockEffectBehavior : BlockEffectBehavior<TntBlockEffectData>
    {
        [SerializeField] Transform bombVisuals;
        [SerializeField] Transform textVisualRoot;
        [SerializeField] TMP_Text turnsText;
        
        [SerializeField] GameObject explosionVfxPrefab;
        [SerializeField] GameObject detonatorParticle;
        
        private int turnsLeft;
        private Tweener punchTween;
        private Tweener warningBlinkTween;
        private Sequence explosionSequence;
        private TntBlockEffectConfig config;
        private Color originalTextColor;
        private bool isExploding;
        private Vector3 bombVisualsStartLocalPos;
        private Vector3 bombVisualsStartLocalScale;

        public override void OnCreated(LevelBlockBehavior blockBehavior)
        {
            if (!ValidateDataOrDisable())
                return;

            config = GetConfig<TntBlockEffectConfig>();
            Bounds bounds = blockBehavior.Figure.GetHorizontalCenterBounds();

            transform.position = blockBehavior.transform.position + bounds.center + new Vector3(0, 0.5f * orderID, 0);
            bombVisuals.SetParent(blockBehavior.ModelParentTransform, true);
            
            detonatorParticle?.SetActive(false);
            if (bombVisuals)
            {
                bombVisualsStartLocalPos = bombVisuals.localPosition;
                bombVisualsStartLocalScale = bombVisuals.localScale;
            }
            
            turnsLeft = Mathf.Max(0, Data.tntTurn);
            turnsText.text = turnsLeft.ToString();
            if (turnsText)
                originalTextColor = turnsText.color;

            ApplyWarningForCurrentTurns();
        }

        public override void OnMapSpawnCompleted()
        {
            detonatorParticle?.SetActive(true);
        }

        public override BlockEffectData GetCurrentEffectData()
        {
            return new TntBlockEffectData { tntTurn = turnsLeft };
        }

        public override void OnDisabled(LevelBlockBehavior blockBehavior, DisableSource disableSource)
        {
            if (!blockBehavior) return;
            if (turnsLeft > 0)
            {
                Services.AudioService.PlaySound(AudioId.Obstacle_Bomb_out);
            }
            if (textVisualRoot)
                textVisualRoot.localScale = Vector3.one;
            punchTween?.Kill();
            punchTween = null;
            warningBlinkTween?.Kill();
            warningBlinkTween = null;
            explosionSequence?.Kill();
            explosionSequence = null;
            explosionSequence = null;
            if (bombVisuals)
            {
                Destroy(bombVisuals.gameObject);
            }
        }


        public override void OnBlockFullFilledAfterAnimationGlobal(LevelBlockBehavior levelBlockBehavior, BlockColor filledColor)
        {
            if (!gameObject.activeSelf) return;
            if (levelBlockBehavior == linkedBlock)
            {
                return;
            }
            turnsLeft = Mathf.Max(0, turnsLeft - 1);
            
            if (turnsLeft <= 0)
            {
                OnNoTurnLeft();
            }
            else
            {
                turnsText.text = turnsLeft.ToString();

                if (turnsText)
                    turnsText.color = originalTextColor;

                warningBlinkTween?.Kill();
                warningBlinkTween = null;

                if (!ApplyWarningForCurrentTurns() && textVisualRoot)
                {
                    punchTween?.Kill();
                    punchTween = null;
                    textVisualRoot.localScale = Vector3.one;
                    float punchScale = config != null ? config.TurnPunchScale : 0.3f;
                    float punchDuration = config != null ? config.TurnPunchDuration : 0.3f;
                    punchTween = textVisualRoot.DOPunchScale(Vector3.one * punchScale, punchDuration)
                        .SetEase(DG.Tweening.Ease.OutSine);
                }
            }
        }

        private bool ApplyWarningForCurrentTurns()
        {
            if (!textVisualRoot || config == null) return false;

            if (turnsLeft == 2)
            {
                ApplyWarningPhase(config.WarningColor2, config.WarningBlinkScale2, config.WarningBlinkDuration2);
                return true;
            }

            if (turnsLeft == 1)
            {
                ApplyWarningPhase(config.WarningColor1, config.WarningBlinkScale1, config.WarningBlinkDuration1);
                return true;
            }

            return false;
        }

        private void ApplyWarningPhase(Color color, float blinkScale, float blinkDuration)
        {
            punchTween?.Kill();
            punchTween = null;
            warningBlinkTween?.Kill();
            warningBlinkTween = null;

            if (textVisualRoot)
                textVisualRoot.localScale = Vector3.one;
            if (turnsText)
                turnsText.color = color;

            if (!textVisualRoot) return;

            warningBlinkTween = textVisualRoot
                .DOScale(Vector3.one * (1f + blinkScale), blinkDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(DG.Tweening.Ease.InOutSine);
        }

        private void OnNoTurnLeft()
        {
            if (isExploding) return;
            isExploding = true;

            punchTween?.Kill();
            punchTween = null;
            warningBlinkTween?.Kill();
            warningBlinkTween = null;
            explosionSequence?.Kill();
            explosionSequence = null;

            config = GetConfig<TntBlockEffectConfig>();

            float moveDuration = config ? config.ExplodeMoveDuration : 0.4f;
            float moveUpY = config ? config.ExplodeMoveUpY : 1.2f;
            float scaleMultiplier = config ? config.ExplodeScaleMultiplier : 1.5f;
            float hideTextDuration = config ? config.ExplodeHideTextDuration : 0.2f;
            float finalShakeDuration = config ? config.ExplodeFinalShakeDuration : 0.15f;
            float finalShakeStrength = config ? config.ExplodeFinalShakeStrength : 0.15f;

            explosionSequence = DOTween.Sequence();

            if (textVisualRoot)
            {
                // First: hide the turn text, then animate bomb.
                explosionSequence.Append(textVisualRoot.DOScale(0f, hideTextDuration).SetEase(DG.Tweening.Ease.InBack));
            }

            if (bombVisuals)
            {
                float startLocalY = bombVisuals.localPosition.y;
                explosionSequence.Append(
                    bombVisuals.DOLocalMoveY(startLocalY + moveUpY, moveDuration)
                        .SetEase(DG.Tweening.Ease.OutBack, 1.6f)
                );
                explosionSequence.Join(
                    bombVisuals.DOScale(scaleMultiplier, moveDuration)
                        .SetEase(DG.Tweening.Ease.OutBack)
                );

                // More violent shaking to sell \"about to explode\".
                explosionSequence.Append(
                    bombVisuals.DOShakePosition(
                        finalShakeDuration,
                        strength: new Vector3(finalShakeStrength, finalShakeStrength, 0f),
                        vibrato: 35,
                        randomness: 90f,
                        snapping: false,
                        fadeOut: true
                    )
                );
                explosionSequence.Join(
                    bombVisuals.DOShakeRotation(
                        finalShakeDuration,
                        strength: new Vector3(0f, 0f, 5f),
                        vibrato: 35,
                        randomness: 90f,
                        fadeOut: true
                    )
                );
            }

            float gameOverDelay = explosionSequence.Duration() + 1f;
            GameController.Instance.GameOver(LoseReason.TntExploded, gameOverDelay);

            explosionSequence.OnComplete(() =>
            {
                SpawnExplosionVfx();
                Services.AudioService.PlaySound(AudioId.Obstacle_Bomb_Explosion);
                if (bombVisuals)
                    bombVisuals.gameObject.SetActive(false);
            });
        }
        
        public override void OnRevived(LoseReason loseReason, int seconds)
        {
            if (loseReason != LoseReason.TntExploded) return;
            if(hiddenVisualSources.Contains(ToggleVisualSource.IceEffect)) return;
            config = GetConfig<TntBlockEffectConfig>();
            int extraTurns = config ? config.ExtraTurnsAfterRevive : 3;
            AddTurns(extraTurns);
            isExploding = false;
            explosionSequence?.Kill();
            explosionSequence = null;
            warningBlinkTween?.Kill();
            warningBlinkTween = null;
            punchTween?.Kill();
            punchTween = null;
            if (turnsText)
                turnsText.color = originalTextColor;
            if (textVisualRoot)
                textVisualRoot.localScale = Vector3.one;
            ReEnableAfterExplosion();
            RefreshTurnVisuals();
            ApplyWarningForCurrentTurns();
        }

        protected override void ApplyVisualState(bool visible)
        {
            base.ApplyVisualState(visible);
            if (bombVisuals)
                bombVisuals.gameObject.SetActive(visible);
        }

        private void AddTurns(int extraTurns)
        {
            turnsLeft += extraTurns;
        }

        private void RefreshTurnVisuals()
        {
            if (turnsText)
                turnsText.text = Mathf.Max(0, turnsLeft).ToString();
        }

        private void ReEnableAfterExplosion()
        {
            // Ensure visuals come back after revive (bomb was hidden on explosion).
            isActive = true;
            gameObject.SetActive(true);

            if (bombVisuals)
            {
                bombVisuals.gameObject.SetActive(true);
                bombVisuals.localPosition = bombVisualsStartLocalPos;
                bombVisuals.localScale = bombVisualsStartLocalScale == Vector3.zero ? Vector3.one : bombVisualsStartLocalScale;
            }

            detonatorParticle?.SetActive(true);
        }

        private void SpawnExplosionVfx()
        {
            // Vector3 pos = bombVisuals ? bombVisuals.position : transform.position;
            // Quaternion rot = bombVisuals ? bombVisuals.rotation : transform.rotation;

            GameObject vfxInstance = Instantiate(explosionVfxPrefab);
            vfxInstance.transform.position = bombVisuals.position + new Vector3(0, 2, 0);
            vfxInstance.transform.localRotation = Quaternion.Euler(90f, 0, 0f);
            
            ParticleSystem ps = vfxInstance.GetComponentInChildren<ParticleSystem>(true);
            if (ps)
            {
                ps.PlayCase().Disabled += () =>
                {
                    if (vfxInstance)
                        Destroy(vfxInstance);
                };
            }
            else
            {
                Destroy(vfxInstance, 2f);
            }
        }

    }
}