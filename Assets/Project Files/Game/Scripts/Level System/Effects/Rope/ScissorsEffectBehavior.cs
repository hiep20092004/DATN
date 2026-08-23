using UnityEngine;

namespace WaterFlow.Game
{
    public class ScissorsEffectBehavior  : BlockEffectBehavior<ScissorBlockEffectData>
    {
        [SerializeField] ScissorsMovementBehavior scissorsMovementBehavior;
        [SerializeField] private ObstacleColorCustomConfig overrideColorData;
        [SerializeField] MeshRenderer leftHalfMeshRenderer;
        [SerializeField] MeshRenderer rightHalfMeshRenderer;
        
        [Space]
        [SerializeField] float offsetY = 0.1f;
        [SerializeField] GameObject linkedDebugGameObject;
        
        private BlockColor scissorsColor;
        private bool isCollected;
        private RopeBehavior ropeLink;
        
        public override void OnCreated(LevelBlockBehavior blockBehavior)
        {
            if (!ValidateDataOrDisable())
                return;

            Bounds bounds = blockBehavior.Figure.GetHorizontalCenterBounds();
            transform.position = blockBehavior.transform.position + bounds.center + new Vector3(0, offsetY * orderID, 0);
            this.transform.SetParent(blockBehavior.ModelParentTransform, true);
            
            scissorsColor = Data.scissorColor;

            scissorsMovementBehavior.Init();
            
            ApplyScissorColor();
        }

        private void ApplyScissorColor()
        {
            BlockColorData colorData = LevelController.Instance.GetBlockColorData(scissorsColor);
            Material instanceMaterial = null;
            if (overrideColorData != null &&
                overrideColorData.TryGetOverride(colorData.Type, out ColorOverrideConfig overrideEntry))
                instanceMaterial = overrideEntry.Material;
            if (!instanceMaterial) return;
            if (Application.isPlaying)
            {
                leftHalfMeshRenderer.material = instanceMaterial;
                rightHalfMeshRenderer.material = instanceMaterial;
            }
            else
            {
                leftHalfMeshRenderer.sharedMaterial = instanceMaterial;
                rightHalfMeshRenderer.sharedMaterial = instanceMaterial;
            }
        }

        public override void OnCreatedAllBlock()
        {
            ropeLink = RopesManager.LinkRope(scissorsColor);

            bool isActiveDebug = false;
#if UNITY_EDITOR
            if (ropeLink)
            {
                scissorsMovementBehavior.SetSimulateTarget(ropeLink);
            }
            else
            {
                isActiveDebug = true;
            }
#endif
            linkedDebugGameObject?.SetActive(isActiveDebug);
        }
        

        private void CollectScissors()
        {
            if (isCollected) return;
            isCollected = true;
            
            if (ropeLink)
            {
                scissorsMovementBehavior.StartMovement(ropeLink);
            }

            DisableEffect();
        }

        public override void OnBlockFullAfterAnimationFilled()
        {
            CollectScissors();
        }

        public override void OnDisabled(LevelBlockBehavior blockBehavior, DisableSource disableSource)
        {
            if (isCollected) return;
            CollectScissors();
        }

    }
}