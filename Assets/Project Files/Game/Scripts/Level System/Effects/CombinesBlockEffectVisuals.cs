using WaterFlow.Enums;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    public class CombinesBlockEffectVisuals : MonoBehaviour
    {
        private static readonly AudioId[] TieBlockBreakAudio =
        {
            AudioId.Obstacle_TieBlock_01,
            AudioId.Obstacle_TieBlock_02,
            AudioId.Obstacle_TieBlock_03,
        };

        [SerializeField] ParticleSystem linkObstacleFx;
        [SerializeField] ParticleSystem linkAFx;
        [SerializeField] ParticleSystem linkBFx;
        [SerializeField] MeshRenderer blockAMeshRenderer;
        [SerializeField] MeshRenderer blockBMeshRenderer;

        private CombinesEffectBehavior.ConnectedBlocks connectedBlocks;

        public void Init(CombinesEffectBehavior.ConnectedBlocks connectedBlocks)
        {
            this.connectedBlocks = connectedBlocks;
            
            var materialA = connectedBlocks.BlockA.OriginColorConfig.Material;
            var materialB = connectedBlocks.BlockB.OriginColorConfig.Material;
            
            if (blockAMeshRenderer) blockAMeshRenderer.material = materialA;
            if (blockBMeshRenderer) blockBMeshRenderer.material = materialB;
            
            if (linkAFx)
            {
                var ra = linkAFx.GetComponent<ParticleSystemRenderer>();
                if (ra) ra.sharedMaterial = materialA;
            }

            if (linkBFx)
            {
                var rb = linkBFx.GetComponent<ParticleSystemRenderer>();
                if (rb) rb.sharedMaterial = materialB;
            }

            if (linkObstacleFx) linkObstacleFx.gameObject.SetActive(false);
        }

        public void Hide(bool showDestroyFx)
        {
            if (showDestroyFx && linkObstacleFx)
            {
                PlayBreakAudio();
                
                var fxTransform = linkObstacleFx.transform;
                var localPosition = fxTransform.localPosition;
                localPosition.y = 0.2f;
                fxTransform.localPosition = localPosition;
                fxTransform.SetParent(null, true);
                fxTransform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                linkObstacleFx.gameObject.SetActive(true);
                Destroy(linkObstacleFx.gameObject, 1f);
            }
            Destroy(this.gameObject);
        }

        private void PlayBreakAudio()
        {
            AudioId pickedAudio = TieBlockBreakAudio[Random.Range(0, TieBlockBreakAudio.Length)];
            Services.AudioService.PlaySound(pickedAudio);
        }

        public bool InvolvesBlock(LevelBlockBehavior block)
        {
            if (!block || connectedBlocks == null)
                return false;

            return connectedBlocks.BlockA == block || connectedBlocks.BlockB == block;
        }
    }
}
