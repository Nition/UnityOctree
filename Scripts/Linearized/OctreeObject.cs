using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace UnityOctree
{
    public class OctreeObject<T> where T : class
    {
        public T obj; //Object reference held by this instance
        public FastBounds bounds;
        public bool isPoint; //Point data only, ignore extents
        private LooseOctree<T>.OctreeNode node; //Reference to the node this object is held by

        public void SetNode(LooseOctree<T>.OctreeNode node)
        {
            this.node = node;
        }
        public bool Remove()
        {
            return node.RemoveObject(this);
        }
    }
}
