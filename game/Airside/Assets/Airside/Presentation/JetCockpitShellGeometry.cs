using System;
using System.Collections.Generic;

namespace Airside.Presentation
{
    /// <summary>Continuous opaque flight-deck skin. The only boundary is the deliberate window belt.
    /// Coordinates are relative to the fitted pilot-eye datum; independent of Unity.</summary>
    public static class JetCockpitShellGeometry
    {
        public readonly struct Point
        {
            public readonly float X, Y, Z;
            public Point(float x, float y, float z) { X = x; Y = y; Z = z; }
        }
        /// <summary>Project-authored family silhouettes. These retain the existing eye,
        /// sill and panel datums; they are not certified measurements or exterior hull fits.</summary>
        public sealed class Layout
        {
            public readonly float ShoulderZ, FrontSillRatio, FrontHeadRatio, FrontHeadZ, FrontHeadY;
            public readonly float? QuarterlightZ;
            internal Layout(float shoulder, float sillRatio, float headRatio, float headZ, float headY, float? divider)
            { ShoulderZ = shoulder; FrontSillRatio = sillRatio; FrontHeadRatio = headRatio;
                FrontHeadZ = headZ; FrontHeadY = headY; QuarterlightZ = divider; }
        }
        // Lower shoulders stay outside the existing instrument-panel half-width (0.875 W).
        private static readonly Layout Legacy = new(0.72f, 0.90f, 0.73f, 1.05f, FrontTopY, 0.41f);
        private static readonly Layout Classic = new(0.67f, 0.92f, 0.76f, 1.06f, 0.64f, 0.35f);
        private static readonly Layout A350 = new(0.64f, 0.94f, 0.83f, 1.10f, 0.66f, 0.20f);
        private static readonly Layout Dreamliner = new(0.62f, 0.94f, 0.86f, 1.12f, 0.67f, null);
        private static readonly Layout A220 = new(0.65f, 0.91f, 0.79f, 1.07f, 0.65f, 0.25f);
        private static readonly Layout EJet = new(0.70f, 0.90f, 0.74f, 1.04f, 0.63f, 0.38f);
        public static Layout ForDeck(JetFlightDeck deck) => deck switch
        {
            JetFlightDeck.Boeing737Ng or JetFlightDeck.Boeing737Max => Legacy,
            JetFlightDeck.AirbusClassic => Classic,
            JetFlightDeck.AirbusA350 => A350,
            JetFlightDeck.Boeing787 => Dreamliner,
            JetFlightDeck.AirbusA220 => A220,
            JetFlightDeck.Embraer => EJet,
            _ => throw new ArgumentOutOfRangeException(nameof(deck)),
        };
        public readonly struct Frame
        {
            public readonly string Name;
            public readonly Point Start, End;
            public readonly float Width;
            public readonly bool PanelMaterial;
            public Frame(string name, Point start, Point end, float width, bool panel = false)
            { Name = name; Start = start; End = end; Width = width; PanelMaterial = panel; }
        }
        public sealed class Geometry
        {
            public readonly List<Point> Vertices = new();
            public readonly List<int> Triangles = new();
            public readonly HashSet<int> WindowBoundary = new();
            public readonly List<Frame> Frames = new();
        }

        // Eye-relative sightline datums shared by the shell, window frames and instrument panel.
        // Transport flight decks give about 15-20 degrees of over-the-nose view (FAA AC 25.773-1
        // pilot compartment view): the glareshield's near top edge cuts the sight line there.
        public const float SillY = -0.24f;          // every window sill, front and side
        public const float SideTopY = 0.72f;        // side window head / roof line
        public const float FrontTopY = 0.62f;       // windscreen head (~30 degrees up at the brow)
        public const float GlareNearZ = 0.85f;      // glareshield leading edge, aft of the panel
        public const float GlareTopY = -0.2475f;    // glareshield top surface
        public static float OverNoseDownDegrees =>
            (float)(Math.Atan2(-GlareTopY, GlareNearZ) * 180.0 / Math.PI);

        public static Geometry Build(float halfWidth) => Build(halfWidth, Legacy);
        public static Geometry Build(JetCockpitProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            return Build(profile.HalfWidth, profile.Shell);
        }
        private static Geometry Build(float halfWidth, Layout layout)
        {
            if (halfWidth <= 0f || float.IsNaN(halfWidth) || float.IsInfinity(halfWidth))
                throw new ArgumentOutOfRangeException(nameof(halfWidth));
            var g = new Geometry();
            // Weld identical coordinates before triangulation, allowing topology checks to find
            // an unintended seam instead of hiding it with overlapping primitive boxes.
            var ids = new Dictionary<(float, float, float), int>();
            int Vertex(float x, float y, float z)
            {
                var key = (x, y, z);
                if (ids.TryGetValue(key, out var existing)) return existing;
                var id = g.Vertices.Count; ids.Add(key, id);
                g.Vertices.Add(new Point(x, y, z)); return id;
            }
            void Face(params int[] ring)
            {
                float x = 0, y = 0, z = 0;
                foreach (var id in ring) { var p = g.Vertices[id]; x += p.X; y += p.Y; z += p.Z; }
                var centre = Vertex(x / ring.Length, y / ring.Length, z / ring.Length);
                var c = g.Vertices[centre]; var a = g.Vertices[ring[0]]; var b = g.Vertices[ring[1]];
                var ax = a.X - c.X; var ay = a.Y - c.Y; var az = a.Z - c.Z;
                var bx = b.X - c.X; var by = b.Y - c.Y; var bz = b.Z - c.Z;
                var nx = ay * bz - az * by; var ny = az * bx - ax * bz; var nz = ax * by - ay * bx;
                // This convex shell surrounds an interior point at (0, -0.4, -0.2).
                var reverse = nx * c.X + ny * (c.Y + 0.4f) + nz * (c.Z + 0.2f) < 0f;
                for (var i = 0; i < ring.Length; i++)
                {
                    g.Triangles.Add(centre);
                    g.Triangles.Add(ring[reverse ? (i + 1) % ring.Length : i]);
                    g.Triangles.Add(ring[reverse ? i : (i + 1) % ring.Length]);
                }
            }
            var floor = new List<int>(); var roof = new List<int>();
            var bottom = new int[2, 4]; var top = new int[2, 4]; var sill = new int[2, 4];
            var zs = new[] { -1.73f, -0.61f, layout.ShoulderZ, 1.30f };
            for (var side = 0; side < 2; side++)
            {
                var sign = side == 0 ? -1f : 1f;
                for (var station = 0; station < 4; station++)
                {
                    var front = station == 3;
                    bottom[side, station] = Vertex(sign * halfWidth * (front ? layout.FrontSillRatio : 1f), -1.40f, zs[station]);
                    sill[side, station] = Vertex(sign * halfWidth * (front ? layout.FrontSillRatio : 1f), SillY, zs[station]);
                    top[side, station] = Vertex(sign * halfWidth * (front ? layout.FrontHeadRatio : 1f), front ? layout.FrontHeadY : SideTopY, front ? layout.FrontHeadZ : zs[station]);
                    if (station > 0) { g.WindowBoundary.Add(sill[side, station]); g.WindowBoundary.Add(top[side, station]); }
                }
                for (var station = 0; station < 3; station++)
                    Face(bottom[side, station], bottom[side, station + 1], sill[side, station + 1], sill[side, station]);
                // Solid rear sidewall, split at sill height to avoid a T-junction with the lower wall.
                Face(sill[side, 0], sill[side, 1], top[side, 1], top[side, 0]);
            }
            for (var station = 0; station < 4; station++) floor.Add(bottom[0, station]);
            for (var station = 3; station >= 0; station--) floor.Add(bottom[1, station]);
            // Flat main ceiling keeps the overhead panel below the lining. Only the
            // forward windscreen wedge slopes: a fan across a non-planar perimeter
            // pulled the centre of the old roof down through the overhead controls.
            for (var station = 0; station < 3; station++) roof.Add(top[0, station]);
            for (var station = 2; station >= 0; station--) roof.Add(top[1, station]);
            Face(floor.ToArray()); Face(roof.ToArray());
            Face(top[0, 2], top[0, 3], top[1, 3], top[1, 2]);
            Face(bottom[0, 0], sill[0, 0], sill[1, 0], bottom[1, 0]);
            Face(sill[0, 0], top[0, 0], top[1, 0], sill[1, 0]);
            Face(bottom[0, 3], bottom[1, 3], sill[1, 3], sill[0, 3]);
            // Frames use the exact welded aperture vertices above. Shell and rails
            // cannot drift apart when a family changes its shoulder or brow.
            void Rail(string name, int a, int b, float width, bool panel = false) =>
                g.Frames.Add(new Frame(name, g.Vertices[a], g.Vertices[b], width, panel));
            for (var side = 0; side < 2; side++)
            {
                Rail("Side window upper rail", top[side, 1], top[side, 2], 0.07f);
                Rail("Side window sill", sill[side, 1], sill[side, 2], 0.07f, true);
                Rail("Forward side window upper rail", top[side, 2], top[side, 3], 0.07f);
                Rail("Forward side window sill", sill[side, 2], sill[side, 3], 0.07f, true);
                Rail("Rear window pillar", sill[side, 1], top[side, 1], 0.06f);
                Rail("Front windscreen outer pillar", sill[side, 3], top[side, 3], 0.065f);
                if (layout.QuarterlightZ.HasValue)
                {
                    var x = (side == 0 ? -1f : 1f) * halfWidth;
                    var z = layout.QuarterlightZ.Value;
                    g.Frames.Add(new Frame("Side quarterlight pillar", new Point(x, SillY, z), new Point(x, SideTopY, z), 0.045f));
                }
            }
            var centreSill = new Point(0f, SillY, zs[3]);
            var centreHead = new Point(0f, layout.FrontHeadY, layout.FrontHeadZ);
            g.Frames.Add(new Frame("Windscreen centre post", centreSill, centreHead, 0.047f));
            Rail("Windscreen brow", top[0, 3], top[1, 3], 0.075f);
            Rail("Windscreen sill", sill[0, 3], sill[1, 3], 0.07f, true);
            return g;
        }
    }
}
