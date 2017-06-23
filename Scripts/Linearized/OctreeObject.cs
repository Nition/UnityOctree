using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace UnityOctree
{
    public class OctreeObject<T>:LooseOctree<T>.OctreeBoundedObject where T : class
    {
        public T obj; //Object reference held by this instance
        public bool isPoint; //Point data only, ignore extents
        public int listIndex;
        private LooseOctree<T>.OctreeNode node; //Reference to the node this object is held by
        public OctreeObject<T> next = null;
        public OctreeObject<T> previous = null;
        public void SetNode(LooseOctree<T>.OctreeNode node)
        {
            this.node = node;
        }
        public void Remove()
        {
            node.RemoveObject(this);
        }
    }
}
