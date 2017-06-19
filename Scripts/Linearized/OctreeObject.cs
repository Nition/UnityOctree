using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace UnityOctree
{
    public partial class LooseOctree<T> where T : class
    {
        public class OctreeObject
        {
            public T obj; //Object reference held by this instance
            public FastBounds bounds;
            public bool isPoint; //Point data only, ignore extents
            private LooseOctree<T> tree;
            public uint locationCode; //Where in the tree this object is located

           public OctreeObject(LooseOctree<T> tree)
            {
                this.tree = tree;
            }

            public bool Remove()
            {
                return tree.nodes[locationCode].RemoveObject(this);
            }
        }
    }
}
