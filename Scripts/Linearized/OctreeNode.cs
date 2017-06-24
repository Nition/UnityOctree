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
            OctreeObject<T> firstObject = null;
            Queue<OctreeObject<T>> orphanObjects;
            Queue<OctreeObject<T>> removals;
            OctreeNode parent; //This node's immediate parent
            OctreeNode[] children = new OctreeNode[8];
            int childIndex = -1;
            int branchItemCount = 0;
            int localItemCount = 0;
            int treeDepth = 0;
            bool isLeaf; //Leaves have no children
            private int childHasObjects = 0; //Bitmask for child nodes
            public void Initialize(int index, LooseOctree<T> tree, OctreeNode parent)
            {
                Initialize(index, tree);
                this.parent = parent;
                treeDepth = parent.treeDepth + 1;
            }
            public void Initialize(int index, LooseOctree<T> tree)
            {
                orphanObjects = tree.orphanObjects;
                removals = tree.removals;
                branchItemCount = 0;
                localItemCount = 0;
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
                OctreeObject<T> obj = firstObject;
                while (obj != null)
                {
                    if (drawObjects)
                    {
                        if (obj.isPoint)
                            Gizmos.DrawSphere(obj.boundsCenter, 0.25F);
                        else
                            Gizmos.DrawCube(obj.boundsCenter, obj.boundsSize);
                    }
                    if (drawConnections)
                    {
                        Gizmos.DrawLine(boundsCenter, obj.boundsCenter);
                        Color prev = Gizmos.color;
                        if (obj.previous != null)
                        {
                            Gizmos.color = Color.cyan;
                            Gizmos.DrawLine(obj.boundsCenter, obj.previous.boundsCenter);
                            Gizmos.color = prev;
                        }
                    }
                    obj = obj.next;
                }
                Gizmos.color = Color.white;
            }

            private void ChildHasObjects(int index, bool set)
            {
                if (set)
                    childHasObjects |= 1 << (index + 1);
                else
                    childHasObjects &= ~1 << (index + 1);

            }
            private bool ChildHasObjects(int index)
            {
                return (childHasObjects & 1 << (index + 1)) != 0;
            }

            //Adds a child node at index. If any existing objects fit, moves them into it
            private void Split()
            {
                Debug.Assert(isLeaf, "Trying to split a non-leaf node.");
                Debug.Assert(removals.Count == 0, "removals queue is not empty when it should be");

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
                    OctreeObject<T> obj = firstObject;
                    while (obj != null)
                    {
                        OctreeObject<T> next = obj.next; //Store ref to next object so we don't lose it reorganizing
                        childNode = children[index = BestFitChild(ref obj.boundsCenter)];
                        if (childNode.ContainsBounds(ref obj.boundsMin, ref obj.boundsMax))
                        {
                            Unlink(obj);
                            childNode.PutObjectInNode(obj, false);
                            objCount++;
                        }
                        obj = next;
                    }
                }

                branchItemCount += objCount;//Item was added below us, so our branch count needs to go up
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
                rootNode.MergeNode(rootNode);
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

            void AddLink(OctreeObject<T> obj)
            {
                if (firstObject == null)
                {//Adding the first object
                    firstObject = obj;
                    obj.previous = null;
                    obj.next = null;
                }
                else
                { //Insert at beginning
                    obj.previous = null;
                    obj.next = firstObject;
                    firstObject.previous = obj;
                    firstObject = obj;
                }
                obj.SetNode(this);
                if ((localItemCount += 1) == 1 && childIndex != -1) //Let our parent know we now have objects
                    parent.ChildHasObjects(childIndex, true);
            }

            void Unlink(OctreeObject<T> obj)
            {
                if (obj == firstObject)
                { //Removing the first object.
                    firstObject = obj.next;
                }
                else
                { //Removing a chained object
                    if(obj.next != null)
                        obj.next.previous = obj.previous;
                    obj.previous.next = obj.next;
                }
                obj.SetNode(null);
                obj.next = null;
                obj.previous = null;
                if ((localItemCount -= 1) == 0 && childIndex != -1)
                { //Let our parent know we ran out of objects
                    parent.ChildHasObjects(childIndex, false);
                    Debug.Assert(firstObject == null, "Moved all objects but firstObject is not null");
                }
            }

            //Moves all objects to another node
            //Returns a count of items moved. Updates localItemCount
            //Does not handle branch count updates
            private int MoveObjects(OctreeNode node)
            {
                if (localItemCount == 0)
                    return 0;//Nothing to move

                OctreeObject<T> obj = firstObject;
                OctreeObject<T> lastObj = obj;
                while (obj != null)
                {
                    obj.SetNode(node);
                    lastObj = obj;
                    obj = obj.next;
                }
                if (node.firstObject == null)
                    node.firstObject = firstObject;
                else
                { //Insert entire chain at the front
                    node.firstObject.previous = lastObj;
                    lastObj.next = node.firstObject;
                    node.firstObject = firstObject;
                }


                firstObject = null;
                int count = localItemCount;
                localItemCount = 0;
                node.localItemCount += count;
                Debug.Assert(firstObject == null, "Moved all objects but firstObject is not null");
                return count;
            }

            //Find the lowest node that fully contains the given bounds
            private bool FindFittingNode(ref Vector3 center, ref Vector3 minBounds, ref Vector3 maxBounds, out OctreeNode node)
            {
                node = null;
                if (!ContainsBounds(ref minBounds, ref maxBounds))
                    return false;

                OctreeNode childNode;
                bool firstLoop = true;
                //    while (nodes.TryGetValue(childCode, out childNode))
                {
                    //    if (!firstLoop && !childNode.ContainsBounds(ref minBounds, ref maxBounds))
                    { //Doesn't fit in this node. Put it in the parent node. If it's the firstLoop, we already checked this.
                      //             node = childNode.parent;
                        return true;
                    }

                    if (!childNode.isLeaf)
                    { //Drop down another level if we have a valid child
                      //          childCode = ChildCode(childCode, childNode.BestFitChild(ref center));
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
                    if (childNode.isLeaf && childNode.localItemCount >= numObjectsAllowed && childNode.ContainsBounds(ref newObj.doubleBoundsMin, ref newObj.doubleBoundsMax))
                        childNode.Split();//We hit the limit and we can split. We only split if the object will fit in the new bounds

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
                //Debug.Assert(localItemCount <= numObjectsAllowed, "Too many objects in node");
                AddLink(obj);
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
                if (childIndex == -1)
                    return; //This is the root. We have no parents to update

                OctreeNode parentNode = parent;
                OctreeNode topLevel = null;
                while (true)
                {
                    if (addedItem)
                        parentNode.branchItemCount++;
                    else if ((parentNode.branchItemCount -= 1) + parentNode.localItemCount <= numObjectsAllowed)
                        topLevel = parentNode; //Record our path as long as we're not above the item limit

                    if (parentNode.childIndex == -1)
                        break; //We're already at root. Can't go up anymore
                    else
                        parentNode = parentNode.parent;
                }
                Debug.Assert(parentNode != null, "ParentNode is null. This shouldn't happen");
                Debug.Assert(orphanObjects.Count == 0, "orphanObjects queue is not empty when it should be");
                if (topLevel != null)
                {
                    int objCount = topLevel.MergeNode(topLevel);
                    topLevel.branchItemCount -= objCount; //Items are now in this node which doesn't count towards our branch count
                    Debug.Assert(branchItemCount >= 0, "branchItemCount is zero. Something went wrong.");
                    Debug.Assert(localItemCount <= numObjectsAllowed, "Merged too many objects into one node!");
                }
            }

            //Merge all children into this node
            public int MergeNode(OctreeNode topLevel)
            {
                if (isLeaf)//No children to merge
                    return 0;

                int index;
                int count = 0;
                ObjectPool<OctreeNode> nodePool = tree.nodePool;
                for (index = 0; index < 8; index++)
                {
                    uint childCode;
                    OctreeNode childNode = children[index];
                    if (!childNode.isLeaf)
                        count += childNode.MergeNode(topLevel);
                    if (childHasObjects != 0 && ChildHasObjects(index))
                    {
                        Debug.Assert(childNode.localItemCount > 0, "Attempting to merge child with no objects.");
                        count += childNode.MoveObjects(topLevel);
                    }
                    nodePool.Push(childNode);//Push node instance back to the pool
                }
                tree.nodeCount -= 8;
                childHasObjects = 0; //Children are all gone
                isLeaf = true; //We are now a leaf
                return count;
            }

            //Re-set the bounds of this node to fit the parent
            private void ResetBounds()
            {
                Debug.Assert(childIndex != -1, "Cannot call ResetBounds on root node.");
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
                    //        if (node.Value.objects.Contains(obj))
                    //            return true;
                //}
                return false;
            }
#endif
            public void RemoveObject(OctreeObject<T> obj)
            {
                Unlink(obj);
                UpdateBranchCount(false);
                tree.objectPool.Push(obj);
                if (childIndex == -1 && isLeaf == true)
                    Debug.Assert(branchItemCount == 0, "Root has a branch count but it has no children.");
            }
        }
    }
}