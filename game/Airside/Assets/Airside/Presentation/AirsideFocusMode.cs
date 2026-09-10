namespace Airside.Presentation
{
    /// <summary>
    /// Visible world contract: one aircraft, the runway, the ground, and sun lighting.
    /// Everything else is presentation-only and stays unspawned while
    /// <see cref="AirsideBareField.Enabled"/> is true.
    ///
    /// The simulation still runs turnaround tasks, ground traffic and every
    /// reservation exactly as before, and the HUD still reports them, so nothing
    /// here changes outcomes or save data.
    /// </summary>
    public static class AirsideFocusMode
    {
        public const bool AircraftOnly = true;

        public static bool BareWorld => AirsideBareField.Enabled;

        /// <summary>Fuel truck, baggage train, passenger bus, ARFF truck, landside cars.</summary>
        public static bool ShowGroundVehicles => !AircraftOnly && !BareWorld;

        /// <summary>Stairs, chocks, GPU cart, pushback tug.</summary>
        public static bool ShowStandEquipment => !AircraftOnly && !BareWorld;

        /// <summary>Marshallers, ground crew, passengers, landside walkers.</summary>
        public static bool ShowPeople => !AircraftOnly && !BareWorld;

        /// <summary>GT-201 / GT-202 circuit aircraft — hidden on the bare field.</summary>
        public static bool ShowGroundTrafficAircraft => !AircraftOnly && !BareWorld;

        /// <summary>Terminal, hangar, ops shed, ARFF, fuel farm.</summary>
        public static bool ShowBuildings => !BareWorld;

        /// <summary>Coast, hills, trees, fence, roads, car park, clouds, birds.</summary>
        public static bool ShowEnvironment => !BareWorld;

        /// <summary>Signs, cones, GSE props, windsock, parked GA.</summary>
        public static bool ShowWorldProps => !BareWorld;

        /// <summary>Apron floods, ALS, streetlights, runway edge lamps, beacon.</summary>
        public static bool ShowDecorativeLights => !BareWorld;

        /// <summary>How many commercial models to draw; sim may still run more slots.</summary>
        public static int VisibleCommercialFlights => AircraftOnly || BareWorld ? 1 : int.MaxValue;
    }
}
