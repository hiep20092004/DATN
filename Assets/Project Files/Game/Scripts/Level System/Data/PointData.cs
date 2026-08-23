using System;
using UnityEngine;

namespace WaterFlow.Game
{
    [Serializable]
    public class PointData
    {
        [SerializeField] bool isFilled;
        [SerializeField] bool useInHorizontalCenteredBounds;
        [SerializeField] bool useInVerticalCenteredBounds;
        
        public bool IsFilled => isFilled;
        public bool UseInHorizontalCenteredBounds => useInHorizontalCenteredBounds;
        public bool UseInVerticalCenteredBounds => useInVerticalCenteredBounds;

        public PointData(PointData pointData)
        {
            this.isFilled = pointData.isFilled;
            this.useInHorizontalCenteredBounds = pointData.useInHorizontalCenteredBounds;
            this.useInVerticalCenteredBounds = pointData.useInVerticalCenteredBounds;
        }

        public PointData(bool isFilled, bool useInHorizontalCenteredBounds, bool useInVerticalCenteredBounds)
        {
            this.isFilled = isFilled;
            this.useInHorizontalCenteredBounds = useInHorizontalCenteredBounds;
            this.useInVerticalCenteredBounds = useInVerticalCenteredBounds;
        }
    }
}