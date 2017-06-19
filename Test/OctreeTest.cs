using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
namespace UnityOctree
{
    public class OctreeTest : MonoBehaviour
    {

        public UnityEngine.UI.Text iterationsDisplay;
        public UnityEngine.UI.Text totalTimeDisplay;
        public UnityEngine.UI.Text averageTimeDisplay;
        public UnityEngine.UI.Text numObjectsDisplay;
        public UnityEngine.UI.Text numNodesDisplay;
        public bool drawNodes;
        public bool drawConnections;
        public bool drawObjects;

        LooseOctree<GameObject> tree = null;
        List<LooseOctree<GameObject>.OctreeObject> treeObj = new List<LooseOctree<GameObject>.OctreeObject>();
        List<Vector3> positions = new List<Vector3>();
        // Use this for initialization
        void Start()
        {
            {
                for (int x = -99; x <= 99; x += 30)
                {
                    for (int y = -99; y <= 99; y += 30)
                    {
                        for (int z = -99; z <= 99; z += 30)
                        {
                            positions.Add(new Vector3(x, y, z));
                        }
                    }
                }

            }
            numObjectsDisplay.text = "Objects in tree: " + positions.Count;
            float[] results = new float[4];
            float total = 0;
            iterationsDisplay.text = "Iterations: " + results.Length;
            tree = new LooseOctree<GameObject>(200F, Vector3.zero, 1.25F);
            Stopwatch timer = new Stopwatch();
            for (int i = 0; i < results.Length; i++)
            {
                treeObj.Clear();
                timer.Reset();
                timer.Start();
                PopulateTree();
                timer.Stop();
                results[i] = timer.ElapsedMilliseconds;
                timer.Reset();
                total += results[0];
            }
            numNodesDisplay.text = "Nodes: " + tree.NodeCount();
            totalTimeDisplay.text = "Total time: " + total + "ms";
            averageTimeDisplay.text = "Average time: " + total / results.Length + "ms";

            tree.Print();
            tree = null;
            treeObj.Clear();
            tree = new LooseOctree<GameObject>(200F, Vector3.zero, 1.25F);
            StartCoroutine(PopulateTreeSlow());
        }
        IEnumerator PopulateTreeSlow()
        {
            int count = 0;
            int thisRun = 0;
            GameObject obj = new GameObject("Dummy");
            foreach (Vector3 pos in positions)
            {
                count++;
                thisRun++;
                treeObj.Add(tree.Add(obj, pos));
                if (thisRun == 100)
                {
                    thisRun = 0;
                    yield return new WaitForEndOfFrame();
                }
            }
            List<LooseOctree<GameObject>.OctreeObject> removals = new List<LooseOctree<GameObject>.OctreeObject>();
            while (treeObj.Count > 0)
            {
                foreach (LooseOctree<GameObject>.OctreeObject obje in treeObj)
                {
                    if (Random.Range(0, 100) > 50)
                    {
                        obje.Remove();
                        removals.Add(obje);
                    }
                    yield return new WaitForSeconds(0.1F);
                }
                foreach (LooseOctree<GameObject>.OctreeObject obje in removals)
                {
                    treeObj.Remove(obje);
                }
            }
        }

        void PopulateTree()
        {
            int count = 0;
            int thisRun = 0;
            GameObject obj = new GameObject("Dummy");
            foreach (Vector3 pos in positions)
            {
                count++;
                thisRun++;
                treeObj.Add(tree.Add(obj, pos));
            }
            foreach (LooseOctree<GameObject>.OctreeObject obje in treeObj)
            {
                if (Random.Range(0, 100) > 50)
                {
                    obje.Remove();
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (tree != null)
                tree.DrawAll(drawNodes, drawObjects, drawConnections);
        }
        // Update is called once per frame
        void Update()
        {

        }
    }
}
