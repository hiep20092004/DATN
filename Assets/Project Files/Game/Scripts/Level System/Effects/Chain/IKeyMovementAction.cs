using UnityEngine;

namespace WaterFlow.Game
{
    public interface IKeyMovementAction
    {
        public bool IsTargetAvailable { get; }
        public Vector3 TargetPosition { get; }
        void OnKeyLinked();
        void OnKeyReached();
        void OnKeyFinished();
    }
}