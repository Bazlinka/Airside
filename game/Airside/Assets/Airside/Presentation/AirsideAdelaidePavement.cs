using System;

namespace Airside.Presentation
{
    /// <summary>
    /// Real-metre YPAD pavement silhouette for the bare Adelaide field: main 05/23,
    /// cross 12/30, parallel Taxiways F + A, D/E/D2/E2 runway exits, A–F links,
    /// terminal + RFDS apron pads, Code-C/E fillets, and sealed taxi shoulders.
    /// Pure constants — no UnityEngine types — so headless tests and the runtime
    /// builder share one layout.
    ///
    /// Simulation taxi topology stays on the 1:20 miniature until a separate ADR
    /// moves reservations onto these metres. Presentation only. No buildings.
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

        // --- Taxiway F (parallel, first north of 05/23) ---

        public const string TaxiwayFName = "Taxiway F";
        public const float TaxiwayWidthMetres = 23f;
        public const float TaxiwayFCenterZ = 95f;
        public const float TaxiwayFLengthMetres = 3000f;

        public static float TaxiwayHalfWidth => TaxiwayWidthMetres * 0.5f;

        // --- Taxiway A (parallel, terminal side of F — DAP A spine) ---

        public const string TaxiwayAName = "Taxiway A";

        /// <summary>
        /// Centreline Z for Taxiway A. ~105 m north of F matches the DAP parallel
        /// separation between the F and A spines on the terminal side.
        /// </summary>
        public const float TaxiwayACenterZ = 200f;

        public const float TaxiwayALengthMetres = 2400f;

        // --- Runway exit links (F ↔ 05/23) ---

        public const string TaxiwayDName = "Taxiway D";
        public const string TaxiwayEName = "Taxiway E";
        public const string TaxiwayD2Name = "Taxiway D2";
        public const string TaxiwayE2Name = "Taxiway E2";

        /// <summary>Along-runway station of Taxiway D (toward 23 / east).</summary>
        public const float TaxiwayDCenterX = 1100f;

        /// <summary>Along-runway station of Taxiway E (toward 05 / west).</summary>
        public const float TaxiwayECenterX = -1100f;

        /// <summary>Inner exit toward 23 (DAP D2-ish station).</summary>
        public const float TaxiwayD2CenterX = 550f;

        /// <summary>Inner exit toward 05 (DAP E2-ish station).</summary>
        public const float TaxiwayE2CenterX = -550f;

        /// <summary>
        /// Length of a runway↔F stub from the main runway outer edge to Taxiway F inner edge.
        /// </summary>
        public static float TaxiLinkLengthZ =>
            TaxiwayFCenterZ - MainHalfWidth - TaxiwayHalfWidth;

        public static float TaxiLinkCenterZ =>
            MainHalfWidth + TaxiLinkLengthZ * 0.5f;

        /// <summary>X stations of all runway↔F exit links.</summary>
        public static float[] RunwayExitCenterXs { get; } =
        {
            TaxiwayECenterX, TaxiwayE2CenterX, TaxiwayD2CenterX, TaxiwayDCenterX
        };

        public static string[] RunwayExitNames { get; } =
        {
            TaxiwayEName, TaxiwayE2Name, TaxiwayD2Name, TaxiwayDName
        };

        // --- A↔F cross-links ---

        /// <summary>
        /// Along-runway stations of the A↔F connectors (DAP feeders between the
        /// parallel spines — silhouette of B/T/L-class joins without naming the maze).
        /// </summary>
        public static float[] AfLinkCenterXs { get; } =
        {
            -900f, -300f, 300f, 900f
        };

        /// <summary>Length of an A↔F stub (F north edge → A south edge).</summary>
        public static float AfLinkLengthZ =>
            TaxiwayACenterZ - TaxiwayFCenterZ - TaxiwayWidthMetres;

        public static float AfLinkCenterZ =>
            (TaxiwayFCenterZ + TaxiwayACenterZ) * 0.5f;

        // --- Aprons (pads only — no buildings) ---

        public const string TerminalApronName = "Apron terminal";

        /// <summary>Terminal 1 apron pad north of Taxiway A (concrete, empty).</summary>
        public const float TerminalApronCenterX = 150f;
        public const float TerminalApronCenterZ = 340f;
        public const float TerminalApronLengthX = 1200f;
        public const float TerminalApronWidthZ = 160f;

        /// <summary>Apron entry stubs from A north edge into the terminal apron.</summary>
        public static float[] ApronEntryCenterXs { get; } =
        {
            -200f, 200f, 600f
        };

        public static float ApronEntryLengthZ =>
            TerminalApronCenterZ - TerminalApronWidthZ * 0.5f
            - (TaxiwayACenterZ + TaxiwayHalfWidth);

        public static float ApronEntryCenterZ =>
            TaxiwayACenterZ + TaxiwayHalfWidth + ApronEntryLengthZ * 0.5f;

        public const string RfdsApronName = "Apron RFDS";

        /// <summary>Small RFDS / south apron silhouette south of 05/23.</summary>
        public const float RfdsApronCenterX = -900f;
        public const float RfdsApronCenterZ = -180f;
        public const float RfdsApronLengthX = 220f;
        public const float RfdsApronWidthZ = 120f;

        // --- Shoulders ---

        /// <summary>Runway graded shoulder (dirt) each side of 05/23 and 12/30.</summary>
        public const float ShoulderWidthMetres = 7.5f;

        /// <summary>
        /// Sealed taxi shoulder each side (ICAO/CASA Code C/E sealed shoulder band).
        /// </summary>
        public const float TaxiSealedShoulderMetres = 3.5f;

        // --- Fillets ---

        /// <summary>
        /// Outer pavement fillet radius at runway ↔ taxi and F ↔ A T-junctions.
        /// Sized for Code C/E 90° turns (≈ 40–45 m pavement radius).
        /// </summary>
        public const float TaxiFilletRadiusMetres = 42f;

        /// <summary>Slightly tighter fillet for apron taxilane entries.</summary>
        public const float ApronFilletRadiusMetres = 28f;

        /// <summary>Rounded semicircle caps on long parallel taxi ends.</summary>
        public const float TaxiwayEndCapRadiusMetres = 11.5f; // = half width

        /// <summary>
        /// Soft circular pad at the 05/23 × 12/30 crossing so the diamond join
        /// does not read as two hard rectangle edges.
        /// </summary>
        public const float RunwayCrossingPadRadiusMetres = 38f;

        /// <summary>Arc segments per quarter fillet (presentation mesh density).</summary>
        public const int FilletArcSegments = 14;

        // --- Ops plateau (covers strips + F/A + aprons + fillets) ---

        public const float PlateauHalfX = 1600f;

        /// <summary>
        /// Covers 12/30 tips (~790 m) plus terminal apron (~420 m) and RFDS pad.
        /// </summary>
        public const float PlateauHalfZ = 860f;

        /// <summary>
        /// One concave-corner fillet: quarter-disk centre at the rectangle join,
        /// sweeping <see cref="SweepRadians"/> from <see cref="StartRadians"/> (CCW from +X).
        /// </summary>
        public readonly struct FilletSpec
        {
            public FilletSpec(float centerX, float centerZ, float radius, float startRadians, float sweepRadians)
            {
                CenterX = centerX;
                CenterZ = centerZ;
                Radius = radius;
                StartRadians = startRadians;
                SweepRadians = sweepRadians;
            }

            public float CenterX { get; }
            public float CenterZ { get; }
            public float Radius { get; }
            public float StartRadians { get; }
            public float SweepRadians { get; }
        }

        /// <summary>
        /// All fillet disks for the silhouette. Pure layout — the builder turns these into meshes.
        /// </summary>
        public static FilletSpec[] AllFillets()
        {
            var hw = TaxiwayHalfWidth;
            var r = TaxiFilletRadiusMetres;
            var apronR = ApronFilletRadiusMetres;
            var list = new FilletSpec[64];
            var n = 0;

            void AddTJunctionSouthOfHorizontal(float linkX, float horizZ)
            {
                // Horizontal taxi south edge ↔ link west/east.
                list[n++] = new FilletSpec(linkX - hw, horizZ - hw, r, (float)Math.PI, (float)Math.PI * 0.5f);
                list[n++] = new FilletSpec(linkX + hw, horizZ - hw, r, (float)Math.PI * 1.5f, (float)Math.PI * 0.5f);
            }

            void AddTJunctionNorthOfHorizontal(float linkX, float horizZ)
            {
                list[n++] = new FilletSpec(linkX - hw, horizZ + hw, r, (float)Math.PI * 0.5f, (float)Math.PI * 0.5f);
                list[n++] = new FilletSpec(linkX + hw, horizZ + hw, r, 0f, (float)Math.PI * 0.5f);
            }

            void AddRunwayExit(float linkX)
            {
                AddTJunctionSouthOfHorizontal(linkX, TaxiwayFCenterZ);
                // Runway north edge ↔ link.
                list[n++] = new FilletSpec(linkX - hw, MainHalfWidth, r, (float)Math.PI * 0.5f, (float)Math.PI * 0.5f);
                list[n++] = new FilletSpec(linkX + hw, MainHalfWidth, r, 0f, (float)Math.PI * 0.5f);
            }

            for (var i = 0; i < RunwayExitCenterXs.Length; i++)
                AddRunwayExit(RunwayExitCenterXs[i]);

            // A↔F links: fillets on F north and A south.
            for (var i = 0; i < AfLinkCenterXs.Length; i++)
            {
                var x = AfLinkCenterXs[i];
                AddTJunctionNorthOfHorizontal(x, TaxiwayFCenterZ);
                AddTJunctionSouthOfHorizontal(x, TaxiwayACenterZ);
            }

            // Apron entries: A north edge ↔ apron south edge (tighter radius).
            var aNorth = TaxiwayACenterZ + hw;
            var apronSouth = TerminalApronCenterZ - TerminalApronWidthZ * 0.5f;
            for (var i = 0; i < ApronEntryCenterXs.Length; i++)
            {
                var x = ApronEntryCenterXs[i];
                list[n++] = new FilletSpec(x - hw, aNorth, apronR, (float)Math.PI * 0.5f, (float)Math.PI * 0.5f);
                list[n++] = new FilletSpec(x + hw, aNorth, apronR, 0f, (float)Math.PI * 0.5f);
                list[n++] = new FilletSpec(x - hw, apronSouth, apronR, (float)Math.PI, (float)Math.PI * 0.5f);
                list[n++] = new FilletSpec(x + hw, apronSouth, apronR, (float)Math.PI * 1.5f, (float)Math.PI * 0.5f);
            }

            // End caps on F and A.
            var capR = TaxiwayEndCapRadiusMetres;
            var fHalf = TaxiwayFLengthMetres * 0.5f;
            list[n++] = new FilletSpec(fHalf, TaxiwayFCenterZ, capR, (float)-Math.PI * 0.5f, (float)Math.PI);
            list[n++] = new FilletSpec(-fHalf, TaxiwayFCenterZ, capR, (float)Math.PI * 0.5f, (float)Math.PI);
            var aHalf = TaxiwayALengthMetres * 0.5f;
            list[n++] = new FilletSpec(aHalf, TaxiwayACenterZ, capR, (float)-Math.PI * 0.5f, (float)Math.PI);
            list[n++] = new FilletSpec(-aHalf, TaxiwayACenterZ, capR, (float)Math.PI * 0.5f, (float)Math.PI);

            // Crossing soften pad.
            list[n++] = new FilletSpec(0f, 0f, RunwayCrossingPadRadiusMetres, 0f, (float)Math.PI * 2f);

            if (n != list.Length)
                Array.Resize(ref list, n);
            return list;
        }

        /// <summary>Distance from a point to the nearest main or cross runway pavement.</summary>
        public static float DistanceToRunwayPavement(float worldX, float worldZ)
        {
            var main = DistanceToAxisAlignedStrip(
                worldX, worldZ, 0f, 0f, MainHalfLength, MainHalfWidth);
            var cross = DistanceToOrientedStrip(
                worldX, worldZ, CrossCenterX, CrossCenterZ,
                CrossHalfLength, CrossHalfWidth, CrossYawRadians);
            var d = Math.Min(main, cross);
            var pad = DistanceToDisk(worldX, worldZ, 0f, 0f, RunwayCrossingPadRadiusMetres);
            return Math.Min(d, pad);
        }

        /// <summary>Distance to any silhouette pavement (runways + taxi + aprons + fillets).</summary>
        public static float DistanceToPavement(float worldX, float worldZ)
        {
            var d = DistanceToRunwayPavement(worldX, worldZ);

            d = Math.Min(d, DistanceToAxisAlignedStrip(
                worldX, worldZ, 0f, TaxiwayFCenterZ,
                TaxiwayFLengthMetres * 0.5f, TaxiwayHalfWidth));
            d = Math.Min(d, DistanceToAxisAlignedStrip(
                worldX, worldZ, 0f, TaxiwayACenterZ,
                TaxiwayALengthMetres * 0.5f, TaxiwayHalfWidth));

            for (var i = 0; i < RunwayExitCenterXs.Length; i++)
            {
                d = Math.Min(d, DistanceToAxisAlignedStrip(
                    worldX, worldZ, RunwayExitCenterXs[i], TaxiLinkCenterZ,
                    TaxiwayHalfWidth, TaxiLinkLengthZ * 0.5f));
            }

            for (var i = 0; i < AfLinkCenterXs.Length; i++)
            {
                d = Math.Min(d, DistanceToAxisAlignedStrip(
                    worldX, worldZ, AfLinkCenterXs[i], AfLinkCenterZ,
                    TaxiwayHalfWidth, AfLinkLengthZ * 0.5f));
            }

            for (var i = 0; i < ApronEntryCenterXs.Length; i++)
            {
                d = Math.Min(d, DistanceToAxisAlignedStrip(
                    worldX, worldZ, ApronEntryCenterXs[i], ApronEntryCenterZ,
                    TaxiwayHalfWidth, ApronEntryLengthZ * 0.5f));
            }

            d = Math.Min(d, DistanceToAxisAlignedStrip(
                worldX, worldZ, TerminalApronCenterX, TerminalApronCenterZ,
                TerminalApronLengthX * 0.5f, TerminalApronWidthZ * 0.5f));
            d = Math.Min(d, DistanceToAxisAlignedStrip(
                worldX, worldZ, RfdsApronCenterX, RfdsApronCenterZ,
                RfdsApronLengthX * 0.5f, RfdsApronWidthZ * 0.5f));

            // Sealed taxi shoulders on the long parallels.
            var shoulderHalf = TaxiwayHalfWidth + TaxiSealedShoulderMetres;
            d = Math.Min(d, DistanceToAxisAlignedStrip(
                worldX, worldZ, 0f, TaxiwayFCenterZ,
                TaxiwayFLengthMetres * 0.5f, shoulderHalf));
            d = Math.Min(d, DistanceToAxisAlignedStrip(
                worldX, worldZ, 0f, TaxiwayACenterZ,
                TaxiwayALengthMetres * 0.5f, shoulderHalf));

            var fillets = AllFillets();
            for (var i = 0; i < fillets.Length; i++)
            {
                var f = fillets[i];
                d = Math.Min(d, DistanceToDisk(worldX, worldZ, f.CenterX, f.CenterZ, f.Radius));
            }

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

        public static bool ContainsTerminalApron(float worldX, float worldZ) =>
            Math.Abs(worldX - TerminalApronCenterX) <= TerminalApronLengthX * 0.5f + 1e-3f
            && Math.Abs(worldZ - TerminalApronCenterZ) <= TerminalApronWidthZ * 0.5f + 1e-3f;

        public static bool ContainsFillet(float worldX, float worldZ)
        {
            var fillets = AllFillets();
            for (var i = 0; i < fillets.Length; i++)
            {
                var f = fillets[i];
                var dx = worldX - f.CenterX;
                var dz = worldZ - f.CenterZ;
                if (dx * dx + dz * dz <= f.Radius * f.Radius + 1e-3f)
                    return true;
            }

            return false;
        }

        private static float DistanceToDisk(
            float worldX, float worldZ, float centerX, float centerZ, float radius)
        {
            var dx = worldX - centerX;
            var dz = worldZ - centerZ;
            var dist = (float)Math.Sqrt(dx * dx + dz * dz) - radius;
            return dist < 0f ? 0f : dist;
        }

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
