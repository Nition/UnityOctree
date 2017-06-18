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
            private Bounds _bounds;
            public Bounds bounds
            {
                get { return _bounds; }
                set
                {
                    _bounds = value;
                    maxBounds = _bounds.max;
                    minBounds = _bounds.min;
                    center = _bounds.center;
                    size = bounds.extents * 2F;
                }
            }



            public Vector3 maxBounds;
            public Vector3 minBounds;
            public Vector3 center;
            public Vector3 size;
            public bool isPoint; //Point data only, ignore extents
            public uint locationCode; //Where in the tree this object is located
                                      /// <summary>
                                      /// Remove this object from the tree
                                      /// </summary>
            public bool Remove()
            {
                return true;
            }
        }
    }
}
