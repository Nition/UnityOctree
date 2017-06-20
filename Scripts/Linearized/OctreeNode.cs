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
            LooseOctree<T> tree;
            Vector3 vCopy = new Vector3(); //Micro opt
            FastBounds fbCopy = new FastBounds(Vector3.zero, Vector3.zero);
            public List<OctreeObject<T>> objects = new List<OctreeObject<T>>(numObjectsAllowed * 2);
            public int branchItemCount;
            public int childExists = 0; //Bitmask for child nodes
            public void Initialize(uint locationCode, LooseOctree<T> tree)
            {
                this.locationCode = locationCode;
                this.tree = tree;
            }

            Queue<OctreeObject<T>> removals = new Queue<OctreeObject<T>>(numObjectsAllowed * 2);
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
                uint newCode;
                OctreeNode newNode = tree.nodePool.Pop();
                newNode.Initialize(newCode = ChildCode(locationCode, index), tree);
                tree.nodes[newCode] = newNode; //Add to dictionary
                SetChildBounds(newNode);
                childExists = SetChild(childExists, index); //Set flag
                int objCount;
                if ((objCount = objects.Count) > 0)
                {
                    for (int i = 0; i < objCount; i++)
                    {
                        OctreeObject<T> obj;
                        if (BestFitChild(obj = objects[i]) == index)
                            if (newNode.TryAddObj(obj, false))//Make sure the object fits in the new location
                                removals.Enqueue(obj);
                    }
                    while (removals.Count > 0)
                    {
                        objects.Remove(removals.Dequeue());
                        branchItemCount++; //Item was added below us, so our branch count needs to go up
                    }

                }
                return true;
            }

            uint BestFitChild(OctreeObject<T> obj)
            {
                Vector3 objCenter, center;
                return ((objCenter = obj.bounds.center).x <= (center = actualBounds.center).x ? 0U : 1U) + (objCenter.y >= center.y ? 0U : 4U) + (objCenter.z <= center.z ? 0U : 2U);
            }

            /// <summary>
            /// Try to add an object, starting at this node
            /// </summary>
            public bool TryAddObj(OctreeObject<T> newObj, bool updateCount)
            {
                if (!actualBounds.ContainsBounds(ref newObj.bounds))
                    return false; //Doesn't fit. Abort off the bat.

                uint childCode = locationCode; //Start here
                OctreeNode childNode;
                uint index;
                while (tree.nodes.TryGetValue(childCode, out childNode))
                {
                    if (!childNode.actualBounds.ContainsBounds(ref newObj.bounds))
                    { //Doesn't fit in this node. Put it in the parent node
                        if (childCode == 1U)//We are root. Abort
                            return false;
                        tree.nodes[ParentCode(childCode)].PutObjectInNode(newObj, updateCount);
                        return true;
                    }

                    if (!CheckChild(childNode.childExists, index = childNode.BestFitChild(newObj)) && childNode.objects.Count >= numObjectsAllowed)
                        childNode.AddChild(index);//We're over the limit and no child exists here yet. Make one

                    if (CheckChild(childNode.childExists, index))
                    { //Drop down another level
                        childCode = ChildCode(childCode, childNode.BestFitChild(newObj));
                    }
                    else
                    { //Place it here. We have no children and we're under the limit
                        childNode.PutObjectInNode(newObj, updateCount);
                        return true;
                    }
                }
                return false;
            }
            private void PutObjectInNode(OctreeObject<T> obj, bool updateCount)
            {
                obj.SetNode(this);
                objects.Add(obj);
                if (updateCount)
                    UpdateBranchCount(true);
            }
            Queue<OctreeObject<T>> orphanObjects = new Queue<OctreeObject<T>>(numObjectsAllowed*8);
            /// <summary>
            /// Traverse up the tree and update counts for all nodes.
            /// If a branch has zero objects in it, it will be merged
            /// </summary>
            /// <param name="addedItem"></param>
            public void UpdateBranchCount(bool addedItem)
            {
                if (locationCode == 1U)
                    return; //This is the root. We have no parents to update

                OctreeNode parentNode;
                OctreeNode topLevel = null;
                uint parentCode = ParentCode(locationCode);
                while (tree.nodes.TryGetValue(parentCode, out parentNode))
                {
                    if (addedItem)
                        parentNode.branchItemCount++;
                    else if (parentNode.branchItemCount-- < numObjectsAllowed)
                        topLevel = parentNode; //Record our path as long as we're below the item limit

                    if (parentCode == 1U)
                        break; //We're already at root. Can't go up anymore
                    else
                        parentCode = ParentCode(parentNode.locationCode); //Step up another level
                }

                if (topLevel != null)
                    topLevel.MergeNode(orphanObjects);
            }
            //Merge all children into this node
            public void MergeNode(Queue<OctreeObject<T>> orphanObjects)
            {
                if (childExists == 0)//No children to merge
                    return;

                for (uint i = 0; i < 8; i++)
                {
                    if (!CheckChild(childExists, i))
                        continue; //Make sure child exists

                    uint childCode;
                    OctreeNode childNode = tree.nodes[childCode = ChildCode(locationCode, i)];
                    if (childNode.branchItemCount > numObjectsAllowed)
                        continue; //Can't merge this child

                    childNode.MergeNode(orphanObjects);
                    //Add objects to orphaned list
                    List<OctreeObject<T>> childObjects = childNode.objects;
                    int childCount = childObjects.Count;
                    for (int o = 0; o < childCount; o++)
                        orphanObjects.Enqueue(childObjects[o]);

                    childExists = UnsetChild(childExists, i);

                    childObjects.Clear();//Clear out list
                    tree.nodePool.Push(childNode);//Push node instance back to the pool
                    tree.nodes.Remove(childCode);//Remove from dictionary
                }

                while (orphanObjects.Count > 0)
                {
                    branchItemCount--; //Items are now in this node which doesn't count towards our branch count
                    PutObjectInNode(orphanObjects.Dequeue(), false);
                }
            }
            public void SetAllChildBounds(bool recursive)
            {
                for (uint i = 0; i < 8U; i++)
                {
                    uint childCode;
                    OctreeNode childNode = tree.nodes[childCode = ChildCode(locationCode, i)];
                    SetChildBounds(childNode); //Set bounds of each of this node's children
                    if (recursive)
                    {
                        if (childNode.childExists != 0) //Don't recurse into leaf nodes. They have no children to set
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
                Vector3 pos = vCopy;
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
                float looseSize = ((actualBounds.size.x / tree.looseness) * .5F) * tree.looseness;
                Vector3 vLooseSize = vCopy;
                vLooseSize.x = looseSize;
                vLooseSize.y = looseSize;
                vLooseSize.z = looseSize;
                pos.x += actualBounds.center.x;
                pos.y += actualBounds.center.y;
                pos.z += actualBounds.center.z;
                FastBounds childBounds = fbCopy.SetValues(ref pos, ref vLooseSize);
                childNode.actualBounds = childBounds;
            }

            public bool RemoveObject(OctreeObject<T> obj)
            {
                bool removed = false;
                if (objects.Remove(obj))
                {
                    UpdateBranchCount(false);
                    removed = true;
                }

                tree.objectPool.Push(obj);
                return removed;
            }
        }
    }
}