using System.Collections.Generic;
using UnityEngine;

namespace WaterFlow.Framework.Systems.ObjectPooling
{
    [CreateAssetMenu(fileName = "PreloadPoolingObjects", menuName = "WaterFlow Services/Pooling/Preload Pooling Service")]
    public class PreloadPoolingObjects : ScriptableObject
    {
        public List<string> objectsToPreload = new List<string>();
    }
}