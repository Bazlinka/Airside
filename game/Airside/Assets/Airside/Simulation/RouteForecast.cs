using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>Plain-language route forecast and the exact base revenue used at settlement.</summary>
    public readonly struct RouteForecast
    {
        private RouteForecast(int passengers, int seats, long cost, long revenue,
            bool freight = false, double tonnes = 0, double capacityTonnes = 0)
        {
            ExpectedPassengers = passengers;
            Seats = seats;
            Cost = cost;
            Revenue = revenue;
            IsFreight = freight;
            FreightTonnes = tonnes;
            FreightCapacityTonnes = capacityTonnes;
        }

        public int ExpectedPassengers { get; }
        public int Seats { get; }
        public long Cost { get; }
        public long Revenue { get; }
        public long Margin => Revenue - Cost;

        /// <summary>A freighter's forecast (ADR 0194): tonnes, not seats.</summary>
        public bool IsFreight { get; }
        public double FreightTonnes { get; }
        public double FreightCapacityTonnes { get; }

        /// <summary>"25/34 seats" for a passenger flight, "2.4/3.5 t freight" for a freighter.</summary>
        public string LoadText => IsFreight
            ? $"{FreightTonnes:0.#}/{FreightCapacityTonnes:0.#} t freight"
            : $"{ExpectedPassengers}/{Seats} seats";

        /// <summary>The same forecast under a difficulty's revenue and cost dials (ADR 0123).</summary>
        public RouteForecast Under(DifficultyProfile difficulty) =>
            new(ExpectedPassengers, Seats, difficulty.ScaleCost(Cost), difficulty.ScaleRevenue(Revenue),
                IsFreight, FreightTonnes, FreightCapacityTonnes);

        /// <summary>
        /// A freighter's forecast: tonnes offered against its payload, paid on the same base as a passenger
        /// flight with a higher floor. It costs the same to dispatch, so the refund on a cancel still matches.
        /// </summary>
        public static RouteForecast ForFreight(Destination origin, Destination destination, AircraftType type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            var km = origin.DistanceKmTo(destination);
            var capacity = FreightRates.CapacityTonnes(type);
            var tonnes = Math.Min(capacity, FreightRates.DemandTonnes(destination.Code));
            var filled = tonnes / capacity;
            var basePay = FlightEconomics.FlightPay(type, km, RouteAccess.BandOf(destination));
            var revenue = Math.Max(0, (long)Math.Round(basePay * (FreightRates.BasePayFloor + FreightRates.FillGain * filled)));
            return new RouteForecast(0, 0, FlightEconomics.DispatchCost(type, km), revenue, true, tonnes, capacity);
        }

        public static RouteForecast For(Destination origin, Destination destination, AircraftType type) =>
            For(origin, destination, type, 1.0);

        /// <summary>…with demand scaled for the day's events (<see cref="DemandEvents"/>, ADR 0127).</summary>
        public static RouteForecast For(Destination origin, Destination destination, AircraftType type, double demandMultiplier)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            var km = origin.DistanceKmTo(destination);
            var seats = Math.Max(1, AircraftCatalogue.TypicalSeats(type));
            var passengers = Math.Min(seats, (int)Math.Round(DemandFor(destination.Code) * Math.Max(0.0, demandMultiplier)));
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
