using System;

namespace Airside.Presentation
{
    /// <summary>Fan-local XY coverage; the quad's spin axis is local Z.</summary>
    public static class AircraftFanDiscGeometry
    {
        public static float IncludeRadiusSquared(float maximum, float x, float y)
            => Math.Max(maximum, x * x + y * y);

        // Preserve the existing minimum fallback and 2.5% tip margin, without
        // truncating the larger widebody fans to a narrowbody diameter.
        public static float Diameter(float radiusSquared)
            => (float)Math.Sqrt(Math.Max(0.45f * 0.45f, radiusSquared)) * 2.05f;
    }
}
