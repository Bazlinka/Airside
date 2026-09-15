namespace Airside.Presentation
{
    /// <summary>
    /// The visible world is an empty Adelaide Airport at published YPAD metres —
    /// not the 1:20 miniature. Main runway 05/23 extents live here; the cross
    /// strip, taxi skeleton and shared names live on
    /// <see cref="AirsideAdelaidePavement"/>.
    ///
    /// This holds no UnityEngine types so the same numbers can be checked headlessly.
    /// </summary>
    public static class AirsideBareField
    {
        /// <summary>
        /// When true the player sees ground, YPAD pavement silhouette, one aircraft
        /// and sun lighting. Buildings, cars, signs, apron clutter, coast, trees and
        /// decorative lights are not spawned. The normal release default remains this
        /// focused circuit; <c>-airsideFullAirport</c> is an explicit visual/performance
        /// QA mode that exercises the dormant complete-airport path in a packaged player.
        /// </summary>
        public static readonly bool Enabled = !HasLaunchFlag("-airsideFullAirport");

        public static bool HasLaunchFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag))
                return false;
            var args = System.Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>Adelaide Airport Limited published site area.</summary>
        public const float AdelaideAirportHectares = 785f;

        /// <summary>YPAD runway 05/23 — typical jet runway length and width.</summary>
        public const float RunwayLengthMetres = 3100f;

        public const float RunwayWidthMetres = 45f;

        /// <summary>
        /// Ground along the runway axis: the real layout (OpenStreetMap) runs from the 05
        /// threshold at −1 550 to taxiway T1 by the terminal at +1 760, plus a margin.
        /// </summary>
        public const float GroundLengthMetres = 3900f;

        /// <summary>
        /// Ground across the runway axis: 12/30 runs from −370 to its runway 12 end at
        /// +1 190 on the terminal side, plus a margin.
        /// </summary>
        public const float GroundWidthMetres = 2800f;

        public const float GroundHeightMetres = 0.8f;
        public const float GroundCenterY = -0.45f;
        public const float RunwayHeightMetres = 0.14f;
        public const float RunwayCenterY = -0.02f;

        public const string GroundObjectName = "Airport ground";

        /// <summary>YPAD main strip. Prefer <see cref="AirsideAdelaidePavement.MainRunwayName"/>.</summary>
        public const string RunwayObjectName = AirsideAdelaidePavement.MainRunwayName;

        public const float OverviewDistance = 2400f;
        public const float OverviewPitch = 50f;
        public const float OverviewYaw = 200f;
        public const float OverviewFov = 48f;
        public const float CameraFarClip = 10000f;
        public const float MinOrbitDistance = 18f;
        public const float MaxOrbitDistance = 4500f;
        public const float OverviewPanMetresPerSecond = 220f;
        public const float DayFogDensity = 0.00016f;
        public const float NightFogDensity = 0.00024f;

        public static float GroundAreaSquareMetres => GroundLengthMetres * GroundWidthMetres;

        public static float GroundHectares => GroundAreaSquareMetres / 10000f;

        public static float RunwayHalfLength => RunwayLengthMetres * 0.5f;

        public static float RunwayHalfWidth => RunwayWidthMetres * 0.5f;

        public static bool ContainsRunway(float worldX, float worldZ) =>
            worldX >= -RunwayHalfLength && worldX <= RunwayHalfLength
            && worldZ >= -RunwayHalfWidth && worldZ <= RunwayHalfWidth;

        public static bool ContainsGround(float worldX, float worldZ) =>
            worldX >= -GroundLengthMetres * 0.5f && worldX <= GroundLengthMetres * 0.5f
            && worldZ >= -GroundWidthMetres * 0.5f && worldZ <= GroundWidthMetres * 0.5f;
    }
}
