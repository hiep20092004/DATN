using System;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    public static class BlockUtilities
    {
        public static float GetMinFillBound(LocalAxis localAxis, Bounds bounds)
        {
            switch (localAxis)
            {
                case LocalAxis.X:
                    return bounds.min.x;
                case LocalAxis.Y:
                    return bounds.min.y;
                case LocalAxis.Z:
                    return bounds.min.z;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        public static float GetMaxFillBound(LocalAxis localAxis, Bounds bounds)
        {
            switch (localAxis)
            {
                case LocalAxis.X:
                    return bounds.max.x;
                case LocalAxis.Y:
                    return bounds.max.y;
                case LocalAxis.Z:
                    return bounds.max.z;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}