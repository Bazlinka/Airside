namespace Airside.Presentation
{
    /// <summary>
    /// The visible world is an empty Adelaide Airport: one runway, flat ground, and
    /// nothing else. Published YPAD figures, in real metres — not the 1:20 miniature.
    ///
    /// This holds no UnityEngine types so the same numbers can be checked headlessly.
    /// </summary>
    public static class AirsideBareField
    {
        /// <summary>
        /// When true the player sees only ground, runway, one aircraft and sun lighting.
        /// Buildings, cars, signs, taxiways, apron, coast, trees and decorative lights
        /// are not spawned.
        /// </summary>
        public const bool Enabled = true;

        /// <summary>Adelaide Airport Limited published site area.</summary>
        public const float AdelaideAirportHectares = 785f;

        /// <summary>YPAD runway 05/23 — typical jet runway length and width.</summary>
        public const float RunwayLengthMetres = 3100f;

        public const float RunwayWidthMetres = 45f;

        /// <summary>
        /// Ground along the runway axis. 150 m overrun each end so the 3100 m strip
        /// sits inside the published 785 ha site.
        /// </summary>
        public const float GroundLengthMetres = 3400f;

        /// <summary>
        /// Cross-runway width chosen so length × width = 785.06 ha
        /// (3 400 m × 2 309 m = 7 850 600 m²).
        /// </summary>
        public const float GroundWidthMetres = 2309f;

        public const float GroundHeightMetres = 0.8f;
        public const float GroundCenterY = -0.45f;
        public const float RunwayHeightMetres = 0.14f;
        public const float RunwayCenterY = -0.02f;

        public const string GroundObjectName = "Airport ground";
        public const string RunwayObjectName = "Runway W";

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
