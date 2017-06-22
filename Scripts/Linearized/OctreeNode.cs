using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace UnityOctree
{
    public partial class LooseOctree<T> where T : class
    {
        public partial class OctreeNode : OctreeBoundedObject
        {
            LooseOctree<T> tree;
            //public List<OctreeObject<T>> objects = new List<OctreeObject<T>>(numObjectsAllowed * 2);
            public OctreeObject<T>[] objects = new OctreeObject<T>[numObjectsAllowed];
            Stack<OctreeObject<T>> orphanObjects;
            OctreeNode parent; //This node's immediate parent
            OctreeNode[] children = new OctreeNode[8];
            int childIndex = -1;
            int branchItemCount = 0;
            int localItemCount = 0;
            int treeDepth = 0;
            bool isLeaf; //Leaves have no children
            private int childHasObjects; //Bitmask for child nodes
            public void Initialize(int index, LooseOctree<T> tree, OctreeNode parent)
            {
                Initialize(index, tree);
                this.parent = parent;
                treeDepth = parent.treeDepth + 1;
            }
            public void Initialize(int index, LooseOctree<T> tree)
            {
                orphanObjects = tree.orphanObjects;
                branchItemCount = 0;
                localItemCount = 0;
                treeDepth = 0;
                childHasObjects = 0;
                parent = null;
                isLeaf = true;
                childIndex = index;
                //this.locationCode = locationCode;
                this.tree = tree;
            }
            public void DrawNode(bool recursive, bool drawNodes, bool drawObjects, bool drawConnections, bool drawLabels)
            {
                DrawOne(drawNodes, drawObjects, drawConnections, drawLabels);
                if (!isLeaf)
                    for (int index = 0; index < 8; index++)
                    {
                        OctreeNode childNode = children[index];
                        if (recursive && !childNode.isLeaf)
                            childNode.DrawNode(recursive, drawNodes, drawObjects, drawConnections, drawLabels);
                        else
                            childNode.DrawOne(drawNodes, drawObjects, drawConnections, drawLabels);
                    }
            }
            private void DrawOne(bool drawNodes, bool drawObjects, bool drawConnections, bool drawLabels)
            {
                float tintVal = treeDepth / 7F; // Will eventually get values > 1. Color rounds to 1 automatically
                Gizmos.color = new Color(tintVal, 0F, 1.0f - tintVal);
                if (drawNodes)
                    Gizmos.DrawWireCube(boundsCenter, boundsSize);
#if UNITY_EDITOR
                if (drawLabels)
                    UnityEditor.Handles.Label(boundsCenter, "Depth(" + treeDepth + ") : Branch(" + branchItemCount + ") : Here(" + localItemCount + ")");
#endif
                Gizmos.color = new Color(tintVal, childIndex / 7F, 1.0f - tintVal);
                for (int o = 0; o < localItemCount; o++)
                {
                    OctreeObject<T> obj = objects[o];
                    if (drawObjects)
                    {
                        if (obj.isPoint)
                            Gizmos.DrawSphere(obj.boundsCenter, 0.25F);
                        else
                            Gizmos.DrawCube(obj.boundsCenter, obj.boundsSize);
                    }
                    if (drawConnections)
                        Gizmos.DrawLine(boundsCenter, obj.boundsCenter);
                }
                Gizmos.color = Color.white;
            }

            private void ChildHasObjects(int index, bool set)
            {
                if (set)
                    childHasObjects |= 1 << (index + 1);
                else
                    childHasObjects &= ~1 << (index + 1);

                Debug.Assert(ChildHasObjects(index) == set, "Setting mask failed");

            }
            private bool ChildHasObjects(int index)
            {
                return (childHasObjects & 1 << (index + 1)) != 0;
            }

            //Adds a child node at index. If any existing objects fit, moves them into it
            private void Split()
            {
                Debug.Assert(isLeaf, "Trying to split a non-leaf node.");
                //Debug.Assert(GetDepth(locationCode) < maxDepth, "Max depth reached. This will cause integer overflow. Recommend increasing numObjectsAllowed or switching to 64bit integers.");
                //Debug.Assert(removals.Count == 0, "removals queue is not empty when it should be");

                int objCount = 0;
                int i, index;
                OctreeNode childNode;
                for (index = 0; index < 8; index++)
                {
                    childNode = tree.nodePool.Pop();
                    childNode.Initialize(index, tree, this);
                    children[index] = childNode;
                    childNode.ResetBounds();
                }
                tree.nodeCount += 8;
                if (localItemCount > 0) //Place objects into the new nodes
                {
                    for (i = 0; i < localItemCount; i++)
                    {
                        OctreeObject<T> obj;
                        childNode = children[BestFitChild(ref (obj = objects[i]).boundsCenter)];
                        Debug.Assert(obj != null, "Attempting to split null object into child node");
                        if (childNode.ContainsBounds(ref obj.boundsMin, ref obj.boundsMax))//Make sure the object fits in the new location. Otherwise keep it here
                        {
                            childNode.PutObjectInNode(obj, false);
                            ChildHasObjects(index, true);
                            objCount++;
                            //removals.Push(obj);
                        }
                    }
                }

                branchItemCount += objCount;//Item was added below us, so our branch count needs to go up
                localItemCount -= objCount;
                if (localItemCount == 0)
                {//Let our parent know we ran out of objects
                    if (childIndex != -1)
                        parent.ChildHasObjects(childIndex, false);
                    //objects.Clear(); //We moved everything
                }
                //else
                //{
                //    while (objCount > 0)
                //    {
                //        objects[removals.Pop().listIndex] = null;
                //        objCount--;
                //    }
                //}
                isLeaf = false; //We are now a branch
            }

            int BestFitChild(ref Vector3 objCenter)
            {
                return (objCenter.x <= boundsCenter.x ? 0 : 1) + (objCenter.y >= boundsCenter.y ? 0 : 4) + (objCenter.z <= boundsCenter.z ? 0 : 2);
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
                    if (!rootNode.TryAddObj(orphanObjects.Pop(), true))
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
                return false;
            }
            /// <summary>
            /// Try to add an object, starting at this node
            /// </summary>
            public bool TryAddObj(OctreeObject<T> newObj, bool updateCount)
            {
                OctreeNode childNode = this;//Start here
                int index;
                while (true)
                {
                    if (!childNode.ContainsBounds(ref newObj.boundsMin, ref newObj.boundsMax))
                    { //Doesn't fit in this node. Put it in the parent node.
                        if (childNode.childIndex == -1)
                            return false;
                        childNode.parent.PutObjectInNode(newObj, updateCount);
                        return true;
                    }

                    index = childNode.BestFitChild(ref newObj.boundsCenter);
                    if (childNode.isLeaf && childNode.localItemCount >= numObjectsAllowed)
                        childNode.Split();//We hit the limit and we can split

                    if (!childNode.isLeaf)
                    { //Drop down another level if we have children
                        childNode = childNode.children[index];
                    }
                    else
                    { //Place it here. We have no children and we're under the limit
                        childNode.PutObjectInNode(newObj, updateCount);
                        return true;
                    }
                }
            }

            private void PutObjectInNode(OctreeObject<T> obj, bool updateCount)
            {
                Debug.Assert(obj != null, "Attempting to put null into node");
                obj.SetNode(this);
                int i = 0;
                for (i=0; i <= localItemCount; i++)
                {//Find the first empty slot
                    if (objects[i] == null)
                    break;
                }
                objects[i] = obj;
                obj.listIndex = i;
                Debug.Assert(localItemCount >= 0, "localItemCount is negative");

                if (localItemCount == 0 && childIndex != -1) //Let our parent know we have objects
                    parent.ChildHasObjects(childIndex, true);

                localItemCount++;

                if (updateCount)
                    UpdateBranchCount(true);
            }

            /// <summary>
            /// Traverse up the tree and update counts for all nodes.
            /// If a branch has < numObjectsAllowed in it, it will be merged
            /// </summary>
            /// <param name="addedItem"></param>
            public void UpdateBranchCount(bool addedItem)
            {
                if (childIndex == -1)
                    return; //This is the root. We have no parents to update

                OctreeNode parentNode = parent;
                OctreeNode topLevel = null;
                while (true)
                {
                    if (addedItem)
                        parentNode.branchItemCount++;
                    else if (parentNode.branchItemCount-- + parentNode.localItemCount <= numObjectsAllowed)
                        topLevel = parentNode; //Record our path as long as we're not above the item limit

                    if (parentNode.childIndex == -1)
                        break; //We're already at root. Can't go up anymore
                    else
                        parentNode = parentNode.parent;
                }
#if UNITY_EDITOR
                if (parentNode == null)
                {
                    Debug.Assert(parentNode != null, "parentNode is null. This shouldn't happen");
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
                        Debug.Assert(orphanObjects.Peek() != null, "Null object in orphanObjects");
                        topLevel.PutObjectInNode(orphanObjects.Pop(), false);
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

                OctreeNode childNode;
                int index;
                for (index = 0; index < 8; index++)
                {
                    if (!(childNode = children[index]).isLeaf)
                        childNode.MergeNode();
                    if (childHasObjects != 0 && ChildHasObjects(index))
                    {
                        Debug.Assert(childNode.localItemCount != 0, "Node item count does not match mask. Showing " + childNode.localItemCount + " objects with a mask of " + childHasObjects);
                        //Add objects to orphaned list
                        int childCount = childNode.localItemCount;
                        int i;
                        for (i = 0; i < childCount; i++)
                        {
                            Debug.Log(objects[i] + " : " + i);
                            Debug.Assert(objects[i] != null, "Null object found in node's list");
                            Debug.Assert(orphanObjects.Count > 0 && orphanObjects.Peek() != null, "Pushed null onto orphan queue");
                            orphanObjects.Push(childNode.objects[i]);
                        }
                        //childNode.objects.Clear();//Clear out list
                    }
                    tree.nodePool.Push(childNode);//Push node instance back to the pool
                }
                tree.nodeCount -= 8;
                childHasObjects = 0; //Children are all gone
                isLeaf = true; //We are now a leaf
            }

            //Re-set the bounds of this node to fit the parent
            private void ResetBounds()
            {
                Debug.Assert(childIndex != -11, "Cannot call ResetBounds on root node.");
                Vector3 parentBase = parent.baseSize;
                Vector3 parentCenter = parent.boundsCenter;
                float quarter = parentBase.x * .25F;
                Vector3 pos = vCopy;
                int index = childIndex;
                switch (childIndex)
                {
                    case (0)://-X,+Y,-Z = 0+0+0=0
                        pos.x = -quarter;
                        pos.y = quarter;
                        pos.z = -quarter;
                        break;
                    case (1)://+X,+Y,-Z = 1+0+0=1
                        pos.x = quarter;
                        pos.y = quarter;
                        pos.z = -quarter;
                        break;
                    case (2)://-X,+Y,+Z = 0+0+2=2
                        pos.x = -quarter;
                        pos.y = quarter;
                        pos.z = quarter;
                        break;
                    case (3)://+X,+Y,+Z = 1+0+2=3
                        pos.x = quarter;
                        pos.y = quarter;
                        pos.z = quarter;
                        break;
                    case (4)://-X,-Y,-Z = 0+4+0=4
                        pos.x = -quarter;
                        pos.y = -quarter;
                        pos.z = -quarter;
                        break;
                    case (5)://+X,-Y,-Z = 1+4+0=5
                        pos.x = quarter;
                        pos.y = -quarter;
                        pos.z = -quarter;
                        break;
                    case (6)://-X,-Y,+Z = 0+4+2=6
                        pos.x = -quarter;
                        pos.y = -quarter;
                        pos.z = quarter;
                        break;
                    case (7)://+X,-Y,+Z = 1+4+2=7
                        pos.x = quarter;
                        pos.y = -quarter;
                        pos.z = quarter;
                        break;
                }
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
                //foreach (KeyValuePair<uint, OctreeNode> node in tree.nodes)
                //{
                //    if (node.Value.objects.Contains(obj))
                //        return true;
                //}
                return false;
            }
#endif
            public void RemoveObject(OctreeObject<T> obj)
            {
                //Debug.Assert(objects.Contains(obj), "Object is not in this node");
                //Debug.Assert(objects.IndexOf(obj) == obj.listIndex,"Object index does not match");
                //Debug.Log(objects.IndexOf(obj) + " : " + obj.listIndex);
                if (localItemCount-- == 0 && childIndex != -1) //Let our parent know we ran out of objects
                    parent.ChildHasObjects(childIndex, false);

                objects[obj.listIndex] = null;
                UpdateBranchCount(false);
                tree.objectPool.Push(obj);
#if UNITY_EDITOR
                if (childIndex == -1 && isLeaf == true)
                    Debug.Assert(branchItemCount == 0, "Root has a branch count but it has no children.");
#endif
            }
        }
    }
}