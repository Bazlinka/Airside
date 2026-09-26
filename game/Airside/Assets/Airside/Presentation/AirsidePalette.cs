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

        // ---- Glass Cockpit HUD (ADR 0122) -------------------------------------------------
        // The in-game HUD's own instrument palette: graphite glass panels, avionics amber for
        // the one thing that needs the player, aqua for selection and live state. The world
        // palette above stays the art-direction palette for the airport itself.

        /// <summary>Graphite glass — every HUD card and sheet, drawn translucent.</summary>
        public const string GlassHex = "#0E1216";

        /// <summary>A raised glass layer inside a sheet: sub-cards, secondary buttons, wells.</summary>
        public const string GlassRaisedHex = "#1B222A";

        /// <summary>The 1 px inner highlight around glass.</summary>
        public const string GlassEdgeHex = "#FFFFFF";

        /// <summary>Primary instrument text.</summary>
        public const string InstrumentTextHex = "#E8EDF1";

        /// <summary>Secondary instrument text — captions, units, column headers.</summary>
        public const string InstrumentMutedHex = "#8793A0";

        /// <summary>Aqua — selection, live state, links and progress.</summary>
        public const string AquaHex = "#3FD0C9";

        /// <summary>Avionics amber — the primary action and the one current priority.</summary>
        public const string AmberHex = "#FFB547";

        /// <summary>Go green — done, on time, available.</summary>
        public const string GoGreenHex = "#4CD37A";

        /// <summary>Warning red — refused, cancelled, overdue.</summary>
        public const string WarnRedHex = "#FF5F56";

        /// <summary>Magenta — routes and flight paths, as on a nav display.</summary>
        public const string RouteMagentaHex = "#E15AA8";

        /// <summary>Text drawn on an amber or aqua fill.</summary>
        public const string OnAccentHex = "#0B0F12";

        public static string Hex(HudTone tone) => tone switch
        {
            HudTone.Muted => InstrumentMutedHex,
            HudTone.Accent => AquaHex,
            HudTone.Caution => AmberHex,
            HudTone.Positive => GoGreenHex,
            HudTone.Negative => WarnRedHex,
            HudTone.Route => RouteMagentaHex,
            _ => InstrumentTextHex
        };
    }
}
