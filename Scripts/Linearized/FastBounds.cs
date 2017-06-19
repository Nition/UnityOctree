using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityOctree
{
    /// <summary>
    /// A stripped down version of UnityEngine's bounds struct.
    /// Keeps all methods and values local to prevent managed/unmanaged overhead.
    /// Immutable
    /// </summary>
    public struct FastBounds
    {
        public readonly Vector3 extents;
        public readonly Vector3 center;
        public readonly Vector3 size;
        public readonly Vector3 min;
        public readonly Vector3 max;

        public FastBounds(Vector3 center, Vector3 size)
        {
            this.center = center;
            this.size = size;
            extents = new Vector3(size.x*.5F,size.y*.5F,size.z*.5F);
            max = new Vector3(center.x + extents.x, center.y + extents.y, center.z + extents.z);
            min = new Vector3(center.x - extents.x, center.y - extents.y, center.z - extents.z);
        }

        /// <summary>
        /// Convert UnityEngine bounds to fastbounds
        /// </summary>
        public FastBounds(Bounds bounds)
        {
            center = bounds.center;
            size = bounds.size;
            extents = bounds.extents;
            max = new Vector3(center.x + extents.x, center.y + extents.y, center.z + extents.z);
            min = new Vector3(center.x - extents.x, center.y - extents.y, center.z - extents.z);
        }

        /// <summary>
        /// Returns a UnityEngine bounds with the same position and size
        /// </summary>
        /// <returns></returns>
        public Bounds toBounds()
        {
            return new Bounds(center, size);
        }

        public bool ContainsBounds(Bounds bounds)
        {
            return ContainsBounds(new FastBounds(bounds));
        }
        /// <summary>
        /// Returns true if the bounds fully encapsulates the provided bounds
        /// </summary>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public bool ContainsBounds(FastBounds bounds)
        {
            if (bounds.min.x <= min.x || bounds.max.x >= max.x)
                return false; //Other X is beyond or exactly aligned with outer X
            if (bounds.min.y <= min.y || bounds.max.y >= max.y)
                return false; //Other Y is beyond or exactly aligned with outer Y
            if (bounds.min.z <= min.z || bounds.max.z >= max.z)
                return false; //Other Z is beyond or exactly aligned with outer Z

            return true;
        }

        /// <summary>
        /// Returns true if point falls fully within these bounds
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public bool ContainsPoint(Vector3 point)
        {
            //Point is exactly equal or outside of min/max of bounds
            if (point.x >= max.x || point.x <= min.x)
                return false;
            if (point.y >= max.x || point.y <= min.y)
                return false;
            if (point.z >= max.z || point.y <= min.y)
                return false;

            return false;
        }

        public bool IntersectRay(Ray ray)
        {
            float dummy;
            return IntersectRay(ray, out dummy);
        }

        public bool IntersectRayFat(Ray ray,float maxDistance)
        {
            float dummy;
            return IntersectRayFat(ray, maxDistance, out dummy);
        }
        /// <summary>
        /// Returns true if a ray comes within maxDistance of the bounds
        /// </summary>
        /// <param name="ray"></param>
        /// <param name="distance"></param>
        /// <returns></returns>
        public bool IntersectRayFat(Ray ray, float maxDistance, out float distance)
        {
            //Artifically increase bounds size by distance per side
            return IntersectRayInternal(ray, new Vector3(min.x*maxDistance,min.y*maxDistance,min.z*maxDistance), new Vector3(max.x*maxDistance,max.y*maxDistance,max.z*maxDistance), out distance);
        }

        /// <summary>
        /// Returns true if ray intersects these bounds.
        /// </summary>
        /// <param name="ray"></param>
        /// <param name="distance"></param>
        /// <returns></returns>
        public bool IntersectRay(Ray ray, out float distance)
        {
            return IntersectRayInternal(ray, min, max, out distance);
        }

        private bool IntersectRayInternal(Ray ray, Vector3 min, Vector3 max, out float distance)
        {
            //Direction must be unit length
            Vector3 dirFrac = new Vector3(1.0F / ray.direction.x, 1.0F / ray.direction.y, 1.0F / ray.direction.z);
            Vector3 origin = ray.origin; //Ray origin
            float t1 = (min.x - origin.x) * dirFrac.x;
            float t2 = (max.x - origin.x) * dirFrac.x;
            float t3 = (min.y - origin.y) * dirFrac.y;
            float t4 = (max.y - origin.y) * dirFrac.y;
            float t5 = (min.z - origin.z) * dirFrac.z;
            float t6 = (max.z - origin.z) * dirFrac.z;

            float tmin = Mathf.Max(Mathf.Max(Mathf.Min(t1, t2), Mathf.Min(t3, t4)), Mathf.Min(t5, t6)); //First intersection
            float tmax = Mathf.Min(Mathf.Min(Mathf.Max(t1, t2), Mathf.Max(t3, t4)), Mathf.Max(t5, t6)); //Second intersection

            // if tmax < 0, ray (line) is intersecting AABB, but the whole AABB is behind the ray
            if (tmax < 0)
            {
                distance = tmax;
                return false;
            }

            // if tmin > tmax, ray doesn't intersect AABB
            if (tmin > tmax)
            {
                distance = tmax;
                return false;
            }

            distance = tmin;
            return true;
        }

        public bool IntersectBounds(Bounds bounds)
        {
            return IntersectBounds(new FastBounds(bounds));
        }

        public bool IntersectBounds(FastBounds bounds)
        {
            return
                min.x <= bounds.max.x &&
                max.x >= bounds.min.x &&
                min.y <= bounds.max.y &&
                max.y >= bounds.min.y &&
                min.z <= bounds.max.z &&
                max.z >= bounds.min.z;
        }
    }
}