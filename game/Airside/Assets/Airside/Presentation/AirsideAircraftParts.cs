using System;

namespace Airside.Presentation
{
    /// <summary>
    /// Pure name contract for the aircraft landing-gear presentation passes. The
    /// authored kit ships every part as a flat node with its mesh baked at world
    /// position, so the tyre/wheel/rim meshes that <see cref="AirsidePrototype"/>
    /// spins on the ground roll must first have their pivots rebaked to the axle
    /// centre — exactly like the propeller hubs. This helper is the single source
    /// of truth for "which parts spin", shared by the rebake pass and the roll
    /// pass so the set that is rebaked can never drift from the set that is
    /// rotated. No UnityEngine types on purpose: the headless harness checks the
    /// same contract the editor uses.
    /// </summary>
    public static class AirsideAircraftParts
    {
        /// <summary>
        /// True for the rubber tyre, wheel and rim meshes that roll on the ground.
        /// Struts, oleos, scissors and doors are carried by the leg, not spun, so
        /// they must return false. Car-style "wheel arch" / "hub" caps never roll.
        /// </summary>
        public static bool RollsInPlace(string partName)
        {
            if (string.IsNullOrEmpty(partName))
                return false;
            if (partName.StartsWith("Tire", StringComparison.Ordinal))
                return true;
            if (partName.StartsWith("Rim", StringComparison.Ordinal))
                return true;
            return partName.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0
                   && partName.IndexOf("arch", StringComparison.OrdinalIgnoreCase) < 0
                   && partName.IndexOf("hub", StringComparison.OrdinalIgnoreCase) < 0;
        }

        /// <summary>
        /// True for the three retracting legs. These are the struts the gear retract
        /// pitches and the struts whose pivot is rebaked to the top hinge, so the two
        /// passes stay in step: the legs that fold are exactly the legs that fold from
        /// the correct point. Densified "Gear scissors/oleo/door *" parts are carried
        /// by the leg, not pitched themselves, so they must return false.
        /// </summary>
        public static bool IsGearStrut(string partName) =>
            partName is "Gear nose" or "Gear L" or "Gear R";

        /// <summary>
        /// True for a cabin side-window glass pane (either side), false for its frame,
        /// the windscreen and the cockpit glass. The kit bakes these panes flat at the
        /// fuselage's widest half-width, but the skin curves inward toward the roof, so
        /// the pane tops sit proud of the body. This is the set the inset pass recesses
        /// into the skin and the set the night glow lights, kept in one place so the two
        /// cannot disagree about what counts as a cabin window.
        /// </summary>
        public static bool IsCabinWindowGlass(string partName)
        {
            if (string.IsNullOrEmpty(partName))
                return false;
            if (partName.IndexOf("frame", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            return partName.StartsWith("Cabin window", StringComparison.OrdinalIgnoreCase)
                   || partName.StartsWith("Cabin windows", StringComparison.OrdinalIgnoreCase)
                   || partName.IndexOf("cabin_window", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
