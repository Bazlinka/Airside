namespace Airside.Presentation
{
    /// <summary>
    /// The approved Airside palette as plain hex strings
    /// (docs/art/ART_DIRECTION_AND_ASSET_SPEC.md). Free of UnityEngine so the headless
    /// workspace layouts and the offline mockup renderer read exactly the colours the
    /// game paints; <see cref="AirsideTheme"/> parses these into Unity colours.
    ///
    /// Only the approved hues appear here. <see cref="CoastalBlueStrongHex"/> and
    /// <see cref="CoastalBlueDeepHex"/> are value/saturation steps of the approved
    /// Coastal Blue, not new hues — a filled primary action needs to read as raised
    /// against a Runway Ink panel.
    /// </summary>
    public static class AirsidePalette
    {
        public const string RunwayInkHex = "#17242A";
        public const string TarmacHex = "#343B40";
        public const string ConcreteHex = "#9CA3A2";
        public const string EucalyptusHex = "#4F6F60";
        public const string DryGrassHex = "#8A8A58";
        public const string SandHex = "#C8B286";
        public const string CoastalBlueHex = "#39708A";
        public const string SafetyYellowHex = "#F2C14B";
        public const string SignalRedHex = "#C95D50";
        public const string ClearGreenHex = "#5F8B68";
        public const string CloudHex = "#EEF1EC";
        public const string OpenSkyHex = "#A7C9D9";

        /// <summary>Filled primary action — the approved Coastal Blue, brighter and more saturated.</summary>
        public const string CoastalBlueStrongHex = "#2E86B0";

        /// <summary>Panel interior a shade below Runway Ink, for workspace bodies and table wells.</summary>
        public const string CoastalBlueDeepHex = "#101C24";

        public static string Hex(HudTone tone) => tone switch
        {
            HudTone.Muted => ConcreteHex,
            HudTone.Accent => CoastalBlueStrongHex,
            HudTone.Caution => SafetyYellowHex,
            HudTone.Positive => ClearGreenHex,
            HudTone.Negative => SignalRedHex,
            _ => CloudHex
        };
    }
}
