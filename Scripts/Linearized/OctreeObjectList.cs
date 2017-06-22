using System.Collections.Generic;
namespace UnityOctree
{
    class OctreeObjectList<T> where T : class
    {
        private OctreeObject<T>[] entries; //Objects
        private bool[] available; //List of available indices

        private int nextEntryIndex = -1; //Index to store the next item
        public int count; //Count of active objects

        public OctreeObjectList(int size)
        {
            available = new bool[size];
            entries = new OctreeObject<T>[size];
        }

        public void Clear()
        {
            nextEntryIndex = -1; //Start adding from the front
            for (int i = 0; i < available.Length; i++)
                available[i] = true;
            count = 0;
        }

        public void AddToStack(Stack<OctreeObject<T>> stack)
        {
            OctreeObject<T> obj;
            StartLoop();
            while (GetNext(out obj))
                stack.Push(obj);
        }

        int dirtyCount = 0;
        /// <summary>
        /// Add an object
        /// </summary>
        public int Add(OctreeObject<T> obj)
        {
            int index;
            if (dirtyCount > 0)
            {
                index = GetAvailableIndex();
                dirtyCount--;
            }
            else
            {
                index = nextEntryIndex;
                nextEntryIndex++;
            }

            entries[index] = obj;
            obj.listIndex = index;
            count++;
            return index;
        }

        private int GetAvailableIndex()
        {
            int index = 0;
            while (available[index] == false)
                index++;

            return index;
        }

        public void Remove(OctreeObject<T> obj)
        {
            available[obj.listIndex] = true; //Record this index as available
            dirtyCount++;
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
            while (curPos < nextEntryIndex && available[curPos] == true)
                curPos++;

            if ((obj = entries[curPos]) != null)
                return true;

            return false;
        }

        public OctreeObject<T> GetAt(int index)
        {
            while (index < nextEntryIndex && available[index] == true)
                index++; //Loop forward until we find an index that is not available

            return entries[index];
        }

        public OctreeObject<T> this[int index]
        {
            get
            {
                return GetAt(index);
            }
            set
            {
                entries[index] = value;
            }
        }
    }
}
