UnityOctree
===========

A dynamic octree implementation for Unity written in C#.    
Originally written for my game [Scraps](http://www.scrapsgame.com) but intended to be general-purpose.

There are two octree implementations here:

**BoundsOctree** stores any type of object, with the object boundaries defined as an axis-aligned bounding box.

**PointOctree** is the same basic implementation, but stores objects as a point in space instead of bounds. This allows some simplification of the code.

Example Usage
===========