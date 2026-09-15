using System;
using Airside.Simulation;

namespace Airside.Presentation
{
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
