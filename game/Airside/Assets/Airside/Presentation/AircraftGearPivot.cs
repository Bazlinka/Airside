using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Pitch pivots on the main-gear contact point, not on the model root. The jet kits are rooted at
    /// the nose stop, so pitching about the root swung the main tyres 2-3 m below the path on
    /// rotation and in the flare. Presentation only; never feeds simulation.
    /// </summary>
    public static class AircraftGearPivot
    {
        /// <summary>
        /// Metres to raise the root so the contact point, given in the root's local space, keeps the
        /// height it has when the aircraft is level.
        /// </summary>
        public static float LiftMetres(Quaternion rotation, Vector3 contactLocal)
        {
            var level = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f) * contactLocal;
            var pitched = rotation * contactLocal;
            return level.y - pitched.y;
        }
    }
}
