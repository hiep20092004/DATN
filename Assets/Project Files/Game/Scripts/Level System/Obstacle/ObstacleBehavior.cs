using UnityEngine;

namespace WaterFlow.Game
{
    public class ObstacleBehavior : MonoBehaviour
    {
        private Vector2Int position;
        public Vector2Int Position => position;

        public LevelRepresentation OwnerLevel { get; private set; }

        public void Init(Vector2Int cellPosition, LevelRepresentation ownerLevel)
        {
            position = cellPosition;
            OwnerLevel = ownerLevel;
        }
    }
}
