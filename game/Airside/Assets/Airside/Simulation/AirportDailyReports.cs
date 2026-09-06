using System.Collections.Generic;

namespace Airside.Simulation
{
    /// <summary>
    /// Snapshot of one simulated day, produced at midnight settlement.
    /// </summary>
    public sealed class DailyReport
    {
        public DailyReport(
            int dayNumber,
            WeatherKind closingWeather,
            int flightsCompleted,
            long turnaroundRevenue,
            long routeIncome,
            long generalAviationIncome,
            long cargoIncome,
            long delayCost,
            long operatingCost,
            long netCashChange,
            int reputationChange,
            int groundCrew)
        {
            DayNumber = dayNumber;
            ClosingWeather = closingWeather;
            FlightsCompleted = flightsCompleted;
            TurnaroundRevenue = turnaroundRevenue;
            RouteIncome = routeIncome;
            GeneralAviationIncome = generalAviationIncome;
            CargoIncome = cargoIncome;
            DelayCost = delayCost;
            OperatingCost = operatingCost;
            NetCashChange = netCashChange;
            ReputationChange = reputationChange;
            GroundCrew = groundCrew;
        }

        public int DayNumber { get; }
        public WeatherKind ClosingWeather { get; }
        public int FlightsCompleted { get; }
        public long TurnaroundRevenue { get; }
        public long RouteIncome { get; }
        public long GeneralAviationIncome { get; }
        public long CargoIncome { get; }
        public long DelayCost { get; }
        public long OperatingCost { get; }
        public long NetCashChange { get; }
        public int ReputationChange { get; }
        public int GroundCrew { get; }

        public long FlightIncome => TurnaroundRevenue + RouteIncome + GeneralAviationIncome + CargoIncome;

        public string SummaryLine
        {
            get
            {
                var sign = NetCashChange >= 0 ? "+" : string.Empty;
                return $"Day {DayNumber}: {FlightsCompleted} flight(s) · net {sign}${NetCashChange:N0} · {Weather.Describe(ClosingWeather)}";
            }
        }
    }

    /// <summary>
    /// Bounded history of daily reports. Rebuilt by replaying the timeline — not
    /// stored as its own save field.
    /// </summary>
    public sealed class AirportDailyReports
    {
        public const int MaximumReports = 7;

        private readonly List<DailyReport> _reports = new();

        public IReadOnlyList<DailyReport> All => _reports;

        public DailyReport Latest => _reports.Count == 0 ? null : _reports[_reports.Count - 1];

        public void Add(DailyReport report)
        {
            _reports.Add(report);
            while (_reports.Count > MaximumReports)
                _reports.RemoveAt(0);
        }
    }
}
