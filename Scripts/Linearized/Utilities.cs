using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace UnityOctree
{
    public partial class LooseOctree<T> where T : class
    {
        /// <summary>
        /// Returns the index of the locationCode
        /// </summary>
        /// <param name="locationCode"></param>
        /// <returns></returns>
        public static uint GetIndex(uint locationCode)
        {
            uint mask = (1U << 3) - 1U;
            return locationCode & mask;
        }

        /// <summary>
        /// Returns location code for sibling of the provided locationCode at index
        /// </summary>
        /// <param name="locationCode"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static uint SiblingCode(uint locationCode, uint index)
        {
            Debug.Assert(index > 7U, "SiblingCode index must be between 0-7");
            return ChildCode(ParentCode(locationCode), index);
        }
        /// <summary>
        /// Returns location code for sibling of the provided locationCode at current index+1
        /// </summary>
        /// <param name="locationCode"></param>
        /// <returns></returns>
        public static uint NextSiblingCode(uint locationCode)
        {
            uint index = GetIndex(locationCode);
            Debug.Assert(index < 7U, "Calling NextSiblingCode on last index.");
            return SiblingCode(locationCode, index+1);
        }
        /// <summary>
        /// Returns location code for sibling of the provided locationCode at current index-1
        /// </summary>
        /// <param name="locationCode"></param>
        /// <returns></returns>
        public static uint PreviousSiblingCode(uint locationCode)
        {
            uint index = GetIndex(locationCode);
            Debug.Assert(index > 0U, "Calling PreviousSiblingCode on zero index.");
            return SiblingCode(locationCode, index-1);
        }

        /// <summary>
        /// Returns tree depth of the provided locationCode
        /// </summary>
        /// <param name="locationCode"></param>
        /// <returns></returns>
        private static int GetDepth(uint locationCode)
        {
            uint start = locationCode;
            for (int d = 0; locationCode > 0U; d++)
            {
                if (locationCode == 1U)
                    return d;
                locationCode >>= 3;
            }
            Debug.Assert(false, "Could not find depth of node at index + " + GetIndex(start) + " and position: + " + start + "! Stopped searching at " + locationCode);
            return -1;
        }
        public static int SetChild(int mask, uint index)
        {
            return mask |= 1<<((int)index+1);
        }
        public static int UnsetChild(int mask, uint index)
        {
            return mask &= ~1<<((int)index+1);
        }
        public static bool CheckChild(int mask, uint index)
        {
            return (mask & 1U<<((int)index+1)) != 0U;
        }
        /// <summary>
        /// Returns the parent of the provided locationCode
        /// </summary>
        /// <param name="childLocationCode"></param>
        /// <returns></returns>
        public static uint ParentCode(uint childLocationCode)
        {
            Debug.Assert(childLocationCode != 1, "Trying to get parent locationCode for root. It has no parent!");
            return childLocationCode >> 3;
        }

        public static uint ChildCode(uint parentLocationCode, uint index)
        {
            return parentLocationCode << 3 | index;
        }
    }
}