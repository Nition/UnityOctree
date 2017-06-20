using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityOctree
{
    public class ObjectPool<T> where T : class, new()
    {
        Queue<T> pool = new Queue<T>();
        public ObjectPool(int initialSize)
        {
            for (int i = 0; i <= initialSize; i++)
                pool.Enqueue(new T());
        }
        public T Pop()
        {
            T obj;
            if (pool.Count > 0)
            {
                obj = pool.Dequeue();
            }
            else
            {
                obj = new T();
            }
            return obj;
        }
        public void Push(T obj)
        {
            pool.Enqueue(obj);
        }
    }
}
