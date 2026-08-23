using System;

namespace WaterFlow.Core
{
    public class PropertyGrouperAttribute : BaseAttribute
    {
        public PropertyGrouperAttribute(Type targetAttributeType) : base(targetAttributeType)
        {
        }
    }
}