using System;

namespace Airside.Simulation
{
    public readonly struct ParafieldPoint
    {
        public readonly double X, Z;
        public ParafieldPoint(double x, double z) { X = x; Z = z; }
        public double Distance(ParafieldPoint p) => Math.Sqrt((X-p.X)*(X-p.X)+(Z-p.Z)*(Z-p.Z));
        public static ParafieldPoint Lerp(ParafieldPoint a, ParafieldPoint b, double t) =>
            new(a.X+(b.X-a.X)*t, a.Z+(b.Z-a.Z)*t);
        public ParafieldPoint Offset(double x, double z) => new(X+x, Z+z);
    }

    public readonly struct ParafieldRunway
    {
        public readonly string Name;
        public readonly ParafieldPoint A, B;
        public readonly double Width;
        public ParafieldRunway(string name, ParafieldPoint a, ParafieldPoint b, double width)
        { Name=name; A=a; B=b; Width=width; }
        public double Length => A.Distance(B);
    }

    public static partial class ParafieldLayout
    {
        public const string Code = "YPPF";
        public const double ElevationMetres = 17.374;
        public static ParafieldRunway MainRunway => Runways[0];

        // The airport is on a level aerodrome platform within the real DEM.
        // Ease back to the sampled hills beyond the airport, without a hard disc edge.
        public static float GroundHeight(double worldX, double worldZ, float original)
        {
            var x=worldX-CentreX; var z=worldZ-CentreZ;
            var r=Math.Sqrt(x*x+z*z);
            var t=Math.Clamp((r-1500)/600,0,1);
            t=t*t*(3-2*t);
            return (float)(ElevationMetres+(original-ElevationMetres)*t);
        }
    }
}
