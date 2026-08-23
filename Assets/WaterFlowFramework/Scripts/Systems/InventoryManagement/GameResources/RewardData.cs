using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Sirenix.Serialization;
using WaterFlow.Enums;
using WaterFlow.Framework.Systems.TimeManagement;

namespace WaterFlow.Framework.Systems.InventoryManagement.GameResources
{
    [Serializable]
    public class RewardData
    {
        public List<ResourceData> resourceUnits = new();

        public void AddReward(ResourceData resourceUnit)
        {
            if (resourceUnits == null) resourceUnits = new();
            int index = resourceUnits.FindIndex(e => e.Key == resourceUnit.Key);
            if (index < 0) resourceUnits.Add(resourceUnit);
            else resourceUnits[index].Add(resourceUnit);
        }

        public void MultiplyReward(int multiplier)
        {
            if (resourceUnits == null) return;
            foreach (var resourceUnit in resourceUnits)
            {
                resourceUnit.Multiply(multiplier);
            }
        }
        
        public bool HasAnyResources => resourceUnits is { Count: > 0 };

        public RewardData Clone()
        {
            var dst = new RewardData { resourceUnits = new List<ResourceData>() };
            if (resourceUnits != null)
            {
                foreach (var rd in resourceUnits)
                {
                    if (rd != null)
                        dst.AddReward(rd.Clone());
                }
            }

            return dst.HasAnyResources ? dst : null;
        }
    }
    
}