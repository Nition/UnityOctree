using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityOctree
{
    public partial class LooseOctree<T> where T : class
    {
        private static string GetStringCode(uint locationCode)
        {
            string name = System.Convert.ToString(locationCode, 2);
            name = System.Text.RegularExpressions.Regex.Replace(name, ".{3}", "$0 ").Trim();
            return name;
        }

        public static uint GetIndex(uint locationCode)
        {
            uint mask = (1 << 3) - 1;
            return locationCode & mask;
        }

        private static int GetDepth(uint locationCode)
        {
            int count = 0;
            while (locationCode > 1U)
            { //Keep shifting right until we hit 1
                count++;
                locationCode = ParentCode(locationCode);
            }
            return count;
        }

        public static uint ParentCode(uint childLocationCode)
        {
            return childLocationCode >> 3;
        }

        public static bool Encapsulates(Vector3 outerMinBounds, Vector3 outerMaxBounds, Vector3 innerMinBounds, Vector3 innerMaxBounds)
        {//Returns false if the bounds are exactly aligned at any edge
            if (innerMinBounds.x <= outerMinBounds.x || innerMaxBounds.x >= outerMaxBounds.x)
                return false; //Inner X is beyond or exactly aligned with outer X
            if (innerMinBounds.y <= outerMinBounds.y || innerMaxBounds.y >= outerMaxBounds.y)
                return false; //Inner Y is beyond or exactly aligned with outer Y
            if (innerMinBounds.z <= outerMinBounds.z || innerMaxBounds.y >= outerMaxBounds.y)
                return false; //Inner Z is beyond or exactly aligned with outer Z

            return true;
        }
        public static bool Intersects(Vector3 outerMinBounds, Vector3 outerMaxBounds, Vector3 innerMinBounds, Vector3 innerMaxBounds)
        {
            return (
            (innerMinBounds.x <= outerMaxBounds.x && innerMaxBounds.x >= outerMinBounds.x) &&
            (innerMinBounds.y <= outerMaxBounds.y && innerMaxBounds.y >= outerMinBounds.y) &&
            (innerMinBounds.z <= outerMaxBounds.z && innerMaxBounds.y >= outerMinBounds.y));
        }
        public static float SqrDistanceToRay(Ray ray, Vector3 point)
        {
            return Vector3.Cross(ray.direction, point - ray.origin).sqrMagnitude;
        }

        public static uint ChildCode(uint parentLocationCode, uint index)
        {
            return parentLocationCode << 3 | index;
        }
    }
}