using System;
using UnityEngine;

namespace WaterFlow.Game
{
    [Serializable]
    public class ElementTypeEditorData
    {
        [SerializeField] private ElementType type;
        [SerializeField] private Color color;
        [SerializeField] private Texture2D texture;

        public ElementType Type => type;
        public Color Color => color;
        public Texture2D Texture => texture;
    }
}