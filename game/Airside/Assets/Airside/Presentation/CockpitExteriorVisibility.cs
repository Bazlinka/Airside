using System;

namespace Airside.Presentation
{
    /// <summary>Which parts of the exterior aircraft kit stay drawn while the flight-deck shell replaces
    /// the fuselage. Everything a pilot really sees out of the windows must stay: the whole wing
    /// (flaps, ailerons, spoilers, winglet, nav light), the engines with their fans, intakes, pylons
    /// and exhausts, and the turboprop propellers and spinners. Names are the runtime display names
    /// (for example "Flap L"), matched case-insensitively. Independent of Unity.</summary>
    public static class CockpitExteriorVisibility
    {
        private static readonly string[] VisiblePrefixes =
        {
            "wing", "flap", "aileron", "spoiler", "slat", "static wick", "nav light",
            "engine", "nacelle", "intake", "fan", "pylon", "exhaust", "cowl", "oil cooler",
            "prop", "spinner", "hub cap",
        };

        public static bool KeepsDuringCockpit(string partName)
        {
            if (string.IsNullOrEmpty(partName)) return false;
            foreach (var prefix in VisiblePrefixes)
                if (partName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
