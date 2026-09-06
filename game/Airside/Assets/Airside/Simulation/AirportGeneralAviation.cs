namespace Airside.Simulation
{
    /// <summary>
    /// Foundation for general-aviation traffic: charters, training flights and
    /// small aircraft that use the existing apron and economy systems rather
    /// than a dedicated GA ramp. Movements are a deterministic count per
    /// simulated day (not a random draw, so behaviour stays identical under
    /// live play and offline catch-up) and pay a landing fee settled at every
    /// midnight alongside the daily report.
    ///
    /// This slice deliberately covers the economics only. Visible light
    /// aircraft with their own taxi/stand reservation are a later slice, once
    /// GA has its own apron geometry and art (see the later-production
    /// backlog in <c>docs/art/ART_DIRECTION_AND_ASSET_SPEC.md</c>); giving GA
    /// movements a physical reservation now would need the same soak-tested
    /// deadlock-freedom guarantee the commercial/fleet corridor has, which is
    /// out of scope for this change.
    ///
    /// Movement count is reconstructed by replaying the
    /// <c>expand-ga-apron</c> command — no save-schema field.
    /// </summary>
    public sealed class AirportGeneralAviation
    {
        public const int BaselineMovementsPerDay = 3;
        public const int MovementsPerExpansion = 3;
        public const int MaximumMovementsPerDay = 6;
        public const long LandingFeePerMovement = 45;
        public const long ApronExpansionCost = 4000;

        public int MovementsPerDay { get; private set; } = BaselineMovementsPerDay;

        public long DailyIncome => MovementsPerDay * LandingFeePerMovement;

        public bool CanExpand => MovementsPerDay < MaximumMovementsPerDay;

        public bool ExpandApron()
        {
            if (!CanExpand)
                return false;

            MovementsPerDay += MovementsPerExpansion;
            return true;
        }
    }
}
