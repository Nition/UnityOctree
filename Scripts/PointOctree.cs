using System.Collections.Generic;
using UnityEngine;

// A Dynamic Octree for storing any objects that can be described as a single point
// See also: BoundsOctree, where objects are described by AABB bounds
// Octree:	An octree is a tree data structure which divides 3D space into smaller partitions (nodes)
//			and places objects into the appropriate nodes. This allows fast access to objects
//			in an area of interest without having to check every object.
// Dynamic: The octree grows or shrinks as required when objects as added or removed
//			It also splits and merges nodes as appropriate. There is no maximum depth.
//			Nodes have a constant - numObjectsAllowed - which sets the amount of items allowed in a node before it splits.
// T:		The content of the octree can be anything, since the bounds data is supplied separately.

// Originally written for my game Scraps (http://www.scrapsgame.com) but intended to be general-purpose.
// Copyright 2014 Nition, BSD licence (see LICENCE file). www.momentstudio.co.nz
// Unity-based, but could be adapted to work in pure C#
public class PointOctree<T> {
	// The total amount of objects currently in the tree
	public int Count { get; private set; }

	// Root node of the octree
	PointOctreeNode<T> rootNode;

	// Size that the octree was on creation
	readonly float initialSize;

	// Minimum side length that a node can be - essentially an alternative to having a max depth
	readonly float minSize;

	/// <summary>
	/// Constructor for the point octree.
	/// </summary>
	/// <param name="initialWorldSize">Size of the sides of the initial node. The octree will never shrink smaller than this.</param>
	/// <param name="initialWorldPos">Position of the centre of the initial node.</param>
	/// <param name="minNodeSize">Nodes will stop splitting if the new nodes would be smaller than this.</param>
	public PointOctree(float initialWorldSize, Vector3 initialWorldPos, float minNodeSize) {
		if (minNodeSize > initialWorldSize) {
			Debug.LogWarning("Minimum node size must be at least as big as the initial world size. Was: " + minNodeSize + " Adjusted to: " + initialWorldSize);
			minNodeSize = initialWorldSize;
		}
		Count = 0;
		initialSize = initialWorldSize;
		minSize = minNodeSize;
		rootNode = new PointOctreeNode<T>(initialSize, minSize, initialWorldPos);
	}

	// #### PUBLIC METHODS ####

	/// <summary>
	/// Add an object.
	/// </summary>
	/// <param name="obj">Object to add.</param>
	/// <param name="objPos">Position of the object.</param>
	public void Add(T obj, Vector3 objPos) {
		// Add object or expand the octree until it can be added
		int count = 0; // Safety check against infinite/excessive growth
		while (!rootNode.Add(obj, objPos)) {
			Grow(objPos - rootNode.Center);
			if (++count > 20) {
				Debug.LogError("Aborted Add operation as it seemed to be going on forever (" + (count - 1) + ") attempts at growing the octree.");
				return;
			}
		}
		Count++;
	}

	/// <summary>
	/// Remove an object. Makes the assumption that the object only exists once in the tree.
	/// </summary>
	/// <param name="obj">Object to remove.</param>
	/// <returns>True if the object was removed successfully.</returns>
	public bool Remove(T obj) {
		bool removed = rootNode.Remove(obj);

		// See if we can shrink the octree down now that we've removed the item
		if (removed) {
			Count--;
			Shrink();
		}

		return removed;
	}

	/// <summary>
	/// Removes the specified object at the given position. Makes the assumption that the object only exists once in the tree.
	/// </summary>
	/// <param name="obj">Object to remove.</param>
	/// <param name="objPos">Position of the object.</param>
	/// <returns>True if the object was removed successfully.</returns>
	public bool Remove(T obj, Vector3 objPos) {
		bool removed = rootNode.Remove(obj, objPos);

		// See if we can shrink the octree down now that we've removed the item
		if (removed) {
			Count--;
			Shrink();
		}

		return removed;
	}

	/// <summary>
	/// Returns objects that are within <paramref name="maxDistance"/> of the specified ray.
	/// If none, returns false. Uses supplied list for results.
	/// </summary>
	/// <param name="ray">The ray. Passing as ref to improve performance since it won't have to be copied.</param>
	/// <param name="maxDistance">Maximum distance from the ray to consider</param>
	/// <param name="nearBy">Pre-initialized list to populate</param>
	/// <returns>True if items are found, false if not</returns>
	public bool GetNearbyNonAlloc(Ray ray, float maxDistance, List<T> nearBy) {
		nearBy.Clear();
		rootNode.GetNearby(ref ray, maxDistance, nearBy);
		if (nearBy.Count > 0)
			return true;
		return false;
	}

	/// <summary>
	/// Returns objects that are within <paramref name="maxDistance"/> of the specified ray.
	/// If none, returns an empty array (not null).
	/// </summary>
	/// <param name="ray">The ray. Passing as ref to improve performance since it won't have to be copied.</param>
	/// <param name="maxDistance">Maximum distance from the ray to consider.</param>
	/// <returns>Objects within range.</returns>
	public T[] GetNearby(Ray ray, float maxDistance) {
		List<T> collidingWith = new List<T>();
		rootNode.GetNearby(ref ray, maxDistance, collidingWith);
		return collidingWith.ToArray();
	}

	/// <summary>
	/// Returns objects that are within <paramref name="maxDistance"/> of the specified position.
	/// If none, returns an empty array (not null).
	/// </summary>
	/// <param name="position">The position. Passing as ref to improve performance since it won't have to be copied.</param>
	/// <param name="maxDistance">Maximum distance from the position to consider.</param>
	/// <returns>Objects within range.</returns>
	public T[] GetNearby(Vector3 position, float maxDistance) {
		List<T> collidingWith = new List<T>();
		rootNode.GetNearby(ref position, maxDistance, collidingWith);
		return collidingWith.ToArray();
	}

	/// <summary>
	/// Returns objects that are within <paramref name="maxDistance"/> of the specified position.
	/// If none, returns false. Uses supplied list for results.
	/// </summary>
	/// <param name="maxDistance">Maximum distance from the position to consider</param>
	/// <param name="nearBy">Pre-initialized list to populate</param>
	/// <returns>True if items are found, false if not</returns>
	public bool GetNearbyNonAlloc(Vector3 position, float maxDistance, List<T> nearBy) {
		nearBy.Clear();
		rootNode.GetNearby(ref position, maxDistance, nearBy);
		if (nearBy.Count > 0)
			return true;
		return false;
	}

	/// <summary>
	/// Return all objects in the tree.
	/// If none, returns an empty array (not null).
	/// </summary>
	/// <returns>All objects.</returns>
	public ICollection<T> GetAll() {
		List<T> objects = new List<T>(Count);
		rootNode.GetAll(objects);
		return objects;
	}

	/// <summary>
	/// Draws node boundaries visually for debugging.
	/// Must be called from OnDrawGizmos externally. See also: DrawAllObjects.
	/// </summary>
	public void DrawAllBounds() {
		rootNode.DrawAllBounds();
	}

	/// <summary>
	/// Draws the bounds of all objects in the tree visually for debugging.
	/// Must be called from OnDrawGizmos externally. See also: DrawAllBounds.
	/// </summary>
	public void DrawAllObjects() {
		rootNode.DrawAllObjects();
	}

	// #### PRIVATE METHODS ####

	/// <summary>
	/// Grow the octree to fit in all objects.
	/// </summary>
	/// <param name="direction">Direction to grow.</param>
	void Grow(Vector3 direction) {
		int xDirection = direction.x >= 0 ? 1 : -1;
		int yDirection = direction.y >= 0 ? 1 : -1;
		int zDirection = direction.z >= 0 ? 1 : -1;
		PointOctreeNode<T> oldRoot = rootNode;
		float half = rootNode.SideLength / 2;
		float newLength = rootNode.SideLength * 2;
		Vector3 newCenter = rootNode.Center + new Vector3(xDirection * half, yDirection * half, zDirection * half);

		// Create a new, bigger octree root node
		rootNode = new PointOctreeNode<T>(newLength, minSize, newCenter);

        if (oldRoot.HasAnyObjects()) {
            // Create 7 new octree children to go with the old root as children of the new root
            int rootPos = rootNode.BestFitChild(oldRoot.Center);
            PointOctreeNode<T>[] children = new PointOctreeNode<T>[8];
            for (int i = 0; i < 8; i++) {
                if (i == rootPos) {
                    children[i] = oldRoot;
                }
                else {
                    xDirection = i % 2 == 0 ? -1 : 1;
                    yDirection = i > 3 ? -1 : 1;
                    zDirection = (i < 2 || (i > 3 && i < 6)) ? -1 : 1;
                    children[i] = new PointOctreeNode<T>(oldRoot.SideLength, minSize, newCenter + new Vector3(xDirection * half, yDirection * half, zDirection * half));
                }
            }

            // Attach the new children to the new root node
            rootNode.SetChildren(children);
        }
	}

	/// <summary>
	/// Shrink the octree if possible, else leave it the same.
	/// </summary>
	void Shrink() {
		rootNode = rootNode.ShrinkIfPossible(initialSize);
	}
}
