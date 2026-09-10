namespace Airside.Simulation
{
    /// <summary>
    /// Adelaide Airport (YPAD) runway 05/23 at 1:20 miniature scale — 3100 m real maps to
    /// 155 m on the airfield. Simulation and presentation read the same numbers so taxi
    /// paths, rollout lengths and painted surfaces stay aligned.
    /// </summary>
    public static class AirportLayout
    {
        public const float ScaleDenominator = 20f;
        public const float RunwayLengthRealMetres = 3100f;
        public const float RunwayLength = RunwayLengthRealMetres / ScaleDenominator;
        public const float RunwayHalfLength = RunwayLength * 0.5f;
        public const float RunwayWidth = 6.8f;

        public const float WestThresholdX = -RunwayHalfLength;
        public const float EastThresholdX = RunwayHalfLength;

        /// <summary>High-speed vacate (~900 m real) onto parallel taxiway B.</summary>
        public const float ArrivalExitX = -58f;

        /// <summary>West-end A1 entry and departure hold-short chord origin.</summary>
        public const float DepartureEntryX = -68f;

        public const float TaxiwayAlphaZ = 12f;
        public const float TaxiwayBravoZ = 20f;
        public const float TaxiwayEastX = 12f;
        public const float ApronThroatX = 14f;
        public const float StandX = 19f;

        public const float AlphaJunctionX = DepartureEntryX + 10f;
    }

    /// <summary>Arrival and departure taxi paths share the apron but not the runway connectors.</summary>
    public readonly struct StandTaxiRoutes
    {
        public StandTaxiRoutes(TaxiRoute arrival, TaxiRoute departure)
        {
            Arrival = arrival;
            Departure = departure;
        }

        public TaxiRoute Arrival { get; }
        public TaxiRoute Departure { get; }
    }
}
