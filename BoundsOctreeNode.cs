using System.Collections.Generic;
using UnityEngine;

// A node in a BoundsOctree
// Copyright 2014 Bill Borman, GNU General Public Licence v3. http://www.gnu.org/copyleft/gpl.html
public class BoundsOctreeNode<T> {
    // Center coordinates
	public Vector3 Center { get; private set; }
	public float BaseLength { get; private set; }
	float looseness;
	float minSize;
	// Actual length of sides, taking looseness value into account
	float adjLength;
    // Bounding box that represents this octree.
    Bounds bounds = default(Bounds);
    // Objects in this node
	readonly List<OctreeObject> objects = new List<OctreeObject>();
    BoundsOctreeNode<T>[] children = null;
	// Bounds of potential children to this node. These are actual size (with looseness taken into account), not base size
	Bounds[] childBounds;
	// If there are already numObjectsAllowed in a node, we split it into children
	// A good number seems to be something around 8-15
	const int numObjectsAllowed = 5;

	// An object in the octree
	class OctreeObject {
		public T Obj;
		public Bounds Bounds;
	}

	// Constructor
	public BoundsOctreeNode(float baseLengthVal, float minSizeVal, float loosenessVal, Vector3 centerVal) {
		SetValues(baseLengthVal, minSizeVal, loosenessVal, centerVal);
	}

	// #### PUBLIC METHODS ####

	// Adds the specified ocject
	public bool Add(T obj, Bounds objBounds) {
		if (!Encapsulates(bounds, objBounds)) {
			return false;
		}
		SubAdd(obj, objBounds);
		return true;
	}

    // Removes the specified object. Assumes that the object only exists once in the tree
    public bool Remove(T obj) {
		bool removed = false;

		for(int i = 0; i < objects.Count; i++) {
			if (objects[i].Obj.Equals(obj)) {
				removed = objects.Remove(objects[i]);
				break;
			}
		}

		if (!removed && children != null) {
			for(int i = 0; i < 8; i++) {
				removed = children[i].Remove(obj);
				if (removed) break;
			}
		}

		if (removed && children != null) {
			// Check if we should merge nodes now that we've removed an item
			if (ShouldMerge()) {
				Merge();
			}
		}

		return removed;
    }

	// Check if the specified object intersects any others in the tree
	public bool IsColliding(Bounds checkBounds) {
		// Are the input bounds at least partially in this node?
		if (!bounds.Intersects(checkBounds)) {
			return false;
		}

		//Debug.Log("Checking node at depth " + depth + " containing " + objects.Count + " object(s) and " + 8 + " children.");
		
		// Check against any objects in this node
		for(int i = 0; i < objects.Count; i++) {
			if (objects[i].Bounds.Intersects(checkBounds)) {
				return true;
			}
		}
		
		// Check children
		if (children != null) {
			for(int i = 0; i < 8; i++) {
				if (children[i].IsColliding(checkBounds)) {
					return true;
				}
			}
		}

		return false;
	}

	// Check if the specified bounds are intersecting anything,
	// and return anything it's intersecting with. If none, returns an empty list (not null)
	public T[] GetColliding(Bounds checkBounds) {
		List<T> collidingWith = new List<T>();
		// Are the input bounds at least partially in this node?
		if (!bounds.Intersects(checkBounds)) {
			return collidingWith.ToArray();
		}

		// Check against any objects in this node
		for (int i = 0; i < objects.Count; i++) {
			if (objects[i].Bounds.Intersects(checkBounds)) {
				collidingWith.Add(objects[i].Obj);
			}
		}

		// Check children
		if (children != null) {
			for (int i = 0; i < 8; i++) {
				T[] childColliding = children[i].GetColliding(checkBounds);
				if (childColliding != null) collidingWith.AddRange(childColliding);
			}
		}
		return collidingWith.ToArray();
	}

	// Set the 8 children of this octree
	public void SetChildren(BoundsOctreeNode<T>[] childOctrees) {
		if (childOctrees.Length != 8) {
			Debug.LogError("Child octree array must be length 8. Was length: " + childOctrees.Length);
			return;
		}

		children = childOctrees;
	}

	// Intended for debugging. Must be called from OnGrawGizmos externally
	// See also DrawAllObjects and DrawCollisionChecks
	public void DrawAllBounds(float depth = 0) {
		float tintVal = depth / 7; // Will eventually get values > 1. Color rounds to 1 automatically
		Gizmos.color = new Color(tintVal, 0, 1.0f - tintVal);
		
		Bounds thisBounds = new Bounds(Center, new Vector3(adjLength, adjLength, adjLength));
		Gizmos.DrawWireCube(thisBounds.center, thisBounds.size);

		if (children != null) {
			depth++;
			for (int i = 0; i < 8; i++) {
				children[i].DrawAllBounds(depth);
			}
		}
		Gizmos.color = Color.white;
	}

	// Intended for debugging. Must be called from OnGrawGizmos externally
	// See also DrawAllBounds and DrawCollisionChecks
	public void DrawAllObjects() {
		float tintVal = BaseLength / 20;
		Gizmos.color = new Color(0, 1.0f - tintVal, tintVal, 0.25f);

		foreach (OctreeObject obj in objects) {
			Gizmos.DrawCube(obj.Bounds.center, obj.Bounds.size);
		}

		if (children != null) {
			for (int i = 0; i < 8; i++) {
				children[i].DrawAllObjects();
			}
		}

		Gizmos.color = Color.white;
	}

	// We can shrink the octree if:
	// - This node is >= double minLength in length
	// - All objects in the root node are within one octant
	// - This node doesn't have children, or does but 7/8 children are empty
	// We can also shrink it if there are no objects left at all!
	// Returns the new root, or the existing one if we didn't shrink
	public BoundsOctreeNode<T> ShrinkIfPossible(float minLength) {
		if (BaseLength < (2 * minLength)) {
			return this;
		}
		if (objects.Count == 0 && children.Length == 0) {
			return this;
		}

		// Check objects in root
		int bestFit = -1;
		for (int i = 0; i < objects.Count; i++) {
			OctreeObject curObj = objects[i];
			int newBestFit = BestFitChild(curObj.Bounds);
			if (i == 0 || newBestFit == bestFit) {
				// In same octant as the other(s). Does it fit completely inside that octant?
				if (Encapsulates(childBounds[newBestFit], curObj.Bounds)) {
					if (bestFit < 0) {
						bestFit = newBestFit;
					}
				}
				else {
					// Nope, so we can't reduce. Otherwise we continue
					return this;
				}
			}
			else {
				return this; // Can't reduce - objects fit in different octants
			}
		}

		// Check objects in children if there are any
		if (children != null) {
			bool childHadContent = false;
			for (int i = 0; i < children.Length; i++) {
				if (children[i].HasAnyObjects()) {
					if (childHadContent) {
						return this; // Can't shrink - another child had content already
					}
					if (bestFit >= 0 && bestFit != i) {
						return this; // Can't reduce - objects in root are in a different octant to objects in child
					}
					childHadContent = true;
					bestFit = i;
				}
			}
		}

		// Can reduce
		if (children == null) {
			// We don't have any children, so just shrink this node to the new size
			// We already know that everything will still fit in it
			SetValues(BaseLength / 2, minSize, looseness, childBounds[bestFit].center);
			return this;
		}

		// We have children. Use the appropriate child as the new root node
		return children[bestFit];
	}

	/*
	/// <summary>
	/// Get the total amount of objects in this node and all its children, grandchildren etc. Useful for debugging.
	/// This could technically cause a stack overflow in very deep octrees.
	/// </summary>
	/// <param name="startingNum">Used by recursive calls to add to the previous total.</param>
	/// <returns>Total objects in this node and its children, grandchildren etc.</returns>
	public int GetTotalObjects(int startingNum = 0) {
		int totalObjects = startingNum + objects.Count;
		if (children != null) {
			for (int i = 0; i < 8; i++) {
				totalObjects += children[i].GetTotalObjects();
			}
		}
		return totalObjects;
	}
	*/

	// #### PRIVATE METHODS ####

	void SetValues(float baseLengthVal, float minSizeVal, float loosenessVal, Vector3 centerVal) {
		BaseLength = baseLengthVal;
		minSize = minSizeVal;
		looseness = loosenessVal;
        Center = centerVal;
		adjLength = looseness * baseLengthVal;

        // Create the bounding box.
		Vector3 size = new Vector3(adjLength, adjLength, adjLength);
        bounds = new Bounds(Center, size);

		float quarter = adjLength / looseness / 4f;
	    float childActualLength = (BaseLength / 2) * looseness;
		Vector3 childActualSize = new Vector3(childActualLength, childActualLength, childActualLength);
		childBounds = new Bounds[8];
		childBounds[0] = new Bounds(Center + new Vector3(-quarter, quarter, -quarter), childActualSize);
		childBounds[1] = new Bounds(Center + new Vector3(quarter, quarter, -quarter), childActualSize);
		childBounds[2] = new Bounds(Center + new Vector3(-quarter, quarter, quarter), childActualSize);
		childBounds[3] = new Bounds(Center + new Vector3(quarter, quarter, quarter), childActualSize);
		childBounds[4] = new Bounds(Center + new Vector3(-quarter, -quarter, -quarter), childActualSize);
		childBounds[5] = new Bounds(Center + new Vector3(quarter, -quarter, -quarter), childActualSize);
		childBounds[6] = new Bounds(Center + new Vector3(-quarter, -quarter, quarter), childActualSize);
		childBounds[7] = new Bounds(Center + new Vector3(quarter, -quarter, quarter), childActualSize);
	}

	// Add the given object to the octree.
	void SubAdd(T obj, Bounds objBounds) {
		// We know it fits at this level if we've got this far
		// Just add if few objects are here, or children would be below min size
		if (objects.Count < numObjectsAllowed || (BaseLength / 2) < minSize) {
			OctreeObject newObj = new OctreeObject {Obj = obj, Bounds = objBounds};
			//Debug.Log("ADD " + obj.name + " to depth " + depth);
			objects.Add(newObj);
		}
		else {
			// Fits at this level, but we can go deeper. Would it fit there?

			// Create the 8 children
			int bestFitChild;
			if (children == null) {
				Split();
				if (children == null) {
					Debug.Log("Child creation failed for an unknown reason. Early exit.");
					return;
				}

				// Now that we have the new children, see if this node's existing objects would fit there
				for (int i = objects.Count - 1; i >= 0; i--) {
					OctreeObject existingObj = objects[i];
					// Find which child the object is closest to based on where the
					// object's center is located in relation to the octree's center.
					bestFitChild = BestFitChild(existingObj.Bounds);
					// Does it fit?
					if (Encapsulates(children[bestFitChild].bounds, existingObj.Bounds)) {
						children[bestFitChild].SubAdd(existingObj.Obj, existingObj.Bounds); // Go a level deeper					
						objects.Remove(existingObj); // Remove from here
					}
				}
			}

			// Now handle the new object we're adding now
			bestFitChild = BestFitChild(objBounds);
			if (Encapsulates(children[bestFitChild].bounds, objBounds)) {
				children[bestFitChild].SubAdd(obj, objBounds);
			}
			else {
				OctreeObject newObj = new OctreeObject { Obj = obj, Bounds = objBounds };
				//Debug.Log("ADD " + obj.name + " to depth " + depth);
				objects.Add(newObj);
			}
		}
	}

    // Splits the octree into eight children
    void Split() {
		float quarter = adjLength / looseness / 4f;
	    float newLength = BaseLength / 2;
		children = new BoundsOctreeNode<T>[8];
		children[0] = new BoundsOctreeNode<T>(newLength, minSize, looseness, Center + new Vector3(-quarter, quarter, -quarter));
		children[1] = new BoundsOctreeNode<T>(newLength, minSize, looseness, Center + new Vector3(quarter, quarter, -quarter));
		children[2] = new BoundsOctreeNode<T>(newLength, minSize, looseness, Center + new Vector3(-quarter, quarter, quarter));
		children[3] = new BoundsOctreeNode<T>(newLength, minSize, looseness, Center + new Vector3(quarter, quarter, quarter));
		children[4] = new BoundsOctreeNode<T>(newLength, minSize, looseness, Center + new Vector3(-quarter, -quarter, -quarter));
		children[5] = new BoundsOctreeNode<T>(newLength, minSize, looseness, Center + new Vector3(quarter, -quarter, -quarter));
		children[6] = new BoundsOctreeNode<T>(newLength, minSize, looseness, Center + new Vector3(-quarter, -quarter, quarter));
		children[7] = new BoundsOctreeNode<T>(newLength, minSize, looseness, Center + new Vector3(quarter, -quarter, quarter));
    }

	// Merge all children into this node - opposite of Split
	// Note: We only have to check one level down since a merge will never happen if the children already have children,
	// since THAT won't happen unless there are already too many objects to merge
	void Merge() {
		// Note: We know children != null or we wouldn't be merging
		for (int i = 0; i < 8; i++) {
			BoundsOctreeNode<T> curChild = children[i];
			int numObjects = curChild.objects.Count;
			for (int j = numObjects - 1; j >= 0; j--) {
				OctreeObject curObj = curChild.objects[j];
				objects.Add(curObj);
			}
		}
		// Remove the child nodes (and the objects in them - they've been added elsewhere now)
		children = null;
	}

	// Return true if innerBounds is fully encapsulated by outerBounds
	static bool Encapsulates(Bounds outerBounds, Bounds innerBounds) {
		return outerBounds.Contains(innerBounds.min) && outerBounds.Contains(innerBounds.max);
	}

	int BestFitChild(Bounds objBounds) {
		return (objBounds.center.x <= Center.x ? 0 : 1) + (objBounds.center.y >= Center.y ? 0 : 4) + (objBounds.center.z <= Center.z ? 0 : 2);
	}

	// Returns true there are few enough objects in this object's children that we should merge them into this
	bool ShouldMerge() {
		int totalObjects = objects.Count;
		if (children != null) {
			foreach(BoundsOctreeNode<T> child in children) {
				if (child.children != null) {
					// If any of the *children* have children, there are definitely too many to merge,
					// or the child woudl have been merged already
					return false;
				}
				totalObjects += child.objects.Count;
			}
		}
		return totalObjects <= numObjectsAllowed;
	}

	// Returns true if this node or any of its children, grandchildren etc have something in them
	bool HasAnyObjects() {
		if (objects.Count > 0) return true;

		if (children != null) {
			for (int i = 0; i < 8; i++) {
				if (children[i].HasAnyObjects()) return true;
			}
		}

		return false;
	}
}
