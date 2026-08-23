using System;
using DG.Tweening;
using WaterFlow.Enums;
using WaterFlow.Core;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WaterFlow.Game
{
    public sealed class IceBlockEffectBehavior: BlockEffectBehavior<IceBlockEffectData>
    {
        [SerializeField] Transform textVisualRoot;
        [SerializeField] TextMeshProUGUI turnsText;
        [SerializeField] TextMeshProUGUI turnsShadowText;
        [SerializeField] Material iceMaterial;
        [SerializeField] Material iceGlassMaterial;

        [Space]
        [SerializeField] ParticleSystem turnParticle;
        [SerializeField] ParticleSystem disableParticle;

        [Header("Turn counter punch")]
        [SerializeField] float turnPunchScale = 0.2f;
        [SerializeField] float turnPunchDuration = 0.2f;

        private Material storedMaterial;
        private Material storedGlassMaterial;
        private Material meshMaterial;
        private Material glassMaterial;
        private int turnsLeft;

        private AudioId[] iceBreakAudio = new[]
            { AudioId.Obstacle_Ice_break_01, AudioId.Obstacle_Ice_break_02, AudioId.Obstacle_Ice_break_03 };
        private static int lastTimePlayAudio = -1; // For preventing audio overlap
        private Tweener punchTween;

        private bool restoredLinkedEffectsVisual;

        public override bool HidesBlockWater => IsActive;

        public override void OnCreated(LevelBlockBehavior blockBehavior)
        {
            if (!ValidateDataOrDisable())
                return;

            restoredLinkedEffectsVisual = false;
            var meshRenderer = blockBehavior.MeshRenderer;
            var meshGlass = blockBehavior.MeshGlass;
            if (Application.isPlaying)
            {
                meshMaterial = meshRenderer.material;
                glassMaterial = meshGlass.material;
            }
            else
            {
                meshMaterial = meshRenderer.sharedMaterial;
                glassMaterial = meshGlass.sharedMaterial;
            }
            blockBehavior.SetVisibleBlockWater(false);
            
            storedMaterial = meshMaterial;
            storedGlassMaterial = glassMaterial;

            if (iceMaterial) meshRenderer.material = iceMaterial;
            if (iceGlassMaterial) meshGlass.material = iceGlassMaterial;
            blockBehavior.ChangeOuterMaterial(iceMaterial);

            if (blockBehavior.Figure != null)
            {
                Bounds bounds = blockBehavior.Figure.GetHorizontalCenterBounds();
                transform.position = blockBehavior.transform.position + bounds.center;
            }
            turnsLeft = Mathf.Max(0, Data.iceTurnsAmount);
            turnsText.text = turnsLeft.ToString();
            turnsShadowText.text = turnsLeft.ToString();
            foreach (BlockEffectBehavior effect in linkedBlock.Effects)
            {
                if (effect == this) continue;
                effect.OnToggleVisual(false, ToggleVisualSource.IceEffect);
            }
        }

        public override void OnDisabled(LevelBlockBehavior blockBehavior, DisableSource disableSource)
        {
            if (!blockBehavior) return;
            punchTween?.Kill();
            punchTween = null;
            if (textVisualRoot)
                textVisualRoot.localScale = Vector3.one;
            blockBehavior.ChangeBodyMaterial(storedMaterial);
            blockBehavior.RestoreOuterMaterial();
            blockBehavior.ChangeGlassMaterial(storedGlassMaterial);
            blockBehavior.SetVisibleBlockWater(true);
            RestoreLinkedEffectsVisual();
            if (Time.frameCount != lastTimePlayAudio)
            {
                lastTimePlayAudio = Time.frameCount;
                Services.AudioService.PlaySound(AudioId.Obstacle_Ice_break_02);
            }
        }
        
        public override void OnBlockFullFilledAfterAnimationGlobal(LevelBlockBehavior levelBlockBehavior, BlockColor filledColor)
        {
            if (!gameObject.activeInHierarchy) return;
            
            turnsLeft = Mathf.Max(0, turnsLeft - 1);
            if (Time.frameCount != lastTimePlayAudio)
            {
                lastTimePlayAudio = Time.frameCount;
                AudioId pickedAudio = iceBreakAudio[Random.Range(0, iceBreakAudio.Length)];
                Services.AudioService.PlaySound(pickedAudio);
            }
            
            if (turnsLeft <= 0)
            {
                if (disableParticle)
                {
                    disableParticle.transform.SetParent(null);
                    disableParticle.PlayCase().Disabled += () =>
                    {
                        Destroy(disableParticle.gameObject);
                    };
                }
                
                DisableEffect();
                RestoreLinkedEffectsVisual();
            }
            else
            {
                if (turnParticle)
                    turnParticle.Play();
                
                turnsText.text = turnsLeft.ToString();
                turnsShadowText.text = turnsLeft.ToString();

                if (textVisualRoot)
                {
                    punchTween?.Kill();
                    textVisualRoot.localScale = Vector3.one;
                    punchTween = textVisualRoot.DOPunchScale(Vector3.one * turnPunchScale, turnPunchDuration)
                        .SetEase(DG.Tweening.Ease.OutSine);
                }
            }
        }
        
        private void RestoreLinkedEffectsVisual()
        {
            if (restoredLinkedEffectsVisual) return;
            restoredLinkedEffectsVisual = true;

            foreach (BlockEffectBehavior effect in linkedBlock.Effects)
            {
                if (effect == this) continue;
                effect.OnToggleVisual(true, ToggleVisualSource.IceEffect);
            }
        }

        
        public override void OnNewEffectAddedToBlock(BlockEffectBehavior effect)
        {
            if (!effect) return;
            if (turnsLeft == 0) return;

            // Turn off visuals of the effect that was added to the block
            effect.gameObject.SetActive(false);
        }

        public override bool IsClickable()
        {
            return false;
        }

        public override AudioId GetOverrideClickAudioId()
        {
            return AudioId.Click_obs_ice;
        }

        public override BlockEffectData GetCurrentEffectData()
        {
            return new IceBlockEffectData { iceTurnsAmount = turnsLeft };
        }

    }
}
