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

        /// <summary>
        /// Metres per second the tyres roll at, signed for direction of travel.
        ///
        /// <paramref name="groundLegSpeed"/> is the authored Adelaide taxi/pushback pose speed,
        /// and is null only when the aircraft is flying the demo circuit path, where
        /// <paramref name="circuitSpeed"/> applies. The circuit speed covers the takeoff roll
        /// and the landing rollout and reports zero for every other phase, so using it alone
        /// left the wheels stationary through every taxi, pushback and queue shuffle.
        ///
        /// Pushback is drawn tail-first, so its wheels have to turn the other way.
        /// </summary>
        public static float TireRollMetresPerSecond(float? groundLegSpeed, bool tailFirst, float circuitSpeed)
        {
            if (!groundLegSpeed.HasValue)
                return circuitSpeed;
            return tailFirst ? -groundLegSpeed.Value : groundLegSpeed.Value;
        }

        /// <summary>
        /// True for the skin panels the fuselage livery sheet belongs on.
        ///
        /// The filter this replaced accepted any part whose name merely contained "nose",
        /// which also matched the nose gear's own <c>Tire nose *</c>, <c>Wheel nose *</c>,
        /// <c>Rim nose *</c> and <c>Gear nose</c> parts — so every liveried aircraft rolled
        /// on livery-painted nose wheels. Anything carried by a landing-gear leg is skin,
        /// not fuselage, and keeps its own rubber and metal.
        /// </summary>
        public static bool TakesFuselageLivery(string partName)
        {
            if (string.IsNullOrEmpty(partName))
                return false;

            // Landing gear first: it owns most of the "nose" names on the airframe.
            if (RollsInPlace(partName))
                return false;
            if (partName.IndexOf("gear", StringComparison.OrdinalIgnoreCase) >= 0
                || partName.IndexOf("strut", StringComparison.OrdinalIgnoreCase) >= 0
                || partName.IndexOf("oleo", StringComparison.OrdinalIgnoreCase) >= 0
                || partName.IndexOf("scissors", StringComparison.OrdinalIgnoreCase) >= 0
                || partName.IndexOf("bay", StringComparison.OrdinalIgnoreCase) >= 0
                || partName.IndexOf("fairing", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            return partName is "Fuselage" or "FuselageMid" or "Fuselage mid"
                       or "FuselageAft" or "Fuselage aft" or "Nose"
                   || partName.IndexOf("fuselage", StringComparison.OrdinalIgnoreCase) >= 0
                   || partName.IndexOf("nose", StringComparison.OrdinalIgnoreCase) >= 0
                   || partName.IndexOf("cabin_ring", StringComparison.OrdinalIgnoreCase) >= 0
                   || partName.IndexOf("tail_cone", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool StartsWith(string value, string prefix) =>
            value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
