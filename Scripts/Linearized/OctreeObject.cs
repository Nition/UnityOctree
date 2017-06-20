using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace UnityOctree
{
    //Helper class for external access. Makes syntax a little easier to read
    //OctreeObject<GameObject> instead of LooseOctree<GameObject>.OctreeObject
    public class OctreeObject<T>:LooseOctree<T>.OctreeObject  where T : class
    {

    }
    public partial class LooseOctree<T> where T : class
    {
        public class OctreeObject
        {
            public T obj; //Object reference held by this instance
            public FastBounds bounds;
            public bool isPoint; //Point data only, ignore extents
            private OctreeNode node; //Reference to the node this object is held by

            public void SetNode(OctreeNode node)
            {
                this.node = node;
            }
            public bool Remove()
            {
                return node.RemoveObject(this);
            }
        }
    }
}
