using System.Collections.Generic;
using UnityEngine;

// A node in a PointOctree
// Copyright 2014 Nition, BSD licence (see LICENCE file). http://nition.co
public class PointOctreeNode<T> where T : class {
	// Centre of this node
	public Vector3 Center { get; private set; }
	// Length of the sides of this node
	public float SideLength { get; private set; }

	// Minimum size for a node in this octree
	float minSize;
	// Bounding box that represents this node
	Bounds bounds = default(Bounds);
	// Objects in this node
	readonly List<OctreeObject> objects = new List<OctreeObject>();
	// Child nodes, if any
	PointOctreeNode<T>[] children = null;
	// bounds of potential children to this node. These are actual size (with looseness taken into account), not base size
	Bounds[] childBounds;
	// If there are already numObjectsAllowed in a node, we split it into children
	// A generally good number seems to be something around 8-15
	const int NUM_OBJECTS_ALLOWED = 8;
	// For reverting the bounds size after temporary changes
	Vector3 actualBoundsSize;

	// An object in the octree
	class OctreeObject {
		public T Obj;
		public Vector3 Pos;
	}

	/// <summary>
	/// Constructor.
	/// </summary>
	/// <param name="baseLengthVal">Length of this node, not taking looseness into account.</param>
	/// <param name="minSizeVal">Minimum size of nodes in this octree.</param>
	/// <param name="centerVal">Centre position of this node.</param>
	public PointOctreeNode(float baseLengthVal, float minSizeVal, Vector3 centerVal) {
		SetValues(baseLengthVal, minSizeVal, centerVal);
	}

	// #### PUBLIC METHODS ####

	/// <summary>
	/// Add an object.
	/// </summary>
	/// <param name="obj">Object to add.</param>
	/// <param name="objPos">Position of the object.</param>
	/// <returns></returns>
	public bool Add(T obj, Vector3 objPos) {
		if (!Encapsulates(bounds, objPos)) {
			return false;
		}
		SubAdd(obj, objPos);
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
	/// Return objects that are within maxDistance of the specified ray.
	/// </summary>
	/// <param name="ray">The ray.</param>
	/// <param name="maxDistance">Maximum distance from the ray to consider.</param>
	/// <param name="result">List result.</param>
	/// <returns>Objects within range.</returns>
	public void GetNearby(ref Ray ray, ref float maxDistance, List<T> result) {
		// Does the ray hit this node at all?
		// Note: Expanding the bounds is not exactly the same as a real distance check, but it's fast.
		// TODO: Does someone have a fast AND accurate formula to do this check?
		bounds.Expand(new Vector3(maxDistance * 2, maxDistance * 2, maxDistance * 2));
		bool intersected = bounds.IntersectRay(ray);
		bounds.size = actualBoundsSize;
		if (!intersected) {
			return;
		}

		// Check against any objects in this node
		for (int i = 0; i < objects.Count; i++) {
			if (DistanceToRay(ray, objects[i].Pos) <= maxDistance) {
				result.Add(objects[i].Obj);
			}
		}

		// Check children
		if (children != null) {
			for (int i = 0; i < 8; i++) {
				children[i].GetNearby(ref ray, ref maxDistance, result);
			}
		}
	}

	/// <summary>
	/// Set the 8 children of this octree.
	/// </summary>
	/// <param name="childOctrees">The 8 new child nodes.</param>
	public void SetChildren(PointOctreeNode<T>[] childOctrees) {
		if (childOctrees.Length != 8) {
			Debug.LogError("Child octree array must be length 8. Was length: " + childOctrees.Length);
			return;
		}

		children = childOctrees;
	}

	/// <summary>
	/// Draws node boundaries visually for debugging.
	/// Must be called from OnDrawGizmos externally. See also: DrawAllObjects.
	/// </summary>
	/// <param name="depth">Used for recurcive calls to this method.</param>
	public void DrawAllBounds(float depth = 0) {
		float tintVal = depth / 7; // Will eventually get values > 1. Color rounds to 1 automatically
		Gizmos.color = new Color(tintVal, 0, 1.0f - tintVal);

		Bounds thisBounds = new Bounds(Center, new Vector3(SideLength, SideLength, SideLength));
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
	/// NOTE: marker.tif must be placed in your Unity /Assets/Gizmos subfolder for this to work.
	/// </summary>
	public void DrawAllObjects() {
		float tintVal = SideLength / 20;
		Gizmos.color = new Color(0, 1.0f - tintVal, tintVal, 0.25f);

		foreach (OctreeObject obj in objects) {
			Gizmos.DrawIcon(obj.Pos, "marker.tif", true);
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
	public PointOctreeNode<T> ShrinkIfPossible(float minLength) {
		if (SideLength < (2 * minLength)) {
			return this;
		}
		if (objects.Count == 0 && children.Length == 0) {
			return this;
		}

		// Check objects in root
		int bestFit = -1;
		for (int i = 0; i < objects.Count; i++) {
			OctreeObject curObj = objects[i];
			int newBestFit = BestFitChild(curObj.Pos);
			if (i == 0 || newBestFit == bestFit) {
				if (bestFit < 0) {
					bestFit = newBestFit;
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
			SetValues(SideLength / 2, minSize, childBounds[bestFit].center);
			return this;
		}

		// We have children. Use the appropriate child as the new root node
		return children[bestFit];
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
	/// <param name="centerVal">Centre position of this node.</param>
	void SetValues(float baseLengthVal, float minSizeVal, Vector3 centerVal) {
		SideLength = baseLengthVal;
		minSize = minSizeVal;
		Center = centerVal;

		// Create the bounding box.
		actualBoundsSize = new Vector3(SideLength, SideLength, SideLength);
		bounds = new Bounds(Center, actualBoundsSize);

		float quarter = SideLength / 4f;
		float childActualLength = SideLength / 2;
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
	/// <param name="objPos">Position of the object.</param>
	void SubAdd(T obj, Vector3 objPos) {
		// We know it fits at this level if we've got this far
		// Just add if few objects are here, or children would be below min size
		if (objects.Count < NUM_OBJECTS_ALLOWED || (SideLength / 2) < minSize) {
			OctreeObject newObj = new OctreeObject { Obj = obj, Pos = objPos };
			//Debug.Log("ADD " + obj.name + " to depth " + depth);
			objects.Add(newObj);
		}
		else { // Enough objects in this node already: Create new children
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
					bestFitChild = BestFitChild(existingObj.Pos);
					children[bestFitChild].SubAdd(existingObj.Obj, existingObj.Pos); // Go a level deeper					
					objects.Remove(existingObj); // Remove from here
				}
			}

			// Now handle the new object we're adding now
			bestFitChild = BestFitChild(objPos);
			children[bestFitChild].SubAdd(obj, objPos);
		}
	}

	/// <summary>
	/// Splits the octree into eight children.
	/// </summary>
	void Split() {
		float quarter = SideLength / 4f;
		float newLength = SideLength / 2;
		children = new PointOctreeNode<T>[8];
		children[0] = new PointOctreeNode<T>(newLength, minSize, Center + new Vector3(-quarter, quarter, -quarter));
		children[1] = new PointOctreeNode<T>(newLength, minSize, Center + new Vector3(quarter, quarter, -quarter));
		children[2] = new PointOctreeNode<T>(newLength, minSize, Center + new Vector3(-quarter, quarter, quarter));
		children[3] = new PointOctreeNode<T>(newLength, minSize, Center + new Vector3(quarter, quarter, quarter));
		children[4] = new PointOctreeNode<T>(newLength, minSize, Center + new Vector3(-quarter, -quarter, -quarter));
		children[5] = new PointOctreeNode<T>(newLength, minSize, Center + new Vector3(quarter, -quarter, -quarter));
		children[6] = new PointOctreeNode<T>(newLength, minSize, Center + new Vector3(-quarter, -quarter, quarter));
		children[7] = new PointOctreeNode<T>(newLength, minSize, Center + new Vector3(quarter, -quarter, quarter));
	}

	/// <summary>
	/// Merge all children into this node - the opposite of Split.
	/// Note: We only have to check one level down since a merge will never happen if the children already have children,
	/// since THAT won't happen unless there are already too many objects to merge.
	/// </summary>
	void Merge() {
		// Note: We know children != null or we wouldn't be merging
		for (int i = 0; i < 8; i++) {
			PointOctreeNode<T> curChild = children[i];
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
	/// Checks if outerBounds encapsulates the given point.
	/// </summary>
	/// <param name="outerBounds">Outer bounds.</param>
	/// <param name="point">Point.</param>
	/// <returns>True if innerBounds is fully encapsulated by outerBounds.</returns>
	static bool Encapsulates(Bounds outerBounds, Vector3 point) {
		return outerBounds.Contains(point);
	}

	/// <summary>
	/// Find which child node this object would be most likely to fit in.
	/// </summary>
	/// <param name="objPos">The object's position.</param>
	/// <returns>One of the eight child octants.</returns>
	int BestFitChild(Vector3 objPos) {
		return (objPos.x <= Center.x ? 0 : 1) + (objPos.y >= Center.y ? 0 : 4) + (objPos.z <= Center.z ? 0 : 2);
	}

	/// <summary>
	/// Checks if there are few enough objects in this node and its children that the children should all be merged into this.
	/// </summary>
	/// <returns>True there are less or the same abount of objects in this and its children than numObjectsAllowed.</returns>
	bool ShouldMerge() {
		int totalObjects = objects.Count;
		if (children != null) {
			foreach (PointOctreeNode<T> child in children) {
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

	/// <summary>
	/// Returns the closest distance to the given ray from a point.
	/// </summary>
	/// <param name="ray">The ray.</param>
	/// <param name="point">The point to check distance from the ray.</param>
	/// <returns>Distance from the point to the closest point of the ray.</returns>
	public static float DistanceToRay(Ray ray, Vector3 point) {
		return Vector3.Cross(ray.direction, point - ray.origin).magnitude;
	}
}
