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
        public bool drawLabels;

        LooseOctree<GameObject> tree = null;
        Queue<OctreeObject<GameObject>> treeObj = new Queue<OctreeObject<GameObject>>();
        List<Vector3> positions = new List<Vector3>();
        // Use this for initialization
        IEnumerator Start()
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
            tree = new LooseOctree<GameObject>(200F, Vector3.zero, 1.5F);
            numObjectsDisplay.text = "Objects per iteration: " + positions.Count;
            float[] buildResults = new float[20];
            float buildTotal = 0;
            float[] destroyResults = new float[20];
            float destroyTotal = 0;

            Stopwatch timer = new Stopwatch();
            for (int i = 0; i < buildResults.Length; i++)
            {
                iterationsDisplay.text = "Iterations: " + (i+1);
                timer.Reset();
                timer.Start();
                PopulateTree();
                timer.Stop();
                buildResults[i] = timer.ElapsedMilliseconds;
                buildTotal += buildResults[i];
                numNodesDisplay.text = "Nodes per iteration: " + tree.NodeCount();
                timer.Reset();
                timer.Start();
                DestroyTree();
                timer.Stop();
                destroyResults[i] = timer.ElapsedMilliseconds;
                destroyTotal += destroyResults[i];
                averageTimeDisplay.text = "Average time: Build(" + Mathf.Round(buildTotal/(i+1)) + "ms) - Destroy(" + Mathf.Round(destroyTotal / (i + 1)) + "ms)";
                totalTimeDisplay.text = "Total time: Build(" + buildTotal + "ms) - Destroy(" + destroyTotal + "ms)";
                yield return new WaitForSeconds(1F);

            }
            averageTimeDisplay.text = "Average time: Build(" + buildTotal / buildResults.Length + "ms) - Destroy(" + destroyTotal / destroyResults.Length + "ms)";
            StartCoroutine(PopulateTreeSlow());
        }
        IEnumerator PopulateTreeSlow()
        {
            int count = 0;
            int thisRun = 0;
            GameObject obj = new GameObject("Dummy");
            for (int i=0;i<positions.Count;i++)
            {
                Vector3 pos = positions[i];
                count++;
                thisRun++;
                treeObj.Enqueue(tree.Add(obj, ref pos));
                if (thisRun == 2000)
                {
                    thisRun = 0;
                    yield return new WaitForEndOfFrame();
                }
            }
            //tree.Print();
            thisRun = 0;
            while (treeObj.Count > 0)
            {
                if (Random.Range(0F, 100F) > 50)
                {
                    treeObj.Dequeue().Remove();
                    thisRun++;
                }
                if (thisRun == 10)
                {
                    thisRun = 0;
                    yield return new WaitForEndOfFrame();
                }
            }
        }

        void PopulateTree()
        {
            GameObject obj = new GameObject("Dummy");
            int i;
            int count = positions.Count;
            for (i = 0; i < count; i++)
            {
                Vector3 pos = positions[i];
                treeObj.Enqueue(tree.Add(obj, ref pos));
            }
        }
        void DestroyTree()
        {
            while (treeObj.Count > 0)
                treeObj.Dequeue().Remove();
        }

        private void OnDrawGizmos()
        {
            if (tree != null)
                tree.DrawAll(drawNodes, drawObjects, drawConnections, drawLabels);
        }
        // Update is called once per frame
        void Update()
        {

        }
    }
}
