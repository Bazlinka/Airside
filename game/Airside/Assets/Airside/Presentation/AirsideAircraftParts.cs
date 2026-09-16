using System;

namespace Airside.Presentation
{
    public enum AircraftNavigationLight
    {
        None,
        Left,
        Right,
        Tail
    }

    /// <summary>
    /// Pure name contract for the aircraft landing-gear presentation passes. The
    /// authored kit ships every part as a flat node with its mesh baked at
    /// aircraft-space position, so the tyre/wheel/rim meshes that
    /// <see cref="AirsidePrototype"/> spins on the ground roll must first have their
    /// pivots rebaked to the axle centre — exactly like the propeller hubs.
    ///
    /// This helper is the single source of truth for "which parts spin", shared by
    /// the rebake pass and the roll pass so the set that is rebaked can never drift
    /// from the set that is rotated. That drift is what broke the wheels before: the
    /// rebake was lost while the roll pass survived, and every tyre swung in an arc
    /// around the fuselage centreline instead of turning on its axle.
    ///
    /// No UnityEngine types on purpose: the headless harness checks the same
    /// contract the editor uses.
    /// </summary>
    public static class AirsideAircraftParts
    {
        /// <summary>
        /// Classifies both authored names and their friendly runtime names. The white tail
        /// navigation lamp used to fall outside the "NavLight" prefix check and never lit.
        /// </summary>
        public static AircraftNavigationLight NavigationLightFor(string partName)
        {
            if (string.IsNullOrEmpty(partName))
                return AircraftNavigationLight.None;
            if (partName.IndexOf("tail_nav_light", StringComparison.OrdinalIgnoreCase) >= 0
                || partName.IndexOf("tail nav light", StringComparison.OrdinalIgnoreCase) >= 0
                || partName.IndexOf("navlight tail", StringComparison.OrdinalIgnoreCase) >= 0)
                return AircraftNavigationLight.Tail;
            if (partName.IndexOf("nav_light_left", StringComparison.OrdinalIgnoreCase) >= 0
                || partName.IndexOf("navlight l", StringComparison.OrdinalIgnoreCase) >= 0)
                return AircraftNavigationLight.Left;
            if (partName.IndexOf("nav_light_right", StringComparison.OrdinalIgnoreCase) >= 0
                || partName.IndexOf("navlight r", StringComparison.OrdinalIgnoreCase) >= 0)
                return AircraftNavigationLight.Right;
            return AircraftNavigationLight.None;
        }

        /// <summary>
        /// True for the rubber tyre, wheel and rim meshes that roll on the ground.
        /// Struts, oleos, scissors, fairings and doors are carried by the leg rather
        /// than spun, so they must return false, and car-style "wheel arch" / "hub"
        /// caps never roll.
        ///
        /// Matching is case-insensitive so both the authored kit names
        /// (<c>tire_left_forward</c>) and the presentation names they are renamed to
        /// (<c>Tire L forward</c>) satisfy the same contract.
        /// </summary>
        public static bool RollsInPlace(string partName)
        {
            if (string.IsNullOrEmpty(partName))
                return false;

            if (partName.IndexOf("arch", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            if (partName.IndexOf("hub", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            if (partName.IndexOf("fairing", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            return StartsWith(partName, "tire")
                   || StartsWith(partName, "rim")
                   || partName.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool StartsWith(string value, string prefix) =>
            value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
