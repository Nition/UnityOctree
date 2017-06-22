using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityOctree
{
    public partial class LooseOctree<T> where T : class
    {
        public readonly OctreeNode rootNode;
        public const int numObjectsAllowed = 8;
        private readonly uint[] ChildMask = {
            2, 4, 8, 16,
            32, 64, 128, 256};
        private ObjectPool<OctreeNode> nodePool;
        private ObjectPool<OctreeObject<T>> objectPool;
        private Stack<OctreeObject<T>> orphanObjects;
        private Stack<OctreeObject<T>> removals; //Same reference, different name.
        private readonly Vector3 vCopy = new Vector3(); //Micro optimization. Empty V3 to avoid calling constructor
        private Vector3 pointSize = Vector3.zero; //Default size for point types
        private readonly float looseness;
        public int nodeCount { get; private set; }
        public LooseOctree(float initialSize, Vector3 initialWorldPosition, float loosenessVal, int estimatedMaxObjectCount)
        {
            Debug.Assert(loosenessVal >= 1.0F && loosenessVal <= 2.0F, "Octree looseness should be between 1.0 - 2.0");
            objectPool = new ObjectPool<OctreeObject<T>>(estimatedMaxObjectCount);
            orphanObjects = new Stack<OctreeObject<T>>(numObjectsAllowed);
            nodePool = new ObjectPool<OctreeNode>(Mathf.CeilToInt(estimatedMaxObjectCount / numObjectsAllowed));
            removals = orphanObjects;
            this.looseness = loosenessVal;
            rootNode = nodePool.Pop();
            rootNode.Initialize(-1, this);
            rootNode.SetBounds(ref initialWorldPosition, Vector3.one * initialSize, looseness);
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

        public void DrawAll(bool drawNodes, bool drawObjects, bool drawConnections, bool drawLabels)
        {
            rootNode.DrawNode(true, drawNodes, drawObjects, drawConnections, drawLabels);
        }


    }
}