using System;
using UnityEngine;

namespace WaterFlow.Core
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class CreateScriptableObjectAttribute : PropertyAttribute
    {
        public CreateScriptableObjectAttribute() { }
    }
}