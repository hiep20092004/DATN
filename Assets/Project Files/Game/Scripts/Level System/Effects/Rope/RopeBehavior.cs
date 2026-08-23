using System.Collections;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    public class RopeBehavior : MonoBehaviour
    {
        public  readonly int TEXTURE_ID = Shader.PropertyToID("_BaseMap");
        
        [OnValueChanged("OnRopeTypeChanged")]
        [SerializeField] RopeType ropeType;
        [SerializeField] ObstacleColorCustomConfig ropeColorData;
        [SerializeField] private MeshRenderer leftMeshRenderer;
        [SerializeField] private MeshRenderer rightMeshRenderer;
        
        [Space]
        [SerializeField] VisualData[] visualDatas;
        [SerializeField] float cutVanishDuration = 0.2f;

        private RopeEffectBehavior ropeEffectBehavior;

        private VisualData visualData;
        private BlockColorData colorData;
        private MeshFilter meshFilterLeft;
        private MeshFilter meshFilterRight;
        private MaterialPropertyBlock propertyBlock;
        private Coroutine cutVanishRoutine;
        private bool hasCutTriggered;
        
        private bool isLinked;
        
        public bool IsLinked => isLinked;
        public RopeType RopeType => ropeType;
        public BlockColor RopeColor => colorData.Type;
        public BlockColorData ColorData => colorData;
        public void Init(RopeEffectBehavior ropeEffectBehavior, RopeTransform transformData, BlockColor color)
        {
            this.ropeEffectBehavior = ropeEffectBehavior;
            isLinked = false;
            hasCutTriggered = false;

            meshFilterLeft = leftMeshRenderer.GetComponent<MeshFilter>();
            meshFilterRight = rightMeshRenderer.GetComponent<MeshFilter>();
            
            visualData = GetVisualData(transformData.RopeType);
            colorData = LevelController.Instance.GetBlockColorData(color);

            transform.localPosition = transformData.Position;
            transform.localEulerAngles = transformData.Rotation;
            transform.localScale = transformData.Scale;

            meshFilterLeft.mesh = visualData.MeshLeft;
            meshFilterRight.mesh = visualData.MeshRight;
            
            propertyBlock = new MaterialPropertyBlock();

            if (ropeColorData != null &&
                ropeColorData.TryGetOverride(colorData.Type, out ColorOverrideConfig ropeOverride) &&
                ropeOverride.Texture != null)
                propertyBlock.SetTexture(TEXTURE_ID, ropeOverride.Texture);
            
           // propertyBlock.SetColor(ShaderId.COLOR_SHADER_ID, colorData.Color);
            propertyBlock.SetFloat(ShaderId.HEIGHT_CUTOFF_SHADER_ID, 0f);
            
            leftMeshRenderer.SetPropertyBlock(propertyBlock);
            rightMeshRenderer.SetPropertyBlock(propertyBlock);
        }

        public void OnRopeLinked()
        {
            if (isLinked) return;
            isLinked = true;
        }

        public void OnRopeCut()
        {
            if (!isLinked || hasCutTriggered) return;
            hasCutTriggered = true;

            if (cutVanishRoutine != null)
            {
                StopCoroutine(cutVanishRoutine);
            }

            cutVanishRoutine = StartCoroutine(PlayCutoffAndDisableRoutine());
        }

        private IEnumerator PlayCutoffAndDisableRoutine()
        {
            float duration = Mathf.Max(0.01f, cutVanishDuration);
            float elapsed = 0f;
            
            ropeEffectBehavior.OnRopeCut(this);
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float cutoff = Mathf.Clamp01(elapsed / duration);

                propertyBlock.SetFloat(ShaderId.HEIGHT_CUTOFF_SHADER_ID, cutoff);
                leftMeshRenderer.SetPropertyBlock(propertyBlock);
                rightMeshRenderer.SetPropertyBlock(propertyBlock);

                yield return null;
            }

            propertyBlock.SetFloat(ShaderId.HEIGHT_CUTOFF_SHADER_ID, 1f);
            leftMeshRenderer.SetPropertyBlock(propertyBlock);
            rightMeshRenderer.SetPropertyBlock(propertyBlock);

            leftMeshRenderer.enabled = false;
            rightMeshRenderer.enabled = false;

            ropeEffectBehavior.OnRopeCutAfterAnim(this);
            cutVanishRoutine = null;
        }

        private void OnDestroy()
        {
            if (cutVanishRoutine != null)
            {
                StopCoroutine(cutVanishRoutine);
                cutVanishRoutine = null;
            }
        }

        public void Editor_OnRopeTypeChanged()
        {
            VisualData visualData = GetVisualData(ropeType);
            if (visualData != null)
            {
                leftMeshRenderer.GetComponent<MeshFilter>().mesh = visualData.MeshLeft;
                rightMeshRenderer.GetComponent<MeshFilter>().mesh = visualData.MeshRight;
            }
        }

        private VisualData GetVisualData(RopeType ropeType)
        {
            for (int i = 0; i < visualDatas.Length; i++)
            {
                if (visualDatas[i].RopeType == ropeType)
                {
                    return visualDatas[i];
                }
            }

            Debug.LogError($"Rope visuals data not found for element type: {ropeType} in {gameObject.name}.", gameObject);

            return visualDatas[0];
        }

        [System.Serializable]
        public class VisualData
        {
            [SerializeField] RopeType ropeType;

            [SerializeField] Mesh meshLeft;
            [SerializeField] Mesh meshRight;

            public RopeType RopeType => ropeType;
            public Mesh MeshLeft => meshLeft;
            public Mesh MeshRight => meshRight;
        }
    }
}