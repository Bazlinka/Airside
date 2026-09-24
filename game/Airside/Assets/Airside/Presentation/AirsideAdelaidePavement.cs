using System;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>A presentation-only box placed on the real Adelaide terminal shell.</summary>
    public readonly struct AdelaideTerminalDetail
    {
        public AdelaideTerminalDetail(string name, float x, float y, float z, float width, float height, float depth)
        {
            Name = name;
            X = x;
            Y = y;
            Z = z;
            Width = width;
            Height = height;
            Depth = depth;
        }

        public string Name { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float Width { get; }
        public float Height { get; }
        public float Depth { get; }
    }

    /// <summary>A roof-mounted apron flood and its ground aim point, in Adelaide world metres.</summary>
    public readonly struct AdelaideTerminalFlood
    {
        public AdelaideTerminalFlood(float x, float z, float targetX, float targetZ)
        {
            X = x;
            Z = z;
            TargetX = targetX;
            TargetZ = targetZ;
        }

        public float X { get; }
        public float Z { get; }
        public float TargetX { get; }
        public float TargetZ { get; }
    }

    /// <summary>
    /// Restrained facade and roof breakup for the OSM terminal footprint. Coordinates are real
    /// Adelaide world metres and deliberately remain separate from routes, collision and saves.
    /// </summary>
    public static class AdelaideTerminalArchitecture
    {
        public const float ShellHeightMetres = 14f;
        /// <summary>RFDS hangar prism — one storey plus roof, not the terminal shell.</summary>
        public const float RfdsHangarHeightMetres = 8f;
        public const float FloodHeightMetres = 18f;
        public const float FloodRangeMetres = 115f;

        /// <summary>The authored baggage-hall spur passes through this airside portal.</summary>
        public const float UndercroftPortalHalfWidthMetres = 4.5f;
        public const float UndercroftPortalHeightMetres = 3.4f;
        public static float UndercroftPortalCentreX => AdelaideServiceRoads.UndercroftSpur[0];

        /// <summary>
        /// Portion of a terminal facade edge occupied by the portal. Only the real
        /// airside wall may be cut; the landside walls and RFDS hangar stay intact.
        /// </summary>
        public static bool TryUndercroftPortalOnWall(float ax, float az, float bx, float bz,
            out float first, out float last)
        {
            first = last = 0f;
            var dx = bx - ax;
            if (Math.Abs(dx) < 0.01f)
                return false;

            var left = Math.Max(Math.Min(ax, bx), UndercroftPortalCentreX - UndercroftPortalHalfWidthMetres);
            var right = Math.Min(Math.Max(ax, bx), UndercroftPortalCentreX + UndercroftPortalHalfWidthMetres);
            if (right - left < 0.01f)
                return false;

            var middle = (left + right) * 0.5f;
            var t = (middle - ax) / dx;
            var edgeZ = az + (bz - az) * t;
            if (Math.Abs(edgeZ - AirsideWallZAt(middle)) > 0.15f)
                return false;

            first = Math.Min((left - ax) / dx, (right - ax) / dx);
            last = Math.Max((left - ax) / dx, (right - ax) / dx);
            return true;
        }

        public const float GlazingBayPitchMetres = 22f;
        public const float GlazingPaneWidthMetres = 19f;
        public const float GlazingStartXMetres = 986f;
        public const int GlazingBayCount = 28;

        /// <summary>
        /// The airside wall's real X→Z polyline — a sub-run of <see cref="AdelaideLayout.Terminals"/>'s
        /// own OSM footprint (X=993.3 to X=1604.0, exactly the vertices already authored there),
        /// not a straight line. Glazing used to sit on a single hardcoded Z (435.55) for every
        /// bay: fine at the far end, but 2.14 m off the real wall at the near end, since the
        /// wall itself drifts from Z≈437.7 to Z≈435.8 across the glazing run. Every value here
        /// is copied from <see cref="AdelaideLayout.Terminals"/>, not re-measured, so the two
        /// can never quietly drift apart.
        /// </summary>
        private static readonly (float X, float Z)[] AirsideWallPolyline =
        {
            (993.3f, 437.7f), (1036.5f, 437.6f), (1079.7f, 437.4f), (1122.7f, 437.3f),
            (1165.9f, 437.1f), (1218.5f, 437.0f), (1262.3f, 436.8f), (1305.8f, 436.7f),
            (1344.2f, 436.6f), (1349.1f, 436.6f), (1431.7f, 436.3f), (1474.9f, 436.2f),
            (1517.9f, 436.1f), (1561.0f, 435.9f), (1604.0f, 435.8f)
        };

        /// <summary>Z on the real airside wall at <paramref name="x"/>, clamped to the polyline's own ends.</summary>
        public static float AirsideWallZAt(float x)
        {
            var points = AirsideWallPolyline;
            if (x <= points[0].X)
                return points[0].Z;
            var last = points[points.Length - 1];
            if (x >= last.X)
                return last.Z;
            for (var i = 0; i < points.Length - 1; i++)
            {
                var (x0, z0) = points[i];
                var (x1, z1) = points[i + 1];
                if (x < x0 || x > x1)
                    continue;
                var span = x1 - x0;
                var t = span <= 0f ? 0f : (x - x0) / span;
                return z0 + (z1 - z0) * t;
            }

            return last.Z;
        }

        public static AdelaideTerminalDetail[] AirsideGlazing()
        {
            var result = new AdelaideTerminalDetail[GlazingBayCount];
            for (var i = 0; i < result.Length; i++)
            {
                var centreX = GlazingStartXMetres + GlazingPaneWidthMetres * 0.5f + i * GlazingBayPitchMetres;
                result[i] = new AdelaideTerminalDetail(
                    $"Terminal airside glazing {i + 1:00}", centreX, 7.1f, AirsideWallZAt(centreX),
                    GlazingPaneWidthMetres, 7.2f, 0.32f);
            }

            return result;
        }

        /// <summary>
        /// A dark structural mullion in each 3 m gap between adjacent glazing bays (bay
        /// pitch 22 m, pane width 19 m), proud of the glass plane, so the 28 panes read as
        /// a framed curtain wall instead of one continuous sheet of glass.
        /// </summary>
        public static AdelaideTerminalDetail[] GlazingMullions()
        {
            var bays = AirsideGlazing();
            var result = new AdelaideTerminalDetail[GlazingBayCount - 1];
            var gapWidth = GlazingBayPitchMetres - GlazingPaneWidthMetres;
            for (var i = 0; i < result.Length; i++)
            {
                // Midpoint of the 3 m gap between bay i and bay i+1, not either bay's edge.
                var gapCentreX = GlazingStartXMetres + GlazingPaneWidthMetres + gapWidth * 0.5f
                    + i * GlazingBayPitchMetres;
                // 0.05 m proud of bay i's own glass, not a separate wall lookup at the gap's own
                // X: the wall's real slope is steep enough in a couple of segments that a fixed
                // offset from the gap's own (slightly lower) Z could land level with or behind
                // that bay's pane instead of in front of it. Measuring "proud" from the bay
                // whose glass it actually sits beside removes that edge case entirely.
                result[i] = new AdelaideTerminalDetail(
                    $"Terminal airside mullion {i + 1:00}", gapCentreX, 7.3f, bays[i].Z + 0.05f,
                    1.2f, 7.6f, 0.5f);
            }

            return result;
        }

        public static AdelaideTerminalDetail[] RoofBrow() => new[]
        {
            new AdelaideTerminalDetail("Terminal airside brow W", 1040f, 11.55f, 433.4f, 106f, 1.1f, 5.5f),
            new AdelaideTerminalDetail("Terminal airside brow WC", 1150f, 11.55f, 433.4f, 106f, 1.1f, 5.5f),
            new AdelaideTerminalDetail("Terminal airside brow C", 1260f, 11.55f, 433.4f, 106f, 1.1f, 5.5f),
            new AdelaideTerminalDetail("Terminal airside brow EC", 1370f, 11.55f, 433.4f, 106f, 1.1f, 5.5f),
            new AdelaideTerminalDetail("Terminal airside brow E", 1480f, 11.55f, 433.4f, 106f, 1.1f, 5.5f),
            // Was centred at 1584 with width 98 (spanning to X=1633) — the terminal's own real
            // OSM footprint ends at X=1615.8 (AdelaideLayout.Terminals), so the old span
            // cantilevered 17.2 m past the building's own south-east corner into open air.
            // Narrowed to end with a small margin inside that corner, keeping the same ~4 m
            // gap pattern from the previous segment as the rest of the brow.
            new AdelaideTerminalDetail("Terminal airside brow far E", 1575f, 11.55f, 433.4f, 76f, 1.1f, 5.5f)
        };

        public static AdelaideTerminalDetail[] RoofDetails() => new[]
        {
            new AdelaideTerminalDetail("Terminal skylight W", 1045f, 14.14f, 444.5f, 32f, 0.22f, 5f),
            new AdelaideTerminalDetail("Terminal skylight WC", 1130f, 14.14f, 444.5f, 32f, 0.22f, 5f),
            new AdelaideTerminalDetail("Terminal skylight C", 1215f, 14.14f, 444.5f, 32f, 0.22f, 5f),
            new AdelaideTerminalDetail("Terminal skylight EC", 1300f, 14.14f, 444.5f, 32f, 0.22f, 5f),
            new AdelaideTerminalDetail("Terminal skylight E", 1385f, 14.14f, 444.5f, 32f, 0.22f, 5f),
            new AdelaideTerminalDetail("Terminal skylight far E", 1470f, 14.14f, 444.5f, 32f, 0.22f, 5f),
            new AdelaideTerminalDetail("Terminal skylight end", 1555f, 14.14f, 444.5f, 32f, 0.22f, 5f),
            new AdelaideTerminalDetail("Terminal roof plant W", 1110f, 15.1f, 463f, 24f, 2.2f, 10f),
            new AdelaideTerminalDetail("Terminal roof plant C", 1270f, 15.1f, 463f, 28f, 2.2f, 10f),
            new AdelaideTerminalDetail("Terminal roof plant E", 1450f, 15.1f, 468f, 26f, 2.2f, 10f)
        };

        public static AdelaideTerminalFlood[] ApronFloods() => new[]
        {
            new AdelaideTerminalFlood(1005f, 434f, 1005f, 388f),
            new AdelaideTerminalFlood(1100f, 434f, 1100f, 388f),
            new AdelaideTerminalFlood(1195f, 434f, 1195f, 388f),
            new AdelaideTerminalFlood(1290f, 434f, 1290f, 388f),
            new AdelaideTerminalFlood(1385f, 434f, 1385f, 388f),
            new AdelaideTerminalFlood(1480f, 434f, 1480f, 388f),
            new AdelaideTerminalFlood(1575f, 434f, 1575f, 388f),
            new AdelaideTerminalFlood(920f, 530f, 920f, 490f),
            new AdelaideTerminalFlood(1005f, 530f, 1005f, 490f),
            new AdelaideTerminalFlood(1090f, 530f, 1090f, 490f)
        };
    }

    /// <summary>
    /// Real-metre YPAD pavement for the Adelaide field, from the OpenStreetMap layout in
    /// <see cref="AdelaideLayout"/> (ADR 0045): main 05/23, cross 12/30 at its real
    /// crossing, every taxiway centreline with sealed shoulders, and the real apron
    /// outlines. Pure — no UnityEngine types — so the runtime builder, the terrain and
    /// headless tests all read one layout.
    /// </summary>
    public static class AirsideAdelaidePavement
    {
        // --- Main runway 05/23 (world +X = runway 05 → 23, +Z = north-west) ---

        public const string MainRunwayName = "Runway 05/23";

        public static float MainLengthMetres => AirsideBareField.RunwayLengthMetres;
        public static float MainWidthMetres => AirsideBareField.RunwayWidthMetres;
        public static float MainHalfLength => AirsideBareField.RunwayHalfLength;
        public static float MainHalfWidth => AirsideBareField.RunwayHalfWidth;

        /// <summary>
        /// Half-width of the ICAO Annex 14 runway strip for a code 4 precision approach
        /// runway (300 m wide overall).
        /// </summary>
        public const float RunwayStripHalfWidthMetres = 150f;

        /// <summary>Runway holding position distance from the centreline, code E precision runway.</summary>
        public const float RunwayHoldingPositionFromCentrelineMetres = 90f;

        // --- Cross runway 12/30, at its real crossing ---

        public const string CrossRunwayName = "Runway 12/30";

        /// <summary>Published YPAD 12/30 length (metres).</summary>
        public const float CrossLengthMetres = 1652f;

        public const float CrossWidthMetres = 45f;

        /// <summary>Unity yaw of 12/30 against 05/23, measured from the OSM runway geometry.</summary>
        public const float CrossYawDegrees = AdelaideLayout.CrossRunwayYawDegrees;

        public const float CrossCenterX = AdelaideLayout.CrossRunwayCenterX;
        public const float CrossCenterZ = AdelaideLayout.CrossRunwayCenterZ;

        public static float CrossHalfLength => CrossLengthMetres * 0.5f;
        public static float CrossHalfWidth => CrossWidthMetres * 0.5f;
        public static float CrossYawRadians => CrossYawDegrees * (float)Math.PI / 180f;

        /// <summary>
        /// Height the 12/30 slab sits below 05/23. The two were coplanar (both tops at
        /// +0.05 m), so their shared crossing z-fought from every camera angle.
        /// </summary>
        public const float CrossRunwayDropMetres = 0.004f;

        /// <summary>
        /// 12/30 paint in its local strip coordinates with everything that would land on the
        /// main 05/23 pavement removed: long edge lines and dashes are cut at the crossing,
        /// shorter marks inside it are dropped. 05/23 is the primary runway, so its markings
        /// run through the intersection and 12/30's stop at its edge, instead of being painted
        /// across it.
        /// </summary>
        public static AirsideStripMarkings.Mark[] ClipCrossRunwayPaintToMain(AirsideStripMarkings.Mark[] marks)
        {
            if (marks == null)
                return Array.Empty<AirsideStripMarkings.Mark>();

            var sin = (float)Math.Sin(CrossYawRadians);
            var cos = (float)Math.Cos(CrossYawRadians);
            var result = new System.Collections.Generic.List<AirsideStripMarkings.Mark>(marks.Length);
            foreach (var mark in marks)
            {
                // World Z along the mark's centreline is CrossCenterZ - sin * x + cos * z (Unity
                // yaw turns local +X to (cos, -sin)). Widen the band by the mark's half width.
                var margin = MainHalfWidth + Math.Abs(cos) * mark.WidthZ * 0.5f;
                if (Math.Abs(sin) < 1e-4f)
                {
                    result.Add(mark);
                    continue;
                }

                var a = CrossCenterZ + cos * mark.CenterZ;
                var x0 = (a - margin) / sin;
                var x1 = (a + margin) / sin;
                var cutStart = Math.Min(x0, x1);
                var cutEnd = Math.Max(x0, x1);
                if (cutEnd <= mark.MinX || cutStart >= mark.MaxX)
                {
                    result.Add(mark);
                    continue;
                }

                if (cutStart - mark.MinX > 0.5f)
                    result.Add(new AirsideStripMarkings.Mark((mark.MinX + cutStart) * 0.5f, mark.CenterZ,
                        cutStart - mark.MinX, mark.WidthZ));
                if (mark.MaxX - cutEnd > 0.5f)
                    result.Add(new AirsideStripMarkings.Mark((cutEnd + mark.MaxX) * 0.5f, mark.CenterZ,
                        mark.MaxX - cutEnd, mark.WidthZ));
            }

            return result.ToArray();
        }

        // --- Taxiways, aprons, shoulders ---

        public const string TaxiwaysName = "Taxiways";
        public const string ApronsName = "Aprons";
        public const string TerminalsName = "Terminal buildings";

        public const float TaxiwayWidthMetres = 23f;
        public static float TaxiwayHalfWidth => TaxiwayWidthMetres * 0.5f;

        /// <summary>Runway graded shoulder (dirt) each side of 05/23 and 12/30.</summary>
        public const float ShoulderWidthMetres = 7.5f;

        /// <summary>Sealed taxi shoulder each side (ICAO/CASA code C/E).</summary>
        public const float TaxiSealedShoulderMetres = 3.5f;

        /// <summary>
        /// Distance from the runway edge to the hold-short bars on a runway exit, so the
        /// painted holding position lands at <see cref="RunwayHoldingPositionFromCentrelineMetres"/>.
        /// </summary>
        public static float HoldShortFromRunwayEdgeMetres =>
            RunwayHoldingPositionFromCentrelineMetres - MainHalfWidth;

        // --- Ops plateau: dead level over everything paved, with margin ---

        /// <summary>Covers the 05 threshold (−1 550) to the terminal apron and taxiway T1 (+1 760).</summary>
        public const float PlateauHalfX = 1850f;

        /// <summary>Covers 12/30 from its south-east end (−370) to the runway 12 end (+1 190).</summary>
        public const float PlateauHalfZ = 1300f;

        // --- Distance fields ---

        /// <summary>Distance from a point to the nearest main or cross runway pavement.</summary>
        public static float DistanceToRunwayPavement(float worldX, float worldZ) =>
            Math.Min(
                DistanceToAxisAlignedStrip(worldX, worldZ, 0f, 0f, MainHalfLength, MainHalfWidth),
                DistanceToOrientedStrip(worldX, worldZ, CrossCenterX, CrossCenterZ, CrossHalfLength, CrossHalfWidth, CrossYawRadians));

        /// <summary>Distance to the nearest runway strip (the protected graded band), 0 inside one.</summary>
        public static float DistanceToRunwayStrip(float worldX, float worldZ) =>
            Math.Min(
                DistanceToAxisAlignedStrip(worldX, worldZ, 0f, 0f, MainHalfLength, RunwayStripHalfWidthMetres),
                DistanceToOrientedStrip(worldX, worldZ, CrossCenterX, CrossCenterZ, CrossHalfLength, RunwayStripHalfWidthMetres, CrossYawRadians));

        /// <summary>Distance to the nearest taxiway pavement edge, including sealed shoulders; 0 on it.</summary>
        public static float DistanceToTaxiway(float worldX, float worldZ)
        {
            var best = float.MaxValue;
            foreach (var taxiway in AdelaideLayout.Taxiways)
            {
                var half = taxiway.Width * 0.5f + TaxiSealedShoulderMetres;
                var d = DistanceToPolyline(worldX, worldZ, taxiway.Xz) - half;
                if (d < best)
                    best = d;
            }

            return best < 0f ? 0f : best;
        }

        /// <summary>Distance to the nearest apron; 0 inside one.</summary>
        public static float DistanceToApron(float worldX, float worldZ)
        {
            var best = float.MaxValue;
            foreach (var apron in AdelaideLayout.Aprons)
            {
                if (ContainsPolygon(apron.Xz, worldX, worldZ))
                    return 0f;
                var d = DistanceToPolyline(worldX, worldZ, apron.Xz, closed: true);
                if (d < best)
                    best = d;
            }

            return best;
        }

        /// <summary>Distance to any pavement: runways, taxiways with shoulders, aprons.</summary>
        public static float DistanceToPavement(float worldX, float worldZ) =>
            Math.Min(DistanceToRunwayPavement(worldX, worldZ),
                Math.Min(DistanceToTaxiway(worldX, worldZ), DistanceToApron(worldX, worldZ)));

        public static bool ContainsMainRunway(float worldX, float worldZ) =>
            AirsideBareField.ContainsRunway(worldX, worldZ);

        public static bool ContainsCrossRunway(float worldX, float worldZ) =>
            DistanceToOrientedStrip(worldX, worldZ, CrossCenterX, CrossCenterZ, CrossHalfLength + 1e-3f,
                CrossHalfWidth + 1e-3f, CrossYawRadians) <= 0f;

        public static bool ContainsAnyRunway(float worldX, float worldZ) =>
            ContainsMainRunway(worldX, worldZ) || ContainsCrossRunway(worldX, worldZ);

        public static bool ContainsApron(float worldX, float worldZ)
        {
            foreach (var apron in AdelaideLayout.Aprons)
                if (ContainsPolygon(apron.Xz, worldX, worldZ))
                    return true;
            return false;
        }

        /// <summary>Even-odd point-in-polygon for an x,z outline (not closed).</summary>
        public static bool ContainsPolygon(float[] xz, float x, float z)
        {
            var inside = false;
            var count = xz.Length / 2;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                float xi = xz[i * 2], zi = xz[i * 2 + 1], xj = xz[j * 2], zj = xz[j * 2 + 1];
                if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi)
                    inside = !inside;
            }

            return inside;
        }

        /// <summary>Distance from a point to an x,z polyline (or closed outline).</summary>
        public static float DistanceToPolyline(float x, float z, float[] xz, bool closed = false)
        {
            var count = xz.Length / 2;
            var best = float.MaxValue;
            var segments = closed ? count : count - 1;
            for (var i = 0; i < segments; i++)
            {
                var j = (i + 1) % count;
                var d = DistanceToSegment(x, z, xz[i * 2], xz[i * 2 + 1], xz[j * 2], xz[j * 2 + 1]);
                if (d < best)
                    best = d;
            }

            return best;
        }

        private static float DistanceToSegment(float px, float pz, float ax, float az, float bx, float bz)
        {
            var dx = bx - ax;
            var dz = bz - az;
            var lengthSquared = dx * dx + dz * dz;
            var t = lengthSquared > 1e-6f ? ((px - ax) * dx + (pz - az) * dz) / lengthSquared : 0f;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            var cx = ax + dx * t - px;
            var cz = az + dz * t - pz;
            return (float)Math.Sqrt(cx * cx + cz * cz);
        }

        private static float DistanceToAxisAlignedStrip(
            float worldX, float worldZ, float centerX, float centerZ, float halfLengthX, float halfWidthZ)
        {
            var dx = Math.Abs(worldX - centerX) - halfLengthX;
            var dz = Math.Abs(worldZ - centerZ) - halfWidthZ;
            if (dx < 0f) dx = 0f;
            if (dz < 0f) dz = 0f;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        private static float DistanceToOrientedStrip(
            float worldX, float worldZ, float centerX, float centerZ, float halfLength, float halfWidth, float yawRadians)
        {
            // Unity yaw turns local +X towards world −Z, so un-rotate into the strip frame.
            var dx = worldX - centerX;
            var dz = worldZ - centerZ;
            var cos = (float)Math.Cos(yawRadians);
            var sin = (float)Math.Sin(yawRadians);
            var localX = dx * cos - dz * sin;
            var localZ = dx * sin + dz * cos;
            var lx = Math.Abs(localX) - halfLength;
            var lz = Math.Abs(localZ) - halfWidth;
            if (lx < 0f) lx = 0f;
            if (lz < 0f) lz = 0f;
            return (float)Math.Sqrt(lx * lx + lz * lz);
        }
    }
}
