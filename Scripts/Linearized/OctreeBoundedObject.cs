using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityOctree
{
    public partial class LooseOctree<T> where T : class
    {
        public class OctreeBoundedObject
        {
            public Vector3 boundsExtents;
            public Vector3 boundsCenter;
            public Vector3 boundsSize;

            public Vector3 doubleBoundsMax;
            public Vector3 doubleBoundsMin;

            public Vector3 boundsMin;
            public Vector3 boundsMax;
            protected Vector3 baseSize;
            protected Vector3 vCopy = new Vector3();
            public void SetBoundsPosition(Vector3 newPos)
            {
                boundsCenter = newPos;
                boundsMax.x = boundsCenter.x + boundsExtents.x;
                boundsMax.y = boundsCenter.y + boundsExtents.y;
                boundsMax.z = boundsCenter.z + boundsExtents.z;
                boundsMin.x = boundsCenter.x - boundsExtents.x;
                boundsMin.y = boundsCenter.y - boundsExtents.y;
                boundsMin.z = boundsCenter.z - boundsExtents.z;
                doubleBoundsMax.x = boundsCenter.x + (boundsExtents.x * 2F);
                doubleBoundsMax.y = boundsCenter.y + (boundsExtents.y * 2F);
                doubleBoundsMax.z = boundsCenter.z + (boundsExtents.z * 2F);
                doubleBoundsMin.x = boundsCenter.x - (boundsExtents.x * 2F);
                doubleBoundsMin.y = boundsCenter.y - (boundsExtents.y * 2F);
                doubleBoundsMin.z = boundsCenter.z - (boundsExtents.z * 2F);
            }

            public void SetBounds(Bounds bounds)
            {
                boundsCenter = bounds.center;
                boundsSize = bounds.size;
                boundsExtents = bounds.extents;
                boundsMax.x = boundsCenter.x + boundsExtents.x;
                boundsMax.y = boundsCenter.y + boundsExtents.y;
                boundsMax.z = boundsCenter.z + boundsExtents.z;
                boundsMin.x = boundsCenter.x - boundsExtents.x;
                boundsMin.y = boundsCenter.y - boundsExtents.y;
                boundsMin.z = boundsCenter.z - boundsExtents.z;
                doubleBoundsMax.x = boundsCenter.x + (boundsExtents.x * 2F);
                doubleBoundsMax.y = boundsCenter.y + (boundsExtents.y * 2F);
                doubleBoundsMax.z = boundsCenter.z + (boundsExtents.z * 2F);
                doubleBoundsMin.x = boundsCenter.x - (boundsExtents.x * 2F);
                doubleBoundsMin.y = boundsCenter.y - (boundsExtents.y * 2F);
                doubleBoundsMin.z = boundsCenter.z - (boundsExtents.z * 2F);
                baseSize = boundsSize;
            }
            public void SetBounds(ref Vector3 center, Vector3 size, float looseness)
            {
                Vector3 newSize = size;
                newSize.x *= looseness;
                newSize.y *= looseness;
                newSize.z *= looseness;
                SetBounds(center, newSize);
                baseSize = size;
            }
            public void SetBounds(Vector3 center, Vector3 size)
            {
                boundsCenter = center;
                boundsSize = size;
                boundsExtents.x = boundsSize.x * .5F;
                boundsExtents.y = boundsSize.y * .5F;
                boundsExtents.z = boundsSize.z * .5F;
                boundsMax.x = boundsCenter.x + boundsExtents.x;
                boundsMax.y = boundsCenter.y + boundsExtents.y;
                boundsMax.z = boundsCenter.z + boundsExtents.z;
                boundsMin.x = boundsCenter.x - boundsExtents.x;
                boundsMin.y = boundsCenter.y - boundsExtents.y;
                boundsMin.z = boundsCenter.z - boundsExtents.z;
                doubleBoundsMax.x = boundsCenter.x + (boundsExtents.x * 2F);
                doubleBoundsMax.y = boundsCenter.y + (boundsExtents.y * 2F);
                doubleBoundsMax.z = boundsCenter.z + (boundsExtents.z * 2F);
                doubleBoundsMin.x = boundsCenter.x - (boundsExtents.x * 2F);
                doubleBoundsMin.y = boundsCenter.y - (boundsExtents.y * 2F);
                doubleBoundsMin.z = boundsCenter.z - (boundsExtents.z * 2F);
                baseSize = size;
            }

            public bool ContainsBounds(Bounds bounds)
            {
                Vector3 min = bounds.min;
                Vector3 max = bounds.max;
                return ContainsBounds(ref min, ref max);
            }
            /// <summary>
            /// Returns true if the bounds fully encapsulates the provided bounds
            /// </summary>
            /// <param name="bounds"></param>
            /// <returns></returns>
            public bool ContainsBounds(ref Vector3 minBounds, ref Vector3 maxBounds)
            {
                if (minBounds.x <= boundsMin.x || maxBounds.x >= boundsMax.x)
                    return false; //Other X is beyond or exactly aligned with outer X
                if (minBounds.y <= boundsMin.y || maxBounds.y >= boundsMax.y)
                    return false; //Other Y is beyond or exactly aligned with outer Y
                if (minBounds.z <= boundsMin.z || maxBounds.z >= boundsMax.z)
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
                if (point.x >= boundsMax.x || point.x <= boundsMin.x)
                    return false;
                if (point.y >= boundsMax.x || point.y <= boundsMin.y)
                    return false;
                if (point.z >= boundsMax.z || point.y <= boundsMin.y)
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
                return IntersectRayFat(ref ray, maxDistance, out dummy);
            }
            /// <summary>
            /// Returns true if a ray comes within maxDistance of the bounds
            /// </summary>
            /// <param name="ray"></param>
            /// <param name="distance"></param>
            /// <returns></returns>
            public bool IntersectRayFat(ref Ray ray, float maxDistance, out float distance)
            {
                Vector3 minBounds = vCopy;
                minBounds.x = boundsMin.x - maxDistance;
                minBounds.y = boundsMin.y - maxDistance;
                minBounds.z = boundsMin.z - maxDistance;
                Vector3 maxBounds = vCopy;
                maxBounds.x = boundsMax.x + maxDistance;
                maxBounds.y = boundsMax.y + maxDistance;
                maxBounds.z = boundsMax.z + maxDistance;
                //Artifically increase bounds size by distance per side
                return IntersectRayInternal(ray, minBounds, maxBounds, out distance);
            }

            /// <summary>
            /// Returns true if ray intersects these bounds.
            /// </summary>
            /// <param name="ray"></param>
            /// <param name="distance"></param>
            /// <returns></returns>
            public bool IntersectRay(ref Ray ray, out float distance)
            {
                return IntersectRayInternal(ray, boundsMin, boundsMax, out distance);
            }

            private bool IntersectRayInternal(Ray ray, Vector3 minBounds, Vector3 maxBounds, out float distance)
            {
                //Direction must be unit length
                Vector3 dirFrac = new Vector3(1.0F / ray.direction.x, 1.0F / ray.direction.y, 1.0F / ray.direction.z);
                Vector3 origin = ray.origin; //Ray origin
                float t1 = (minBounds.x - origin.x) * dirFrac.x;
                float t2 = (maxBounds.x - origin.x) * dirFrac.x;
                float t3 = (minBounds.y - origin.y) * dirFrac.y;
                float t4 = (maxBounds.y - origin.y) * dirFrac.y;
                float t5 = (minBounds.z - origin.z) * dirFrac.z;
                float t6 = (maxBounds.z - origin.z) * dirFrac.z;

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
                Vector3 min = bounds.min;
                Vector3 max = bounds.max;
                return IntersectBounds(ref min, ref max);
            }

            public bool IntersectBounds(ref Vector3 minBounds, ref Vector3 maxBounds)
            {
                return
                    boundsMin.x <= maxBounds.x &&
                    boundsMax.x >= minBounds.x &&
                    boundsMin.y <= maxBounds.y &&
                    boundsMax.y >= minBounds.y &&
                    boundsMin.z <= maxBounds.z &&
                    boundsMax.z >= minBounds.z;
            }
        }
    }
}

