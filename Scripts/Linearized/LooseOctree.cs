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


        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        readonly float looseness;
        readonly float initialSize;
        public static uint[] ChildMask = {
            1<<1, 1<<2, 1<<3, 1<<4,
            1<<5, 1<<6, 1<<7, 1<<8};
        ObjectPool<OctreeNode> nodePool = new ObjectPool<OctreeNode>(maxDepth * maxDepth * maxDepth * maxDepth);
        ObjectPool<OctreeObject<T>> objectPool = new ObjectPool<OctreeObject<T>>(numObjectsAllowed * maxDepth * maxDepth * maxDepth * maxDepth);
        //Node map
        private Dictionary<uint, OctreeNode> nodes = new Dictionary<uint, OctreeNode>();

        private OctreeNode rootNode; //Keep a ref to the root node so we don't need to keep looking it up. Micro optimization
        Vector3 vCopy = new Vector3(); //Micro optimization. Empty V3 to avoid calling constructor
        FastBounds fbCopy = new FastBounds(Vector3.zero, Vector3.zero);
        public LooseOctree(float initialSize, Vector3 initialWorldPosition, float loosenessVal)
        {
            this.initialSize = initialSize;
            this.looseness = Mathf.Clamp(loosenessVal, 1.0F, 2.0F);
            FastBounds newBounds = new FastBounds(initialWorldPosition, Vector3.one * initialSize * looseness);
            rootNode = nodePool.Pop();
            rootNode.Initialize(1U, this);
            rootNode.actualBounds = newBounds;
            nodes[1U] = rootNode;
        }

        public int NodeCount()
        {
            return nodes.Count;
        }
        private void Grow(Vector3 direction)
        {
            int xDirection = direction.x >= 0 ? 1 : -1;
            int yDirection = direction.y >= 0 ? 1 : -1;
            int zDirection = direction.z >= 0 ? 1 : -1;
            float half = rootNode.actualBounds.size.x * .5F / looseness;
            float newLength = half * 4;
            //Resize the root
            Vector3 offset = vCopy;
            offset.x = xDirection * half;
            offset.y = yDirection * half;
            offset.z = zDirection * half;
            Vector3 newCenter = rootNode.actualBounds.center;
            newCenter.x += offset.x;
            newCenter.y += offset.y;
            newCenter.z += offset.z;
            offset.x = newLength * looseness;
            offset.y = newLength * looseness;
            offset.z = newLength * looseness;
            FastBounds newBounds = new FastBounds(newCenter, offset);
            rootNode.actualBounds = newBounds;
            //Resize all elements
            for (uint i = 0; i < 7U; i++)
            {
                rootNode.SetAllChildBounds(true);
            }

        }

        Vector3 pointSize = new Vector3(0F, 0F, 0F);
        public OctreeObject<T> Add(T obj, Vector3 position)
        {
            fbCopy.SetPositionOnly(ref position);
            OctreeObject<T> newObj = Add(obj, ref fbCopy);
            newObj.isPoint = true;
            return newObj;
        }

        public OctreeObject<T> Add(T obj, Bounds bounds)
        {
            FastBounds fb = fbCopy.FromBounds(ref bounds);
            return Add(obj, ref fb);
        }

        public OctreeObject<T> Add(T obj, ref FastBounds bounds)
        {
            OctreeObject<T> newObj = objectPool.Pop();
            newObj.obj = obj;
            newObj.bounds = bounds;
            int count = 0;
            while (!rootNode.TryAddObj(newObj, true))
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

        private List<OctreeObject<T>> allObjects = new List<OctreeObject<T>>();
        private List<OctreeObject<T>> removals = new List<OctreeObject<T>>();
        public List<OctreeObject<T>> GetAllObjects(bool removeFromNodes)
        {
            foreach (KeyValuePair<uint, OctreeNode> node in nodes)
            {
                foreach (OctreeObject<T> obj in node.Value.objects)
                {
                    allObjects.Add(obj);
                    if (removeFromNodes)
                        removals.Add(obj);
                }
            }

            foreach (OctreeObject<T> obj in removals)
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
                Debug.LogFormat("|---Depth({0})---Location(X{1} Y{2} Z{3})---Size({4})---Index({5})---Objects( Here:{6} Branch:{7})---|", GetDepth(node.Key), node.Value.actualBounds.center.x, node.Value.actualBounds.center.y, node.Value.actualBounds.center.z, node.Value.actualBounds.size.x, GetIndex(node.Key), node.Value.objects.Count, node.Value.branchItemCount);
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
                foreach (OctreeObject<T> obj in node.Value.objects)
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