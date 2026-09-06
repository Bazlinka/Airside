namespace Airside.Simulation
{
    /// <summary>
    /// Foundation for baggage handling: a per-day capacity in scheduled
    /// flights, distinct from the terminal's hard schedule cap
    /// (<see cref="AirportTerminal"/>). Baseline matches the existing
    /// two-stand/two-desk cap (12/day), so — since the terminal already caps
    /// scheduled flights at 12/day everywhere in this codebase's current
    /// tests — it never binds under any existing behaviour.
    ///
    /// Unlike the terminal cap, which blocks accepting more routes, exceeding
    /// baggage capacity is a soft consequence: a per-flight mishandling cost,
    /// settled with the other running costs at every midnight, for whatever
    /// demand the stand/terminal caps allow through above what baggage
    /// handling can process. A buildable sortation expansion relieves it.
    /// Turnaround task timing (the existing "Unload bags"/"Load bags" steps
    /// in <see cref="TurnaroundWorkflow"/>) is untouched by this slice — full
    /// per-flight baggage volume and task-level depth is future work, once a
    /// session can re-run the turnaround/staffing tests this would otherwise
    /// risk.
    ///
    /// Handling capacity is reconstructed by replaying the
    /// <c>expand-baggage-sortation</c> command — no save-schema field.
    /// </summary>
    public sealed class AirportBaggage
    {
        public const int BaselineHandlingCapacityPerDay = 12;
        public const int CapacityPerExpansion = 6;
        public const int MaximumHandlingCapacityPerDay = 18;
        public const long MishandlingCostPerExcessFlight = 60;
        public const long SortationExpansionCost = 4500;

        public int HandlingCapacityPerDay { get; private set; } = BaselineHandlingCapacityPerDay;

        public bool CanExpand => HandlingCapacityPerDay < MaximumHandlingCapacityPerDay;

        public bool ExpandSortation()
        {
            if (!CanExpand)
                return false;

            HandlingCapacityPerDay += CapacityPerExpansion;
            return true;
        }

        /// <summary>
        /// Cost for a day scheduling <paramref name="scheduledFlightsPerDay"/>
        /// flights against this handling capacity. Zero at or under capacity.
        /// </summary>
        public long MishandlingCostFor(int scheduledFlightsPerDay)
        {
            var excess = scheduledFlightsPerDay - HandlingCapacityPerDay;
            return excess > 0 ? excess * MishandlingCostPerExcessFlight : 0;
        }
    }
}
