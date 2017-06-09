UnityOctree
===========

A dynamic octree implementation for Unity written in C#.    
Originally written for my game [Scraps](http://www.scrapsgame.com) but intended to be general-purpose.

There are two octree implementations here:    
**BoundsOctree** stores any type of object, with the object boundaries defined as an axis-aligned bounding box. It's a dynamic octree and can also be a loose octree.   
**PointOctree** is the same basic implementation, but stores objects as a point in space instead of bounds. This allows some simplification of the code. It's a dynamic octree as well.

**Octree:** An octree a tree data structure which divides 3D space into smaller partitions (nodes) and places objects into the appropriate nodes. This allows fast access to objects in an area of interest without having to check every object.

**Dynamic:** The octree grows or shrinks as required when objects are added or removed. It also splits and merges nodes as appropriate. There is no maximum depth. Nodes have a constant (*numObjectsAllowed*) which sets the amount of items allowed in a node before it splits.

**Loose:** The octree's nodes can be larger than 1/2 their parent's length and width, so they overlap to some extent. This can alleviate the problem of even tiny objects ending up in large nodes if they're near boundaries. A looseness value of 1.0 will make it a "normal" octree.

**A few functions are implemented:**

With BoundsOctree, you can pass in bounds and get a true/false answer for if it's colliding with anything (IsColliding), or get a list of everything it's collising with (GetColliding).
With PointOctree, you can cast a ray and get a list of objects that are within x distance of that ray (GetNearby).

It shouldn't be too hard to implement additional functions if needed. For instance, PointOctree could check for points that fall inside a given bounds.

**Considerations:**

Tree searches are recursive, so there is technically the potential for a stack overflow on very large trees. The minNodeSize parameter limits node side and hence the depth of the tree, putting a cap on recursion.

I tried switching to an iterative solution using my own stack, but creating and manipulating the stack made the results generally slower than the simple recursive solution. However, I wouldn't be surprised it someone smarter than me can come up with a faster solution.

Another note: You may notice when viewing the bounds visualisation that the child nodes' outer edges are all inside the parent nodes. But loose octrees are meant to make the inner nodes bigger... aren't they? The answer is yes, but the parent nodes are *also* bigger, and e.g. ((1.2 * 10) - 10) is bigger than ((1.2 * 5) - 5), so the parent node ends up being bigger overall.

This seems to be the standard way that loose octrees are done. I did an experiment: I tried making the child node dimensions looseness * the parent's actual size, instead of looseness * the parent's base size before looseness is applied. This seems more intuitively correct to me, but performance seems to be about the same.

Example Usage
===========

**Create An Octree**

```C#
// Initial size (metres), initial centre position, minimum node size (metres), looseness
BoundsOctree<GameObject> boundsTree = new BoundsOctree<GameObject>(15, MyContainer.position, 1, 1.25f);
// Initial size (metres), initial centre position, minimum node size (metres)
PointOctree<GameObject> pointTree = new PointOctree<GameObject>(15, MyContainer.position, 1);
```

- Here I've used GameObject, but the tree's content can be any type you like (as long as it's all the *same* type
- The initial size should ideally cover an area just encompassing all your objects. If you guess too small, the octree will grow automatically, but it will be eight times the size (double dimensions), which could end up covering a large area unnecessarily. At the same time, the octree will be able to shrink down again if the outlying objects are removed. If you guess an initial size that's too big, it won't be able to shrink down, but that may be the safer option. Don't worry too much: In reality the starting value isn't hugely important for performance.
- The initial position should ideally be in the centre of where your objects are.
- The minimum node size is effectively a depth limit; it limits how many times the tree can divide. If all your objects are e.g. 1m+ wide, you wouldn't want to set it smaller than 1m.
- The best way to choose a looseness value is to try different values (between 1 and maybe 1.5) and check the performance with your particular data. Generally around 1.2 is good.

**Add And Remove**

```C#
boundsTree.Add(myObject, myBounds);
boundsTree.Remove(myObject);

pointTree.Add(myObject, myVector3);
boundsTree.Remove(myObject);
```
- The object's type depends on the tree's type.
- The bounds or point determine where it's inserted.

**Built-in Functions**

```C#
bool isColliding = boundsTree.IsColliding(bounds);
```

```C#
List<GameObject> collidingWith = new List<GameObject>();
boundsTree.GetColliding(collidingWith, bounds);
```
- Where GameObject is the type of the octree

```C#
pointTree.GetNearby(myRay, 4);
```
- Where myRay is a [Ray](http://docs.unity3d.com/Documentation/ScriptReference/Ray.html)
- In this case we're looking for any point within 4m of the closest point on the ray

**Debugging Visuals**

![Visualisation example.](https://raw.github.com/nition/UnityOctree/master/octree-visualisation.jpg)

```C#
void OnDrawGizmos() {
	boundsTree.DrawAllBounds(); // Draw node boundaries
	boundsTree.DrawAllObjects(); // Draw object boundaries
	boundsTree.DrawCollisionChecks(); // Draw the last *numCollisionsToSave* collision check boundaries

	pointTree.DrawAllBounds(); // Draw node boundaries
	pointTree.DrawAllObjects(); // Mark object positions
}
```
- Must be in Unity's OnDrawGizmos() method in a class that inherits from MonoBehaviour
- Point octrees need the marker.tif file to be in your Unity /Assets/Gizmos subfolder for DrawAllObjects to work


**Potential Improvements**

A significant portion of the octree's time is taken just to traverse through the nodes themselves. There's potential for a performance increase there, maybe by linearising the tree - that is, representing all the nodes as a one-dimensional array lookup.

