using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace UnityOctree
{
    public partial class LooseOctree<T> where T : class
    {
        public partial class OctreeNode : OctreeBoundedObject
        {
            public uint locationCode;
            LooseOctree<T> tree;
            public List<OctreeObject<T>> objects = new List<OctreeObject<T>>(numObjectsAllowed * 2);
            Queue<OctreeObject<T>> orphanObjects;
            Queue<OctreeObject<T>> removals;
            OctreeNode parent; //This node's immediate parent
            public int branchItemCount;
            public int localItemCount;
            bool isLeaf; //Leaves have no children
            public int childHasObjects = 0; //Bitmask for child nodes
            public void Initialize(uint locationCode, LooseOctree<T> tree)
            {
                orphanObjects = tree.orphanObjects;
                removals = tree.removals;
                branchItemCount = 0;
                localItemCount = 0;
                childHasObjects = 0;
                parent = null;
                isLeaf = true;
                this.locationCode = locationCode;
                this.tree = tree;
            }

            private void ChildHasObjects(uint index, bool set)
            {
                if (set)
                    childHasObjects = SetChild(childHasObjects, index);
                else
                    childHasObjects = UnsetChild(childHasObjects, index);

            }
            private bool ChildHasObjects(uint index)
            {
                return CheckChild(childHasObjects, index);
            }

            //Adds a child node at index. If any existing objects fit, moves them into it
            private void Split()
            {
                Debug.Assert(isLeaf, "Trying to split a non-leaf node.");
                Debug.Assert(GetDepth(locationCode) < maxDepth, "Max depth reached. This will cause integer overflow. Recommend increasing numObjectsAllowed or switching to 64bit integers.");
                Debug.Assert(removals.Count == 0, "removals queue is not empty when it should be");

                uint newCode, index;
                int objCount, i;
                OctreeNode childNode;
                for (index = 0; index < 8; index++)
                {
                    childNode = tree.nodePool.Pop();
                    childNode.Initialize(newCode = ChildCode(locationCode, index), tree);
                    tree.nodes[newCode] = childNode; //Add to dictionary
                    childNode.parent = this;
                    childNode.ResetBounds();
                }

                if (localItemCount > 0) //Place objects into the new nodes
                {
                    for (i = 0; i < localItemCount; i++)
                    {
                        OctreeObject<T> obj;
                        childNode = tree.nodes[ChildCode(locationCode, index = BestFitChild(ref (obj = objects[i]).boundsCenter))];
                        if (childNode.ContainsBounds(ref obj.boundsMin, ref obj.boundsMax))//Make sure the object fits in the new location. Otherwise keep it here
                        {
                            childNode.PutObjectInNode(obj, false);
                            ChildHasObjects(index, true);
                            removals.Enqueue(obj);
                        }
                    }
                }

                objCount = removals.Count;
                branchItemCount += objCount;//Item was added below us, so our branch count needs to go up
                localItemCount -= objCount;
                if (localItemCount == 0 && locationCode != 1U)
                {//Let our parent know we ran out of objects
                    parent.ChildHasObjects(GetIndex(locationCode), false);
                    objects.Clear(); //We moved everything
                    removals.Clear();
                }
                else
                {
                    while (objCount > 0)
                    {
                        objects.Remove(removals.Dequeue());
                        objCount--;
                    }
                }
                isLeaf = false; //We are now a branch
            }

            uint BestFitChild(OctreeObject<T> obj)
            {
                return BestFitChild(ref obj.boundsCenter);
            }

            uint BestFitChild(ref Vector3 objCenter)
            {
                return (objCenter.x <= boundsCenter.x ? 0U : 1U) + (objCenter.y >= boundsCenter.y ? 0U : 4U) + (objCenter.z <= boundsCenter.z ? 0U : 2U);
            }

            public void RebuildTree()
            {
                OctreeNode rootNode = tree.rootNode;
                //Merge from the root
                rootNode.MergeNode();
                rootNode.branchItemCount = 0;
                int itemCount = orphanObjects.Count;
                while (itemCount > 0)
                {
                    if (!rootNode.TryAddObj(orphanObjects.Dequeue(), true))
                    {
                        Debug.LogError("Rebuild failed. Could not re-add objects");
                        return;
                    }
                    itemCount--;
                }
            }

            //Find the lowest node that fully contains the given bounds
            private bool FindFittingNode(ref Vector3 center, ref Vector3 minBounds, ref Vector3 maxBounds, out OctreeNode node)
            {
                node = null;
                if (!ContainsBounds(ref minBounds, ref maxBounds))
                    return false;

                uint childCode = locationCode;//Start here
                OctreeNode childNode;
                var nodes = tree.nodes;
                bool firstLoop = true;
                while (nodes.TryGetValue(childCode, out childNode))
                {
                    if (!firstLoop && !childNode.ContainsBounds(ref minBounds, ref maxBounds))
                    { //Doesn't fit in this node. Put it in the parent node. If it's the firstLoop, we already checked this.
                        node = childNode.parent;
                        return true;
                    }

                    if (!childNode.isLeaf)
                    { //Drop down another level if we have a valid child
                        childCode = ChildCode(childCode, childNode.BestFitChild(ref center));
                    }
                    else
                    { //No children
                        node = childNode;
                        return true;
                    }

                    firstLoop = false;
                }

                return false;
            }
            /// <summary>
            /// Try to add an object, starting at this node
            /// </summary>
            public bool TryAddObj(OctreeObject<T> newObj, bool updateCount)
            {
                if (!ContainsBounds(ref newObj.boundsMin, ref newObj.boundsMax))
                    return false; //Doesn't fit. Abort off the bat.

                OctreeNode childNode = null;
                uint childCode = locationCode; //Start here
                uint index;
                var nodes = tree.nodes;
                bool firstLoop = true;
                while (nodes.TryGetValue(childCode, out childNode))
                {
                    if (!firstLoop && !childNode.ContainsBounds(ref newObj.boundsMin, ref newObj.boundsMax))
                    { //Doesn't fit in this node. Put it in the parent node. If it's the firstLoop, we already checked this.
                        childNode.parent.PutObjectInNode(newObj, updateCount);
                        return true;
                    }

                    index = childNode.BestFitChild(ref newObj.boundsCenter);
                    if (childNode.isLeaf && childNode.localItemCount >= numObjectsAllowed)
                        childNode.Split();//We hit the limit and we can split

                    if (!childNode.isLeaf)
                    { //Drop down another level if we have children
                        childCode = ChildCode(childCode, index);
                    }
                    else
                    { //Place it here. We have no children and we're under the limit
                        childNode.PutObjectInNode(newObj, updateCount);
                        return true;
                    }
                    firstLoop = false;
                }
                return false;
            }

            private void PutObjectInNode(OctreeObject<T> obj, bool updateCount)
            {
                obj.SetNode(this);
                objects.Add(obj);

                if (localItemCount == 0 && locationCode != 1U) //Let our parent know we have objects
                    parent.ChildHasObjects(GetIndex(locationCode), true);

                localItemCount++;

                if (updateCount)
                    UpdateBranchCount(true);
            }

            /// <summary>
            /// Traverse up the tree and update counts for all nodes.
            /// If a branch has zero objects in it, it will be merged
            /// </summary>
            /// <param name="addedItem"></param>
            public void UpdateBranchCount(bool addedItem)
            {
                if (locationCode == 1U)
                    return; //This is the root. We have no parents to update

                OctreeNode parentNode = parent;
                OctreeNode topLevel = null;
                var nodes = tree.nodes;
                while (true)
                {
                    if (addedItem)
                        parentNode.branchItemCount++;
                    else if (parentNode.branchItemCount-- + parentNode.localItemCount <= numObjectsAllowed)
                        topLevel = parentNode; //Record our path as long as we're not above the item limit

                    if (parentNode.locationCode == 1U)
                        break; //We're already at root. Can't go up anymore
                    else
                        parentNode = parentNode.parent;
                }
#if UNITY_EDITOR
                if (parentNode == null)
                {
                    Debug.Assert(parentNode != null, "ParentNode is null. This shouldn't happen");
                    throw new System.InvalidOperationException();
                }
#endif
                Debug.Assert(orphanObjects.Count == 0, "orphanObjects queue is not empty when it should be");
                if (topLevel != null)
                {
                    topLevel.MergeNode();
                    int objCount = orphanObjects.Count;
                    topLevel.branchItemCount -= objCount; //Items are now in this node which doesn't count towards our branch count
                    while (objCount > 0)
                    {
                        topLevel.PutObjectInNode(orphanObjects.Dequeue(), false);
                        objCount--;
                    }
                    Debug.Assert(localItemCount <= numObjectsAllowed, "Merged too many objects into one node!");
                }
            }
            //Merge all children into this node
            public void MergeNode()
            {
                if (isLeaf)//No children to merge
                    return;

                uint index;
                var nodes = tree.nodes;
                ObjectPool<OctreeNode> nodePool = tree.nodePool;
                for (index = 0U; index < 8U; index++)
                {
                    uint childCode;
                    OctreeNode childNode = nodes[childCode = ChildCode(locationCode, index)];
                    if (!childNode.isLeaf)
                        childNode.MergeNode();
                    if (childHasObjects != 0 && ChildHasObjects(index))
                    {
                        Debug.Assert(childNode.objects.Count > 0, "Attempting to merge child with no objects");
                        //Add objects to orphaned list
                        List<OctreeObject<T>> childObjects = childNode.objects;
                        int childCount = childObjects.Count;
                        int o;
                        for (o = 0; o < childCount; o++)
                            orphanObjects.Enqueue(childObjects[o]);

                        childObjects.Clear();//Clear out list
                    }
                    nodePool.Push(childNode);//Push node instance back to the pool
                    nodes.Remove(childCode);//Remove from dictionary
                }
                childHasObjects = 0; //Children are all gone
                isLeaf = true; //We are now a leaf
            }

            //Re-set the bounds of this node to fit the parent
            private void ResetBounds()
            {
                Debug.Assert(locationCode != 1, "Cannot call ResetBounds on root node.");
                uint index = GetIndex(locationCode);
                //-X,-Y,+Z = 0+4+2=6
                //+X,-Y,+Z = 1+4+2=7
                //+X,+Y,+Z = 1+0+2=3
                //-X,+Y,+Z = 0+0+2=2
                //-X,+Y,-Z = 0+0+0=0
                //+X,+Y,-Z = 1+0+0=1
                //+X,-Y,-Z = 1+4+0=5
                //-X,-Y,-Z = 0+4+0=4
                Vector3 parentBase = parent.baseSize;
                Vector3 parentCenter = parent.boundsCenter;
                float quarter = parentBase.x * .25F;
                Vector3 pos = vCopy;
                if (index == 4U || index == 5U || index == 7U || index == 6U)
                    pos.y = -quarter;
                else//(index == 1U || index == 0U || index == 2U || index == 3U)
                    pos.y = quarter;
                if (index == 4U || index == 5U || index == 1U || index == 0U)
                    pos.z = -quarter;
                else//(index == 2U || index == 3U || index == 7U || index == 6U)
                    pos.z = quarter;
                if (index == 4U || index == 0U || index == 2U || index == 6U)
                    pos.x = -quarter;
                else//(index == 5U || index == 1U || index == 3U || index == 7U)
                    pos.x = quarter;
                pos.x += parentCenter.x;
                pos.y += parentCenter.y;
                pos.z += parentCenter.z;
                Vector3 newSize = vCopy;
                newSize.x = parentBase.x * .5F;
                newSize.y = parentBase.y * .5F;
                newSize.z = parentBase.z * .5F;
                SetBounds(ref pos, newSize, tree.looseness);
            }
#if UNITY_EDITOR
            private bool FindObjectInTree(OctreeObject<T> obj)
            {
                foreach (KeyValuePair<uint, OctreeNode> node in tree.nodes)
                {
                    if (node.Value.objects.Contains(obj))
                        return true;
                }
                return false;
            }
#endif
            public void RemoveObject(OctreeObject<T> obj)
            {
                bool removed = objects.Remove(obj);
                localItemCount--;
                if (localItemCount == 0 && locationCode != 1U) //Let our parent know we ran out of objects
                    parent.ChildHasObjects(GetIndex(locationCode), false);
                UpdateBranchCount(false);
                tree.objectPool.Push(obj);
#if UNITY_EDITOR
                if (locationCode == 1U && isLeaf == true)
                {
                    Debug.Assert(tree.nodes.Count == 1, "Node count >1, orphaned nodes in tree.");
                    Debug.Assert(branchItemCount == 0, "Root has a branch count but it has no children.");
                }
                if (!removed)
                {
                    Debug.LogError("Failed to remove object. Did you call remove on an already-removed object? Found in tree: " + FindObjectInTree(obj));
                    throw new System.InvalidOperationException();
                }
#endif
            }
        }
    }
}