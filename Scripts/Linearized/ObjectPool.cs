using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityOctree
{
    public class ObjectPool<T> where T : class, new()
    {
        private Stack<T> pool = new Stack<T>();
        private int poolCount = 0;
        public ObjectPool(int initialSize)
        {
            for (int i = 0; i <= initialSize; i++)
            {
                poolCount++;
                pool.Push(new T());
            }
        }
        public T Pop()
        {
            T obj;
            if (poolCount > 0)
            {
                obj = pool.Pop();
                poolCount--;
            }
            else
            {
                obj = new T();
            }
            return obj;
        }
        public void Push(T obj)
        {
            poolCount++;
            pool.Push(obj);
        }
    }
}