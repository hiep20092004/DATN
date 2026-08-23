using System.Collections.Generic;
using DG.Tweening;
using WaterFlow.Framework.Systems.LoadObject;
using UnityEngine;

namespace WaterFlow.Framework.Systems.ObjectPooling
{
    [CreateAssetMenu(fileName = "DefaultPoolingService", menuName = "WaterFlow Services/Pooling/Pooling Service")]
    public class DefaultPoolingService : PoolingService
    {
        [SerializeField] private Service<LoadObjectService> loadObjectService = new();
        [SerializeField] private Service<LoadObjectServiceAsync> loadObjectServiceAsync = new();

        public override T Create<T>(string objectName, Vector3 position, Transform parent = null, params object[] args)
        {
            var res = Create<T>(objectName, parent, args);
            res.transform.position = position;
            return res;
        }


        public override T Create<T>(string objectName, Transform parent = null, params object[] args)
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
                objPref = loadObjectService.Instance.LoadObject<GameObject>(objectName);
                ObjPrefs.Add(objectName, objPref);
            }

            var gameObj = Object.Instantiate(objPref, parent);
            gameObj.name = objectName;
            var res2 = gameObj.GetComponent<T>();
            res2.Setup();
            res2.OnCreateObj(args);
            return res2;
        }

        public override T CreateObject<T>(Component prefab, Transform parent, params object[] args)
        {
            var objectName = prefab.name;
            if (Pool.TryGetValue(objectName, out var queue))
                if (queue.TryDequeue(out var obj) && !obj.transform.gameObject.activeSelf)
                {
                    obj.transform.gameObject.SetActive(true);
                    obj.transform.SetParent(parent);
                    return (T)obj;
                }

            if (!ObjPrefs.TryGetValue(objectName, out var objPref))
            {
                objPref = prefab.gameObject;
                ObjPrefs.Add(objectName, objPref);
            }

            var gameObj = Object.Instantiate(objPref, parent);
            gameObj.name = objectName;
            var res2 = gameObj.GetComponent<T>();
            res2.Setup();
            res2.OnCreateObj(args);
            return res2;
        }

        public override void ReturnObj(IPoolingObject obj, bool keep = true)
        {
            if (!keep)
            {
                obj.OnReturnObj();
                Object.Destroy(obj.transform.gameObject);
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

    }
}