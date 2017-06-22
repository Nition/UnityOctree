using System.Collections.Generic;
namespace UnityOctree
{
    public class OctreeObjectList<T> where T : class
    {
        private OctreeObject<T>[] entries; //Objects
        private Stack<int> available; //List of available indices

        int availableCount;
        public int count; //Count of active objects
        int maxSize;
        int curSize;
        public OctreeObjectList(int size)
        {
            maxSize = size;
            available = new Stack<int>(size);
            entries = new OctreeObject<T>[size];
            Clear();
        }

        public void Clear()
        {
            available.Clear();
            int i;
            for (i = maxSize - 1; i >= 0; i--)
            {
                available.Push(i);
                entries[i] = null;
            }
            availableCount = maxSize;
            count = 0;
        }

        public void AddToStack(Stack<OctreeObject<T>> stack)
        {
            OctreeObject<T> obj;
            StartLoop();
            while (GetNext(out obj))
                stack.Push(obj);
        }

        /// <summary>
        /// Add an object
        /// </summary>
        public int Add(OctreeObject<T> obj)
        {
            int index = available.Pop();
            entries[index] = obj;
            obj.listIndex = index;
            count++;
            return index;
        }

        public void Remove(OctreeObject<T> obj)
        {
            int index;
            available.Push(index = obj.listIndex);
            entries[index] = null;
            count--;
        }

        private int curPos;
        public void StartLoop()
        {
            curPos = -1;
        }
        /// <summary>
        /// Get the next valid object from the list
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public bool GetNext(out OctreeObject<T> obj)
        {
            obj = null;
            while (curPos < maxSize && entries[curPos] == null)
                curPos++;

            if ((obj = entries[curPos]) != null)
                return true;

            return false;
        }

        public OctreeObject<T> GetAt(int index)
        {
            while (index < maxSize && entries[index] == null)
                index++; //Loop forward until we find an index that is valid

            return entries[index];
        }
        public void PutAt(OctreeObject<T> obj,int index)
        {
            obj.listIndex = index;
            entries[index] = obj;
        }
        public OctreeObject<T> this[int index]
        {
            get
            {
                return GetAt(index);
            }
            set
            {
                PutAt(value, index);
            }
        }
    }
}
