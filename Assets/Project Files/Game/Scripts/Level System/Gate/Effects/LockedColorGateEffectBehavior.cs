using System;
using System.Collections;
using WaterFlow.Enums;
using UnityEngine;

namespace WaterFlow.Game
{
    public class LockedColorGateEffectBehavior : GateEffectBehavior, IColorElement, IKeyMovementAction
    {
        [SerializeField] private ObstacleColorCustomConfig starColorSO;
        [SerializeField] private MeshRenderer lockRenderer;
        [SerializeField] private Transform lockCenter;
        [SerializeField] private GameObject lockHole;
        [SerializeField] private ParticleSystem lockOpenFx;
        [SerializeField] private SpriteRenderer[] starIcons;

        private const float LockOpenFxDelay = 0.25f;
        
        private BlockColor keyColor;
        private bool isUnlocked = false;
        private bool isUnlockedVisual = false;
        private MaterialPropertyBlock propertyBlock;
        private bool isKeyReached = false;
        
        public override void OnCreated(GateBehavior gateBehavior)
        {
            keyColor = ((LockedColorGateEffectData)data).lockColor;
            Color color = LevelController.Instance.GetBlockColorData(keyColor).Color;
            propertyBlock = new MaterialPropertyBlock();
            propertyBlock.SetColor(ShaderId.COLOR_SHADER_ID, color);
            lockRenderer.SetPropertyBlock(propertyBlock);

            this.transform.localRotation = GetLockRotation(gateBehavior);
            this.transform.position = GetLockPosition(gateBehavior);
            
            lockHole.SetActive(false);
            lockOpenFx?.gameObject.SetActive(false);
            gateBehavior.SetActiveArrow(false);
            ColorManager.RegisterElement(this);
            SetStarColor(keyColor);
        }
        private static Vector3 GetLockPosition(GateBehavior gateBehavior)
        {
            var type = gateBehavior.Data.GateDirectionType;
            var lockPosition = gateBehavior.ArrowTransform.position;
            return type switch
            {
                GateDirection.Type.Left => lockPosition + new Vector3(-0.1f, 0, -0.15f),
                GateDirection.Type.Right => lockPosition + new Vector3(0.058f, 0, -0.15f),
                GateDirection.Type.Bottom => lockPosition + new Vector3(0, 0, -0.3f),
                _ => lockPosition
            };
        }
        
        private static Quaternion GetLockRotation(GateBehavior gateBehavior)
        {
            var type = gateBehavior.Data.GateDirectionType;
            return type is GateDirection.Type.Top or GateDirection.Type.Bottom ? Quaternion.Euler(0, 0, 0) : Quaternion.Euler(0, 90, 0);
        }

        public BlockColor Color => keyColor;
        public bool IsTargetAvailable => !isUnlockedVisual;
        public Vector3 TargetPosition => lockCenter.position;

        public override void OnDisabled(GateBehavior gateBehavior)
        {
            ColorManager.UnregisterElement(this);
            if (!isKeyReached)
            {
                PlaySound();
            }
            gateBehavior.SetActiveArrow(true);
        }

        private void PlaySound()
        {
            Services.AudioService.PlaySound(AudioId.Obstacle_Locked_exit);
        }

        public override BlockGateState CanGoThroughGate(LevelBlockBehavior levelBlockBehavior)
        {
            if(isUnlocked)
                return BlockGateState.Enterable;
            return BlockGateState.GateLocked;
        }

        public void OnKeyLinked()
        {
            lockHole.SetActive(true);
            isUnlocked = true;
        }

        public void OnKeyReached()
        {
            isKeyReached = true;
            PlaySound();
            StartCoroutine(PlayLockOpenFxAfterDelay());
        }

        private IEnumerator PlayLockOpenFxAfterDelay()
        {
            if (!lockOpenFx) yield break;
            yield return new WaitForSeconds(LockOpenFxDelay);
            lockOpenFx.gameObject.SetActive(true);
            lockOpenFx.transform.SetParent(linkedGate.transform, true);
            lockOpenFx.Play();
        }

        public void OnKeyFinished()
        {
            isUnlockedVisual = true;
            DisableEffect();
        }
        void SetStarColor(BlockColor color)
        {
            if (starColorSO == null || !starColorSO.TryGetOverride(color, out ColorOverrideConfig starColor))
                return;

            foreach (var icon in starIcons)
            {
                if (icon == null) return;
                icon.sprite = starColor.Sprite;
            }
        }
    }
}