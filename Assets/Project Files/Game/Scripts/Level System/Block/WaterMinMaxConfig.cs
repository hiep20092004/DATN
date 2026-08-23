using UnityEngine;

namespace WaterFlow.Game
{
    [CreateAssetMenu(fileName = "WaterMinMaxConfig", menuName = "WaterMinMaxConfig", order = 1)]
    public class WaterMinMaxConfig : ScriptableObject
    {
        [SerializeField] float _maxValue;
        [SerializeField] float _minValue;

        public float MaxValue
        {
            get { return _maxValue; }
        }

        public float MinValue
        {
            get { return _minValue; }
        }

    }
}
