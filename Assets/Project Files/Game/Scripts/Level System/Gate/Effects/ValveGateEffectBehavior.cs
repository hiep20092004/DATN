using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    public class ValveGateEffectBehavior : GateEffectBehavior
    {
        [SerializeField] private Transform valveTransform;
        [SerializeField] private Transform rotatingPart;
        [SerializeField] private MeshRenderer levelRenderer;
        [SerializeField] float rotateDuration = 0.25f;
        [SerializeField] private SpriteRenderer fillImage;
        [SerializeField] private Sprite onIcon;
        [SerializeField] private Sprite offIcon;
        
        private MaterialPropertyBlock propertyBlock;
        private bool isOpen = false;
        private TweenCase rotatingPartTween;
        private int pendingValveTransitionCount;
        
        
        public override void OnCreated(GateBehavior gateBehavior)
        {
            isOpen = ((ValveGateEffectData)data).isValveOpened;
            propertyBlock = new MaterialPropertyBlock();
            levelRenderer.GetPropertyBlock(propertyBlock);
            valveTransform.position = gateBehavior.ArrowTransform.position;
            valveTransform.rotation = gateBehavior.ArrowTransform.rotation;
            gateBehavior.SetActiveArrow(false);
            
            UpdateVisual();
        }

        public override void OnBlockFullFilledBeforeAnimationGlobal(LevelBlockBehavior levelBlockBehavior)
        {
            ChangeState();
        }

        private void ChangeState()
        {
            isOpen = !isOpen;
        }
        public override void OnBlockFullFilledAfterAnimationGlobal(LevelBlockBehavior levelBlockBehavior, BlockColor filledColor)
        {
            UpdateVisual();
        }

        private float GetAngle()
        {
            return isOpen ? 0f : 90f;
        }

        private void UpdateVisual()
        {
            if (rotatingPartTween != null)
            {
                rotatingPartTween.Kill();
                rotatingPartTween = null;
            }
            
            if (!Application.isPlaying)
            {
                rotatingPart.localRotation = Quaternion.Euler(0f, GetAngle(), 0f);
                OnRotateComplete();
                return;
            }

            rotatingPartTween = rotatingPart.DOLocalRotate(Quaternion.Euler(0f, GetAngle(), 0f), rotateDuration).OnComplete(OnRotateComplete);
            rotatingPartTween.StartTween();
        }
        
        private void OnRotateComplete()
        {
            if (propertyBlock != null && levelRenderer)
            {
                levelRenderer.GetPropertyBlock(propertyBlock);
                fillImage.sprite = isOpen? onIcon : offIcon;
                levelRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        public override BlockGateState CanGoThroughGate(LevelBlockBehavior levelBlockBehavior)
        {
            return isOpen
                ? BlockGateState.Enterable
                : BlockGateState.GateValveLocked;
        }


        public override void OnRevived(LoseReason loseReason, int seconds)
        {
            if(loseReason!= LoseReason.ValveFailed) return;
            ChangeState();
            UpdateVisual();
        }

        public override GateEffectData GetCurrentEffectData()
        {
            return new ValveGateEffectData { isValveOpened = isOpen };
        }
    }
}