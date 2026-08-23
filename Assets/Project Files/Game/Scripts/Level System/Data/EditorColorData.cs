using System;
using UnityEngine;

namespace WaterFlow.Game
{
    [Serializable]
    public class EditorColorData
    {
        [SerializeField] BlockColor type;
        [SerializeField] Color color;

        public BlockColor Type => type;
        public Color PaletteColor => color;
    }
}