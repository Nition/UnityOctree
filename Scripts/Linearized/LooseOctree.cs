using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityOctree
{
    public partial class LooseOctree<T> where T : class
    {
        public readonly OctreeNode rootNode;
        public const int numObjectsAllowed = 8;
        public const int maxDepth = 10; //We use 3 bits per level (32 bits/3 = 10). With ulong we could hold (64/3 = 21 levels) 
                                         //Nodes are encoded into an unsigned integer as bits;
                                         //Each set of 3 bits is one "level" in the tree. By shifting left or right, we can find children or parents.
                                         // code >> 3 would shift right, giving the code for the parent
                                         // code << 3 | 4 would shift left and attach a 4. Giving us the fourth child of the node.
                                         //With a 32 bit integer we can hold a depth of 10. With a 64 bit we can hold 21


        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        private readonly uint[] ChildMask = {
            2, 4, 8, 16,
            32, 64, 128, 256};
        private ObjectPool<OctreeNode> nodePool;
        private ObjectPool<OctreeObject<T>> objectPool;
        private Queue<OctreeObject<T>> orphanObjects;
        private Queue<OctreeObject<T>> removals; //Same reference, different name.
        private Dictionary<uint, OctreeNode> nodes;
        private readonly Vector3 vCopy = new Vector3(); //Micro optimization. Empty V3 to avoid calling constructor
        private Vector3 pointSize = Vector3.zero; //Default size for point types
        private readonly float looseness;

        public LooseOctree(float initialSize, Vector3 initialWorldPosition, float loosenessVal,float preAllocationFactor)
        {
            Debug.Assert(loosenessVal >= 1.0F && loosenessVal <= 2.0F, "Octree looseness should be between 1.0 - 2.0");
            int allocationFactor = Mathf.FloorToInt(preAllocationFactor);
            objectPool = new ObjectPool<OctreeObject<T>>((1 << maxDepth) * numObjectsAllowed);
            orphanObjects = new Queue<OctreeObject<T>>(numObjectsAllowed * 8);
            nodePool = new ObjectPool<OctreeNode>(1 << maxDepth);
            nodes = new Dictionary<uint, OctreeNode>(1 << maxDepth);

            removals = orphanObjects;
            this.looseness = loosenessVal;
            rootNode = nodePool.Pop();
            rootNode.Initialize(1U, this);
            rootNode.SetBounds(ref initialWorldPosition, Vector3.one * initialSize, looseness);
            nodes[1U] = rootNode;
        }

        public int NodeCount()
        {
            return nodes.Count;
        }

        public OctreeObject<T> Add(T obj, ref Vector3 position)
        {
            OctreeObject<T> newObj = Add(obj, ref position, ref pointSize);
            newObj.isPoint = true;
            return newObj;
        }

        public OctreeObject<T> Add(T obj, Bounds bounds)
        {
            Vector3 pos = bounds.center;
            Vector3 size = bounds.size;
            return Add(obj, ref pos, ref size);
        }

        public OctreeObject<T> Add(T obj, ref Vector3 position, ref Vector3 size)
        {
            OctreeObject<T> newObj = objectPool.Pop();
            newObj.obj = obj;
            newObj.SetBounds(position, size);
            bool added = rootNode.TryAddObj(newObj, true);
            Debug.Assert(added, "Failed to add object. Likely outside of Octree area. Increase octree size or check for bounds limits (tree.rootNode.boundsMax,tree.rootNode.boundsMin)");
            return newObj;
        }

        public void Print()
        {
            Debug.Log("---------------------------------------------------------------------------");

            foreach (KeyValuePair<uint, OctreeNode> node in nodes)
            {
                Debug.LogFormat("|---Depth({0})---Location(X{1} Y{2} Z{3})---Size({4})---Index({5})---Objects( Here:{6} Branch:{7})---|", GetDepth(node.Key), node.Value.boundsCenter.x, node.Value.boundsCenter.y, node.Value.boundsCenter.z, node.Value.boundsSize.x, GetIndex(node.Key), node.Value.objects.Count, node.Value.branchItemCount);
            }

            Debug.Log("---------------------------------------------------------------------------");
        }
        public void DrawAll(bool drawNodes, bool drawObjects, bool drawConnections, bool drawLabels)
        {

            foreach (KeyValuePair<uint, OctreeNode> node in nodes)
            {
                float tintVal = GetDepth(node.Key) / 7F; // Will eventually get values > 1. Color rounds to 1 automatically
                Gizmos.color = new Color(tintVal, 0F, 1.0f - tintVal);
                if (drawNodes)
                    Gizmos.DrawWireCube(node.Value.boundsCenter, node.Value.boundsSize);

                if (drawLabels)
                    UnityEditor.Handles.Label(node.Value.boundsCenter, "Depth("+System.Convert.ToString(GetDepth(node.Value.locationCode)) + ") : Branch(" + System.Convert.ToString(node.Value.branchItemCount) +")");

                Gizmos.color = new Color(tintVal, GetIndex(node.Key) / 7F, 1.0f - tintVal);
                foreach (OctreeObject<T> obj in node.Value.objects)
                {
                    if (drawObjects)
                    {
                        if (obj.isPoint)
                            Gizmos.DrawSphere(obj.boundsCenter, 0.25F);
                        else
                            Gizmos.DrawCube(obj.boundsCenter, obj.boundsSize);
                    }
                    if (drawConnections)
                        Gizmos.DrawLine(node.Value.boundsCenter, obj.boundsCenter);
                }
            }

            Gizmos.color = Color.white;
        }


    }
}