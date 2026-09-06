namespace Airside.Simulation
{
    /// <summary>
    /// Foundation for cargo traffic: freighter contracts and night-operations
    /// handling, abstracted as a deterministic daily contract income rather
    /// than a physical freighter aircraft with its own taxi/stand reservation.
    /// Same scoping call as <see cref="AirportGeneralAviation"/> — see decision
    /// 0024 for why a new moving aircraft type needs Unity access this session
    /// did not have, to soak-test the reservation/corridor invariants.
    ///
    /// Handling capacity is reconstructed by replaying the
    /// <c>expand-cargo-warehouse</c> command — no save-schema field.
    /// </summary>
    public sealed class AirportCargo
    {
        public const int BaselineContractsPerDay = 2;
        public const int ContractsPerExpansion = 2;
        public const int MaximumContractsPerDay = 6;
        public const long IncomePerContract = 90;
        public const long WarehouseExpansionCost = 5000;

        public int ContractsPerDay { get; private set; } = BaselineContractsPerDay;

        public long DailyIncome => ContractsPerDay * IncomePerContract;

        public bool CanExpand => ContractsPerDay < MaximumContractsPerDay;

        public bool ExpandWarehouse()
        {
            if (!CanExpand)
                return false;

            ContractsPerDay += ContractsPerExpansion;
            return true;
        }
    }
}
