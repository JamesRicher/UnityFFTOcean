using UnityEngine;

namespace OceanRendering
{
    public struct PhillipsSpectrumParams
    {
        public float C; // Multiplicative constant
        public float V; // Windspeed in m/s
        public Vector2 omegaHat; // Normalised wind direction
        public float Lx;
        public float Lz;
        public uint P; // Power to which the dot product is raised - higher = waves more in line with wind
        public float l; // Small wave correction factor
        public float L; // Largest possible wave arising from costant wind of speed V (L = V^2/g)

        public PhillipsSpectrumParams(float C, float V, Vector2 omega, float Lx, float Lz, uint P, float l = 0)
        {
            this.C = C;
            this.V = V;
            omegaHat = omega.normalized;
            this.Lx = Lx;
            this.Lz = Lz;
            this.P = P;
            this.l = l;
            L = V * V / MyConstants.GRAVITY;
        }
    }
}