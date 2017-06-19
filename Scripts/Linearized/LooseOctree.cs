using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityOctree
{
    public partial class LooseOctree<T> where T : class
    {
        public static int numObjectsAllowed = 8;
        public static int maxDepth = 10; //We use 3 bits per level (32 bits/3 = 10). With ulong we could hold (64/3 = 21 levels) 
                                         //Nodes are encoded into an unsigned integer as bits;
                                         //Each set of 3 bits is one "level" in the tree. By shifting left or right, we can find children or parents.
                                         // code >> 3 would shift right, giving the code for the parent
                                         // code << 3 | 4 would shift left and attach a 4. Giving us the fourth child of the node.
                                         //With a 32 bit integer we can hold a depth of 10. With a 64 bit we can hold 21
        readonly float looseness;
        readonly float initialSize;
        //Maps node to their location in the tree. Where key is the location and value is the node.
        Dictionary<uint, OctreeNode> nodes;
        public LooseOctree(float initialSize, Vector3 initialWorldPosition, float loosenessVal)
        {
            nodes = new Dictionary<uint, OctreeNode>();
            this.initialSize = initialSize;
            this.looseness = Mathf.Clamp(loosenessVal, 1.0F, 2.0F);
            FastBounds newBounds = new FastBounds(initialWorldPosition, Vector3.one * initialSize * looseness);
            OctreeNode root = new OctreeNode(1U, this);
            root.actualBounds = newBounds;
            nodes[1U] = root;
        }

        public int NodeCount()
        {
            int count = 0;
            foreach (KeyValuePair<uint, OctreeNode> node in nodes)
            {
                count++;
            }
            return count;
        }
        private void Grow(Vector3 direction)
        {
            int xDirection = direction.x >= 0 ? 1 : -1;
            int yDirection = direction.y >= 0 ? 1 : -1;
            int zDirection = direction.z >= 0 ? 1 : -1;
            OctreeNode rootNode = nodes[1U];
            float half = rootNode.actualBounds.size.x / 2F / looseness;
            float newLength = half * 4;
            //Resize the root
            Vector3 newCenter = rootNode.actualBounds.center + new Vector3(xDirection * half, yDirection * half, zDirection * half);
            FastBounds newBounds = new FastBounds(newCenter, new Vector3(newLength * looseness, newLength * looseness, newLength * looseness));
            rootNode.actualBounds = newBounds;
            //Resize all elements
            for (uint i = 0; i < 7U; i++)
            {
                rootNode.SetAllChildBounds(true);
            }

        }

        public OctreeObject Add(T obj, Vector3 position)
        {
            FastBounds bounds = new FastBounds(position, new Vector3(0F, 0F, 0F));
            OctreeObject newObj = Add(obj, bounds);
            newObj.isPoint = true;
            return newObj;
        }

        public OctreeObject Add(T obj, Bounds bounds)
        {
            return Add(obj, new FastBounds(bounds));
        }

        public OctreeObject Add(T obj, FastBounds bounds)
        {
            OctreeObject newObj = new OctreeObject(this);
            newObj.obj = obj;
            newObj.bounds = bounds;
            int count = 0;
            while (!nodes[1U].TryAddObj(newObj))
            {
                //Grow(newObj.center - nodes[1U].actualCenter);
                if (++count > 20)
                {
                    Debug.LogError("Aborted Add operation as it seemed to be going on forever (" + (count - 1) + ") attempts at growing the octree.");
                    return null;
                }
            }


            return newObj;
        }

        private List<OctreeObject> allObjects = new List<OctreeObject>();
        private List<OctreeObject> removals = new List<OctreeObject>();
        public List<OctreeObject> GetAllObjects(bool removeFromNodes)
        {
            foreach (KeyValuePair<uint, OctreeNode> node in nodes)
            {
                foreach (OctreeObject obj in node.Value.objects)
                {
                    allObjects.Add(obj);
                    if (removeFromNodes)
                        removals.Add(obj);
                }
            }
            foreach (OctreeObject obj in removals)
            {
                obj.Remove();
            }

            return allObjects;
        }
        public void Print()
        {
            Debug.Log("---------------------------------------------------------------------------");
            foreach (KeyValuePair<uint, OctreeNode> node in nodes)
            {
                Debug.LogFormat("| --- Node: Depth({0}) --- Location(X{1} Y{2} Z{3}) --- Size({4}) --- Index({5}) --- |Objects({6}) --- |", GetDepth(node.Key), node.Value.actualBounds.center.x, node.Value.actualBounds.center.y, node.Value.actualBounds.center.z, node.Value.actualBounds.size.x, GetIndex(node.Key), node.Value.objects.Count);
            }
            Debug.Log("---------------------------------------------------------------------------");
        }
        public void DrawAll(bool drawNodes, bool drawObjects, bool drawConnections)
        {
            foreach (KeyValuePair<uint, OctreeNode> node in nodes)
            {
                float tintVal = GetDepth(node.Key) / 7F; // Will eventually get values > 1. Color rounds to 1 automatically
                Gizmos.color = new Color(tintVal, 0F, 1.0f - tintVal);
                if (drawNodes)
                    Gizmos.DrawWireCube(node.Value.actualBounds.center, node.Value.actualBounds.size);
                Gizmos.color = new Color(tintVal, GetIndex(node.Key) / 7F, 1.0f - tintVal);
                foreach (OctreeObject obj in node.Value.objects)
                {
                    if (drawObjects)
                    {
                        if (obj.isPoint)
                            Gizmos.DrawSphere(obj.bounds.center, 0.25F);
                        else
                            Gizmos.DrawCube(obj.bounds.center, obj.bounds.size);
                    }
                    if (drawConnections)
                        Gizmos.DrawLine(node.Value.actualBounds.center, obj.bounds.center);
                }
            }
            Gizmos.color = Color.white;
        }


    }
}