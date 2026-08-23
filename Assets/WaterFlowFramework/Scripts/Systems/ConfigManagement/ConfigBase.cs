using System;
using UnityEngine;

namespace WaterFlow.Framework.Systems.ConfigManagement
{
    public interface IConfig
    {
    }

    [Serializable]
    public class ConfigSo : ScriptableObject, IConfig
    {
    }
}