using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    public sealed class KeyMultiBlockEffectBehavior : BlockEffectBehavior<KeyChainBlockEffectData>
    {
        [SerializeField] KeyMovementBehavior keyBehavior;
        [SerializeField] GameObject keyParticles;
        [SerializeField] private GameObject keyIcon;
        
        [Space]
        [SerializeField] float offsetY = 0.1f;

        private bool isKeyCollected;
        
        
        public override void OnCreated(LevelBlockBehavior blockBehavior)
        {
            if (!ValidateDataOrDisable())
                return;

            Bounds bounds = blockBehavior.Figure.GetVerticalCenterBounds();

            transform.position = blockBehavior.transform.position + bounds.center + new Vector3(0, offsetY * orderID, 0);
            transform.SetParent(blockBehavior.ModelParentTransform, true);
        
            keyParticles?.SetActive(false);
            keyIcon?.SetActive(true);
            keyBehavior?.gameObject.SetActive(false);
        }

        public override void OnCreatedAllBlock()
        {
#if UNITY_EDITOR
            var chainElement = ChainManager.GetChainElement();
            keyBehavior.SetSimulateTarget(chainElement as IKeyMovementAction);
#endif
        }

        public override void OnBlockFullAfterAnimationFilled()
        {
            if (isKeyCollected) return;
            isKeyCollected = true;
            keyIcon?.SetActive(false);
            keyBehavior?.gameObject.SetActive(true);

            var chainElement = ChainManager.GetChainElement();
            if (chainElement != null)
            {
                keyBehavior.StartMovement(chainElement as IKeyMovementAction);
                keyParticles?.SetActive(true);
            }

            DisableEffect();
        }

        public override void OnDisabled(LevelBlockBehavior blockBehavior, DisableSource disableSource)
        {
            if (!isKeyCollected)
            {
                var chainElement = ChainManager.GetChainElement();
                if (chainElement is IKeyMovementAction kmm)
                {
                    kmm.OnKeyLinked();
                    kmm.OnKeyFinished();
                }
            }
        }
    }
}