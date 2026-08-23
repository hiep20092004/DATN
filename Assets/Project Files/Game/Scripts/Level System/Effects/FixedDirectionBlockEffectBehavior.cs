using UnityEngine;

namespace WaterFlow.Game
{
    public sealed class FixedDirectionBlockEffectBehavior : BlockEffectBehavior<FixedDirectionBlockEffectData>
    {
        [SerializeField] Transform arrowTransform;
        [SerializeField] BlockMeshSlicer arrowSlicer;

        [Space]
        [SerializeField] float offsetY = 0.1f;
        
        public override void OnCreated(LevelBlockBehavior blockBehavior)
        {
            if (!ValidateDataOrDisable())
                return;

            Bounds bounds = Data.horizontalDirection ? blockBehavior.Figure.GetHorizontalCenterBounds() : blockBehavior.Figure.GetVerticalCenterBounds();

            arrowSlicer.Init();
            arrowSlicer.ApplyScaling(new Vector3(1, 1, Data.horizontalDirection ? bounds.size.x : bounds.size.z));

            arrowTransform.transform.localRotation = Quaternion.Euler(0, Data.horizontalDirection ? 90 : 0, 0);
            arrowTransform.transform.position = arrowTransform.transform.position + bounds.center + new Vector3(0, offsetY * orderID, 0);
            arrowTransform.transform.SetParent(blockBehavior.ModelParentTransform, true);
        }

        protected override void ApplyVisualState(bool visible)
        {
            base.ApplyVisualState(visible);
            arrowTransform.gameObject.SetActive(visible);
        }

        public override void OnBlockFullAfterAnimationFilled()
        {
            DisableEffect();
        }

        public override void OverrideMovementDirection(ref Vector3 movementDirection)
        {
            if (Data.horizontalDirection)
            {
                movementDirection.z = 0;
            }
            else
            {
                movementDirection.x = 0;
            }
        }

        public override void OnDisabled(LevelBlockBehavior blockBehavior, DisableSource disableSource)
        {
            if(arrowTransform) Destroy(arrowTransform.gameObject);
        }
    }
}