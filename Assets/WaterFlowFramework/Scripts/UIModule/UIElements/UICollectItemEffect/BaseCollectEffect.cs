using System;
using Cysharp.Threading.Tasks;
using WaterFlow.Enums;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using UnityEngine;

public abstract class BaseCollectEffect
{
    public abstract UniTask Collect(GameResourceKey key, int quantity, Vector3 startPos, Vector3 endPos, Action<int> onFinishStep = null, Action callback = null);
}
