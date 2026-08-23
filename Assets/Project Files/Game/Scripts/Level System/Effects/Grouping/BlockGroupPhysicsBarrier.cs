using System.Collections.Generic;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    [DisallowMultipleComponent]
    public sealed class BlockGroupPhysicsBarrier : MonoBehaviour
    {
        // A single box sized to the whole footprint poked into neighbouring cells and overlapped the member
        // colliders, fighting PhysX during drag. Instead we drop one flush 1x1 cell collider on each EMPTY
        // PERIMETER cell of the bound; occupied cells (member blocks, obstacles not hidden by the box) keep
        // their own collider and are passed in via occupiedCells.
        private readonly List<GameObject> cellInstances = new();

        private GameObject cellColliderPrefab;

        public void Configure(GameObject cellColliderPrefab)
        {
            this.cellColliderPrefab = cellColliderPrefab;
        }

        /// <summary>
        /// Rebuilds the per-cell collision footprint for <paramref name="bounds"/>. A collider is spawned
        /// only on perimeter cells of the bound that are absent from <paramref name="occupiedCells"/>
        /// (member blocks / obstacles). <paramref name="cellWorldY"/> is the member blocks' world height so
        /// each cell collider lines up with where a block sits in that cell.
        /// </summary>
        public void ApplyBounds(BlockGroupOccupiedBounds bounds, HashSet<Vector2Int> occupiedCells, float cellWorldY)
        {
            if (!bounds.IsValid || !cellColliderPrefab)
            {
                DeactivateInstancesFrom(0);
                return;
            }

            Vector2Int min = bounds.MinCell;
            Vector2Int max = bounds.MaxCell;
            int used = 0;

            for (int x = min.x; x <= max.x; x++)
            {
                for (int y = min.y; y <= max.y; y++)
                {
                    bool onPerimeter = x == min.x || x == max.x || y == min.y || y == max.y;
                    if (!onPerimeter)
                        continue;

                    var cell = new Vector2Int(x, y);
                    if (occupiedCells != null && occupiedCells.Contains(cell))
                        continue;

                    GameObject instance = GetOrCreateInstance(used++);
                    Transform instanceTransform = instance.transform;
                    instanceTransform.SetPositionAndRotation(
                        new Vector3(cell.x, cellWorldY, cell.y),
                        Quaternion.identity);
                }
            }

            DeactivateInstancesFrom(used);
            Physics.SyncTransforms();
        }

        public void Release()
        {
            DeactivateInstancesFrom(0);
        }

        private void OnDestroy()
        {
            Release();
        }

        private GameObject GetOrCreateInstance(int index)
        {
            if (index < cellInstances.Count && cellInstances[index])
            {
                GameObject existing = cellInstances[index];
                if (!existing.activeSelf)
                    existing.SetActive(true);
                return existing;
            }

            GameObject instance = Instantiate(cellColliderPrefab, transform, true);
            instance.SetLayersRecursively(gameObject.layer);

            if (index < cellInstances.Count)
                cellInstances[index] = instance;
            else
                cellInstances.Add(instance);

            return instance;
        }

        private void DeactivateInstancesFrom(int startIndex)
        {
            for (int i = startIndex; i < cellInstances.Count; i++)
            {
                GameObject instance = cellInstances[i];
                if (instance && instance.activeSelf)
                    instance.SetActive(false);
            }
        }
    }
}
