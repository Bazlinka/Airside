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
    }
}
