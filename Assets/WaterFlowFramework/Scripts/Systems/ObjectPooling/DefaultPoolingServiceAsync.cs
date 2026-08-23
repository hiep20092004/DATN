using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using WaterFlow.Framework.Systems.ObjectPooling;
using WaterFlow.Framework.Systems.LoadObject;
using UnityEngine;

namespace WaterFlow.Framework.Systems.ObjectPooling
{
    [CreateAssetMenu(fileName = "DefaultPoolingServiceAsync", menuName = "WaterFlow Services/Pooling/Pooling Service Async")]
    public class DefaultPoolingServiceAsync : PoolingServiceAsync
    {
        [SerializeField] private Service<LoadObjectServiceAsync> loadObjectService = new();
        [SerializeField] private PreloadPoolingObjects preloadPoolingObjects;
        private readonly HashSet<string> loadingObjects = new();

        public override void Initialize()
        {
            base.Initialize();
            PreloadPoolingObjects().Forget();
        }

        private async UniTaskVoid PreloadPoolingObjects()
        {
            if (preloadPoolingObjects != null)
            {
                foreach (var path in preloadPoolingObjects.objectsToPreload)
                {
                    if (string.IsNullOrEmpty(path)) continue;
                    loadingObjects.Add(path);
                    try
                    {
                        var objPref = await loadObjectService.Instance.LoadAsync<GameObject>(path);
                        ObjPrefs.Add(path, objPref);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        throw;
                    }

                    loadingObjects.Remove(path);
                }
            }
        }

        public override void ReturnObj(IPoolingObject obj, bool keep = true)
        {
            if (!keep)
            {
                obj.OnReturnObj();
                UnityEngine.Object.Destroy(obj.transform.gameObject);
            }
            else
            {
                obj.OnReturnObj();
                obj.transform.DOKill();
                obj.transform.gameObject.SetActive(false);
                obj.transform.SetParent(pool.transform);
                if (Pool.TryGetValue(obj.transform.name, out var queue))
                {
                    queue.Enqueue(obj);
                    return;
                }

                queue = new Queue<IPoolingObject>();
                queue.Enqueue(obj);
                Pool.Add(obj.transform.name, queue);
            }
        }

        public override async UniTask<T> CreateAsync<T>(string objectName, Vector3 position, Transform parent = null, params object[] args)
        {
            var res = await CreateAsync<T>(objectName, parent, args);
            res.transform.position = position;
            return res;
        }
        public override async UniTask<T> CreateAsync<T>(Transform parent, params object[] args)
        {
            var objectName = typeof(T).Name;
            var res = await CreateAsync<T>(objectName, parent, args);
            return res;
        }

        public override async UniTask<T> CreateAsync<T>(string objectName, Transform parent = null, params object[] args)
        {
            if (Pool.TryGetValue(objectName, out var queue))
                if (queue.TryDequeue(out var obj) && !obj.transform.gameObject.activeSelf)
                {
                    obj.transform.gameObject.SetActive(true);
                    obj.transform.SetParent(parent);
                    obj.OnCreateObj(args);
                    return (T)obj;
                }

            if (!ObjPrefs.TryGetValue(objectName, out var objPref))
            {
                if (loadingObjects.Contains(objectName))
                {
                    await UniTask.WaitWhile(() => loadingObjects.Contains(objectName));
                    objPref = ObjPrefs[objectName];
                }
                else
                {
                    loadingObjects.Add(objectName);
                    try
                    {
                        objPref = await loadObjectService.Instance.LoadAsync<GameObject>(objectName);
                        if (objPref == null)
                        {
                            Debug.LogError($"[PoolingServiceAsync] Prefab load returned null. objectName='{objectName}', type='{typeof(T).Name}'. Check Resources path/key.");
                            return default;
                        }

                        ObjPrefs.Add(objectName, objPref);
                    }
                    finally
                    {
                        loadingObjects.Remove(objectName);
                    }
                }
            }

            if (objPref == null)
            {
                Debug.LogError($"[PoolingServiceAsync] Cached prefab is null. objectName='{objectName}', type='{typeof(T).Name}'. This indicates a previous failed load.");
                return default;
            }

            var gameObj = UnityEngine.Object.Instantiate(objPref, parent);
            gameObj.name = objectName;
            var res2 = gameObj.GetComponent<T>();
            res2.Setup();
            res2.OnCreateObj(args);
            return res2;
        }
    }
}