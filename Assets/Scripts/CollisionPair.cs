using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets
{
    public class CollisionPair
    {
        public List<BoundingBox> Bound { get; set; }

        // optional parameters for rendering in the game scene
        public GameObject Object1 { get; set; }
        public GameObject Object2 { get; set; }

        public CollisionPair(List<BoundingBox> b, GameObject g1, GameObject g2)
        {
            Bound = b;
            Object1 = g1;
            Object2 = g2;
        }

        public bool CheckBoundingPair(List<CollisionPair> collisionPairs, BoundingBox b1, BoundingBox b2)
        {
            //return collisionPairs.Exists(pair =>
            //     (pair.Bound1 == b1 && pair.Bound2 == b2) ||
            //     (pair.Bound1 == b2 && pair.Bound2 == b1));

            return collisionPairs.Exists(pair =>
                 (pair.Bound[0] == b1 && pair.Bound[1] == b2) ||
                 (pair.Bound[0] == b2 && pair.Bound[1] == b1));
        }
    }
}
