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
            public int branchItemCount;
            public int childExists = 0; //Bitmask for child nodes
            public void Initialize(uint locationCode, LooseOctree<T> tree)
            {
                orphanObjects = tree.orphanObjects;
                removals = tree.removals;
                branchItemCount = 0;
                childExists = 0;
                this.locationCode = locationCode;
                this.tree = tree;
            }

            //Adds a child node at index. If any existing objects fit, moves them into it
            private bool AddChild(uint index)
            {
                Debug.Assert(index <= 7U, "AddChild index must be between 0-7");
                Debug.Assert(CheckChild(childExists, index) == false, "Trying to add a child that already exists.");
                Debug.Assert(GetDepth(locationCode) < maxDepth, "Max depth reached. This will cause integer overflow. Recommend increasing numObjectsAllowed or switching to 64bit integers.");
                Debug.Assert(removals.Count == 0, "removals queue is not empty when it should be");
                uint newCode;
                OctreeNode newNode = tree.nodePool.Pop();
                newNode.Initialize(newCode = ChildCode(locationCode, index), tree);
                tree.nodes[newCode] = newNode; //Add to dictionary
                newNode.ResetBounds();
                childExists = SetChild(childExists, index); //Set flag
                int objCount;
                if ((objCount = objects.Count) > 0) //Place objects into the new node
                {
                    int i;
                    for (i = 0; i < objCount; i++)
                    {
                        OctreeObject<T> obj;
                        if (BestFitChild(obj = objects[i]) == index)
                            if (newNode.TryAddObj(obj, false))//Make sure the object fits in the new location. Otherwise keep it here
                                removals.Enqueue(obj);
                    }
                    int itemCount = removals.Count;
                    while (itemCount > 0)
                    {
                        objects.Remove(removals.Dequeue());
                        branchItemCount++; //Item was added below us, so our branch count needs to go up
                        itemCount--;
                    }

                }
                return true;
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
            private bool FindContainingNode(ref Vector3 center, ref Vector3 minBounds, ref Vector3 maxBounds, out OctreeNode node)
            {
                node = null;
                if (!ContainsBounds(ref minBounds, ref maxBounds))
                    return false;

                uint childCode = locationCode;//Start here
                OctreeNode childNode;
                uint index;
                Dictionary<uint, OctreeNode> nodes = tree.nodes;
                bool firstLoop = true;
                while (nodes.TryGetValue(childCode, out childNode))
                {
                    if (!firstLoop && !childNode.ContainsBounds(ref minBounds, ref maxBounds))
                    { //Doesn't fit in this node. Put it in the parent node. If it's the firstLoop, we already checked this.
                        node = nodes[ParentCode(childCode)];
                        return true;
                    }

                    if (CheckChild(childNode.childExists, index = childNode.BestFitChild(ref center)))
                    { //Drop down another level if we have a valid child
                        childCode = ChildCode(childCode, index);
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
                OctreeNode previousNode = null;
                uint childCode = locationCode; //Start here
                uint index;
                Dictionary<uint, OctreeNode> nodes = tree.nodes;
                bool firstLoop = true;
                while (nodes.TryGetValue(childCode, out childNode))
                {
                    if (!firstLoop && !childNode.ContainsBounds(ref newObj.boundsMin, ref newObj.boundsMax))
                    { //Doesn't fit in this node. Put it in the parent node. If it's the firstLoop, we already checked this.
                        previousNode.PutObjectInNode(newObj, updateCount);
                        return true;
                    }

                    bool childAdded = false;
                    if (!CheckChild(childNode.childExists, index = childNode.BestFitChild(newObj)) && childNode.objects.Count >= numObjectsAllowed)
                    {
                        childNode.AddChild(index);//We hit the limit and no child exists here yet. Make one
                        childAdded = true;
                    }

                    if (childAdded || CheckChild(childNode.childExists, index))
                    { //Drop down another level if we added a child or it already exists
                        childCode = ChildCode(childCode, index);
                    }
                    else
                    { //Place it here. We have no children and we're under the limit
                        childNode.PutObjectInNode(newObj, updateCount);
                        return true;
                    }
                    previousNode = childNode;
                    firstLoop = false;
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
                Dictionary<uint, OctreeNode> nodes = tree.nodes;
                while (nodes.TryGetValue(parentCode, out parentNode))
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
                Debug.Assert(orphanObjects.Count == 0, "orphanObjects queue is not empty when it should be");
                if (topLevel != null)
                {
                    topLevel.MergeNode();
                    int itemCount = orphanObjects.Count;
                    while (itemCount > 0)
                    {
                        topLevel.branchItemCount--; //Items are now in this node which doesn't count towards our branch count
                        topLevel.PutObjectInNode(orphanObjects.Dequeue(), false);
                        itemCount--;
                    }
                }
            }
            //Merge all children into this node
            public void MergeNode()
            {
                if (childExists == 0)//No children to merge
                    return;

                uint i;
                Dictionary<uint, OctreeNode> nodes = tree.nodes;
                ObjectPool<OctreeNode> nodePool = tree.nodePool;
                for (i = 0; i < 8; i++)
                {
                    if (!CheckChild(childExists, i))
                        continue; //Make sure child exists

                    uint childCode;
                    OctreeNode childNode = nodes[childCode = ChildCode(locationCode, i)];

                    childNode.MergeNode();
                    //Add objects to orphaned list
                    List<OctreeObject<T>> childObjects = childNode.objects;
                    int childCount = childObjects.Count;
                    int o;
                    for (o = 0; o < childCount; o++)
                        orphanObjects.Enqueue(childObjects[o]);

                    childExists = UnsetChild(childExists, i);

                    childObjects.Clear();//Clear out list
                    nodePool.Push(childNode);//Push node instance back to the pool
                    nodes.Remove(childCode);//Remove from dictionary
                }
            }

            //Re-set the bounds of this node to fit the parent
            private void ResetBounds()
            {
                Debug.Assert(locationCode != 1, "Cannot call ResetBounds on root node.");
                uint index = GetIndex(locationCode);
                OctreeNode parentNode = tree.nodes[ParentCode(locationCode)];
                //-X,-Y,+Z = 0+4+2=6
                //+X,-Y,+Z = 1+4+2=7
                //+X,+Y,+Z = 1+0+2=3
                //-X,+Y,+Z = 0+0+2=2
                //-X,+Y,-Z = 0+0+0=0
                //+X,+Y,-Z = 1+0+0=1
                //+X,-Y,-Z = 1+4+0=5
                //-X,-Y,-Z = 0+4+0=4
                float quarter = parentNode.baseSize.x * .25F;
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
                pos.x += parentNode.boundsCenter.x;
                pos.y += parentNode.boundsCenter.y;
                pos.z += parentNode.boundsCenter.z;
                SetBounds(ref pos, parentNode.baseSize * .5F, tree.looseness);
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
                UpdateBranchCount(false);
                tree.objectPool.Push(obj);
#if UNITY_EDITOR
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