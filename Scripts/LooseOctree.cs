using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class LooseOctree<T> where T : class
{
    //Nodes are encoded into an unsigned integer as bits;
    //Each set of 3 bits is one "level" in the tree. By shifting left or right, we can find children or parents.
    // code >> 3 would shift right, giving the code for the parent
    // code << 3 | 4 would shift left and attach a 4. Giving us the fourth child of the node.
    //With a 32 bit integer we can hold a depth of 10. With a 64 bit we can hold 21
    readonly float looseness;
    readonly float initialSize;
    private float maxDepth = (Mathf.Floor(32 / 3)); //We use 3 bits per level
    const int numObjectsAllowed = 8;

    //Maps node to their location in the tree. Where key is the location and value is the node.
    Dictionary<uint, OctreeNode> nodes;

    public LooseOctree(float initialSize, Vector3 initialWorldPosition, float loosenessVal)
    {
        nodes = new Dictionary<uint, OctreeNode>();
        this.initialSize = initialSize;
        this.looseness = Mathf.Clamp(loosenessVal, 1.0F, 2.0F);
        Bounds baseBounds = new Bounds(initialWorldPosition, Vector3.one * initialSize);
        OctreeNode root = new OctreeNode(1U);
        root.baseBounds = baseBounds;
        baseBounds.extents *= looseness;
        root.actualBounds = baseBounds;
        nodes[1U] = root;
        Split(1U);
    }

    private void Grow(Vector3 direction)
    {
        int xDirection = direction.x >= 0 ? 1 : -1;
        int yDirection = direction.y >= 0 ? 1 : -1;
        int zDirection = direction.z >= 0 ? 1 : -1;
        OctreeNode oldRoot = nodes[1U];
        float half = oldRoot.baseBounds.extents.x;
        float newLength = oldRoot.baseBounds.extents.x * 4;
        //Resize the root
        Vector3 newCenter = oldRoot.baseBounds.center + new Vector3(xDirection * half, yDirection * half, zDirection * half);
        Bounds newBounds = new Bounds(newCenter, Vector3.one * newLength);
        OctreeNode newRoot = oldRoot;
        newRoot.baseBounds = newBounds;
        newBounds.extents *= looseness;
        newRoot.actualBounds = newBounds;
        nodes[1U] = newRoot;
        //Resize all elements
        for (uint i = 0; i < 7U; i++)
        {
            SetAllChildBounds(1U, true);
        }

    }

    public uint GetIndex(uint locationCode)
    {
        uint mask = (1 << 3) - 1;
        return locationCode & mask;
    }
    public uint ParentCode(uint childLocationCode)
    {
        return childLocationCode >> 3;
    }
    public uint ChildCode(uint parentLocationCode, uint index)
    {
        return parentLocationCode << 3 | index;
    }

    //Adds a blank child node
    private bool AddChild(uint parentLocationCode, uint index)
    {
        if (index > 7U)
        {
            Debug.LogError("AddChild index must be 0-7");
            return false;
        }
        uint newCode = ChildCode(parentLocationCode, index);
        if (nodes.ContainsKey(newCode))
        {
            Debug.LogError("Location code already assigned: " + GetStringCode(newCode) + "(" + newCode + ")");
            return false;
        }
        OctreeNode newNode = new OctreeNode(newCode);
        nodes[newCode] = newNode; //Record in dictionary
        Debug.Log("Added child at: " + GetStringCode(newCode));
        return true;
    }
    private void SetAllChildBounds(uint locationCode, bool recursive)
    {
        if (!nodes.ContainsKey(locationCode))
        {
            Debug.LogError("Attempting to set bounds for invalid locationCode: " + GetStringCode(locationCode));
            return;
        }

        for (uint i = 0; i < 8U; i++)
        {
            SetChildBounds(locationCode, i); //Set bounds of each of this node's children
            if (recursive)
            {
                uint childCode = ChildCode(locationCode, i);
                if (!nodes[childCode].isLeaf) //Don't recurse into leaf nodes. They have no children
                    SetAllChildBounds(childCode, true);//Recursive resize. Keep dropping down
            }
        }
    }
    //Set bounds of this child based on the parent's bounds
    private void SetChildBounds(uint locationCode, uint index)
    {
        OctreeNode parentNode = nodes[locationCode];
        OctreeNode thisNode = nodes[ChildCode(locationCode, index)];
        //-X,-Y,+Z = 0+4+2=6
        //+X,-Y,+Z = 1+4+2=7
        //+X,+Y,+Z = 1+0+2=3
        //-X,+Y,+Z = 0+0+2=2
        //-X,+Y,-Z = 0+0+0=0
        //+X,+Y,-Z = 1+0+0=1
        //+X,-Y,-Z = 1+4+0=5
        //-X,-Y,-Z = 0+4+0=4

        float quarter = parentNode.baseBounds.extents.x / 2F;
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

        Bounds bounds = new Bounds(parentNode.baseBounds.center + pos, parentNode.baseBounds.extents);
        Bounds actualBounds = bounds;
        actualBounds.extents *= looseness;
        //Modify our local copy of the node
        thisNode.actualBounds = actualBounds;
        thisNode.baseBounds = bounds;
        nodes[ChildCode(locationCode, index)] = thisNode; //Place the child back in the dictionary
    }
    //Split this node into 8 children
    private bool Split(uint locationCode)
    {
        OctreeNode orgNode;
        if (!nodes.TryGetValue(locationCode, out orgNode))
        {
            Debug.LogError("Invalid locationCode for split.");
            return false;
        }

        for (uint i = 0U; i < 8U; i++)
        {
            if (!AddChild(locationCode, i))
                return false;
        }
        SetAllChildBounds(locationCode, false);
        orgNode.isLeaf = false; //We are now a branch, not a leaf
        nodes[orgNode.locationCode] = orgNode;//Put our original node back
        if(orgNode.objects.Count >= numObjectsAllowed) //We have objects that need to be distributed
        {
            List<OctreeObject> removals = new List<OctreeObject>();
            foreach(OctreeObject obj in orgNode.objects)
            {
                uint index = BestFitChild(locationCode, obj.bounds);
                if (!Encapsulates(nodes[ChildCode(locationCode, index)].actualBounds, obj.bounds))
                    continue;
                nodes[ChildCode(locationCode, index)].objects.Add(obj);
                obj.locationCode = ChildCode(locationCode, index);
                removals.Add(obj);
            }
            foreach (OctreeObject obj in removals)
            {
                orgNode.objects.Remove(obj);
            }
        }
        return true;
    }

    public OctreeObject Add(T obj, Vector3 position)
    {
        Bounds bounds = new Bounds(position, Vector3.one);
        OctreeObject newObj = Add(obj, bounds);
        newObj.isPoint = true;
        return newObj;
    }
    public OctreeObject Add(T obj, Bounds bounds)
    {
        OctreeObject newObj = new OctreeObject();
        newObj.obj = obj;
        newObj.bounds = bounds;
        int count = 0;
        while (!TryAddObj(newObj))
        {
            //Grow(bounds.center - nodes[1U].baseBounds.center);
            if (++count > 20)
            {
                Debug.LogError("Aborted Add operation as it seemed to be going on forever (" + (count - 1) + ") attempts at growing the octree.");
                return null;
            }
        }


        return newObj;
    }
    private bool TryAddObj(OctreeObject newObj)
    {
        uint checkLocation = 1U; //locationCode of checkNode
        OctreeNode checkNode; //Node we are currently trying to check
        uint tryLocation=0U; //Our last successful check of a node before we break
        bool found = false;
        //Keep searching down the tree until we find a node with space, or a leaf node that we can split
        while (nodes.TryGetValue(checkLocation, out checkNode))
        { //Search through all children for somewhere to fit the object
            if (!Encapsulates(checkNode.actualBounds, newObj.bounds))
                return false; //This node can't hold it, that means no children can. Abort.

            if (checkNode.isLeaf == true)
            { //End of the branch. Stop looking.
                tryLocation = checkLocation;
                break;
            }

            if (checkNode.objects.Count < numObjectsAllowed)
            {//This node has space. Stop looking.
                tryLocation = checkLocation;
                break;
            }

            uint index = BestFitChild(checkLocation, newObj.bounds); //Find most likely child
            checkLocation = ChildCode(checkLocation, index); //Step into child node
        }

        if (tryLocation == 0U)
        {
            Debug.LogError("TryAddObj failed without finding a location. Something went wrong.");
            return false; //Nothing found
        }

        if(nodes[tryLocation].objects.Count >= numObjectsAllowed)
        {
            Split(tryLocation);
            uint index = BestFitChild(tryLocation, newObj.bounds);
            if(!Encapsulates(nodes[ChildCode(tryLocation,index)].actualBounds,newObj.bounds))
                return false; //Doesn't fit into new children
            nodes[ChildCode(tryLocation, index)].objects.Add(newObj);
            newObj.locationCode = ChildCode(tryLocation, index);
            return true;
        }

        Debug.Log("Adding object to: " + GetStringCode(tryLocation));
        nodes[tryLocation].objects.Add(newObj);
        return true;
    }

    private OctreeObject SubAdd(OctreeObject obj)
    {
        return obj;
    }

    private static bool Encapsulates(Bounds outerBounds, Bounds innerBounds)
    {
        return outerBounds.Contains(innerBounds.min) && outerBounds.Contains(innerBounds.max);
    }

    uint BestFitChild(uint locationCode, Bounds objBounds)
    {
        Vector3 center = nodes[locationCode].baseBounds.center;
        return (objBounds.center.x <= center.x ? 0U : 1U) + (objBounds.center.y >= center.y ? 0U : 4U) + (objBounds.center.z <= center.z ? 0U : 2U);
    }

    private int GetDepth(uint locationCode)
    {
        int count = 0;
        while (locationCode > 1U)
        { //Keep shifting right until we hit 1
            count++;
            locationCode = ParentCode(locationCode);
        }
        return count;
    }

    public void DrawAllNodes()
    {
        foreach (KeyValuePair<uint, OctreeNode> node in nodes)
        {
            float tintVal = GetDepth(node.Key) / 7F; // Will eventually get values > 1. Color rounds to 1 automatically
            Gizmos.color = new Color(tintVal, GetIndex(node.Key) / 7F, 1.0f - tintVal);
            Gizmos.DrawWireCube(node.Value.actualBounds.center, node.Value.actualBounds.extents * 2F);
            foreach(OctreeObject obj in node.Value.objects)
            {
                Gizmos.DrawWireCube(obj.bounds.center, obj.bounds.extents * 2F);
            }
        }
        Gizmos.color = Color.white;
    }
    private string GetStringCode(uint locationCode)
    {
        string name = System.Convert.ToString(locationCode, 2);
        name = System.Text.RegularExpressions.Regex.Replace(name, ".{3}", "$0 ").Trim();
        return name;
    }
    //Using a struct would likely be faster here, but
    //it makes the code less readable when modifying values
    public struct OctreeNode
    {
        public uint locationCode;
        public Bounds baseBounds;
        public Bounds actualBounds; //Actual bounds of this node including looseness
        public bool isLeaf; //Leaf nodes have no children
        public List<OctreeObject> objects;
        public OctreeNode(uint locationCode)
        {
            this.objects = new List<OctreeObject>(numObjectsAllowed);
            this.locationCode = locationCode;
            this.baseBounds = new Bounds();
            this.actualBounds = new Bounds();
            this.isLeaf = true;
        }
    }

    public class OctreeObject
    {
        public T obj; //Object reference held by this instance
        public Bounds bounds; //Bounds and position
        public bool isPoint; //Point data only, ignore extents
        public uint locationCode; //Where in the tree this object is located
    }

}
