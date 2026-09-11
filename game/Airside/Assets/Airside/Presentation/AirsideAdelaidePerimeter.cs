using System;

namespace Airside.Presentation
{
    /// <summary>
    /// Adelaide Airport (YPAD) airside security perimeter for the bare field.
    /// Fence follows the published ~785 ha site rectangle already used by
    /// <see cref="AirsideBareField"/> (3 400 × 2 309 m). No terminal buildings —
    /// only the boundary fence and vehicle gates at real-metre stations that match
    /// the airside access pattern (north/terminal side, Tapleys/west, south).
    /// Pure constants — no UnityEngine.
    /// </summary>
    public static class AirsideAdelaidePerimeter
    {
        /// <summary>Half-extent along runway axis (matches bare ground length / 2).</summary>
        public static float HalfX => AirsideBareField.GroundLengthMetres * 0.5f;

        /// <summary>Half-extent across runway (matches bare ground width / 2).</summary>
        public static float HalfZ => AirsideBareField.GroundWidthMetres * 0.5f;

        /// <summary>
        /// Inset from the grass ground edge so the fence sits on the property line
        /// without z-fighting the ground slab rim.
        /// </summary>
        public const float FenceInsetMetres = 2f;

        /// <summary>
        /// Chain-link security fence height (typical Australian Class A airport
        /// perimeter ≈ 2.4 m mesh before top outriggers).
        /// </summary>
        public const float FenceHeightMetres = 2.44f;

        /// <summary>Barbed / outrigger top above the mesh.</summary>
        public const float TopGuardHeightMetres = 0.40f;

        public static float TotalHeightMetres => FenceHeightMetres + TopGuardHeightMetres;

        /// <summary>
        /// Panel segment length along the ribbon (metres). Long segments keep the
        /// ~11 km perimeter affordable; posts still read at overview distance.
        /// </summary>
        public const float PostSpacingMetres = 40f;

        /// <summary>Post cross-section (metres).</summary>
        public const float PostSizeMetres = 0.12f;

        /// <summary>Mesh panel thickness (metres) — thin silhouette at overview.</summary>
        public const float PanelThicknessMetres = 0.06f;

        /// <summary>Vehicle gate clear opening (metres).</summary>
        public const float VehicleGateWidthMetres = 8f;

        /// <summary>Gate leaf height (metres).</summary>
        public const float GateLeafHeightMetres = 2.2f;

        public readonly struct GateSpec
        {
            public GateSpec(string name, string side, float stationAlongSide)
            {
                Name = name;
                Side = side;
                StationAlongSide = stationAlongSide;
            }

            public string Name { get; }

            /// <summary>"N", "S", "E", or "W".</summary>
            public string Side { get; }

            /// <summary>
            /// Station along that side: for N/S = world X; for E/W = world Z.
            /// Gate is centred on this station.
            /// </summary>
            public float StationAlongSide { get; }
        }

        /// <summary>
        /// Vehicle gates approximating YPAD airside access: north (terminal / Bradman
        /// side, east of ARP), west (Tapleys Hill Road precinct), south (Melrose side).
        /// </summary>
        public static GateSpec[] VehicleGates { get; } =
        {
            new GateSpec("Gate North Terminal", "N", 420f),
            new GateSpec("Gate West Tapleys", "W", 80f),
            new GateSpec("Gate South Melrose", "S", -280f)
        };

        public static float FenceHalfX => HalfX - FenceInsetMetres;
        public static float FenceHalfZ => HalfZ - FenceInsetMetres;

        /// <summary>
        /// How far the bottom rail is sunk below the ground it stands on, so a
        /// panel never shows daylight under it on sloping ground.
        /// </summary>
        public const float FenceEmbedMetres = 0.15f;

        /// <summary>
        /// World Y for the bottom of a fence panel, post or gate leaf at a point.
        ///
        /// The fence follows <see cref="AirsideAdelaideGround"/>, which drops about
        /// 2.4 m into its boundary lip exactly where the fence runs
        /// (<see cref="FenceInsetMetres"/> inside the ground rim). Pinning the
        /// fence to Y = 0 instead leaves the whole ~11 km ribbon hanging in the air
        /// by roughly its own height.
        /// </summary>
        public static float FenceBaseY(float worldX, float worldZ) =>
            AirsideAdelaideGround.WorldHeight(worldX, worldZ) - FenceEmbedMetres;

        /// <summary>
        /// Lowest <see cref="FenceBaseY"/> across a panel run, so a single straight
        /// panel spanning uneven ground is sunk to its lowest point rather than
        /// floating at its highest.
        /// </summary>
        public static float FenceBaseYAlongSegment(
            float startX, float startZ, float endX, float endZ, int samples = 5)
        {
            if (samples < 2)
                samples = 2;
            var min = float.MaxValue;
            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)(samples - 1);
                var x = startX + (endX - startX) * t;
                var z = startZ + (endZ - startZ) * t;
                var y = FenceBaseY(x, z);
                if (y < min)
                    min = y;
            }

            return min;
        }

        public static float PerimeterLengthMetres =>
            2f * (2f * FenceHalfX + 2f * FenceHalfZ);

        public static bool IsInsideFence(float worldX, float worldZ) =>
            Math.Abs(worldX) <= FenceHalfX && Math.Abs(worldZ) <= FenceHalfZ;

        /// <summary>
        /// True when <paramref name="station"/> lies inside a vehicle-gate gap on
        /// the given side (with a small margin for posts).
        /// </summary>
        public static bool IsInGateGap(string side, float station)
        {
            var half = VehicleGateWidthMetres * 0.5f + PostSizeMetres;
            for (var i = 0; i < VehicleGates.Length; i++)
            {
                var g = VehicleGates[i];
                if (!string.Equals(g.Side, side, StringComparison.Ordinal))
                    continue;
                if (Math.Abs(station - g.StationAlongSide) <= half)
                    return true;
            }

            return false;
        }
    }
}
