using System;

namespace WaterFlow.Core
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class ReorderableListAttribute : Attribute
    {
    }
}