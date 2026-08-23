using UnityEngine;

namespace WaterFlow.Game
{
    public class InteractableObjectBehavior : MonoBehaviour
    {
        protected Vector2Int position;
        public Vector2Int Position => position;

        protected InteractableObjectData data;
        public InteractableObjectData Data => data;

        protected LevelRepresentation OwnerLevel { get; private set; }

        public void Init(InteractableObjectData data, Vector2Int position, LevelRepresentation ownerLevel)
        {
            this.position = position;
            this.data = data;
            OwnerLevel = ownerLevel;

            OnCreated();
        }

        public virtual void OnCreated() { }

        public virtual void OnBlockPicked(LevelBlockBehavior levelBlockBehavior) { }
        public virtual void OnBlockReleased(LevelBlockBehavior levelBlockBehavior, Vector2Int snapTargetPosition) { }

        public virtual void OnBlockDestructed(LevelBlockBehavior levelBlockBehavior) { }
    }
}