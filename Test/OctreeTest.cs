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
        List<Vector3> positions = new List<Vector3>();
        // Use this for initialization
        void Start()
        {
            {
                for (int x = -99; x <= 99; x += 3)
                {
                    for (int y = -99; y <= 99; y += 3)
                    {
                        for (int z = -99; z <= 99; z += 3)
                        {
                            positions.Add(new Vector3(x, y, z));
                        }
                    }
                }

            }
            numObjectsDisplay.text = "Objects in tree: " + positions.Count;
            Stopwatch timer = Stopwatch.StartNew();
            float[] results = new float[4];
            float total = 0;
            iterationsDisplay.text = "Iterations: " + results.Length;
            tree = null;
            System.GC.Collect();
            tree = new LooseOctree<GameObject>(200F, Vector3.zero, 1.25F);
            timer.Start();
            PopulateTree();
            timer.Stop();
            results[0] = timer.ElapsedMilliseconds;
            timer.Reset();
            total += results[0];
            numNodesDisplay.text = "Nodes: " + tree.NodeCount();
            totalTimeDisplay.text = "Total time: " + total + "ms";
            averageTimeDisplay.text = "Average time: " + total / results.Length + "ms";
            tree = null;
            System.GC.Collect();
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
                tree.Add(obj, pos);
                if (thisRun == 500)
                {
                    thisRun = 0;
                    yield return new WaitForEndOfFrame();
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
                tree.Add(obj, pos);
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
