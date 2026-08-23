using System;
using System.Collections;
using System.Collections.Generic;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.ObjectPooling;
using UnityEngine;

public class PoolingServiceBase : ServiceSo, IServiceInitialize
{
    protected static readonly Dictionary<string, Queue<IPoolingObject>> Pool = new(StringComparer.Ordinal);
    protected static readonly Dictionary<string, GameObject> ObjPrefs = new(StringComparer.Ordinal);
    protected static GameObject pool;

    public virtual void Initialize()
    {
        if (pool == null)
        {
            pool = new GameObject("Pool");
            pool.AddComponent<DontDestroyOnLoadObject>();
            Pool.Clear();
            ObjPrefs.Clear();
        }
    }
}