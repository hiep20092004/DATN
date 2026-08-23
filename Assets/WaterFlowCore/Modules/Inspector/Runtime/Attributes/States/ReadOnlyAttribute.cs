using System;
using UnityEngine;

namespace WaterFlow.Core
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class ReadOnlyAttribute : PropertyAttribute
    {
        public ReadOnlyAttribute() { }
    }
}