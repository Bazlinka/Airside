using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// When the landing gear is selected down on approach. Pure data, no UnityEngine, so the headless
    /// harness checks it. Airline SOPs vary, so these are typical values, not a manufacturer procedure:
    /// the gear goes down at about glideslope capture, around 2,000 ft above the airfield for jets
    /// (Airbus SOP examples call "gear down" at 2,000 ft AGL) and a little lower for turboprops, and it
    /// must be down and locked before the 1,000 ft stabilised-approach gate (Flight Safety Foundation:
    /// stable by 1,000 ft in IMC, 500 ft in VMC). Real extension takes roughly 15-30 s, so the picture
    /// shows a slow lowering rather than a pop. See docs/testing/aircraft-lighting-2026-10-08/README.md.
    /// </summary>
    public static class ApproachGear
    {
        public const float FeetPerMetre = 3.28084f;

        /// <summary>Seconds the legs take to swing fully down and lock once selected.</summary>
        public const float ExtendSeconds = 22f;

        /// <summary>Height above the airfield, in metres, at or below which the gear is selected down.</summary>
        public static float DownHeightMetres(AircraftType type)
        {
            switch (AircraftLightingProfile.FamilyOf(type))
            {
                case AircraftLightingFamily.Turboprop: return 460f;   // ~1,500 ft
                case AircraftLightingFamily.Widebody: return 760f;    // ~2,500 ft
                default: return 610f;                                  // ~2,000 ft
            }
        }

        /// <summary>
        /// True when an aircraft on approach has the gear selected down. Landing and anything on the
        /// ground is always down; a go-around or circuit keeps its own rule.
        /// </summary>
        public static bool ApproachDown(AircraftType type, float heightAboveFieldMetres) =>
            heightAboveFieldMetres <= DownHeightMetres(type);
    }
}
