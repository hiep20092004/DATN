using System;
using DG.Tweening;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    public class BubbleEffect : MonoBehaviour
    {
        private readonly int BUBBLE_FADE = Shader.PropertyToID("_Fade");
        private Material bubbleMaterial;
        private MeshFilter meshFilter;

        public void Initialize(Material waterMaterial, Mesh waterMesh)
        {
            bool isActive = Application.isPlaying && waterMaterial;
            this.gameObject.SetActive(false);
            if(!isActive) return;
            if(!meshFilter) meshFilter = GetComponent<MeshFilter>();
            if(meshFilter) meshFilter.mesh = waterMesh;
            if(!bubbleMaterial) bubbleMaterial = gameObject.GetComponent<Renderer>().material;
            if (!bubbleMaterial) return;
            bubbleMaterial.SetFloat(BUBBLE_FADE, 1f);
            bubbleMaterial.SetFloat(ShaderId.FILL_AMOUNT_SHADER_ID, waterMaterial.GetFloat(ShaderId.FILL_AMOUNT_SHADER_ID));
            bubbleMaterial.SetFloat(ShaderId.FILL_BOUNDS_MIN_SHADER_ID, waterMaterial.GetFloat(ShaderId.FILL_BOUNDS_MIN_SHADER_ID));
            bubbleMaterial.SetFloat(ShaderId.FILL_BOUNDS_MAX_SHADER_ID, waterMaterial.GetFloat(ShaderId.FILL_BOUNDS_MAX_SHADER_ID));
        }

        public void BubbleEffectFill(float percent,float fillingWaterSpeed)
        {
            if (bubbleMaterial == null) return;
            bubbleMaterial.SetFloat(BUBBLE_FADE, 1f);
            bubbleMaterial.DoFloat(ShaderId.FILL_AMOUNT_SHADER_ID, percent, fillingWaterSpeed);
        }

        public void BubbleFadeOff()
        {
            if (bubbleMaterial == null) return;
            bubbleMaterial.DOFloat(0f, BUBBLE_FADE, 0.3f);
        }

        private void OnDisable()
        {
            if (bubbleMaterial != null)
                bubbleMaterial.SetFloat(BUBBLE_FADE, 1f);
        }
    }
}
