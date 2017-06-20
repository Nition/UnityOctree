using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace UnityOctree
{
    public partial class LooseOctree<T> where T : class
    {
        public partial class OctreeNode
        {
            public uint locationCode;
            public FastBounds actualBounds;
            public bool isLeaf = true; //Leaf nodes have no children
            LooseOctree<T> tree;
            public List<OctreeObject> objects = new List<OctreeObject>();
            public int childExists = 0; //Bitmask for child nodes
            public void Initialize(uint locationCode, LooseOctree<T> tree)
            {
                this.locationCode = locationCode;
                this.tree = tree;
            }

            Queue<OctreeObject> removals = new Queue<OctreeObject>();
            //Adds a child node at index. If any existing objects fit, moves them into it
            private bool AddChild(uint index)
            {
                if (index > 7U)
                {
                    Debug.LogError("AddChild index must be 0-7");
                    return false;
                }
                if (CheckChild(childExists, index))
                {
                    Debug.LogError("This child already exists: " + index + ".");
                    return false;
                }
                uint newCode = ChildCode(locationCode, index);
                OctreeNode newNode = tree.nodePool.Pop();
                newNode.Initialize(newCode, tree);
                tree.nodes[newCode] = newNode;
                SetChildBounds(newNode);
                childExists = SetChild(childExists, index); //Set flag
                isLeaf = false;
                int objCount = objects.Count;
                if (objCount > 0)
                {
                    for (int i = 0; i < objCount; i++)
                    {
                        OctreeObject obj = objects[i];
                        if (BestFitChild(obj) == index)
                            if (newNode.TryAddObj(obj)) //Make sure the object fits in the new location
                                removals.Enqueue(obj);
                    }
                    while(removals.Count > 0)
                        objects.Remove(removals.Dequeue());

                }
                return true;
            }

            uint BestFitChild(OctreeObject obj)
            {
                Vector3 center = actualBounds.center;
                Vector3 objCenter = obj.bounds.center;
                return (objCenter.x <= center.x ? 0U : 1U) + (objCenter.y >= center.y ? 0U : 4U) + (objCenter.z <= center.z ? 0U : 2U);
            }

            /// <summary>
            /// Try to add an object, starting at this node
            /// Recursive method
            /// </summary>
            public bool TryAddObj(OctreeObject newObj)
            {
                if (!actualBounds.ContainsBounds(newObj.bounds))
                    return false;

                uint index = BestFitChild(newObj); //Find most likely child
                uint childCode = ChildCode(locationCode, index);
                if (objects.Count >= numObjectsAllowed && !CheckChild(childExists, index))
                    AddChild(index); //We're at or over the limit. Split into a new node if it doesn't exist.

                if (!isLeaf && CheckChild(childExists, index))
                { //We are in a non-leaf node and we have a suitable child. Try putting it there
                    if (tree.nodes[childCode].TryAddObj(newObj))
                        return true;
                }

                //Object didn't fit in children. Put it here.
                newObj.SetNode(this);
                objects.Add(newObj);
                return true;
            }

            public void SetAllChildBounds(bool recursive)
            {
                for (uint i = 0; i < 8U; i++)
                {
                    uint childCode = ChildCode(locationCode, i);
                    OctreeNode childNode = tree.nodes[childCode];
                    SetChildBounds(childNode); //Set bounds of each of this node's children
                    if (recursive)
                    {
                        if (!childNode.isLeaf) //Don't recurse into leaf nodes. They have no children to set
                            childNode.SetAllChildBounds(true);//Recursive resize. Keep dropping down
                    }
                }
            }

            //Re-set the bounds of child at index
            private void SetChildBounds(OctreeNode childNode)
            {
                uint index = GetIndex(childNode.locationCode);
                //-X,-Y,+Z = 0+4+2=6
                //+X,-Y,+Z = 1+4+2=7
                //+X,+Y,+Z = 1+0+2=3
                //-X,+Y,+Z = 0+0+2=2
                //-X,+Y,-Z = 0+0+0=0
                //+X,+Y,-Z = 1+0+0=1
                //+X,-Y,-Z = 1+4+0=5
                //-X,-Y,-Z = 0+4+0=4
                float quarter = (actualBounds.size.x / tree.looseness) * .25F;
                Vector3 pos = new Vector3();
                if (index == 4U || index == 5U || index == 7U || index == 6U)
                    pos.y = -quarter;
                else//if (index == 1U || index == 0U || index == 2U || index == 3U)
                    pos.y = quarter;
                if (index == 4U || index == 5U || index == 1U || index == 0U)
                    pos.z = -quarter;
                else//if (index == 2U || index == 3U || index == 7U || index == 6U)
                    pos.z = quarter;
                if (index == 4U || index == 0U || index == 2U || index == 6U)
                    pos.x = -quarter;
                else//if (index == 5U || index == 1U || index == 3U || index == 7U)
                    pos.x = quarter;
                float baseSize = (actualBounds.size.x / tree.looseness) * .5F;
                float looseSize = baseSize * tree.looseness;
                FastBounds childBounds = new FastBounds(actualBounds.center + pos, new Vector3(looseSize, looseSize, looseSize));
                childNode.actualBounds = childBounds;
            }

            public bool RemoveObject(OctreeObject obj)
            {
                bool removed = false;
                if (objects.Remove(obj))
                {
                    if (!MergeIfPossible() && locationCode != 1U) //If we can't merge and we aren't root, check if our parent can
                    {
                        uint parentCode = ParentCode(locationCode);
                        tree.nodes[parentCode].MergeIfPossible();
                    }
                    removed = true;
                }

                tree.objectPool.Push(obj);
                return removed;
            }

            private bool MergeIfPossible()
            {
                return true;
            }
            List<uint> childCodes = new List<uint>();
            /// <summary>
            /// Returns a list of every node below this one
            /// </summary>
            public bool GetAllChildCodes(uint startingLocation, List<uint> childCodes)
            {
                if (GetDepth(startingLocation) == maxDepth)
                    return false;
                for (uint i = 0; i < 8; i++)
                {
                    uint childCode = ChildCode(startingLocation, i);
                    childCodes.Add(childCode);
                    GetAllChildCodes(childCode, childCodes);
                }
                if (childCodes.Count > 0)
                    return true;

                return false;
            }
        }
    }
}