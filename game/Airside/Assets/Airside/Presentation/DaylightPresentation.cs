namespace Airside.Presentation
{
    /// <summary>
    /// Pure mapping from simulation daylight to what the field should show.
    /// Presentation only — the pin is a debug override, not a sim rule.
    /// </summary>
    public static class DaylightPresentation
    {
        /// <summary>
        /// When <paramref name="pinToNoon"/> is true, always return full daylight (1).
        /// Otherwise pass through the simulation's 0..1 daylight factor.
        /// </summary>
        public static float Resolve(bool pinToNoon, double simulationDaylight)
        {
            if (pinToNoon)
                return 1f;
            if (simulationDaylight <= 0.0)
                return 0f;
            if (simulationDaylight >= 1.0)
                return 1f;
            return (float)simulationDaylight;
        }
    }
}
