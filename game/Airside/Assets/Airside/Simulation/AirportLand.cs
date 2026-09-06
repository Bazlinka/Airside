namespace Airside.Simulation
{
    /// <summary>
    /// Land banking. Reserving the adjacent plot for a future second runway is
    /// itself the complete feature at this stage — it does not yet unlock or
    /// gate anything else. The project plan's building loop names this
    /// explicitly as a trade-off ("buying adjacent land may protect space for
    /// a second runway while delaying a nearer-term terminal improvement"), so
    /// a one-off reservation with a real cost is a self-contained slice of
    /// that system; second-runway construction itself is a future milestone
    /// once the airport is far enough along to need one.
    ///
    /// Reservation is reconstructed by replaying the
    /// <c>reserve-second-runway-land</c> command — no save-schema field.
    /// </summary>
    public sealed class AirportLand
    {
        public const long SecondRunwayLandCost = 10000;

        public bool SecondRunwayLandReserved { get; private set; }

        public bool ReserveSecondRunwayLand()
        {
            if (SecondRunwayLandReserved)
                return false;

            SecondRunwayLandReserved = true;
            return true;
        }
    }
}
