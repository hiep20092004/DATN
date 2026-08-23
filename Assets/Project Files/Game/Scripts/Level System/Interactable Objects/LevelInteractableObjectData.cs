using System;
using UnityEngine;

namespace WaterFlow.Game
{
    [Serializable]
    public class LevelInteractableObjectData
    {
        [SerializeField] InteractableObjectType type;
        [SerializeField] InteractableObjectBehavior behavior;
        
        public InteractableObjectType Type => type;
        public InteractableObjectBehavior Behavior => behavior;

    }
}