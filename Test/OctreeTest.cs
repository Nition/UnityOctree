using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OctreeTest : MonoBehaviour
{

    LooseOctree<GameObject> tree = null;
    List<GameObject> objects = new List<GameObject>();
    // Use this for initialization
    void Start()
    {
        tree = new LooseOctree<GameObject>(200F, Vector3.zero, 1.25F);
        int count = 0;
        for (int x = -1000; x <= 1000; x += 40)
        {
            for (int y = -1000; y <= 1000; y += 40)
            {
                for (int z = -1000; z <= 1000; z += 40)
                {
                    count++;
                    GameObject obj = new GameObject("TestObject" + count);
                    objects.Add(obj);
                    obj.transform.position = new Vector3(x, y, z);
                }
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (tree != null)
            tree.DrawAllNodes();
    }
    // Update is called once per frame
    void Update()
    {

    }
}
