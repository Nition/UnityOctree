using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityOctree
{
    public partial class LooseOctree<T>
    {
        private partial class OctreeNode
        {
            public bool GetColliding(ref Ray checkRay, float maxDistance = float.PositiveInfinity)
            {
                // Is the input ray at least partially in this node?
                float distance;
                if (!bounds.IntersectRay(checkRay, out distance) || distance > maxDistance)
                {
                    return false;
                }

                // Check against any objects in this node
                for (int i = 0; i < objects.Count; i++)
                {
                    if (objects[i].Bounds.IntersectRay(checkRay, out distance) && distance <= maxDistance)
                    {
                        return true;
                    }
                }

                // Check children
                if (children != null)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        if (children[i].IsColliding(ref checkRay, maxDistance))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            public void GetColliding(ref Bounds checkBounds, List<T> result)
            {
                // Are the input bounds at least partially in this node?
                if (!bounds.Intersects(checkBounds))
                {
                    return;
                }

                // Check against any objects in this node
                for (int i = 0; i < objects.Count; i++)
                {
                    if (objects[i].Bounds.Intersects(checkBounds))
                    {
                        result.Add(objects[i].Obj);
                    }
                }

                // Check children
                if (children != null)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        children[i].GetColliding(ref checkBounds, result);
                    }
                }
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
                // Does the ray hit this node at all?
                // Note: Expanding the bounds is not exactly the same as a real distance check, but it's fast.
                // TODO: Does someone have a fast AND accurate formula to do this check?
                bounds.Expand(new Vector3(maxDistance * 2, maxDistance * 2, maxDistance * 2));
                bool intersected = bounds.IntersectRay(ray);
                bounds.size = actualBoundsSize;
                if (!intersected)
                {
                    return;
                }

                // Check against any objects in this node
                for (int i = 0; i < objects.Count; i++)
                {
                    if (SqrDistanceToRay(ray, objects[i].Pos) <= (maxDistance * maxDistance))
                    {
                        result.Add(objects[i].Obj);
                    }
                }

                // Check children
                if (children != null)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        children[i].GetNearby(ref ray, ref maxDistance, result);
                    }
                }
            }

            public void GetColliding(ref Ray checkRay, List<T> result, float maxDistance = float.PositiveInfinity)
            {
                float distance;
                // Is the input ray at least partially in this node?
                if (!bounds.IntersectRay(checkRay, out distance) || distance > maxDistance)
                {
                    return;
                }

                // Check against any objects in this node
                for (int i = 0; i < objects.Count; i++)
                {
                    if (objects[i].Bounds.IntersectRay(checkRay, out distance) && distance <= maxDistance)
                    {
                        result.Add(objects[i].Obj);
                    }
                }

                // Check children
                if (children != null)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        children[i].GetColliding(ref checkRay, result, maxDistance);
                    }
                }
            }
        }




    }
}
