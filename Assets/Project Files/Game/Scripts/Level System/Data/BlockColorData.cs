using System;
using UnityEngine;

namespace WaterFlow.Game
{
    [Serializable]
    public class BlockColorData
    {
        [SerializeField] BlockColor type;
        [SerializeField] Material material;
        [SerializeField] Material pipeMaterial;
        [SerializeField] Material glassMaterial;
        [SerializeField] Material waterMaterial;
        [SerializeField] Material waterInPipe;
        [SerializeField] Material flowMaterial;
        [SerializeField] Color color;
        
        public BlockColor Type => type;
        public Material Material => material;
        public Material PipeMaterial => pipeMaterial;
        public Material GlassMaterial => glassMaterial;
        public Material WaterMaterial => waterMaterial;
        public Material FlowMaterial => flowMaterial;
        public Material WaterInPipeMaterial => waterInPipe;
        public Color Color => color;
    }
}