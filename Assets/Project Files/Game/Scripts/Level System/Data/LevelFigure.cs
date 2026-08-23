using System;
using System.Collections.Generic;
using WaterFlow.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WaterFlow.Game
{
    [Serializable]
    public class LevelFigure
    {
        [LevelEditorSetting]
        [SerializeField] Vector2Int size = new Vector2Int(3, 3);
        [SerializeField, LevelEditorSetting] PointData[] points;
        [SerializeField, LevelEditorSetting] int activePoints = -1;
        [SerializeField, LevelEditorSetting] Vector2Int pivotPoint = new Vector2Int(0, 0);
        
        [NonSerialized] private bool cacheBuilt;
        [NonSerialized] private int[] rowCounts; 
        [NonSerialized] private int[] columnCounts; 
        [NonSerialized] private int totalPoints;
        [NonSerialized] private int height;
        
        public Vector2Int Size => size;
        public PointData[] Points => points;
        public int ActivePoints => activePoints;
        public Vector2Int PivotPoint => pivotPoint;
        
        public LevelFigure Clone()
        {
            LevelFigure levelFigure = new LevelFigure();
            levelFigure.size = size;
            levelFigure.points = new PointData[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                levelFigure.points[i] = new PointData(points[i]);
            }
            levelFigure.activePoints = activePoints;
            levelFigure.pivotPoint = pivotPoint;

            return levelFigure;
        }

        public PointData GetRegularPoint()
        {
            if (points.Length == 0)
                return null;

            int startIndex = Random.Range(0, points.Length + 1);
            for (int i = 0; i < points.Length; i++)
            {
                int index = (startIndex + i) % points.Length;
                if (points[index].IsFilled)
                    return points[index];
            }

            return null;
        }

        public Bounds GetHorizontalCenterBounds()
        {
            float minX = float.MaxValue;
            float maxX = float.MinValue;

            float minY = float.MaxValue;
            float maxY = float.MinValue;
            
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i].UseInHorizontalCenteredBounds)
                {
                    float x = i % size.x;
                    float y = i / size.x;

                    if (x < minX)
                        minX = x;
                    if (x > maxX)
                        maxX = x;
                    if (y < minY)
                        minY = y;
                    if(y > maxY)
                        maxY = y;
                }
            }

            if (minX == float.MaxValue || maxX == float.MinValue || minY == float.MaxValue || maxY == float.MinValue)
                return new Bounds(Vector3.zero, Vector3.zero);

            return new Bounds(new Vector3((minX + maxX) / 2, 0, (minY + maxY) / 2), new Vector3(maxX - minX + 1, 0, maxY - minY + 1));
        }

        public Bounds GetVerticalCenterBounds()
        {
            float minX = float.MaxValue;
            float maxX = float.MinValue;

            float minY = float.MaxValue;
            float maxY = float.MinValue;

            for (int i = 0; i < points.Length; i++)
            {
                if (points[i].UseInVerticalCenteredBounds)
                {
                    float x = i % size.x;
                    float y = i / size.x;

                    if (x < minX)
                        minX = x;
                    if (x > maxX)
                        maxX = x;
                    if (y < minY)
                        minY = y;
                    if (y > maxY)
                        maxY = y;
                }
            }

            if (minX == float.MaxValue || maxX == float.MinValue || minY == float.MaxValue || maxY == float.MinValue)
                return new Bounds(Vector3.zero, Vector3.zero);

            return new Bounds(new Vector3((minX + maxX) / 2, 0, (minY + maxY) / 2), new Vector3(maxX - minX + 1, 0, maxY - minY + 1));
        }

        /// <summary>
        /// Figure cell indices match block local XZ (see <see cref="LevelBlockBehavior"/> gizmos).
        /// Returns the axis-aligned center of filled cells so preview roots can be offset and the
        /// scaled shape is centered on the preview container's XZ origin.
        /// </summary>
        public bool TryGetFigureFilledBoundsCenterXZ(out Vector2 centerXZ)
        {
            centerXZ = default;
            if (points == null || points.Length == 0)
                return false;

            int minX = int.MaxValue, maxX = int.MinValue;
            int minZ = int.MaxValue, maxZ = int.MinValue;
            bool any = false;
            int index = 0;
            for (int iz = 0; iz < size.y; iz++)
            {
                for (int ix = 0; ix < size.x; ix++)
                {
                    if (points[index].IsFilled)
                    {
                        any = true;
                        if (ix < minX) minX = ix;
                        if (ix > maxX) maxX = ix;
                        if (iz < minZ) minZ = iz;
                        if (iz > maxZ) maxZ = iz;
                    }

                    index++;
                }
            }

            if (!any)
                return false;

            centerXZ = new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
            return true;
        }
        
        public float GetWaterFillPercent(int filledPoints)
        {
            BuildCacheIfNeeded();

            if (totalPoints == 0)
                return 0f;

            int remaining = Mathf.Clamp(filledPoints, 0, totalPoints);

            int accumulated = 0;
            float waterHeight = 0f;

            for (int y = 0; y < height; y++)
            {
                int row = rowCounts[y];
                int next = accumulated + row;

                if (row == 0)
                {
                    if (y == height - 1)
                        waterHeight = height;
                    continue;
                }

                if (remaining < next)
                {
                    float fraction = (float)(remaining - accumulated) / row;
                    waterHeight = y + fraction;
                    break;
                }

                if (remaining == next)
                {
                    waterHeight = y + 1f;
                    break;
                }

                accumulated = next;

                if (y == height - 1)
                    waterHeight = height;
            }

            return Mathf.Clamp01(waterHeight / height);
        }
        
        public int GetDepthAtColumn(int columnIndex)
        {
            BuildCacheIfNeeded();
            if (columnIndex < 0 || columnIndex >= size.x) return 0;
            return columnCounts[columnIndex];
        }

        public bool IsCellOccupiedAt(Vector2Int originPosition, Vector2Int targetCell)
        {
            if (points == null || points.Length == 0)
                return false;

            int relativeX = targetCell.x - originPosition.x;
            int relativeY = targetCell.y - originPosition.y;

            if (relativeX < 0 || relativeY < 0 || relativeX >= size.x || relativeY >= size.y)
                return false;

            int index = relativeX + relativeY * size.x;
            if (index < 0 || index >= points.Length)
                return false;

            return points[index].IsFilled;
        }

        public Vector2Int[] GetOffsetsRelativeToPivot()
        {
            if (points == null || points.Length == 0)
                return Array.Empty<Vector2Int>();

            List<Vector2Int> list = new List<Vector2Int>();
            int index = 0;
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    if (points[index].IsFilled)
                    {
                        if (pivotPoint.x != x || pivotPoint.y != y)
                            list.Add(new Vector2Int(x - pivotPoint.x, y - pivotPoint.y));
                    }

                    index++;
                }
            }

            return list.ToArray();
        }

        public int GetBlockPieceCount()
        {
            if (points == null || points.Length == 0)
                return 1;

            int nonPivotFilled = 0;
            int index = 0;
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    if (points[index].IsFilled && (pivotPoint.x != x || pivotPoint.y != y))
                        nonPivotFilled++;
                    index++;
                }
            }

            return nonPivotFilled + 1;
        }
        
        
        private void BuildCacheIfNeeded()
        {
            if (cacheBuilt)
                return;

            height = size.y;
            totalPoints = activePoints;
            rowCounts = new int[height];
            columnCounts = new int[size.x];
            
            for (int y = 0; y < height; y++)
            {
                int count = 0;
                for (int x = 0; x < size.x; x++)
                {
                    int index = x + y * size.x;
                    if (points[index].IsFilled)
                        count++;
                }
                rowCounts[y] = count;
            }
            
            for (int x = 0; x < size.x; x++)
            {
                int count = 0;
                for (int y = 0; y < height; y++)
                {
                    int index = x + y * size.x;
                    if (points[index].IsFilled)
                        count++;
                }
                columnCounts[x] = count;
            }

            cacheBuilt = true;
        }
    }
}