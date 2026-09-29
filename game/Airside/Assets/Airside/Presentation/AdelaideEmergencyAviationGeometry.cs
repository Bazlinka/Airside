using System;

namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0186 — the OSM Helipad West surface and markings beside the SA Ambulance rescue/retrieval base.
    /// Pure geometry keeps the committed real-world position testable without UnityEngine.
    /// </summary>
    public static class AdelaideEmergencyAviationGeometry
    {
        public const long OsmWayId = 1229789628;
        public const float PadCentreX = -302.737f;
        public const float PadCentreZ = 697.242f;
        public const float PadRadiusMetres = 18.94f;
        public const float HelicopterYawDegrees = 90f;
        public const int EdgeLightCount = 12;

        private static readonly RoadColor Concrete = RoadColor.Srgb(0.58f, 0.60f, 0.60f, 1f);
        private static readonly RoadColor Perimeter = RoadColor.Srgb(0.94f, 0.78f, 0.10f, 1f);
        private static readonly RoadColor White = RoadColor.Srgb(0.93f, 0.93f, 0.89f, 1f);
        private static readonly RoadColor Fixture = RoadColor.Srgb(0.16f, 0.18f, 0.19f, 1f);
        private static readonly RoadColor Lens = RoadColor.Srgb(0.92f, 0.95f, 0.82f, 1f);

        // OSM way 1229789628 transformed through scripts/ypad_osm.py. The closing
        // duplicate is omitted; BuildSurface closes it explicitly.
        public static readonly float[] Outline =
        {
            -288.17f, 709.24f, -284.99f, 703.81f, -283.76f, 697.66f,
            -284.59f, 691.46f, -287.42f, 685.89f, -291.92f, 681.54f,
            -297.60f, 678.92f, -303.83f, 678.30f, -309.95f, 679.75f,
            -315.26f, 683.11f, -319.21f, 688.01f, -321.32f, 693.84f,
            -321.44f, 700.02f, -319.56f, 705.91f, -315.87f, 710.87f,
            -310.78f, 714.35f, -304.81f, 716.00f, -298.63f, 715.64f,
            -292.87f, 713.30f
        };

        public static int BuildSurface(RoadMeshSink sink, RoadBuildOptions options)
        {
            var y = options.Height(PadCentreX, PadCentreZ) + options.YOffset * 0.45f;
            var n = Outline.Length / 2;
            for (var i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                sink.Tri(PadCentreX, y, PadCentreZ,
                    Outline[i * 2], options.Height(Outline[i * 2], Outline[i * 2 + 1]) + options.YOffset * 0.45f, Outline[i * 2 + 1],
                    Outline[j * 2], options.Height(Outline[j * 2], Outline[j * 2 + 1]) + options.YOffset * 0.45f, Outline[j * 2 + 1],
                    Concrete);
            }

            return n;
        }

        public static int BuildPaint(RoadMeshSink sink, RoadBuildOptions options)
        {
            Ring(sink, options, PadRadiusMetres - 1.15f, 0.55f, Perimeter, 48);
            Ring(sink, options, 7.0f, 0.42f, White, 36);
            // A 5.8 m H, aligned to the runway-frame axes for a crisp overhead read.
            Rect(sink, options, PadCentreX - 2.15f, PadCentreZ, 0f, 1f, 0.42f, 2.9f, White);
            Rect(sink, options, PadCentreX + 2.15f, PadCentreZ, 0f, 1f, 0.42f, 2.9f, White);
            Rect(sink, options, PadCentreX, PadCentreZ, 1f, 0f, 2.15f, 0.42f, White);
            return 3;
        }

        public static int BuildFixtures(RoadMeshSink sink, RoadBuildOptions options)
        {
            for (var i = 0; i < EdgeLightCount; i++)
            {
                var angle = i * 2f * (float)Math.PI / EdgeLightCount;
                var x = PadCentreX + (float)Math.Cos(angle) * (PadRadiusMetres - 0.55f);
                var z = PadCentreZ + (float)Math.Sin(angle) * (PadRadiusMetres - 0.55f);
                var y = options.Height(x, z) + options.YOffset;
                sink.Cylinder(x, y, z, 0.12f, 0.28f, 8, Fixture);
                sink.Cylinder(x, y + 0.28f, z, 0.16f, 0.10f, 8, Lens);
            }

            return EdgeLightCount;
        }

        private static void Ring(RoadMeshSink sink, RoadBuildOptions options, float radius, float width,
            RoadColor colour, int segments)
        {
            var inner = radius - width * 0.5f;
            var outer = radius + width * 0.5f;
            var lift = options.YOffset + AdelaideRoadGeometry.PaintLift;
            for (var i = 0; i < segments; i++)
            {
                var a0 = i * 2f * (float)Math.PI / segments;
                var a1 = (i + 1) * 2f * (float)Math.PI / segments;
                float X(float a, float r) => PadCentreX + (float)Math.Cos(a) * r;
                float Z(float a, float r) => PadCentreZ + (float)Math.Sin(a) * r;
                float Y(float x, float z) => options.Height(x, z) + lift;
                var ax = X(a0, inner); var az = Z(a0, inner);
                var bx = X(a0, outer); var bz = Z(a0, outer);
                var cx = X(a1, inner); var cz = Z(a1, inner);
                var dx = X(a1, outer); var dz = Z(a1, outer);
                sink.Quad(ax, Y(ax, az), az, bx, Y(bx, bz), bz,
                    cx, Y(cx, cz), cz, dx, Y(dx, dz), dz, colour);
            }
        }

        private static void Rect(RoadMeshSink sink, RoadBuildOptions options, float x, float z, float ux, float uz,
            float halfLength, float halfWidth, RoadColor colour)
        {
            var vx = -uz;
            var vz = ux;
            var lift = options.YOffset + AdelaideRoadGeometry.PaintLift * 1.5f;
            float X(float a, float b) => x + ux * a + vx * b;
            float Z(float a, float b) => z + uz * a + vz * b;
            float Y(float px, float pz) => options.Height(px, pz) + lift;
            var ax = X(-halfLength, -halfWidth); var az = Z(-halfLength, -halfWidth);
            var bx = X(-halfLength, halfWidth); var bz = Z(-halfLength, halfWidth);
            var cx = X(halfLength, -halfWidth); var cz = Z(halfLength, -halfWidth);
            var dx = X(halfLength, halfWidth); var dz = Z(halfLength, halfWidth);
            sink.Quad(ax, Y(ax, az), az, bx, Y(bx, bz), bz,
                cx, Y(cx, cz), cz, dx, Y(dx, dz), dz, colour);
        }
    }
}
