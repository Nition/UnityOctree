UnityOctree
===========

A dynamic octree implementation for Unity written in C#.    
Originally written for my game [Scraps](http://www.scrapsgame.com) but intended to be general-purpose.

There are two octree implementations here:    
**BoundsOctree** stores any type of object, with the object boundaries defined as an axis-aligned bounding box. It's a dynamic octree and can also be a loose octree.   
**PointOctree** is the same basic implementation, but stores objects as a point in space instead of bounds. This allows some simplification of the code. It's a dynamic octree as well.

**Octree:** An octree a tree data structure which divides 3D space into smaller partitions (nodes) and places objects into the appropriate nodes. This allows fast access to objects in an area of interest without having to check every object.    
***Dynamic:** The octree grows or shrinks as required when objects as added or removed. It also splits and merges nodes as appropriate. There is no maximum depth. Nodes have a constant - numObjectsAllowed - which sets the amount of items allowed in a node before it splits.    
***Loose:** The octree's nodes can be larger than 1/2 their parent's length and width, so they overlap to some extent. This can alleviate the problem of even tiny objects ending up in large nodes if they're near boundaries. A looseness value of 1.0 will make it a "normal" octree.

A few functions are implemented :    
With BoundsOctree, you can pass in bounds and get a true/false answer for if it's colliding with anything (IsColliding), or get a list of everything it's collising with (GetColliding).
With PointOctree, you can cast a ray and get a list of objects that are within x distance of that ray (GetNearby).

It shouldn't be too hard to implement additional functions if needed.

Example Usage
===========

**Create An Octree**

```C#
// Initial size (metres), initial centre position, minimum node size (metres), looseness
BoundsOctree<GameObject> boundsTree = new BoundsOctree<GameObject>(15, MyObjectContainer.position, 1, 1.25f);
// Initial size (metres), initial centre position, minimum node size (metres)
PointOctree<GameObject> pointTree = new PointOctree<GameObject>(15, MyObjectContainer.position, 1);
````

- The initial size should ideally cover an area just encompassing all your objects. If you guess too small, the octree will grow automatically, but it will be eight times the size (double dimensions), which could end up covering a large area unnecessarily. At the same time, the octree will be able to shrink down again if the outlying objects are removed. If you guess an initial size that's too big, it won't be able to shrink down, but that may be the safer bet. Don't worry too much: In reality the starting value isn't hugely important for performance.
- The initial position should ideally be in the centre of where your objects are.
- The minimum node size is effectively a depth limit; it limits how many times the tree can divide. If all your objects are 1m+ wide, you wouldn't want to set it smaller than 1m.
- The best way to choose a looseness value is to try different values (between 1 and maybe 1.5) and check the performance with your particular data. Generally around 1.25 is reliably good.

**Add And Remove**