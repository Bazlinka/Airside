namespace Airside.Presentation
{
    /// <summary>
    /// Visible world contract: one aircraft, the runway, the ground, and sun lighting.
    /// Everything else is presentation-only and stays unspawned while
    /// <see cref="AirsideBareField.Enabled"/> is true.
    ///
    /// The simulation drives one aircraft round the circuit and nothing else;
    /// there are no objectives, economy or scoring to hide (ADR 0041).
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

        /// <summary>Terminal, hangar, ops shed, ARFF, fuel farm.</summary>
        public static bool ShowBuildings => !BareWorld;

        /// <summary>Coast, hills, trees, fence, roads, car park, clouds, birds.</summary>
        public static bool ShowEnvironment => !BareWorld;

        /// <summary>Signs, cones, GSE props, windsock, parked GA.</summary>
        public static bool ShowWorldProps => !BareWorld;

        /// <summary>Apron floods, ALS, streetlights, runway edge lamps, beacon.</summary>
        public static bool ShowDecorativeLights => !BareWorld;

        /// <summary>How many aircraft models to draw. The circuit flies exactly one.</summary>
        public static int VisibleCommercialFlights => 1;
    }
}
