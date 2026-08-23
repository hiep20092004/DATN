using UnityEngine;

namespace WaterFlow.Game
{
    public class KeyColorBlockEffectBehavior : BlockEffectBehavior<KeyColorBlockEffectData>
    {
        [SerializeField] private ObstacleColorCustomConfig starColorSO;
        [SerializeField] KeyMovementBehavior keyBehavior;
        [SerializeField] GameObject keyParticles;
        [SerializeField] MeshRenderer keyMeshRenderer;
        [SerializeField] private GameObject starKey2D;
        [SerializeField] private SpriteRenderer[] starIcons;
        [Space]
        [SerializeField] float offsetY = 0.1f;

        private bool isKeyCollected;
        private BlockColor keyColor;
        private MaterialPropertyBlock propertyBlock;
        public Transform KeyTransform => keyMeshRenderer.transform;
        
        
        public override void OnCreated(LevelBlockBehavior blockBehavior)
        {
            if (!ValidateDataOrDisable())
                return;

            Bounds bounds = blockBehavior.Figure.GetVerticalCenterBounds();
            transform.position = blockBehavior.transform.position + bounds.center + new Vector3(0, offsetY * orderID, 0);
            this.transform.SetParent(blockBehavior.ModelParentTransform, true);
            
            keyColor = Data.keyColor;
            Color color = LevelController.Instance.GetBlockColorData(keyColor).Color;
            propertyBlock = new MaterialPropertyBlock();
            propertyBlock.SetColor(ShaderId.COLOR_SHADER_ID, color);
            keyMeshRenderer.SetPropertyBlock(propertyBlock);
            keyBehavior?.gameObject.SetActive(false);
            starKey2D?.SetActive(true);
            keyParticles?.SetActive(false);
            SetStarColor(keyColor);
        }

        public override void OnCreatedAllBlock()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return;
            var colorElement = ColorManager.GetColorElement(keyColor);
            keyBehavior.SetSimulateTarget(colorElement as IKeyMovementAction);
#endif
        }

        public override void OnBlockFullAfterAnimationFilled()
        {
            if (isKeyCollected) return;
            isKeyCollected = true;

            var colorElement = ColorManager.GetColorElement(keyColor);
            if (colorElement != null)
            {
                keyBehavior?.gameObject.SetActive(true);
                starKey2D?.SetActive(false);
                keyBehavior.StartMovement(colorElement as IKeyMovementAction);
                keyParticles?.SetActive(true);
            }

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

        public override void OnDisabled(LevelBlockBehavior blockBehavior, DisableSource disableSource)
        {
            if (!isKeyCollected)
            {
                var colorElement = ColorManager.GetColorElement(keyColor);
                if (colorElement is IKeyMovementAction kmm)
                {
                    kmm.OnKeyLinked();
                    kmm.OnKeyFinished();
                }
            }
        }
    }
}