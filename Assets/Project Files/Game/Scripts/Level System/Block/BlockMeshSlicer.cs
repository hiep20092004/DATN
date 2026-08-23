using System.Collections.Generic;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    [StaticUnload]
    public class BlockMeshSlicer: MonoBehaviour
    {
        [Header("References")]
        [SerializeField] MeshFilter meshFilter;
        [SerializeField] BoxCollider boxCollider;
        
        [Space]
        [SerializeField] Vector3 boundsCenter;
        [SerializeField] Vector3 boundsSize;

        private MeshData meshData;
        private Vector3[] modifiedVertices;
        private Mesh mesh;

        private static Dictionary<Mesh, MeshData> cachedVerticles = new Dictionary<Mesh, MeshData>();

        public void Init()
        {
            if (meshFilter == null)
            {
                Debug.LogError("MeshFilter references are required.", this);
                return;
            }

            Mesh sharedMesh = meshFilter.sharedMesh;

            // Clone the mesh to avoid affecting other instances
            mesh = Instantiate(meshFilter.sharedMesh);
            meshFilter.sharedMesh = mesh;

            if (!mesh)
            {
                Debug.LogError("MeshFilter has no mesh assigned.", this);
                return;
            }

            meshData = GetCachedVertices(sharedMesh);

            modifiedVertices = new Vector3[meshData.Vertices.Length];
            meshData.Vertices.CopyTo(modifiedVertices, 0);
        }

        public void ApplyScaling(Vector3 scale)
        {
            Vector3 colliderMin = boundsCenter - boundsSize * 0.5f;
            Vector3 colliderMax = boundsCenter + boundsSize * 0.5f;
            Vector3 colliderCenter = boundsCenter;

            Vector3 furthestVertsLocation = GetFurthestVertsLocation();
            Vector3 closestVertsLocation = GetClosestVertsLocation();

            Vector3 totalModelSize = (furthestVertsLocation - closestVertsLocation);
            Vector3 colliderSizeZ = (colliderMax - colliderMin);
            Vector3 extraSpaceWeNeedToAdd = new Vector3(scale.x * totalModelSize.x, scale.y * totalModelSize.y, scale.z * totalModelSize.z) - totalModelSize;
            Vector3 finalHalfOffset = extraSpaceWeNeedToAdd / 2f;

            Vector3[] originalVertices = meshData.Vertices;
            for (int i = 0; i < originalVertices.Length; i++)
            {
                Vector3 localVertex = originalVertices[i];
                Vector3 scaledVertex = localVertex;

                // Check if the vertex is within the inner scaling area (inside bounds of the collider)
                if (localVertex.x > colliderMin.x && localVertex.x < colliderMax.x &&
                    localVertex.y > colliderMin.y && localVertex.y < colliderMax.y &&
                    localVertex.z > colliderMin.z && localVertex.z < colliderMax.z)
                {
                    // Scale only the inner area (middle slice)
                    float relativeZ = Mathf.InverseLerp(colliderMin.z, colliderMax.z, localVertex.z);
                    float scaledZ = Mathf.Lerp(colliderMin.z, colliderMax.z, relativeZ) * scale.z;
                    scaledVertex.z = scaledZ;
                }
                else
                {
                    // Shift the outer areas without scaling thickness

                    // along x axes
                    if (localVertex.x <= colliderMin.x)
                    {
                        scaledVertex.x -= finalHalfOffset.x;
                    }
                    else if (localVertex.x >= colliderMax.x)
                    {
                        scaledVertex.x += finalHalfOffset.x;
                    }

                    // along y axes
                    if (localVertex.y <= colliderMin.y)
                    {
                        scaledVertex.y -= finalHalfOffset.y;
                    }
                    else if (localVertex.y >= colliderMax.y)
                    {
                        scaledVertex.y += finalHalfOffset.y;
                    }

                    // along z axes
                    if (localVertex.z <= colliderMin.z)
                    {
                        scaledVertex.z -= finalHalfOffset.z;
                    }
                    else if (localVertex.z >= colliderMax.z)
                    {
                        scaledVertex.z += finalHalfOffset.z;
                    }
                }

                modifiedVertices[i] = scaledVertex;
            }

            mesh.vertices = modifiedVertices;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            meshFilter.sharedMesh = mesh;

            if (boxCollider)
            {
                boxCollider.size += extraSpaceWeNeedToAdd;
                boxCollider.center = colliderCenter;
            }
        }

        private Vector3 GetFurthestVertsLocation()
        {
            Vector3 furthestVerts = Vector3.zero;

            Vector3[] originalVertices = meshData.Vertices;
            for (int i = 0; i < originalVertices.Length; i++)
            {
                if (originalVertices[i].x > furthestVerts.x)
                {
                    furthestVerts = furthestVerts.SetX(originalVertices[i].x);
                }

                if (originalVertices[i].y > furthestVerts.y)
                {
                    furthestVerts = furthestVerts.SetY(originalVertices[i].y);
                }

                if (originalVertices[i].z > furthestVerts.z)
                {
                    furthestVerts = furthestVerts.SetZ(originalVertices[i].z);
                }
            }

            return furthestVerts;
        }

        private Vector3 GetClosestVertsLocation()
        {
            Vector3 closestVerts = Vector3.zero;

            Vector3[] originalVertices = meshData.Vertices;
            for (int i = 0; i < originalVertices.Length; i++)
            {
                if (originalVertices[i].x < closestVerts.x)
                {
                    closestVerts = closestVerts.SetX(originalVertices[i].x);
                }

                if (originalVertices[i].y < closestVerts.y)
                {
                    closestVerts = closestVerts.SetY(originalVertices[i].y);
                }

                if (originalVertices[i].z < closestVerts.z)
                {
                    closestVerts = closestVerts.SetZ(originalVertices[i].z);
                }
            }

            return closestVerts;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position + boundsCenter, boundsSize);
        }

        private static MeshData GetCachedVertices(Mesh mesh)
        {
            if (cachedVerticles.TryGetValue(mesh, out MeshData meshData))
            {
                return meshData;
            }

            meshData = new MeshData(mesh.vertices);

            cachedVerticles[mesh] = meshData;

            return meshData;
        }

        private static void UnloadStatic()
        {
            cachedVerticles.Clear();
        }

        private class MeshData
        {
            public Vector3[] Vertices;

            public MeshData(Vector3[] vertices)
            {
                Vertices = vertices;
            }
        }
    }
}