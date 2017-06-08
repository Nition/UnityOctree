using System.Collections.Generic;
using UnityEngine;

// A Dynamic, Loose Octree for storing any objects that can be described with AABB bounds
// See also: PointOctree, where objects are stored as single points and some code can be simplified
// Octree:	An octree is a tree data structure which divides 3D space into smaller partitions (nodes)
//			and places objects into the appropriate nodes. This allows fast access to objects
//			in an area of interest without having to check every object.
// Dynamic: The octree grows or shrinks as required when objects as added or removed
//			It also splits and merges nodes as appropriate. There is no maximum depth.
//			Nodes have a constant - numObjectsAllowed - which sets the amount of items allowed in a node before it splits.
// Loose:	The octree's nodes can be larger than 1/2 their parent's length and width, so they overlap to some extent.
//			This can alleviate the problem of even tiny objects ending up in large nodes if they're near boundaries.
//			A looseness value of 1.0 will make it a "normal" octree.
// T:		The content of the octree can be anything, since the bounds data is supplied separately.

// Originally written for my game Scraps (http://www.scrapsgame.com) but intended to be general-purpose.
// Copyright 2014 Nition, BSD licence (see LICENCE file). http://nition.co
// Unity-based, but could be adapted to work in pure C#

// Note: For loops are often used here since in some cases (e.g. the IsColliding method)
// they actually give much better performance than using Foreach, even in the compiled build.
// Using a LINQ expression is worse again than Foreach.
public class BoundsOctree<T> {
	// The total amount of objects currently in the tree
	public int Count { get; private set; }
	
	// Root node of the octree
	BoundsOctreeNode<T> rootNode;
	// Should be a value between 1 and 2. A multiplier for the base size of a node.
	// 1.0 is a "normal" octree, while values > 1 have overlap
	readonly float looseness;
	// Size that the octree was on creation
	readonly float initialSize;
	// Minimum side length that a node can be - essentially an alternative to having a max depth
	readonly float minSize;
	// For collision visualisation. Automatically removed in builds.
	#if UNITY_EDITOR
	const int numCollisionsToSave = 4;
	readonly Queue<Bounds> lastBoundsCollisionChecks = new Queue<Bounds>();
	readonly Queue<Ray> lastRayCollisionChecks = new Queue<Ray>();
	#endif

	/// <summary>
	/// Constructor for the bounds octree.
	/// </summary>
	/// <param name="initialWorldSize">Size of the sides of the initial node, in metres. The octree will never shrink smaller than this.</param>
	/// <param name="initialWorldPos">Position of the centre of the initial node.</param>
	/// <param name="minNodeSize">Nodes will stop splitting if the new nodes would be smaller than this (metres).</param>
	/// <param name="loosenessVal">Clamped between 1 and 2. Values > 1 let nodes overlap.</param>
	public BoundsOctree(float initialWorldSize, Vector3 initialWorldPos, float minNodeSize, float loosenessVal) {
		if (minNodeSize > initialWorldSize) {
			Debug.LogWarning("Minimum node size must be at least as big as the initial world size. Was: " + minNodeSize + " Adjusted to: " + initialWorldSize);
			minNodeSize = initialWorldSize;
		}
		Count = 0;
		initialSize = initialWorldSize;
		minSize = minNodeSize;
		looseness = Mathf.Clamp(loosenessVal, 1.0f, 2.0f);
		rootNode = new BoundsOctreeNode<T>(initialSize, minSize, loosenessVal, initialWorldPos);
	}

	// #### PUBLIC METHODS ####

	/// <summary>
	/// Add an object.
	/// </summary>
	/// <param name="obj">Object to add.</param>
	/// <param name="objBounds">3D bounding box around the object.</param>
	public void Add(T obj, Bounds objBounds) {
		// Add object or expand the octree until it can be added
		int count = 0; // Safety check against infinite/excessive growth
		while (!rootNode.Add(obj, objBounds)) {
			Grow(objBounds.center - rootNode.Center);
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
	/// Check if the specified bounds intersect with anything in the tree. See also: GetColliding.
	/// </summary>
	/// <param name="checkBounds">bounds to check.</param>
	/// <returns>True if there was a collision.</returns>
	public bool IsColliding(Bounds checkBounds) {
		//#if UNITY_EDITOR
		// For debugging
		//AddCollisionCheck(checkBounds);
		//#endif
		return rootNode.IsColliding(ref checkBounds);
	}

	/// <summary>
	/// Check if the specified ray intersects with anything in the tree. See also: GetColliding.
	/// </summary>
	/// <param name="checkRay">ray to check.</param>
	/// <param name="maxDistance">distance to check.</param>
	/// <returns>True if there was a collision.</returns>
	public bool IsColliding(Ray checkRay, float maxDistance) {
		//#if UNITY_EDITOR
		// For debugging
		//AddCollisionCheck(checkRay);
		//#endif
		return rootNode.IsColliding(ref checkRay, maxDistance);
	}

	/// <summary>
	/// Returns an array of objects that intersect with the specified bounds, if any. Otherwise returns an empty array. See also: IsColliding.
	/// </summary>
	/// <param name="collidingWith">list to store intersections.</param>
	/// <param name="checkBounds">bounds to check.</param>
	/// <returns>Objects that intersect with the specified bounds.</returns>
	public void GetColliding(List<T> collidingWith, Bounds checkBounds) {
		//#if UNITY_EDITOR
		// For debugging
		//AddCollisionCheck(checkBounds);
		//#endif
		rootNode.GetColliding(ref checkBounds, collidingWith);
	}

	/// <summary>
	/// Returns an array of objects that intersect with the specified ray, if any. Otherwise returns an empty array. See also: IsColliding.
	/// </summary>
	/// <param name="collidingWith">list to store intersections.</param>
	/// <param name="checkRay">ray to check.</param>
	/// <param name="maxDistance">distance to check.</param>
	/// <returns>Objects that intersect with the specified ray.</returns>
	public void GetColliding(List<T> collidingWith, Ray checkRay, float maxDistance = float.PositiveInfinity) {
		//#if UNITY_EDITOR
		// For debugging
		//AddCollisionCheck(checkRay);
		//#endif
		rootNode.GetColliding(ref checkRay, collidingWith, maxDistance);
	}

	public Bounds GetMaxBounds()
	{
		return rootNode.GetBounds();
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

	// Intended for debugging. Must be called from OnDrawGizmos externally
	// See also DrawAllBounds and DrawAllObjects
	/// <summary>
	/// Visualises collision checks from IsColliding and GetColliding.
	/// Collision visualisation code is automatically removed from builds so that collision checks aren't slowed down.
	/// </summary>
	#if UNITY_EDITOR
	public void DrawCollisionChecks() {
		int count = 0;
		foreach (Bounds collisionCheck in lastBoundsCollisionChecks) {
			Gizmos.color = new Color(1.0f, 1.0f - ((float)count / numCollisionsToSave), 1.0f);
			Gizmos.DrawCube(collisionCheck.center, collisionCheck.size);
			count++;
		}

		foreach (Ray collisionCheck in lastRayCollisionChecks) {
			Gizmos.color = new Color(1.0f, 1.0f - ((float)count / numCollisionsToSave), 1.0f);
			Gizmos.DrawRay(collisionCheck.origin, collisionCheck.direction);
			count++;
		}
		Gizmos.color = Color.white;
	}
	#endif

	// #### PRIVATE METHODS ####

	/// <summary>
	/// Used for visualising collision checks with DrawCollisionChecks.
	/// Automatically removed from builds so that collision checks aren't slowed down.
	/// </summary>
	/// <param name="checkBounds">bounds that were passed in to check for collisions.</param>
	#if UNITY_EDITOR
	void AddCollisionCheck(Bounds checkBounds) {
		lastBoundsCollisionChecks.Enqueue(checkBounds);
		if (lastBoundsCollisionChecks.Count > numCollisionsToSave) {
			lastBoundsCollisionChecks.Dequeue();
		}
	}
	#endif

	/// <summary>
	/// Used for visualising collision checks with DrawCollisionChecks.
	/// Automatically removed from builds so that collision checks aren't slowed down.
	/// </summary>
	/// <param name="checkRay">ray that was passed in to check for collisions.</param>
	#if UNITY_EDITOR
	void AddCollisionCheck(Ray checkRay) {
		lastRayCollisionChecks.Enqueue(checkRay);
		if (lastRayCollisionChecks.Count > numCollisionsToSave) {
			lastRayCollisionChecks.Dequeue();
		}
	}
	#endif

	/// <summary>
	/// Grow the octree to fit in all objects.
	/// </summary>
	/// <param name="direction">Direction to grow.</param>
	void Grow(Vector3 direction) {
		int xDirection = direction.x >= 0 ? 1 : -1;
		int yDirection = direction.y >= 0 ? 1 : -1;
		int zDirection = direction.z >= 0 ? 1 : -1;
		BoundsOctreeNode<T> oldRoot = rootNode;
		float half = rootNode.BaseLength / 2;
		float newLength = rootNode.BaseLength * 2;
		Vector3 newCenter = rootNode.Center + new Vector3(xDirection * half, yDirection * half, zDirection * half);

		// Create a new, bigger octree root node
		rootNode = new BoundsOctreeNode<T>(newLength, minSize, looseness, newCenter);

		if (oldRoot.HasAnyObjects())
		{
			// Create 7 new octree children to go with the old root as children of the new root
			int rootPos = GetRootPosIndex(xDirection, yDirection, zDirection);
			BoundsOctreeNode<T>[] children = new BoundsOctreeNode<T>[8];
			for (int i = 0; i < 8; i++)
			{
				if (i == rootPos)
				{
					children[i] = oldRoot;
				}
				else
				{
					xDirection = i % 2 == 0 ? -1 : 1;
					yDirection = i > 3 ? -1 : 1;
					zDirection = (i < 2 || (i > 3 && i < 6)) ? -1 : 1;
					children[i] = new BoundsOctreeNode<T>(rootNode.BaseLength, minSize, looseness, newCenter + new Vector3(xDirection * half, yDirection * half, zDirection * half));
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

	/// <summary>
	/// Used when growing the octree. Works out where the old root node would fit inside a new, larger root node.
	/// </summary>
	/// <param name="xDir">X direction of growth. 1 or -1.</param>
	/// <param name="yDir">Y direction of growth. 1 or -1.</param>
	/// <param name="zDir">Z direction of growth. 1 or -1.</param>
	/// <returns>Octant where the root node should be.</returns>
	static int GetRootPosIndex(int xDir, int yDir, int zDir) {
		int result = xDir > 0 ? 1 : 0;
		if (yDir < 0) result += 4;
		if (zDir > 0) result += 2;
		return result;
	}
}
