using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace UnityOctree
{
    public partial class LooseOctree<T> where T : class
    {
        public static int SetChild(int mask, int index)
        {
            return mask |= 1<<(index+1);
        }
        public static int UnsetChild(int mask, int index)
        {
            return mask &= ~1<<(index+1);
        }
        public static bool CheckChild(int mask, int index)
        {
            return (mask & 1<<(index+1)) != 0;
        }
    }
}