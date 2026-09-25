using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>Plain-language route forecast and the exact base revenue used at settlement.</summary>
    public readonly struct RouteForecast
    {
        private RouteForecast(int passengers, int seats, long cost, long revenue)
        {
            ExpectedPassengers = passengers;
            Seats = seats;
            Cost = cost;
            Revenue = revenue;
        }

        public int ExpectedPassengers { get; }
        public int Seats { get; }
        public long Cost { get; }
        public long Revenue { get; }
        public long Margin => Revenue - Cost;

        public static RouteForecast For(Destination origin, Destination destination, AircraftType type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            var km = origin.DistanceKmTo(destination);
            var seats = Math.Max(1, AircraftCatalogue.TypicalSeats(type));
            var passengers = Math.Min(seats, DemandFor(destination.Code));
            var filled = passengers / (double)seats;
            var basePay = FlightEconomics.FlightPay(type, km, RouteAccess.BandOf(destination));
            var revenue = Math.Max(0, (long)Math.Round(basePay * (0.35 + 0.90 * filled)));
            return new RouteForecast(passengers, seats, FlightEconomics.DispatchCost(type, km), revenue);
        }

        /// <summary>Authored planning demand per departure; a market estimate, not fake passengers in the 3D scene.</summary>
        public static int DemandFor(string code) => code switch
        {
            "KGC" => 25, "PLO" => 55, "WYA" => 32, "MGB" => 44,
            "CED" => 22, "CPD" => 20, "MQL" => 47, "BHQ" => 39,
            "MEL" => 250, "CBR" => 130, "SYD" => 260, "HBA" => 110,
            "BNE" => 200, "OOL" => 130, "ASP" => 75, "PER" => 170,
            "CNS" => 125, "DRW" => 95, "AKL" => 190, "CHC" => 140,
            "NAN" => 90, "DPS" => 200, "SIN" => 300, "KUL" => 180,
            "HKG" => 220, "DOH" => 170, "DXB" => 200,
            _ => 60
        };
    }
}
