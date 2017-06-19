using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace UnityOctree
{
    public partial class LooseOctree<T> where T : class
    {
        private partial class OctreeNode
        {
            public uint locationCode;
            public FastBounds actualBounds;
            public bool isLeaf; //Leaf nodes have no children
            LooseOctree<T> tree;
            public List<OctreeObject> objects;
            public uint childExists = 0;
            public OctreeNode(uint locationCode, LooseOctree<T> tree)
            {
                this.objects = new List<OctreeObject>();
                this.locationCode = locationCode;
                this.isLeaf = true;
                this.tree = tree;
            }

            //Adds a blank child node at index
            private bool AddChild(uint index)
            {
                if (index > 7U)
                {
                    Debug.LogError("AddChild index must be 0-7");
                    return false;
                }
                uint newCode = ChildCode(locationCode, index);
                if (tree.nodes.ContainsKey(newCode))
                {
                    Debug.LogError("Location code already assigned: " + GetStringCode(newCode) + "(" + newCode + ")");
                    return false;
                }
                OctreeNode newNode = new OctreeNode(newCode, tree);
                tree.nodes[newCode] = newNode; //Record in dictionary
                SetChildBounds(newNode);
                childExists = SetChild(childExists, index);
                isLeaf = false;
                for (int i = 0; i < objects.Count; i++)
                {
                    OctreeObject obj = objects[i];
                    if (BestFitChild(obj) == index)
                    {
                        if (newNode.TryAddObj(obj))
                        {
                            obj.locationCode = newCode;
                            removals.Add(obj);
                            newNode.objects.Add(obj);
                        }
                    }
                }
                for (int i = 0; i < removals.Count; i++)
                {
                    objects.Remove(removals[i]);
                }
                removals.Clear();
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
                { //We're at or over the limit. Split into a new node
                    AddChild(index);
                }

                if (!isLeaf && CheckChild(childExists,index))
                { //We are in a non-leaf node and we have a child. Try putting it there
                    if (tree.nodes[childCode].TryAddObj(newObj))
                        return true;
                }

                //Object didn't fit in children. Put it here.
                //This may make us go over numObjectsAllowed, but it's the easiest
                //way to handle objects on borders.

                newObj.locationCode = locationCode;
                objects.Add(newObj);
                return true;
            }

            public void SetAllChildBounds(bool recursive)
            {
                for (uint i = 0; i < 8U; i++)
                {
                    OctreeNode childNode = tree.nodes[ChildCode(locationCode, i)];
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
                float quarter = (actualBounds.size.x / tree.looseness) / 4F;
                Vector3 pos = new Vector3();
                if (index == 4U || index == 5U || index == 7U || index == 6U)
                    pos.y = -quarter;
                if (index == 4U || index == 5U || index == 1U || index == 0U)
                    pos.z = -quarter;
                if (index == 2U || index == 3U || index == 7U || index == 6U)
                    pos.z = quarter;
                if (index == 1U || index == 0U || index == 2U || index == 3U)
                    pos.y = quarter;
                if (index == 4U || index == 0U || index == 2U || index == 6U)
                    pos.x = -quarter;
                if (index == 5U || index == 1U || index == 3U || index == 7U)
                    pos.x = quarter;
                float baseSize = (actualBounds.size.x / tree.looseness) / 2F;
                float looseSize = baseSize * tree.looseness;
                FastBounds childBounds = new FastBounds(actualBounds.center + pos, new Vector3(looseSize, looseSize, looseSize));
                childNode.actualBounds = childBounds;
            }

            public bool RemoveObject(OctreeObject obj)
            {
                if (objects.Remove(obj))
                {
                    if (!MergeIfPossible() && locationCode != 1U) //If we can't merge and we aren't root, check if our parent can
                        tree.nodes[ParentCode(locationCode)].MergeIfPossible();

                    return true;
                }
                return false;
            }

            private bool MergeIfPossible()
            {
                return false;
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

            List<OctreeObject> removals = new List<OctreeObject>();
            //Split this node into 8 children
            public bool Split()
            {
                if (GetDepth(locationCode) == maxDepth)
                { //We're maxed out on depth. Let's raise the max allowance and re-add our objects
                    Debug.LogWarning("Octree depth has reached the limit (" + maxDepth + "). Increasing max number of objects from " + numObjectsAllowed + " to " + numObjectsAllowed + 1 + ".");
                    numObjectsAllowed++;
                    foreach (OctreeObject obj in tree.GetAllObjects(true))
                    {//Take all objects out of the tree and re-distribute them.
                        tree.nodes[1U].TryAddObj(obj); //Add from the top
                    }
                    return false;
                }

                for (uint i = 0U; i < 8U; i++)
                {
                    if (!AddChild(i))
                        return false;
                }
                SetAllChildBounds(false);
                isLeaf = false; //We are now a branch, not a leaf
                int objCount = objects.Count;
                if (objCount > 0) //We have objects that need to be distributed
                {
                    for (int i = 0; i < objCount; i++)
                    {
                        OctreeObject obj = objects[i];
                        uint index = BestFitChild(obj);
                        OctreeNode childNode = tree.nodes[ChildCode(locationCode, index)];
                        if (!childNode.actualBounds.ContainsBounds(obj.bounds))
                            continue; //Doesn't fit here. Can't move it
                        childNode.objects.Add(obj);
                        obj.locationCode = childNode.locationCode;
                        removals.Add(obj);
                    }
                    int removalCount = removals.Count;
                    if (removalCount > 0)
                    {
                        for (int i = 0; i < removalCount; i++)
                        {
                            objects.Remove(removals[i]);
                        }
                        removals.Clear();
                    }
                }
                return true;
            }
        }
    }
}