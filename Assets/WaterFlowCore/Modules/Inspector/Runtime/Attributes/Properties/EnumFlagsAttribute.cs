using System;

namespace WaterFlow.Core
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class EnumFlagsAttribute : Attribute
    {
        public EnumFlagsAttribute()
        {

        }
    }
}