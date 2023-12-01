using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets
{
    public class BoundingBox
    {
        public Vector3 Min;
        public Vector3 Max;
        public Vector3 Center;
        public bool IsCollide;

        public List<Vector3> Vertices { get; set; } // vertices of each obj

        public Vector3 Minimum(Vector3 position, Vector3 scale)
        {
            //Vector3 min = position - scale / 4; level 2
            //Vector3 min = position - scale / 8; level 3  
            Vector3 min = new Vector3(position.x - scale.x / 2, position.y - scale.y / 2, position.z - scale.z / 2);
            return min;
        }

        public Vector3 Maximum(Vector3 position, Vector3 scale)
        {

            //Vector3 max = position + scale / 4; level 2
            //Vector3 max = position + scale / 8; level 3 
            Vector3 max = new Vector3(position.x + scale.x / 2, position.y + scale.y / 2, position.z + scale.z / 2);
            return max;
        }

        public bool Intersects(BoundingBox other)
        {
            return (Min.x <= other.Max.x && Max.x >= other.Min.x) &&
                   (Min.y <= other.Max.y && Max.y >= other.Min.y) &&
                   (Min.z <= other.Max.z && Max.z >= other.Min.z);
        }
    }
}
