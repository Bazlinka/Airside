using System;

namespace Airside.Presentation
{
    /// <summary>
    /// Real-metre YPAD pavement silhouette for the bare Adelaide field: main 05/23,
    /// cross 12/30, parallel Taxiway F, and D/E exit links. Pure constants — no
    /// UnityEngine types — so headless tests and the runtime builder share one layout.
    ///
    /// Simulation taxi topology stays on the 1:20 miniature until a separate ADR
    /// moves reservations onto these metres. Presentation only.
    /// </summary>
    public static class AirsideAdelaidePavement
    {
        // --- Main runway 05/23 (world +X = runway 05 → 23) ---

        public const string MainRunwayName = "Runway 05/23";

        public static float MainLengthMetres => AirsideBareField.RunwayLengthMetres;
        public static float MainWidthMetres => AirsideBareField.RunwayWidthMetres;
        public static float MainHalfLength => AirsideBareField.RunwayHalfLength;
        public static float MainHalfWidth => AirsideBareField.RunwayHalfWidth;

        // --- Cross runway 12/30 ---

        public const string CrossRunwayName = "Runway 12/30";

        /// <summary>Published YPAD 12/30 length (metres).</summary>
        public const float CrossLengthMetres = 1652f;

        public const float CrossWidthMetres = 45f;

        /// <summary>
        /// Yaw from world +X (05/23) toward the 12 heading.
        /// Magnetic 115° − 042° ≈ 73°.
        /// </summary>
        public const float CrossYawDegrees = 73f;

        public const float CrossCenterX = 0f;
        public const float CrossCenterZ = 0f;

        public static float CrossHalfLength => CrossLengthMetres * 0.5f;
        public static float CrossHalfWidth => CrossWidthMetres * 0.5f;

        public static float CrossYawRadians => CrossYawDegrees * (float)Math.PI / 180f;

        // --- Taxiway F (parallel, terminal / north side of 05/23) ---

        public const string TaxiwayFName = "Taxiway F";
        public const float TaxiwayWidthMetres = 23f;
        public const float TaxiwayFCenterZ = 95f;
        public const float TaxiwayFLengthMetres = 3000f;

        public static float TaxiwayHalfWidth => TaxiwayWidthMetres * 0.5f;

        // --- Exit links D (23 / east) and E (05 / west) ---

        public const string TaxiwayDName = "Taxiway D";
        public const string TaxiwayEName = "Taxiway E";

        /// <summary>Along-runway station of Taxiway D (toward 23 / east threshold).</summary>
        public const float TaxiwayDCenterX = 1100f;

        /// <summary>Along-runway station of Taxiway E (toward 05 / west threshold).</summary>
        public const float TaxiwayECenterX = -1100f;

        /// <summary>
        /// Length of the D/E stub from the main runway outer edge to Taxiway F inner edge.
        /// </summary>
        public static float TaxiLinkLengthZ =>
            TaxiwayFCenterZ - MainHalfWidth - TaxiwayHalfWidth;

        public static float TaxiLinkCenterZ =>
            MainHalfWidth + TaxiLinkLengthZ * 0.5f;

        // --- Shoulders ---

        public const float ShoulderWidthMetres = 7.5f;

        // --- Ops plateau (covers both strips + Taxiway F) ---

        public const float PlateauHalfX = 1600f;
        public const float PlateauHalfZ = 860f;

        /// <summary>Distance from a point to the nearest main or cross runway pavement.</summary>
        public static float DistanceToRunwayPavement(float worldX, float worldZ)
        {
            var main = DistanceToAxisAlignedStrip(
                worldX, worldZ, 0f, 0f, MainHalfLength, MainHalfWidth);
            var cross = DistanceToOrientedStrip(
                worldX, worldZ, CrossCenterX, CrossCenterZ,
                CrossHalfLength, CrossHalfWidth, CrossYawRadians);
            return Math.Min(main, cross);
        }

        /// <summary>Distance to any silhouette pavement (runways + F + D/E).</summary>
        public static float DistanceToPavement(float worldX, float worldZ)
        {
            var d = DistanceToRunwayPavement(worldX, worldZ);
            d = Math.Min(d, DistanceToAxisAlignedStrip(
                worldX, worldZ, 0f, TaxiwayFCenterZ,
                TaxiwayFLengthMetres * 0.5f, TaxiwayHalfWidth));
            d = Math.Min(d, DistanceToAxisAlignedStrip(
                worldX, worldZ, TaxiwayDCenterX, TaxiLinkCenterZ,
                TaxiwayHalfWidth, TaxiLinkLengthZ * 0.5f));
            d = Math.Min(d, DistanceToAxisAlignedStrip(
                worldX, worldZ, TaxiwayECenterX, TaxiLinkCenterZ,
                TaxiwayHalfWidth, TaxiLinkLengthZ * 0.5f));
            return d;
        }

        public static bool ContainsMainRunway(float worldX, float worldZ) =>
            AirsideBareField.ContainsRunway(worldX, worldZ);

        public static bool ContainsCrossRunway(float worldX, float worldZ)
        {
            var dx = worldX - CrossCenterX;
            var dz = worldZ - CrossCenterZ;
            var cos = (float)Math.Cos(CrossYawRadians);
            var sin = (float)Math.Sin(CrossYawRadians);
            var localX = dx * cos + dz * sin;
            var localZ = -dx * sin + dz * cos;
            return Math.Abs(localX) <= CrossHalfLength + 1e-3f
                   && Math.Abs(localZ) <= CrossHalfWidth + 1e-3f;
        }

        public static bool ContainsAnyRunway(float worldX, float worldZ) =>
            ContainsMainRunway(worldX, worldZ) || ContainsCrossRunway(worldX, worldZ);

        private static float DistanceToAxisAlignedStrip(
            float worldX, float worldZ,
            float centerX, float centerZ,
            float halfLengthX, float halfWidthZ)
        {
            var dx = Math.Abs(worldX - centerX) - halfLengthX;
            var dz = Math.Abs(worldZ - centerZ) - halfWidthZ;
            if (dx < 0f) dx = 0f;
            if (dz < 0f) dz = 0f;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        private static float DistanceToOrientedStrip(
            float worldX, float worldZ,
            float centerX, float centerZ,
            float halfLength, float halfWidth,
            float yawRadians)
        {
            var dx = worldX - centerX;
            var dz = worldZ - centerZ;
            var cos = (float)Math.Cos(yawRadians);
            var sin = (float)Math.Sin(yawRadians);
            var localX = dx * cos + dz * sin;
            var localZ = -dx * sin + dz * cos;
            var lx = Math.Abs(localX) - halfLength;
            var lz = Math.Abs(localZ) - halfWidth;
            if (lx < 0f) lx = 0f;
            if (lz < 0f) lz = 0f;
            return (float)Math.Sqrt(lx * lx + lz * lz);
        }
    }
}
