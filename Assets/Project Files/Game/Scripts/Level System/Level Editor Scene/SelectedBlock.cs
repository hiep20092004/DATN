using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    public class SelectedBlock : MonoBehaviour
    {
        private static SelectedBlock instance;
        public static SelectedBlock Instance { get => instance; }

        [SerializeReference]
        public LevelElementData data; // this should be public for [UnpackNested] to work
        public LevelElementData Data { get => data; set => data = value; }

        public SelectedBlock()
        {
            instance = this;
        }
        
    }
}