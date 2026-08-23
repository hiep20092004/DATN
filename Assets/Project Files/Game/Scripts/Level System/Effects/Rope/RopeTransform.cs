using UnityEngine;

namespace WaterFlow.Game
{
    [System.Serializable]
    public class RopeTransform
    {
        [SerializeField] Vector3 position;
        [SerializeField] Vector3 rotation;
        [SerializeField] Vector3 scale = new Vector3(1, 1, 1);
        [Space]
        [SerializeField] RopeType ropeType;
            
        public Vector3 Position => position;
        public Vector3 Rotation => rotation;
        public Vector3 Scale => scale;
        public RopeType RopeType => ropeType;
    }
}