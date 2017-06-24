//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//
//namespace UnityOctree
//{
//    public partial class LooseOctree<T>
//    {
//        public bool IsColliding(Bounds bounds)
//        {
//            return true;
//        }
//        public bool IsColliding(Vector3 minBounds, Vector3 maxBounds)
//        {
//            return true;
//        }
//        public bool IsColliding(Ray checkRay, float maxDistance)
//        {
//            return true;
//        }
//        
//        public bool GetColliding(Vector3 minBounds, Vector3 maxBounds, List<T> result)
//        {
//            return true;
//        }
//        public bool GetColliding(Bounds bounds, List<T> result)
//        {
//            return true;
//        }
//        public bool GetColliding(ref Ray checkRay, float maxDistance, List<T> result)
//        {
//            return true;
//        }
//
//        public bool GetColliding(Bounds bounds, List<OctreeObject<T>> result)
//        {
//            return true;
//        }
//        public bool GetColliding(Vector3 minBounds, Vector3 maxBounds, List<OctreeObject<T>> result)
//        {
//            return true;
//        }
//        public bool GetColliding(ref Ray checkRay, float maxDistance, List<OctreeObject<T>> result)
//        {
//            return true;
//        }
//
//        public bool GetNearby(ref Ray ray, float maxDistance, List<OctreeObject<T>> result)
//        {
//            return true;
//        }
//        public bool GetNearby(ref Ray ray, float maxDistance, List<T> result)
//        {
//            return true;
//        }
//
//        public partial class OctreeNode
//        {
//            /// <summary>
//            /// Check if the specified bounds intersect with anything in the tree. See also: GetColliding.
//            /// </summary>
//            /// <param name="checkBounds">Bounds to check.</param>
//            /// <returns>True if there was a collision.</returns>
//            public bool IsColliding(Bounds checkBounds)
//            {
//                Vector3 min = checkBounds.min;
//                Vector3 max = checkBounds.max;
//                // Are the input bounds at least partially in this node?
//                if (!IntersectBounds(ref min, ref max))
//                {
//                    return false;
//                }
//
//                // Check against any objects in this node
//                for (int i = 0; i < objects.Count; i++)
//                {
//                    if (objects[i].IntersectBounds(ref min, ref max))
//                    {
//                        return true;
//                    }
//                }
//
//                // Check children
//                //if (children != null)
//                //{
//                //    for (int i = 0; i < 8; i++)
//                //    {
//                //        if (children[i].IsColliding(ref checkBounds))
//                //        {
//                //            return true;
//                //        }
//                //    }
//                //}
//
//                return false;
//            }
//
//            /// <summary>
//            /// Check if the specified ray intersects with anything in the tree. See also: GetColliding.
//            /// </summary>
//            /// <param name="checkRay">Ray to check.</param>
//            /// <param name="maxDistance">Distance to check.</param>
//            /// <returns>True if there was a collision.</returns>
//            public bool IsColliding(ref Ray checkRay, ref float maxDistance)
//            {
//                // Is the input ray at least partially in this node?
//                float distance;
//                if (!IntersectRay(ref checkRay, out distance) || distance > maxDistance)
//                {
//                    return false;
//                }
//
//                // Check against any objects in this node
//                for (int i = 0; i < objects.Count; i++)
//                {
//                    if (objects[i].IntersectRay(ref checkRay, out distance) && distance <= maxDistance)
//                    {
//                        return true;
//                    }
//                }
//
//                // Check children
//                //if (children != null)
//                //{
//                //    for (int i = 0; i < 8; i++)
//                //    {
//                //        if (children[i].IsColliding(ref checkRay, maxDistance))
//                //        {
//                //            return true;
//                //        }
//                //    }
//                //}
//
//                return false;
//            }
//
//            /// <summary>
//            /// Returns an array of objects that intersect with the specified bounds, if any. Otherwise returns an empty array. See also: IsColliding.
//            /// </summary>
//            /// <param name="checkBounds">Bounds to check. Passing by ref as it improves performance with structs.</param>
//            /// <param name="result">List result.</param>
//            /// <returns>Objects that intersect with the specified bounds.</returns>
//            public void GetColliding(Bounds checkBounds, List<T> result)
//            {
//                Vector3 min = checkBounds.min;
//                Vector3 max = checkBounds.max;
//                // Are the input bounds at least partially in this node?
//                if (!IntersectBounds(ref min, ref max))
//                {
//                    return;
//                }
//
//                // Check against any objects in this node
//                for (int i = 0; i < objects.Count; i++)
//                {
//                    if (objects[i].IntersectBounds(ref min, ref max))
//                    {
//                        result.Add(objects[i].obj);
//                    }
//                }
//
//                // Check children
//                //if (children != null)
//                //{
//                //    for (int i = 0; i < 8; i++)
//                //    {
//                //        children[i].GetColliding(ref checkBounds, result);
//                //    }
//                //}
//            }
//
//            /// <summary>
//            /// Returns an array of objects that intersect with the specified ray, if any. Otherwise returns an empty array. See also: IsColliding.
//            /// </summary>
//            /// <param name="checkRay">Ray to check. Passing by ref as it improves performance with structs.</param>
//            /// <param name="maxDistance">Distance to check.</param>
//            /// <param name="result">List result.</param>
//            /// <returns>Objects that intersect with the specified ray.</returns>
//            public void GetColliding(ref Ray checkRay, List<T> result, float maxDistance = float.PositiveInfinity)
//            {
//                float distance;
//                // Is the input ray at least partially in this node?
//                if (!IntersectRay(ref checkRay, out distance) || distance > maxDistance)
//                {
//                    return;
//                }
//
//                // Check against any objects in this node
//                for (int i = 0; i < objects.Count; i++)
//                {
//                    if (objects[i].IntersectRay(ref checkRay, out distance) && distance <= maxDistance)
//                    {
//                        result.Add(objects[i].obj);
//                    }
//                }
//
//                // Check children
//                //if (children != null)
//                //{
//                //    for (int i = 0; i < 8; i++)
//                //    {
//                //        children[i].GetColliding(ref checkRay, result, maxDistance);
//                //    }
//                //}
//            }
//
//            /// <summary>
//            /// Return objects that are within maxDistance of the specified ray.
//            /// </summary>
//            /// <param name="ray">The ray.</param>
//            /// <param name="maxDistance">Maximum distance from the ray to consider.</param>
//            /// <param name="result">List result.</param>
//            /// <returns>Objects within range.</returns>
//            public void GetNearby(ref Ray ray, ref float maxDistance, List<T> result)
//            {
//                bool intersected = IntersectRayFat(ref ray, ref maxDistance);
//                if (!intersected)
//                {
//                    return;
//                }
//
//                // Check against any objects in this node
//                for (int i = 0; i < objects.Count; i++)
//                {
//                    if (SqrDistanceToRay(ray, objects[i].boundsCenter) <= (maxDistance * maxDistance))
//                    {
//                        result.Add(objects[i].obj);
//                    }
//                }
//
//                // Check children
//                // if (children != null)
//                // {
//                //     for (int i = 0; i < 8; i++)
//                //     {
//                //         children[i].GetNearby(ref ray, ref maxDistance, result);
//                //     }
//                // }
//            }
//
//            public float SqrDistanceToRay(Ray ray, Vector3 point)
//            {
//                Vector3 lhs = vCopy;
//                lhs.x = ray.direction.x;
//                lhs.y = ray.direction.y;
//                lhs.z = ray.direction.z;
//                Vector3 rhs = vCopy;
//                rhs.x = point.x - ray.origin.x;
//                rhs.y = point.y - ray.origin.y;
//                rhs.z = point.z - ray.origin.z;
//                Vector3 result = vCopy;
//                result.x = lhs.y * rhs.z - lhs.z * rhs.y;
//                result.y = lhs.z * rhs.x - lhs.x * rhs.z;
//                result.z = lhs.x * rhs.y - lhs.y * rhs.x;
//                return result.sqrMagnitude;
//            }
//        }
//
//
//
//
//    }
//}
//