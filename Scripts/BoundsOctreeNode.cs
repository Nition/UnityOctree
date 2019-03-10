using System.Collections.Generic;
using UnityEngine;

// A node in a BoundsOctree
// Copyright 2014 Nition, BSD licence (see LICENCE file). www.momentstudio.co.nz
public class BoundsOctreeNode<T> {
	// Centre of this node
	public Vector3 Center { get; private set; }

	// Length of this node if it has a looseness of 1.0
	public float BaseLength { get; private set; }

	// Looseness value for this node
	float looseness;

	// Minimum size for a node in this octree
	float minSize;

	// Actual length of sides, taking the looseness value into account
	float adjLength;

	// Bounding box that represents this node
	Bounds bounds = default(Bounds);

	// Objects in this node
	readonly List<OctreeObject> objects = new List<OctreeObject>();

	// Child nodes, if any
	BoundsOctreeNode<T>[] children = null;

	bool HasChildren { get { return children != null; } }

	// Bounds of potential children to this node. These are actual size (with looseness taken into account), not base size
	Bounds[] childBounds;

	// If there are already NUM_OBJECTS_ALLOWED in a node, we split it into children
	// A generally good number seems to be something around 8-15
	const int NUM_OBJECTS_ALLOWED = 8;

	// An object in the octree
	struct OctreeObject {
		public T Obj;
		public Bounds Bounds;
	}

	/// <summary>
	/// Constructor.
	/// </summary>
	/// <param name="baseLengthVal">Length of this node, not taking looseness into account.</param>
	/// <param name="minSizeVal">Minimum size of nodes in this octree.</param>
	/// <param name="loosenessVal">Multiplier for baseLengthVal to get the actual size.</param>
	/// <param name="centerVal">Centre position of this node.</param>
	public BoundsOctreeNode(float baseLengthVal, float minSizeVal, float loosenessVal, Vector3 centerVal) {
		SetValues(baseLengthVal, minSizeVal, loosenessVal, centerVal);
	}

	// #### PUBLIC METHODS ####

	/// <summary>
	/// Add an object.
	/// </summary>
	/// <param name="obj">Object to add.</param>
	/// <param name="objBounds">3D bounding box around the object.</param>
	/// <returns>True if the object fits entirely within this node.</returns>
	public bool Add(T obj, Bounds objBounds) {
		if (!Encapsulates(bounds, objBounds)) {
			return false;
		}
		SubAdd(obj, objBounds);
		return true;
	}

	/// <summary>
	/// Remove an object. Makes the assumption that the object only exists once in the tree.
	/// </summary>
	/// <param name="obj">Object to remove.</param>
	/// <returns>True if the object was removed successfully.</returns>
	public bool Remove(T obj) {
		bool removed = false;

		for (int i = 0; i < objects.Count; i++) {
			if (objects[i].Obj.Equals(obj)) {
				removed = objects.Remove(objects[i]);
				break;
			}
		}

		if (!removed && children != null) {
			for (int i = 0; i < 8; i++) {
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

	/// <summary>
	/// Removes the specified object at the given position. Makes the assumption that the object only exists once in the tree.
	/// </summary>
	/// <param name="obj">Object to remove.</param>
	/// <param name="objBounds">3D bounding box around the object.</param>
	/// <returns>True if the object was removed successfully.</returns>
	public bool Remove(T obj, Bounds objBounds) {
		if (!Encapsulates(bounds, objBounds)) {
			return false;
		}
		return SubRemove(obj, objBounds);
	}

	/// <summary>
	/// Check if the specified bounds intersect with anything in the tree. See also: GetColliding.
	/// </summary>
	/// <param name="checkBounds">Bounds to check.</param>
	/// <returns>True if there was a collision.</returns>
	public bool IsColliding(ref Bounds checkBounds) {
		// Are the input bounds at least partially in this node?
		if (!bounds.Intersects(checkBounds)) {
			return false;
		}

		// Check against any objects in this node
		for (int i = 0; i < objects.Count; i++) {
			if (objects[i].Bounds.Intersects(checkBounds)) {
				return true;
			}
		}

		// Check children
		if (children != null) {
			for (int i = 0; i < 8; i++) {
				if (children[i].IsColliding(ref checkBounds)) {
					return true;
				}
			}
		}

		return false;
	}

	/// <summary>
	/// Check if the specified ray intersects with anything in the tree. See also: GetColliding.
	/// </summary>
	/// <param name="checkRay">Ray to check.</param>
	/// <param name="maxDistance">Distance to check.</param>
	/// <returns>True if there was a collision.</returns>
	public bool IsColliding(ref Ray checkRay, float maxDistance = float.PositiveInfinity) {
		// Is the input ray at least partially in this node?
		float distance;
		if (!bounds.IntersectRay(checkRay, out distance) || distance > maxDistance) {
			return false;
		}

		// Check against any objects in this node
		for (int i = 0; i < objects.Count; i++) {
			if (objects[i].Bounds.IntersectRay(checkRay, out distance) && distance <= maxDistance) {
				return true;
			}
		}

		// Check children
		if (children != null) {
			for (int i = 0; i < 8; i++) {
				if (children[i].IsColliding(ref checkRay, maxDistance)) {
					return true;
				}
			}
		}

		return false;
	}

	/// <summary>
	/// Returns an array of objects that intersect with the specified bounds, if any. Otherwise returns an empty array. See also: IsColliding.
	/// </summary>
	/// <param name="checkBounds">Bounds to check. Passing by ref as it improves performance with structs.</param>
	/// <param name="result">List result.</param>
	/// <returns>Objects that intersect with the specified bounds.</returns>
	public void GetColliding(ref Bounds checkBounds, List<T> result) {
		// Are the input bounds at least partially in this node?
		if (!bounds.Intersects(checkBounds)) {
			return;
		}

		// Check against any objects in this node
		for (int i = 0; i < objects.Count; i++) {
			if (objects[i].Bounds.Intersects(checkBounds)) {
				result.Add(objects[i].Obj);
			}
		}

		// Check children
		if (children != null) {
			for (int i = 0; i < 8; i++) {
				children[i].GetColliding(ref checkBounds, result);
			}
		}
	}

	/// <summary>
	/// Returns an array of objects that intersect with the specified ray, if any. Otherwise returns an empty array. See also: IsColliding.
	/// </summary>
	/// <param name="checkRay">Ray to check. Passing by ref as it improves performance with structs.</param>
	/// <param name="maxDistance">Distance to check.</param>
	/// <param name="result">List result.</param>
	/// <returns>Objects that intersect with the specified ray.</returns>
	public void GetColliding(ref Ray checkRay, List<T> result, float maxDistance = float.PositiveInfinity) {
		float distance;
		// Is the input ray at least partially in this node?
		if (!bounds.IntersectRay(checkRay, out distance) || distance > maxDistance) {
			return;
		}

		// Check against any objects in this node
		for (int i = 0; i < objects.Count; i++) {
			if (objects[i].Bounds.IntersectRay(checkRay, out distance) && distance <= maxDistance) {
				result.Add(objects[i].Obj);
			}
		}

		// Check children
		if (children != null) {
			for (int i = 0; i < 8; i++) {
				children[i].GetColliding(ref checkRay, result, maxDistance);
			}
		}
	}

	public void GetWithinFrustum(Plane[] planes, List<T> result) {
		// Is the input node inside the frustum?
		if (!GeometryUtility.TestPlanesAABB(planes, bounds)) {
			return;
		}

		// Check against any objects in this node
		for (int i = 0; i < objects.Count; i++) {
			if (GeometryUtility.TestPlanesAABB(planes, objects[i].Bounds)) {
				result.Add(objects[i].Obj);
			}
		}

		// Check children
		if (children != null) {
			for (int i = 0; i < 8; i++) {
				children[i].GetWithinFrustum(planes, result);
			}
		}
	}

	/// <summary>
	/// Set the 8 children of this octree.
	/// </summary>
	/// <param name="childOctrees">The 8 new child nodes.</param>
	public void SetChildren(BoundsOctreeNode<T>[] childOctrees) {
		if (childOctrees.Length != 8) {
			Debug.LogError("Child octree array must be length 8. Was length: " + childOctrees.Length);
			return;
		}

		children = childOctrees;
	}

	public Bounds GetBounds() {
		return bounds;
	}

	/// <summary>
	/// Draws node boundaries visually for debugging.
	/// Must be called from OnDrawGizmos externally. See also: DrawAllObjects.
	/// </summary>
	/// <param name="depth">Used for recurcive calls to this method.</param>
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

	/// <summary>
	/// Draws the bounds of all objects in the tree visually for debugging.
	/// Must be called from OnDrawGizmos externally. See also: DrawAllBounds.
	/// </summary>
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

	/// <summary>
	/// We can shrink the octree if:
	/// - This node is >= double minLength in length
	/// - All objects in the root node are within one octant
	/// - This node doesn't have children, or does but 7/8 children are empty
	/// We can also shrink it if there are no objects left at all!
	/// </summary>
	/// <param name="minLength">Minimum dimensions of a node in this octree.</param>
	/// <returns>The new root, or the existing one if we didn't shrink.</returns>
	public BoundsOctreeNode<T> ShrinkIfPossible(float minLength) {
		if (BaseLength < (2 * minLength)) {
			return this;
		}
		if (objects.Count == 0 && (children == null || children.Length == 0)) {
			return this;
		}

		// Check objects in root
		int bestFit = -1;
		for (int i = 0; i < objects.Count; i++) {
			OctreeObject curObj = objects[i];
			int newBestFit = BestFitChild(curObj.Bounds.center);
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

		// No objects in entire octree
		if (bestFit == -1) {
			return this;
		}

		// We have children. Use the appropriate child as the new root node
		return children[bestFit];
	}

	/// <summary>
	/// Find which child node this object would be most likely to fit in.
	/// </summary>
	/// <param name="objBounds">The object's bounds.</param>
	/// <returns>One of the eight child octants.</returns>
	public int BestFitChild(Vector3 objBoundsCenter) {
		return (objBoundsCenter.x <= Center.x ? 0 : 1) + (objBoundsCenter.y >= Center.y ? 0 : 4) + (objBoundsCenter.z <= Center.z ? 0 : 2);
	}

    /// <summary>
    /// Checks if this node or anything below it has something in it.
    /// </summary>
    /// <returns>True if this node or any of its children, grandchildren etc have something in them</returns>
    public bool HasAnyObjects() {
        if (objects.Count > 0) return true;

        if (children != null) {
            for (int i = 0; i < 8; i++) {
                if (children[i].HasAnyObjects()) return true;
            }
        }

        return false;
    }

    /*
	/// <summary>
	/// Get the total amount of objects in this node and all its children, grandchildren etc. Useful for debugging.
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

    /// <summary>
    /// Set values for this node. 
    /// </summary>
    /// <param name="baseLengthVal">Length of this node, not taking looseness into account.</param>
    /// <param name="minSizeVal">Minimum size of nodes in this octree.</param>
    /// <param name="loosenessVal">Multiplier for baseLengthVal to get the actual size.</param>
    /// <param name="centerVal">Centre position of this node.</param>
    void SetValues(float baseLengthVal, float minSizeVal, float loosenessVal, Vector3 centerVal) {
		BaseLength = baseLengthVal;
		minSize = minSizeVal;
		looseness = loosenessVal;
		Center = centerVal;
		adjLength = looseness * baseLengthVal;

		// Create the bounding box.
		Vector3 size = new Vector3(adjLength, adjLength, adjLength);
		bounds = new Bounds(Center, size);

		float quarter = BaseLength / 4f;
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

	/// <summary>
	/// Private counterpart to the public Add method.
	/// </summary>
	/// <param name="obj">Object to add.</param>
	/// <param name="objBounds">3D bounding box around the object.</param>
	void SubAdd(T obj, Bounds objBounds) {
		// We know it fits at this level if we've got this far

		// We always put things in the deepest possible child
		// So we can skip some checks if there are children aleady
		if (!HasChildren) {
			// Just add if few objects are here, or children would be below min size
			if (objects.Count < NUM_OBJECTS_ALLOWED || (BaseLength / 2) < minSize) {
				OctreeObject newObj = new OctreeObject { Obj = obj, Bounds = objBounds };
				objects.Add(newObj);
				return; // We're done. No children yet
			}

			// Fits at this level, but we can go deeper. Would it fit there?
			// Create the 8 children
			int bestFitChild;
			if (children == null) {
				Split();
				if (children == null) {
					Debug.LogError("Child creation failed for an unknown reason. Early exit.");
					return;
				}

				// Now that we have the new children, see if this node's existing objects would fit there
				for (int i = objects.Count - 1; i >= 0; i--) {
					OctreeObject existingObj = objects[i];
					// Find which child the object is closest to based on where the
					// object's center is located in relation to the octree's center
					bestFitChild = BestFitChild(existingObj.Bounds.center);
					// Does it fit?
					if (Encapsulates(children[bestFitChild].bounds, existingObj.Bounds)) {
						children[bestFitChild].SubAdd(existingObj.Obj, existingObj.Bounds); // Go a level deeper					
						objects.Remove(existingObj); // Remove from here
					}
				}
			}
		}

		// Handle the new object we're adding now
		int bestFit = BestFitChild(objBounds.center);
		if (Encapsulates(children[bestFit].bounds, objBounds)) {
			children[bestFit].SubAdd(obj, objBounds);
		}
		else {
			// Didn't fit in a child. We'll have to it to this node instead
			OctreeObject newObj = new OctreeObject { Obj = obj, Bounds = objBounds };
			objects.Add(newObj);
		}
	}

	/// <summary>
	/// Private counterpart to the public <see cref="Remove(T, Bounds)"/> method.
	/// </summary>
	/// <param name="obj">Object to remove.</param>
	/// <param name="objBounds">3D bounding box around the object.</param>
	/// <returns>True if the object was removed successfully.</returns>
	bool SubRemove(T obj, Bounds objBounds) {
		bool removed = false;

		for (int i = 0; i < objects.Count; i++) {
			if (objects[i].Obj.Equals(obj)) {
				removed = objects.Remove(objects[i]);
				break;
			}
		}

		if (!removed && children != null) {
			int bestFitChild = BestFitChild(objBounds.center);
			removed = children[bestFitChild].SubRemove(obj, objBounds);
		}

		if (removed && children != null) {
			// Check if we should merge nodes now that we've removed an item
			if (ShouldMerge()) {
				Merge();
			}
		}

		return removed;
	}

	/// <summary>
	/// Splits the octree into eight children.
	/// </summary>
	void Split() {
		float quarter = BaseLength / 4f;
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

	/// <summary>
	/// Merge all children into this node - the opposite of Split.
	/// Note: We only have to check one level down since a merge will never happen if the children already have children,
	/// since THAT won't happen unless there are already too many objects to merge.
	/// </summary>
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

	/// <summary>
	/// Checks if outerBounds encapsulates innerBounds.
	/// </summary>
	/// <param name="outerBounds">Outer bounds.</param>
	/// <param name="innerBounds">Inner bounds.</param>
	/// <returns>True if innerBounds is fully encapsulated by outerBounds.</returns>
	static bool Encapsulates(Bounds outerBounds, Bounds innerBounds) {
		return outerBounds.Contains(innerBounds.min) && outerBounds.Contains(innerBounds.max);
	}

	/// <summary>
	/// Checks if there are few enough objects in this node and its children that the children should all be merged into this.
	/// </summary>
	/// <returns>True there are less or the same abount of objects in this and its children than numObjectsAllowed.</returns>
	bool ShouldMerge() {
		int totalObjects = objects.Count;
		if (children != null) {
			foreach (BoundsOctreeNode<T> child in children) {
				if (child.children != null) {
					// If any of the *children* have children, there are definitely too many to merge,
					// or the child woudl have been merged already
					return false;
				}
				totalObjects += child.objects.Count;
			}
		}
		return totalObjects <= NUM_OBJECTS_ALLOWED;
	}
}