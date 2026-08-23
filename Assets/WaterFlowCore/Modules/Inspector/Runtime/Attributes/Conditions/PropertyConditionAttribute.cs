using System;

namespace WaterFlow.Core
{
    public class PropertyConditionAttribute : BaseAttribute
    {
        public PropertyConditionAttribute(Type targetAttributeType) : base(targetAttributeType)
        {
        }
    }
}