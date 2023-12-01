using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts
{
    /////////////////////////////

    public struct ParticleList
    {
        public List<Particle> particle;
        public List<Vector3> vertexList;

    }

    public class Particle
    {
        private Vector3 pos;
        private Vector3 velo;

        public Vector3 POS
        {
            get { return pos; }
            set { pos = value; }
        }

        public Vector3 VELO
        {
            get { return velo; }
            set { velo = value; }
        }

        public void UpdatePosition(float dt, Vector3 gravity)
        {
            velo += dt * gravity;
            pos += dt * velo;
            //Debug.Log(" update" + velo);
        }

        public void UpdateReverseVelocity(float dt)
        {
            //// + offset of objects, velo.y = target + 15;
            ///

            velo *= -1f;
            pos += dt * velo;
            //Debug.Log(" update" + velo);
        }

    }

    /////////////////////////////

    public struct VelocityData // bound min and max
    {
        public List<Vector3> Velocity;
    }

    /////////////////////////////

    public struct BoundData // bound min and max
    {
        public Vector3 Min;
        public Vector3 Max;
    }

    /////////////////////////////
    
    public struct PairData  // Pair data store as Index number
    {
        public int i1;
        public int i2;
    }

}
