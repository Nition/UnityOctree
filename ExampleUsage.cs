using UnityEngine;

public class ExampleUsage : MonoBehaviour {

	public Transform ObjectContainer;
	public BoxCollider[] Colliders;
	public BoxCollider CollideCheck;
	public GameObject TestingObj;

	// Creation
	BoundsOctree<GameObject> boundsTree;
	//PointOctree<GameObject> pointTree;

	void Start () {
		boundsTree = new BoundsOctree<GameObject>(15, ObjectContainer.position, 1, 1.25f);
		//pointTree = new PointOctree<GameObject>(15, ObjectContainer.position, 1);

		InvokeRepeating("AddAll", 0, 4);
		InvokeRepeating("RemoveAll", 2, 4);
	}

	void AddAll() {
		foreach (BoxCollider col in Colliders) {
			// Create a special bounds object with an unrotated cube because rotated cubes report AABB bounds on their colliders
			// We always want to add them as if they're non-rotated
			Bounds thisBounds = new Bounds(col.transform.position, new Vector3(1, 1, 1));
			boundsTree.Add(col.gameObject, thisBounds);
			//pointTree.Add(col.gameObject, col.transform.position, ObjectContainer.position, ObjectContainer.rotation);
		}
	}

	void RemoveAll() {
		foreach (BoxCollider col in Colliders) {
			boundsTree.Remove(col.gameObject);
		}
	}

	bool CubesAreCollidingNieve(BoxCollider cube) {
		Bounds bounds = cube.bounds;
		bounds.center = Quaternion.Inverse(ObjectContainer.rotation) * (bounds.center - ObjectContainer.position);
		
		foreach (BoxCollider bc in Colliders) {
			if (bc.bounds.Intersects(bounds)) {
				return true;
			}
		}
		return false;
	}

	bool CubesAreCollidingOctree(BoxCollider cube) {
		return boundsTree.IsColliding(cube.bounds);
	}

	// Draws the octree visually, for debugging
	void OnDrawGizmos() {
		boundsTree.DrawAllBounds();
		//boundsTree.DrawAllObjects();

		//pointTree.DrawAllBounds();
		//pointTree.DrawAllObjects();
	}
}
