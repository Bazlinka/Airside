using System;
using System.Collections.Generic;

namespace Airside.Presentation
{
    /// <summary>How a lens group answers daylight (ADR 0124).</summary>
    public enum LensDayResponse
    {
        /// <summary>Runway edge lights: barely visible by day.</summary>
        Edge,
        /// <summary>Taxi centreline and edge lights, stop bars: dim by day.</summary>
        Guidance,
        /// <summary>Runway guard lights: flash yellow day and night.</summary>
        Guard,
        /// <summary>Thresholds, runway ends, PAPI and approach lights: still readable at noon.</summary>
        Approach,
        /// <summary>Stand markers and lead-ins: off-looking by day.</summary>
        Stand
    }

    /// <summary>
    /// ADR 0124 — a low-poly inset airfield light: a squat octagonal metal base and a domed
    /// lens, replacing the cube each lens used to be. Unit size (diameter 1, height 1, base on
    /// y = 0); the runtime scales it to each fixture and merges every fixture of one colour into
    /// one mesh. Also the soft additive halo quads laid round each lens at night. Pure.
    /// </summary>
    public static class AirfieldFixture
    {
        public const int Sides = 8;
        /// <summary>Fraction of the height taken by the metal base; the dome is the rest.</summary>
        public const float BaseFraction = 0.35f;
        public const float LensRadius = 0.36f;

        public sealed class Geometry
        {
            public readonly List<float> Positions = new();
            public readonly List<float> Normals = new();
            public readonly List<int> Triangles = new();
            public int VertexCount => Positions.Count / 3;

            internal int Add(float x, float y, float z, float nx, float ny, float nz)
            {
                Positions.Add(x);
                Positions.Add(y);
                Positions.Add(z);
                var length = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                Normals.Add(nx / length);
                Normals.Add(ny / length);
                Normals.Add(nz / length);
                return VertexCount - 1;
            }

            /// <summary>A triangle wound to face along <paramref name="outward"/> (Unity front face).</summary>
            internal void Triangle(int a, int b, int c, float ox, float oy, float oz)
            {
                float P(int i, int k) => Positions[i * 3 + k];
                var e1x = P(b, 0) - P(a, 0);
                var e1y = P(b, 1) - P(a, 1);
                var e1z = P(b, 2) - P(a, 2);
                var e2x = P(c, 0) - P(a, 0);
                var e2y = P(c, 1) - P(a, 1);
                var e2z = P(c, 2) - P(a, 2);
                var cx = e1y * e2z - e1z * e2y;
                var cy = e1z * e2x - e1x * e2z;
                var cz = e1x * e2y - e1y * e2x;
                if (cx * ox + cy * oy + cz * oz < 0f)
                    (b, c) = (c, b);
                Triangles.Add(a);
                Triangles.Add(b);
                Triangles.Add(c);
            }
        }

        private static float Cos(int i) => (float)Math.Cos(i * 2.0 * Math.PI / Sides + Math.PI / Sides);
        private static float Sin(int i) => (float)Math.Sin(i * 2.0 * Math.PI / Sides + Math.PI / Sides);

        /// <summary>The metal base: octagonal wall and a flat top ring round the lens.</summary>
        public static Geometry Base()
        {
            var g = new Geometry();
            const float r = 0.5f;
            for (var i = 0; i < Sides; i++)
            {
                var j = (i + 1) % Sides;
                // Wall, flat-shaded per side.
                var mx = (Cos(i) + Cos(j)) * 0.5f;
                var mz = (Sin(i) + Sin(j)) * 0.5f;
                var a = g.Add(Cos(i) * r, 0f, Sin(i) * r, mx, 0f, mz);
                var b = g.Add(Cos(j) * r, 0f, Sin(j) * r, mx, 0f, mz);
                var c = g.Add(Cos(j) * r * 0.9f, BaseFraction, Sin(j) * r * 0.9f, mx, 0.25f, mz);
                var d = g.Add(Cos(i) * r * 0.9f, BaseFraction, Sin(i) * r * 0.9f, mx, 0.25f, mz);
                g.Triangle(a, b, c, mx, 0.1f, mz);
                g.Triangle(a, c, d, mx, 0.1f, mz);
                // Top ring between the wall and the lens.
                var e = g.Add(Cos(i) * r * 0.9f, BaseFraction, Sin(i) * r * 0.9f, 0f, 1f, 0f);
                var f = g.Add(Cos(j) * r * 0.9f, BaseFraction, Sin(j) * r * 0.9f, 0f, 1f, 0f);
                var h = g.Add(Cos(j) * LensRadius, BaseFraction, Sin(j) * LensRadius, 0f, 1f, 0f);
                var k = g.Add(Cos(i) * LensRadius, BaseFraction, Sin(i) * LensRadius, 0f, 1f, 0f);
                g.Triangle(e, f, h, 0f, 1f, 0f);
                g.Triangle(e, h, k, 0f, 1f, 0f);
            }

            return g;
        }

        /// <summary>The lens: a two-ring dome with smooth normals so it catches a highlight.</summary>
        public static Geometry Lens()
        {
            var g = new Geometry();
            var rings = new[] { (radius: LensRadius, y: BaseFraction), (radius: LensRadius * 0.72f, y: BaseFraction + (1f - BaseFraction) * 0.72f) };
            var index = new int[rings.Length, Sides];
            for (var ring = 0; ring < rings.Length; ring++)
            for (var i = 0; i < Sides; i++)
            {
                var (radius, y) = rings[ring];
                var up = ring == 0 ? 0.35f : 0.9f;
                index[ring, i] = g.Add(Cos(i) * radius, y, Sin(i) * radius, Cos(i), up, Sin(i));
            }

            var apex = g.Add(0f, 1f, 0f, 0f, 1f, 0f);
            for (var i = 0; i < Sides; i++)
            {
                var j = (i + 1) % Sides;
                var ox = (Cos(i) + Cos(j)) * 0.5f;
                var oz = (Sin(i) + Sin(j)) * 0.5f;
                g.Triangle(index[0, i], index[0, j], index[1, j], ox, 0.5f, oz);
                g.Triangle(index[0, i], index[1, j], index[1, i], ox, 0.5f, oz);
                g.Triangle(index[1, i], index[1, j], apex, ox, 2f, oz);
            }

            return g;
        }

        /// <summary>Halo size in metres for a lens of this diameter: a soft pool a few lens-widths across.</summary>
        public static float HaloSize(float lensDiameter, LensDayResponse response) => response switch
        {
            LensDayResponse.Approach => Math.Max(2.6f, lensDiameter * 7f),
            LensDayResponse.Guard => Math.Max(2.4f, lensDiameter * 6f),
            LensDayResponse.Stand => Math.Max(1.8f, lensDiameter * 5f),
            _ => Math.Max(1.6f, lensDiameter * 7f)
        };

        /// <summary>Halo strength against night (0 day … 1 night); zero means the halo renderer is off.</summary>
        public static float HaloStrength(float night) => night < 0.25f ? 0f : (night - 0.25f) / 0.75f;

        /// <summary>Lens emission multiplier for a group at this much night.</summary>
        public static float LensEmission(LensDayResponse response, float night)
        {
            var day = response switch
            {
                LensDayResponse.Approach => 0.45f,
                LensDayResponse.Guard => 0.55f,
                LensDayResponse.Edge => 0.1f,
                LensDayResponse.Stand => 0.05f,
                _ => 0.1f
            };
            return day + (1.6f - day) * night;
        }

        // Light points (ADR 0167). A real airfield lamp is a point source: from the tower or a
        // kilometre out it is a sharp bright dot, never smaller than the eye can resolve, and it
        // reaches further through haze than the ground it stands on. The true-size 0.34 m lens is
        // sub-pixel from the overview, so each fixture also draws a camera-facing point that never
        // shrinks below a few pixels (Airside/AirfieldLightPoint).

        /// <summary>Smallest on-screen diameter, in pixels, of a fixture's light point.</summary>
        public static float PointMinPixels(LensDayResponse response) => response switch
        {
            LensDayResponse.Approach => 3.4f,
            LensDayResponse.Guard => 3.2f,
            LensDayResponse.Edge => 2.8f,
            LensDayResponse.Stand => 2f,
            _ => 2.3f
        };

        /// <summary>World diameter of the point close up: a small glare round the lens, not a disc.</summary>
        public static float PointWorldSize(float lensDiameter) => Math.Max(0.5f, lensDiameter * 2.4f);

        /// <summary>
        /// Point brightness (HDR multiplier on the lens colour) at this much night. Edge, taxi and
        /// stand points only come up through dusk; thresholds, PAPI, approach and guard lights are
        /// high-intensity and still show by day, as they do at a real field.
        /// </summary>
        public static float PointStrength(LensDayResponse response, float night)
        {
            night = Math.Max(0f, Math.Min(1f, night));
            var dusk = Smooth01((night - 0.12f) / 0.45f);
            var (gain, day) = response switch
            {
                LensDayResponse.Approach => (2.6f, 0.3f),
                LensDayResponse.Guard => (2.4f, 0.35f),
                LensDayResponse.Edge => (2.1f, 0f),
                LensDayResponse.Stand => (1.2f, 0f),
                _ => (1.5f, 0f)
            };
            return gain * Math.Max(day, dusk);
        }

        /// <summary>Distance, in metres, at which a point has dimmed to half (inverse-square, softened).</summary>
        public const float PointHalfBrightnessMetres = 2200f;
        /// <summary>The dimmest a point gets with distance, so a far runway still reads as a line of lights.</summary>
        public const float PointDistanceFloor = 0.3f;
        /// <summary>
        /// Lights see through haze further than surfaces: fog transmission is raised to this power
        /// (0.35 ≈ 1.7× the visual range, in line with runway visual range over meteorological visibility).
        /// </summary>
        public const float PointHazeExponent = 0.35f;

        /// <summary>Distance dimming, mirrored by the shader.</summary>
        public static float PointDistanceFactor(float metres)
        {
            var q = Math.Max(0f, metres) / PointHalfBrightnessMetres;
            return Math.Max(PointDistanceFloor, 1f / (1f + q * q));
        }

        /// <summary>Haze transmission for a light, from the transmission a surface at that distance gets.</summary>
        public static float PointHaze(float surfaceTransmission) =>
            (float)Math.Pow(Math.Max(0f, Math.Min(1f, surfaceTransmission)), PointHazeExponent);

        /// <summary>
        /// Runway guard lights ("wig-wags") alternate their pair at 48 flashes a minute (ICAO Annex 14,
        /// 30–60). Returns the flash phase (0 or 0.5) for a guard lens, or −1 for a steady light.
        /// </summary>
        public static float FlashPhase(string name)
        {
            if (name == null || !name.StartsWith("Runway guard", StringComparison.Ordinal))
                return -1f;
            return name.EndsWith(" R", StringComparison.Ordinal) ? 0.5f : 0f;
        }

        public const float GuardFlashHz = 0.8f;

        /// <summary>Whether a light with this phase is lit at <paramref name="seconds"/>, mirrored by the shader.</summary>
        public static bool FlashOn(float phase, float seconds)
        {
            if (phase < 0f)
                return true;
            var t = seconds * GuardFlashHz + phase;
            return t - Math.Floor(t) < 0.5;
        }

        private static float Smooth01(float x)
        {
            x = Math.Max(0f, Math.Min(1f, x));
            return x * x * (3f - 2f * x);
        }

        /// <summary>Which response a lens gets, from the name the lighting builders give it.</summary>
        public static LensDayResponse ResponseFor(string name)
        {
            if (name == null)
                return LensDayResponse.Guidance;
            if (name.StartsWith("Runway edge", StringComparison.Ordinal))
                return LensDayResponse.Edge;
            if (name.StartsWith("Runway guard", StringComparison.Ordinal))
                return LensDayResponse.Guard;
            if (name.StartsWith("Stand", StringComparison.Ordinal))
                return LensDayResponse.Stand;
            if (name.StartsWith("Threshold", StringComparison.Ordinal) || name.StartsWith("Runway end", StringComparison.Ordinal)
                || name.StartsWith("PAPI", StringComparison.Ordinal) || name.StartsWith("ALS", StringComparison.Ordinal))
                return LensDayResponse.Approach;
            return LensDayResponse.Guidance;
        }
    }
}
