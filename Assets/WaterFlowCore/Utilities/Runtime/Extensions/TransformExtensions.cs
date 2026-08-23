using System.Collections.Generic;
using UnityEngine;

namespace WaterFlow.Core
{
    public static class TransformExtensions
    {
        /// <summary>
        /// Check if the transform is within a certain distance and optionally within a certain angle (FOV) from the target transform.
        /// </summary>
        /// <param name="source">The transform to check.</param>
        /// <param name="target">The target transform to compare the distance and optional angle with.</param>
        /// <param name="maxDistance">The maximum distance allowed between the two transforms.</param>
        /// <param name="maxAngle">The maximum allowed angle between the transform's forward vector and the direction to the target (default is 360).</param>
        /// <returns>True if the transform is within range and angle (if provided) of the target, false otherwise.</returns>
        public static bool InRangeOf(this Transform source, Transform target, float maxDistance, float maxAngle = 360f) {
            Vector3 directionToTarget = (target.position - source.position).With(y: 0);
            return directionToTarget.magnitude <= maxDistance && Vector3.Angle(source.forward, directionToTarget) <= maxAngle / 2;
        }
        
        /// <summary>
        /// Retrieves all the children of a given Transform.
        /// </summary>
        /// <remarks>
        /// This method can be used with LINQ to perform operations on all child Transforms. For example,
        /// you could use it to find all children with a specific tag, to disable all children, etc.
        /// Transform implements IEnumerable and the GetEnumerator method which returns an IEnumerator of all its children.
        /// </remarks>
        /// <param name="parent">The Transform to retrieve children from.</param>
        /// <returns>An IEnumerable&lt;Transform&gt; containing all the child Transforms of the parent.</returns>    
        public static IEnumerable<Transform> Children(this Transform parent) {
            foreach (Transform child in parent) {
                yield return child;
            }
        }

        /// <summary>
        /// Resets transform's position, scale and rotation
        /// </summary>
        /// <param name="transform">Transform to use</param>
        public static void Reset(this Transform transform) {
            transform.position = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }
        
        /// <summary>
        /// Destroys all child game objects of the given transform.
        /// </summary>
        /// <param name="parent">The Transform whose child game objects are to be destroyed.</param>
        public static void DestroyChildren(this Transform parent) {
            parent.ForEveryChild(child => Object.Destroy(child.gameObject));
        }

        /// <summary>
        /// Immediately destroys all child game objects of the given transform.
        /// </summary>
        /// <param name="parent">The Transform whose child game objects are to be immediately destroyed.</param>
        public static void DestroyChildrenImmediate(this Transform parent) {
            parent.ForEveryChild(child => Object.DestroyImmediate(child.gameObject));
        }

        /// <summary>
        /// Enables all child game objects of the given transform.
        /// </summary>
        /// <param name="parent">The Transform whose child game objects are to be enabled.</param>
        public static void EnableChildren(this Transform parent) {
            parent.ForEveryChild(child => child.gameObject.SetActive(true));
        }

        /// <summary>
        /// Disables all child game objects of the given transform.
        /// </summary>
        /// <param name="parent">The Transform whose child game objects are to be disabled.</param>
        public static void DisableChildren(this Transform parent) {
            parent.ForEveryChild(child => child.gameObject.SetActive(false));
        }

        /// <summary>
        /// Executes a specified action for each child of a given transform.
        /// </summary>
        /// <param name="parent">The parent transform.</param>
        /// <param name="action">The action to be performed on each child.</param>
        /// <remarks>
        /// This method iterates over all child transforms in reverse order and executes a given action on them.
        /// The action is a delegate that takes a Transform as parameter.
        /// </remarks>
        public static void ForEveryChild(this Transform parent, System.Action<Transform> action) {
            for (var i = parent.childCount - 1; i >= 0; i--) {
                action(parent.GetChild(i));
            }
        }

        /// <summary>
        /// Flip object x scale
        /// </summary>
        public static void FlipX(this Transform transform, bool flip)
        {
            transform.localScale =
                new Vector3(flip ? -Mathf.Abs(transform.localScale.x) : Mathf.Abs(transform.localScale.x),
                    transform.localScale.y, transform.localScale.z);
        }

        /// <summary>
        /// Flip object y scale
        /// </summary>
        public static void FlipY(this Transform transform, bool flip)
        {
            transform.localScale = new Vector3(transform.localScale.x,
                flip ? -Mathf.Abs(transform.localScale.y) : Mathf.Abs(transform.localScale.y), transform.localScale.z);
        }
        
        
        /// <summary>
        /// Try to get child
        /// </summary>
        /// <returns>child or null if child is not exists</returns>
        public static Transform TryGetChild(this Transform transform, int index)
        {
            if (transform.childCount < index)
                return transform.GetChild(index);

            return null;
        }

        /// <summary>
        /// Reset transforms local position, rotation and scale
        /// </summary>
        public static Transform ResetLocal(this Transform transform)
        {
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            transform.localScale = Vector3.one;

            return transform;
        }

        /// <summary>
        /// Reset transforms position, rotation and scale
        /// </summary>
        public static Transform ResetGlobal(this Transform transform)
        {
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            transform.localScale = Vector3.one;

            return transform;
        }
        
        public static LocalAxis GetLocalAxisClosestToDirection(
            this Transform t,
            Vector3 worldDirection
        )
        {
            worldDirection.Normalize();

            float dotX = Mathf.Abs(Vector3.Dot(t.right, worldDirection));
            float dotY = Mathf.Abs(Vector3.Dot(t.up, worldDirection));
            float dotZ = Mathf.Abs(Vector3.Dot(t.forward, worldDirection));

            if (dotX > dotY && dotX > dotZ)
                return LocalAxis.X;

            if (dotY > dotZ)
                return LocalAxis.Y;

            return LocalAxis.Z;
        }
        
        public static Transform SetPositionX(this Transform transform, float x)
        {
            var position = transform.position;
            position.x = x;
            transform.position = position;

            return transform;
        }

        public static Transform SetPositionY(this Transform transform, float y)
        {
            var position = transform.position;
            position.y = y;
            transform.position = position;

            return transform;
        }

        public static Transform SetPositionZ(this Transform transform, float z)
        {
            var position = transform.position;
            position.z = z;
            transform.position = position;

            return transform;
        }
        
    }
}