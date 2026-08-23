using System;

namespace WaterFlow.Core
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class HideScriptFieldAttribute : Attribute { }
}