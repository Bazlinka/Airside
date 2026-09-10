namespace Airside.Presentation
{
    /// <summary>
    /// Tentative simplification while the aircraft loop is being fixed: hide everything
    /// on the ground that is not an aircraft, so taxi, runway and circuit behaviour can
    /// be judged without apron clutter in the way.
    ///
    /// This is presentation only. The simulation still runs turnaround tasks, ground
    /// traffic and every reservation exactly as before, and the HUD still reports them,
    /// so nothing here changes outcomes or save data. Set <see cref="AircraftOnly"/> to
    /// false to bring the apron back.
    /// </summary>
    public static class AirsideFocusMode
    {
        public const bool AircraftOnly = true;

        /// <summary>Fuel truck, baggage train, passenger bus, ARFF truck, landside cars.</summary>
        public static bool ShowGroundVehicles => !AircraftOnly;

        /// <summary>Stairs, chocks, GPU cart, pushback tug.</summary>
        public static bool ShowStandEquipment => !AircraftOnly;

        /// <summary>Marshallers, ground crew, passengers, landside walkers.</summary>
        public static bool ShowPeople => !AircraftOnly;
    }
}
