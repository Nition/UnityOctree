using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityOctree
{
    public partial class LooseOctree<T>
    {
        public bool IsColliding(Bounds bounds)
        {
            return true;
        }
        public bool IsColliding(FastBounds bounds)
        {
            return true;
        }
        public bool GetColliding(Bounds bounds, List<OctreeObject> result)
        {
            return true;
        }
        public bool GetColliding(Bounds bounds, List<T> result)
        {
            return true;
        }
        public bool GetColliding(FastBounds bounds, List<OctreeObject> result)
        {
            return true;
        }
        public bool GetColliding(FastBounds bounds, List<T> result)
        {
            return true;
        }
        public bool IsColliding(Ray checkRay, float maxDistance)
        {
            return true;
        }
        public bool GetColliding(ref Ray checkRay,float maxDistance,List<OctreeObject> result)
        {
            return true;
        }
        public bool GetNearby(ref Ray ray, float maxDistance, List<OctreeObject> result)
        {
            return true;
        }
        public bool GetNearby(ref Ray ray, float maxDistance, List<T> result)
        {
            return true;
        }
        private partial class OctreeNode
        {
            /// <summary>
            /// Check if the specified bounds intersect with anything in the tree. See also: GetColliding.
            /// </summary>
            /// <param name="checkBounds">Bounds to check.</param>
            /// <returns>True if there was a collision.</returns>
            public bool IsColliding(ref FastBounds checkBounds)
            {
                // Are the input bounds at least partially in this node?
                if (!actualBounds.IntersectBounds(checkBounds))
                {
                    return false;
                }

                // Check against any objects in this node
                for (int i = 0; i < objects.Count; i++)
                {
                    if (objects[i].bounds.IntersectBounds(checkBounds))
                    {
                        return true;
                    }
                }

                // Check children
                //if (children != null)
                //{
                //    for (int i = 0; i < 8; i++)
                //    {
                //        if (children[i].IsColliding(ref checkBounds))
                //        {
                //            return true;
                //        }
                //    }
                //}

                return false;
            }

            /// <summary>
            /// Check if the specified ray intersects with anything in the tree. See also: GetColliding.
            /// </summary>
            /// <param name="checkRay">Ray to check.</param>
            /// <param name="maxDistance">Distance to check.</param>
            /// <returns>True if there was a collision.</returns>
            public bool IsColliding(ref Ray checkRay, float maxDistance)
            {
                // Is the input ray at least partially in this node?
                float distance;
                if (!actualBounds.IntersectRay(checkRay, out distance) || distance > maxDistance)
                {
                    return false;
                }

                // Check against any objects in this node
                for (int i = 0; i < objects.Count; i++)
                {
                    if (objects[i].bounds.IntersectRay(checkRay, out distance) && distance <= maxDistance)
                    {
                        return true;
                    }
                }

                // Check children
                //if (children != null)
                //{
                //    for (int i = 0; i < 8; i++)
                //    {
                //        if (children[i].IsColliding(ref checkRay, maxDistance))
                //        {
                //            return true;
                //        }
                //    }
                //}

                return false;
            }

            /// <summary>
            /// Returns an array of objects that intersect with the specified bounds, if any. Otherwise returns an empty array. See also: IsColliding.
            /// </summary>
            /// <param name="checkBounds">Bounds to check. Passing by ref as it improves performance with structs.</param>
            /// <param name="result">List result.</param>
            /// <returns>Objects that intersect with the specified bounds.</returns>
            public void GetColliding(ref Bounds checkBounds, List<T> result)
            {
                // Are the input bounds at least partially in this node?
                if (!actualBounds.IntersectBounds(checkBounds))
                {
                    return;
                }

                // Check against any objects in this node
                for (int i = 0; i < objects.Count; i++)
                {
                    if (objects[i].bounds.IntersectBounds(checkBounds))
                    {
                        result.Add(objects[i].obj);
                    }
                }

                // Check children
                //if (children != null)
                //{
                //    for (int i = 0; i < 8; i++)
                //    {
                //        children[i].GetColliding(ref checkBounds, result);
                //    }
                //}
            }

            /// <summary>
            /// Returns an array of objects that intersect with the specified ray, if any. Otherwise returns an empty array. See also: IsColliding.
            /// </summary>
            /// <param name="checkRay">Ray to check. Passing by ref as it improves performance with structs.</param>
            /// <param name="maxDistance">Distance to check.</param>
            /// <param name="result">List result.</param>
            /// <returns>Objects that intersect with the specified ray.</returns>
            public void GetColliding(ref Ray checkRay, List<T> result, float maxDistance = float.PositiveInfinity)
            {
                float distance;
                // Is the input ray at least partially in this node?
                if (!actualBounds.IntersectRay(checkRay, out distance) || distance > maxDistance)
                {
                    return;
                }

                // Check against any objects in this node
                for (int i = 0; i < objects.Count; i++)
                {
                    if (objects[i].bounds.IntersectRay(checkRay, out distance) && distance <= maxDistance)
                    {
                        result.Add(objects[i].obj);
                    }
                }

                // Check children
                //if (children != null)
                //{
                //    for (int i = 0; i < 8; i++)
                //    {
                //        children[i].GetColliding(ref checkRay, result, maxDistance);
                //    }
                //}
            }

            /// <summary>
            /// Return objects that are within maxDistance of the specified ray.
            /// </summary>
            /// <param name="ray">The ray.</param>
            /// <param name="maxDistance">Maximum distance from the ray to consider.</param>
            /// <param name="result">List result.</param>
            /// <returns>Objects within range.</returns>
            public void GetNearby(ref Ray ray, ref float maxDistance, List<T> result)
            {
                bool intersected = actualBounds.IntersectRayFat(ray,maxDistance);
                if (!intersected)
                {
                    return;
                }

                // Check against any objects in this node
                for (int i = 0; i < objects.Count; i++)
                {
                    if (SqrDistanceToRay(ray, objects[i].bounds.center) <= (maxDistance * maxDistance))
                    {
                        result.Add(objects[i].obj);
                    }
                }

                // Check children
               // if (children != null)
               // {
               //     for (int i = 0; i < 8; i++)
               //     {
               //         children[i].GetNearby(ref ray, ref maxDistance, result);
               //     }
               // }
            }
        }




    }
}
