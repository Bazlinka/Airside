using System;

namespace Airside.Presentation
{
    /// <summary>
    /// Kind of curved pavement patch in the silhouette.
    /// </summary>
    public enum PavementArcKind
    {
        /// <summary>
        /// Concave fillet in a re-entrant (inside) corner: the curved triangle
        /// bounded by the two pavement edges and an arc tangent to both. This is
        /// what a real taxiway fillet is — it tucks *into* the corner.
        /// </summary>
        CornerFillet,

        /// <summary>
        /// Convex sector or full disk: taxiway end caps and the runway crossing pad.
        /// </summary>
        Sector
    }

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

        /// <summary>
        /// Half-width of the ICAO Annex 14 runway strip for a code 4 precision
        /// approach runway (300 m wide overall). Nothing but the runway, its
        /// shoulders and frangible aids may stand inside this band, so the taxi
        /// spines and apron pads are all checked against it.
        /// </summary>
        public const float RunwayStripHalfWidthMetres = 150f;

        /// <summary>
        /// ICAO Annex 14 minimum runway-centreline to parallel-taxiway-centreline
        /// separation for code 4E on a precision approach runway.
        /// </summary>
        public const float CodeERunwayToTaxiwaySeparationMetres = 182.5f;

        /// <summary>
        /// ICAO Annex 14 minimum taxiway-to-taxiway centreline separation, code E.
        /// </summary>
        public const float CodeETaxiwayToTaxiwaySeparationMetres = 80f;

        /// <summary>
        /// Runway holding position distance from the runway centreline, code E
        /// precision approach runway.
        /// </summary>
        public const float RunwayHoldingPositionFromCentrelineMetres = 90f;

        // --- Cross runway 12/30 ---

        public const string CrossRunwayName = "Runway 12/30";

        /// <summary>Published YPAD 12/30 length (metres).</summary>
        public const float CrossLengthMetres = 1652f;

        public const float CrossWidthMetres = 45f;

        /// <summary>
        /// Yaw from world +X (05/23) toward the 12 heading.
        ///
        /// NOTE: this is a silhouette approximation, not a surveyed figure. The
        /// designators alone bound the crossing angle to roughly 61°–79°
        /// (12 → 115°–124° M, 05 → 045°–054° M), and 73° sits inside that band.
        /// It has NOT been checked against the published YPAD DAP; an earlier
        /// comment here derived it from "115° − 042°", but 042° is the bearing of
        /// a runway designated 04, not 05, so that derivation was wrong even
        /// though the result is plausible. Confirm against the DAP before any
        /// simulation topology is hung off these metres.
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

        /// <summary>
        /// Centreline Z for Taxiway F. Held at the code 4E runway/taxiway
        /// separation so the spine clears the 150 m runway strip half-width with
        /// 21 m to spare (F south edge sits at Z = 171 m).
        /// </summary>
        public const float TaxiwayFCenterZ = CodeERunwayToTaxiwaySeparationMetres;

        public const float TaxiwayFLengthMetres = 3000f;

        public static float TaxiwayHalfWidth => TaxiwayWidthMetres * 0.5f;

        // --- Taxiway A (parallel, terminal side of F — DAP A spine) ---

        public const string TaxiwayAName = "Taxiway A";

        /// <summary>
        /// Centreline Z for Taxiway A. 107.5 m north of F — comfortably over the
        /// code E taxiway-to-taxiway minimum of 80 m.
        /// </summary>
        public const float TaxiwayACenterZ = 290f;

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

        /// <summary>
        /// Distance from the runway edge to the hold-short bars on a runway exit
        /// link, so the painted holding position lands at
        /// <see cref="RunwayHoldingPositionFromCentrelineMetres"/> from the runway
        /// centreline.
        /// </summary>
        public static float HoldShortFromRunwayEdgeMetres =>
            RunwayHoldingPositionFromCentrelineMetres - MainHalfWidth;

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

        /// <summary>
        /// Terminal apron pad north of Taxiway A (concrete, empty). Placed on the
        /// <b>west</b> / terminal side of 12/30 so the cross-runway strip never cuts
        /// the pad (YPAD: apron sits clear of both runways, fed by taxiways).
        /// </summary>
        public const float TerminalApronCenterX = -600f;
        public const float TerminalApronCenterZ = 450f;
        public const float TerminalApronLengthX = 900f;
        public const float TerminalApronWidthZ = 150f;

        /// <summary>
        /// Minimum runway edge clearance the terminal apron must keep (metres).
        /// Used by headless regression so a future layout slip cannot reintroduce
        /// the 12/30-through-apron bug.
        /// </summary>
        public const float ApronRunwayClearanceMetres = 60f;

        /// <summary>Apron entry stubs from A north edge into the terminal apron.</summary>
        public static float[] ApronEntryCenterXs { get; } =
        {
            -800f, -550f, -300f
        };

        public static float ApronEntryLengthZ =>
            TerminalApronCenterZ - TerminalApronWidthZ * 0.5f
            - (TaxiwayACenterZ + TaxiwayHalfWidth);

        public static float ApronEntryCenterZ =>
            TaxiwayACenterZ + TaxiwayHalfWidth + ApronEntryLengthZ * 0.5f;

        public const string RfdsApronName = "Apron RFDS";

        /// <summary>
        /// Small RFDS / south apron silhouette south of 05/23, held clear of the
        /// 150 m runway strip half-width (north edge sits at Z = −170 m).
        /// </summary>
        public const float RfdsApronCenterX = -900f;
        public const float RfdsApronCenterZ = -230f;
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

        /// <summary>
        /// A fillet may never reach further along a stub than the stub is long, or
        /// the two fillets at opposite ends meet and the stub stops reading as a
        /// taxiway. Leaves a little headroom so the tangent point stays on pavement.
        /// </summary>
        public static float ClampFilletRadius(float nominalRadius, float legLengthMetres)
        {
            var limit = legLengthMetres * 0.9f;
            if (limit <= 0f)
                return 0f;
            return nominalRadius < limit ? nominalRadius : limit;
        }

        // --- Ops plateau (covers strips + F/A + aprons + fillets) ---

        public const float PlateauHalfX = 1600f;

        /// <summary>
        /// Covers 12/30 tips (~790 m) plus terminal apron (~525 m) and RFDS pad.
        /// </summary>
        public const float PlateauHalfZ = 860f;

        /// <summary>
        /// One curved pavement patch.
        ///
        /// For <see cref="PavementArcKind.CornerFillet"/>, <see cref="CenterX"/> /
        /// <see cref="CenterZ"/> are the <b>corner point</b> where the two pavement
        /// edges meet and <see cref="StartRadians"/> names the empty quadrant the
        /// fillet fills (0, π/2, π or 3π/2 measured CCW from +X); the sweep is
        /// always a quarter turn. The filled region is that quadrant's r × r square
        /// minus the disk of radius r centred at
        /// (<see cref="ArcCenterX"/>, <see cref="ArcCenterZ"/>) — i.e. a curved
        /// triangle tangent to both edges, which is what a pavement fillet is.
        ///
        /// For <see cref="PavementArcKind.Sector"/>, <see cref="CenterX"/> /
        /// <see cref="CenterZ"/> are the disk centre and the patch is the plain
        /// sector swept from <see cref="StartRadians"/> by <see cref="SweepRadians"/>.
        /// </summary>
        public readonly struct FilletSpec
        {
            public FilletSpec(
                PavementArcKind kind,
                float centerX,
                float centerZ,
                float radius,
                float startRadians,
                float sweepRadians)
            {
                Kind = kind;
                CenterX = centerX;
                CenterZ = centerZ;
                Radius = radius;
                StartRadians = startRadians;
                SweepRadians = sweepRadians;
            }

            public PavementArcKind Kind { get; }
            public float CenterX { get; }
            public float CenterZ { get; }
            public float Radius { get; }
            public float StartRadians { get; }
            public float SweepRadians { get; }

            /// <summary>+1 or −1: which way the fillet opens along X.</summary>
            public float OutwardX =>
                Math.Cos(StartRadians + Math.PI * 0.25) >= 0d ? 1f : -1f;

            /// <summary>+1 or −1: which way the fillet opens along Z.</summary>
            public float OutwardZ =>
                Math.Sin(StartRadians + Math.PI * 0.25) >= 0d ? 1f : -1f;

            /// <summary>
            /// Centre of the tangent arc — offset from the corner by r along each
            /// edge, so the arc touches both edges exactly once.
            /// </summary>
            public float ArcCenterX => CenterX + OutwardX * Radius;

            public float ArcCenterZ => CenterZ + OutwardZ * Radius;

            public bool Contains(float worldX, float worldZ)
            {
                if (Kind == PavementArcKind.Sector)
                {
                    var sdx = worldX - CenterX;
                    var sdz = worldZ - CenterZ;
                    return sdx * sdx + sdz * sdz <= Radius * Radius + 1e-3f;
                }

                var u = (worldX - CenterX) * OutwardX;
                var v = (worldZ - CenterZ) * OutwardZ;
                if (u < -1e-3f || u > Radius + 1e-3f || v < -1e-3f || v > Radius + 1e-3f)
                    return false;
                var au = u - Radius;
                var av = v - Radius;
                return au * au + av * av >= Radius * Radius - 1e-3f;
            }

            /// <summary>
            /// Distance from a point to this patch, 0 when inside. Corner fillets
            /// report <see cref="float.MaxValue"/> for points behind either
            /// pavement edge — those points sit on the abutting slab, which the
            /// caller measures separately.
            /// </summary>
            public float DistanceTo(float worldX, float worldZ)
            {
                if (Kind == PavementArcKind.Sector)
                {
                    var sdx = worldX - CenterX;
                    var sdz = worldZ - CenterZ;
                    var d = (float)Math.Sqrt(sdx * sdx + sdz * sdz) - Radius;
                    return d < 0f ? 0f : d;
                }

                var u = (worldX - CenterX) * OutwardX;
                var v = (worldZ - CenterZ) * OutwardZ;
                if (u < 0f || v < 0f)
                    return float.MaxValue;

                var cu = u < 0f ? 0f : (u > Radius ? Radius : u);
                var cv = v < 0f ? 0f : (v > Radius ? Radius : v);
                var au = cu - Radius;
                var av = cv - Radius;
                if (au * au + av * av >= Radius * Radius)
                    return (float)Math.Sqrt((u - cu) * (u - cu) + (v - cv) * (v - cv));

                // Clamped point falls inside the removed disk — nearest pavement is
                // the tangent arc itself.
                var len = (float)Math.Sqrt(au * au + av * av);
                float nu, nv;
                if (len <= 1e-4f)
                {
                    nu = 0f;
                    nv = Radius;
                }
                else
                {
                    nu = Radius + au / len * Radius;
                    nv = Radius + av / len * Radius;
                }

                return (float)Math.Sqrt((u - nu) * (u - nu) + (v - nv) * (v - nv));
            }
        }

        private static FilletSpec[] _allFillets;

        /// <summary>
        /// All curved pavement patches for the silhouette. Pure layout — the
        /// builder turns these into meshes. Computed once: every input is a
        /// compile-time constant, and this is called per ground-mesh vertex.
        /// The returned array is shared — callers must not mutate it.
        /// </summary>
        public static FilletSpec[] AllFillets() => _allFillets ??= BuildAllFillets();

        private static FilletSpec[] BuildAllFillets()
        {
            var hw = TaxiwayHalfWidth;
            var exitR = ClampFilletRadius(TaxiFilletRadiusMetres, TaxiLinkLengthZ);
            var afR = ClampFilletRadius(TaxiFilletRadiusMetres, AfLinkLengthZ);
            var apronR = ClampFilletRadius(ApronFilletRadiusMetres, ApronEntryLengthZ);
            var quarter = (float)Math.PI * 0.5f;

            var list = new System.Collections.Generic.List<FilletSpec>(64);

            void AddCorner(float cornerX, float cornerZ, float radius, float quadrantStart) =>
                list.Add(new FilletSpec(
                    PavementArcKind.CornerFillet, cornerX, cornerZ, radius, quadrantStart, quarter));

            // Stub meets a horizontal taxiway that lies to the NORTH of it: the two
            // empty corners open south-west and south-east.
            void AddTJunctionSouthOfHorizontal(float linkX, float horizZ, float radius)
            {
                AddCorner(linkX - hw, horizZ - hw, radius, (float)Math.PI);
                AddCorner(linkX + hw, horizZ - hw, radius, (float)Math.PI * 1.5f);
            }

            // Horizontal taxiway lies to the SOUTH: corners open north-west / north-east.
            void AddTJunctionNorthOfHorizontal(float linkX, float horizZ, float radius)
            {
                AddCorner(linkX - hw, horizZ + hw, radius, quarter);
                AddCorner(linkX + hw, horizZ + hw, radius, 0f);
            }

            void AddRunwayExit(float linkX)
            {
                AddTJunctionSouthOfHorizontal(linkX, TaxiwayFCenterZ, exitR);
                // Runway north edge ↔ link: corners open north-west / north-east.
                AddCorner(linkX - hw, MainHalfWidth, exitR, quarter);
                AddCorner(linkX + hw, MainHalfWidth, exitR, 0f);
            }

            for (var i = 0; i < RunwayExitCenterXs.Length; i++)
                AddRunwayExit(RunwayExitCenterXs[i]);

            // A↔F links: fillets on F north and A south.
            for (var i = 0; i < AfLinkCenterXs.Length; i++)
            {
                var x = AfLinkCenterXs[i];
                AddTJunctionNorthOfHorizontal(x, TaxiwayFCenterZ, afR);
                AddTJunctionSouthOfHorizontal(x, TaxiwayACenterZ, afR);
            }

            // Apron entries: A north edge ↔ apron south edge (tighter radius).
            var apronSouthEdge = TerminalApronCenterZ - TerminalApronWidthZ * 0.5f;
            for (var i = 0; i < ApronEntryCenterXs.Length; i++)
            {
                var x = ApronEntryCenterXs[i];
                AddTJunctionNorthOfHorizontal(x, TaxiwayACenterZ, apronR);
                // Apron pad is north of the stub: corners open south-west / south-east.
                AddCorner(x - hw, apronSouthEdge, apronR, (float)Math.PI);
                AddCorner(x + hw, apronSouthEdge, apronR, (float)Math.PI * 1.5f);
            }

            // End caps on F and A (convex semicircles, not corner fillets).
            var capR = TaxiwayEndCapRadiusMetres;
            var fHalf = TaxiwayFLengthMetres * 0.5f;
            list.Add(new FilletSpec(PavementArcKind.Sector, fHalf, TaxiwayFCenterZ, capR, -quarter, (float)Math.PI));
            list.Add(new FilletSpec(PavementArcKind.Sector, -fHalf, TaxiwayFCenterZ, capR, quarter, (float)Math.PI));
            var aHalf = TaxiwayALengthMetres * 0.5f;
            list.Add(new FilletSpec(PavementArcKind.Sector, aHalf, TaxiwayACenterZ, capR, -quarter, (float)Math.PI));
            list.Add(new FilletSpec(PavementArcKind.Sector, -aHalf, TaxiwayACenterZ, capR, quarter, (float)Math.PI));

            // Crossing soften pad.
            list.Add(new FilletSpec(
                PavementArcKind.Sector, 0f, 0f, RunwayCrossingPadRadiusMetres, 0f, (float)Math.PI * 2f));

            return list.ToArray();
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

        /// <summary>
        /// Distance from a point to the nearest runway <b>strip</b> (the protected
        /// graded band around each runway), 0 when inside one. Aprons and taxi
        /// spines are expected to stay out of this.
        /// </summary>
        public static float DistanceToRunwayStrip(float worldX, float worldZ)
        {
            var main = DistanceToAxisAlignedStrip(
                worldX, worldZ, 0f, 0f,
                MainHalfLength, RunwayStripHalfWidthMetres);
            var cross = DistanceToOrientedStrip(
                worldX, worldZ, CrossCenterX, CrossCenterZ,
                CrossHalfLength, RunwayStripHalfWidthMetres, CrossYawRadians);
            return Math.Min(main, cross);
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

            // Stubs carry the same sealed shoulder band as the parallels — the
            // runtime builder spawns them, so the distance field must know.
            var stubHalf = TaxiwayHalfWidth + TaxiSealedShoulderMetres;

            for (var i = 0; i < RunwayExitCenterXs.Length; i++)
            {
                d = Math.Min(d, DistanceToAxisAlignedStrip(
                    worldX, worldZ, RunwayExitCenterXs[i], TaxiLinkCenterZ,
                    stubHalf, TaxiLinkLengthZ * 0.5f));
            }

            for (var i = 0; i < AfLinkCenterXs.Length; i++)
            {
                d = Math.Min(d, DistanceToAxisAlignedStrip(
                    worldX, worldZ, AfLinkCenterXs[i], AfLinkCenterZ,
                    stubHalf, AfLinkLengthZ * 0.5f));
            }

            for (var i = 0; i < ApronEntryCenterXs.Length; i++)
            {
                d = Math.Min(d, DistanceToAxisAlignedStrip(
                    worldX, worldZ, ApronEntryCenterXs[i], ApronEntryCenterZ,
                    stubHalf, ApronEntryLengthZ * 0.5f));
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
                var fd = fillets[i].DistanceTo(worldX, worldZ);
                if (fd < d)
                    d = fd;
            }

            return d;
        }

        /// <summary>
        /// Total pavement half-width across a stub at a given Z, counting the stub
        /// slab and every fillet that reaches it. Used by the regression that keeps
        /// fillets from swallowing the taxiway they are supposed to smooth.
        /// </summary>
        public static float PavementHalfWidthAcrossStub(float stubCenterX, float worldZ)
        {
            var half = 0f;
            for (var probe = 0f; probe <= 200f; probe += 0.25f)
            {
                if (DistanceToPavement(stubCenterX + probe, worldZ) > 0f)
                    break;
                half = probe;
            }

            return half;
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

        /// <summary>
        /// Smallest distance from any point on the terminal apron AABB to either
        /// runway pavement edge. 0 means the pad touches or overlaps a runway
        /// (layout bug) — the strip distance helpers clamp at 0 and never go
        /// negative, so 0 is the failure signal, not a negative number.
        /// </summary>
        public static float TerminalApronClearanceFromRunways(float sampleStepMetres = 10f) =>
            RectClearance(
                TerminalApronCenterX, TerminalApronCenterZ,
                TerminalApronLengthX, TerminalApronWidthZ,
                sampleStepMetres, DistanceToRunwayPavement);

        /// <summary>
        /// Smallest distance from the RFDS apron AABB to either runway pavement edge.
        /// </summary>
        public static float RfdsApronClearanceFromRunways(float sampleStepMetres = 10f) =>
            RectClearance(
                RfdsApronCenterX, RfdsApronCenterZ,
                RfdsApronLengthX, RfdsApronWidthZ,
                sampleStepMetres, DistanceToRunwayPavement);

        /// <summary>Smallest distance from the terminal apron AABB to either runway strip.</summary>
        public static float TerminalApronClearanceFromRunwayStrips(float sampleStepMetres = 10f) =>
            RectClearance(
                TerminalApronCenterX, TerminalApronCenterZ,
                TerminalApronLengthX, TerminalApronWidthZ,
                sampleStepMetres, DistanceToRunwayStrip);

        /// <summary>Smallest distance from the RFDS apron AABB to either runway strip.</summary>
        public static float RfdsApronClearanceFromRunwayStrips(float sampleStepMetres = 10f) =>
            RectClearance(
                RfdsApronCenterX, RfdsApronCenterZ,
                RfdsApronLengthX, RfdsApronWidthZ,
                sampleStepMetres, DistanceToRunwayStrip);

        private static float RectClearance(
            float centerX, float centerZ, float lengthX, float widthZ,
            float sampleStepMetres, Func<float, float, float> distance)
        {
            if (sampleStepMetres < 1f)
                sampleStepMetres = 1f;

            var halfX = lengthX * 0.5f;
            var halfZ = widthZ * 0.5f;
            var min = float.MaxValue;
            for (var x = centerX - halfX; x <= centerX + halfX + 0.01f; x += sampleStepMetres)
            {
                for (var z = centerZ - halfZ; z <= centerZ + halfZ + 0.01f; z += sampleStepMetres)
                {
                    var d = distance(x, z);
                    if (d < min)
                        min = d;
                }
            }

            // Include the corners explicitly (the step may skip them).
            min = Math.Min(min, distance(centerX + halfX, centerZ + halfZ));
            min = Math.Min(min, distance(centerX + halfX, centerZ - halfZ));
            min = Math.Min(min, distance(centerX - halfX, centerZ + halfZ));
            min = Math.Min(min, distance(centerX - halfX, centerZ - halfZ));
            return min;
        }

        public static bool ContainsFillet(float worldX, float worldZ)
        {
            var fillets = AllFillets();
            for (var i = 0; i < fillets.Length; i++)
            {
                if (fillets[i].Contains(worldX, worldZ))
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
