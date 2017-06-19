using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityOctree
{
    public partial class LooseOctree<T> where T : class
    {
        /// <summary>
        /// Returns a string formatted binary code
        /// </summary>
        /// <param name="locationCode"></param>
        /// <returns></returns>
        private static string GetStringCode(uint locationCode)
        {
            string name = System.Convert.ToString(locationCode, 2);
            name = System.Text.RegularExpressions.Regex.Replace(name, ".{3}", "$0 ").Trim();
            return name;
        }

        /// <summary>
        /// Returns the index of the locationCode
        /// </summary>
        /// <param name="locationCode"></param>
        /// <returns></returns>
        public static uint GetIndex(uint locationCode)
        {
            uint mask = (1 << 3) - 1;
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
            if (index > 7U)
            {
                Debug.LogError("Index must be between 0-7");
            }
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
            if (index > 7)
            {
                Debug.LogError("Next sibling index > 7. Wrapping to 0");
                index = 0U;
            }
            else index += 1U;
            return SiblingCode(locationCode, index);
        }
        /// <summary>
        /// Returns location code for sibling of the provided locationCode at current index-1
        /// </summary>
        /// <param name="locationCode"></param>
        /// <returns></returns>
        public static uint PreviousSiblingCode(uint locationCode)
        {
            uint index = GetIndex(locationCode);
            if (index == 0)
            {
                Debug.LogError("Previous sibling index < 0. Wrapping to 7");
                index = 7U;
            }
            else index -= 1U;
            return SiblingCode(locationCode, index);
        }

        /// <summary>
        /// Returns tree depth of the provided locationCode
        /// </summary>
        /// <param name="locationCode"></param>
        /// <returns></returns>
        private static int GetDepth(uint locationCode)
        {
            for (int d = 0; locationCode > 0; d++)
            {
                if (locationCode == 1)
                    return d;
                locationCode >>= 3;
            }
            Debug.LogError("Could not find depth of node!");
            return -1;
        }
        public static uint SetChild(uint mask, uint index)
        {
            index++;
            return mask |= 1U << (int)index;
        }
        public static uint UnsetChild(uint mask, uint index)
        {
            index++;
            return mask = mask & ~(1U << (int)index);
        }
        public static bool CheckChild(uint mask, uint index)
        {
            index++;
            return (mask & (1U << (int)index)) != 0;
        }
        /// <summary>
        /// Returns the parent of the provided locationCode
        /// </summary>
        /// <param name="childLocationCode"></param>
        /// <returns></returns>
        public static uint ParentCode(uint childLocationCode)
        {
            return childLocationCode >> 3;
        }

        public static float SqrDistanceToRay(Ray ray, Vector3 point)
        {
            Vector3 lhs, origin;
            Vector3 rhs = new Vector3(point.x - (origin = ray.origin).x, point.y - origin.y, point.z - origin.z);
            return new Vector3((lhs = ray.direction).y * rhs.z - lhs.z * rhs.y, lhs.z * rhs.x - lhs.x * rhs.z, lhs.x * rhs.y - lhs.y * rhs.x).sqrMagnitude;
        }

        public static uint ChildCode(uint parentLocationCode, uint index)
        {
            return parentLocationCode << 3 | index;
        }
    }
}