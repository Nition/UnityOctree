using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityOctree
{
    /// <summary>
    /// A stripped down version of UnityEngine's bounds struct.
    /// Keeps all methods and values local to prevent managed/unmanaged overhead.
    /// </summary>
    public struct FastBounds
    {
        public Vector3 extents;
        public Vector3 center;
        public Vector3 size;
        public Vector3 min;
        public Vector3 max;
        public Vector3 copy;
        public FastBounds(Vector3 center, Vector3 size)
        {
            copy = new Vector3();
            this.center = center;
            this.size = size;
            extents = copy;//Avoid calling V3 constructor
            extents.x = size.x * .5F;
            extents.y = size.y * .5F;
            extents.z = size.z * .5F;
            max = copy;//Avoid calling V3 constructor
            max.x = center.x + extents.x;
            max.y = center.y + extents.y;
            max.z = center.z + extents.z;
            min = copy; //Avoid calling V3 constructor
            min.x = center.x - extents.x;
            min.y = center.y - extents.y;
            min.z = center.z - extents.z;
        }

        public FastBounds SetPositionOnly(ref Vector3 newPos)
        {
            center = newPos;
            max.x = center.x + extents.x;
            max.y = center.y + extents.y;
            max.z = center.z + extents.z;
            min.x = center.x - extents.x;
            min.y = center.y - extents.y;
            min.z = center.z - extents.z;
            return this;
        }

        public FastBounds FromBounds(ref Bounds bounds)
        {
            center = bounds.center;
            size = bounds.size;
            extents = bounds.extents;
            max.x = center.x + extents.x;
            max.y = center.y + extents.y;
            max.z = center.z + extents.z;
            min.x = center.x - extents.x;
            min.y = center.y - extents.y;
            min.z = center.z - extents.z;
            return this;
        }

        public FastBounds SetValues(ref Vector3 center,ref Vector3 size)
        {
            this.center = center;
            this.size = size;
            extents.x = this.size.x * .5F;
            extents.y = this.size.y * .5F;
            extents.z = this.size.z * .5F;
            max.x = this.center.x + extents.x;
            max.y = this.center.y + extents.y;
            max.z = this.center.z + extents.z;
            min.x = this.center.x - extents.x;
            min.y = this.center.y - extents.y;
            min.z = this.center.z - extents.z;
            return this;
        }

        /// <summary>
        /// Convert UnityEngine bounds to fastbounds
        /// </summary>
        public FastBounds(Bounds bounds)
        {
            copy = new Vector3();
            center = bounds.center;
            size = bounds.size;
            extents = bounds.extents;
            max = copy;//Avoid calling V3 constructor
            max.x = center.x + extents.x;
            max.y = center.y + extents.y;
            max.z = center.z + extents.z;
            min = copy; //Avoid calling V3 constructor
            min.x = center.x - extents.x;
            min.y = center.y - extents.y;
            min.z = center.z - extents.z;
        }

        /// <summary>
        /// Returns a UnityEngine bounds with the same position and size
        /// </summary>
        /// <returns></returns>
        public Bounds toBounds()
        {
            return new Bounds(center, size);
        }

        public bool ContainsBounds(ref Bounds bounds)
        {
            FastBounds fb = new FastBounds(bounds);
            return ContainsBounds(ref fb);
        }
        /// <summary>
        /// Returns true if the bounds fully encapsulates the provided bounds
        /// </summary>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public bool ContainsBounds(ref FastBounds bounds)
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
        public bool ContainsPoint(ref Vector3 point)
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

        public bool IntersectRay(ref Ray ray)
        {
            float dummy;
            return IntersectRay(ref ray, out dummy);
        }

        public bool IntersectRayFat(ref Ray ray, ref float maxDistance)
        {
            float dummy;
            return IntersectRayFat(ref ray, ref maxDistance, out dummy);
        }
        /// <summary>
        /// Returns true if a ray comes within maxDistance of the bounds
        /// </summary>
        /// <param name="ray"></param>
        /// <param name="distance"></param>
        /// <returns></returns>
        public bool IntersectRayFat(ref Ray ray, ref float maxDistance, out float distance)
        {
            Vector3 minBounds = copy;
            minBounds.x = min.x * maxDistance;
            minBounds.y = min.y * maxDistance;
            minBounds.z = min.z * maxDistance;
            Vector3 maxBounds = copy;
            minBounds.x = max.x * maxDistance;
            minBounds.y = max.y * maxDistance;
            minBounds.z = max.z * maxDistance;
            //Artifically increase bounds size by distance per side
            return IntersectRayInternal(ref ray, ref minBounds,ref maxBounds, out distance);
        }

        /// <summary>
        /// Returns true if ray intersects these bounds.
        /// </summary>
        /// <param name="ray"></param>
        /// <param name="distance"></param>
        /// <returns></returns>
        public bool IntersectRay(ref Ray ray, out float distance)
        {
            return IntersectRayInternal(ref ray, ref min, ref max, out distance);
        }

        private bool IntersectRayInternal(ref Ray ray, ref Vector3 min, ref Vector3 max, out float distance)
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

        public bool IntersectBounds(ref Bounds bounds)
        {
            FastBounds fb = new FastBounds(bounds);
            return IntersectBounds(ref fb);
        }

        public bool IntersectBounds(ref FastBounds bounds)
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