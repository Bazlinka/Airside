using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>Where a flight lands and parks at an airport described only by its template.</summary>
    public readonly struct AirportArrival
    {
        public AirportArrival(AirportTemplate airport, RunwayEnd runway, GateTemplate gate, bool gateWasFree)
        {
            Airport = airport;
            Runway = runway;
            Gate = gate;
            GateWasFree = gateWasFree;
        }

        public AirportTemplate Airport { get; }
        public RunwayEnd Runway { get; }

        /// <summary>The gate or bay; null for a helicopter, which uses the apron.</summary>
        public GateTemplate Gate { get; }

        /// <summary>False when every compatible gate was taken and the least-bad one was reused.</summary>
        public bool GateWasFree { get; }

        public string Text => Gate == null ? $"{Runway} · apron" : $"{Runway} · Gate {Gate.Id}";
    }

    /// <summary>
    /// Picks a runway end and a gate for a flight at a templated airport. Deterministic: the same aircraft,
    /// wind, seed and occupied set always give the same answer, whatever the frame rate.
    /// </summary>
    public static class AirportArrivalPlanner
    {
        /// <summary>Below this wind speed the airport's calm-wind runway is used.</summary>
        public const int CalmKnots = 5;

        /// <summary>Planning margin over the type's take-off roll for a runway to be long enough to use.</summary>
        public const double RunwayMargin = 1.3;

        public const double MinimumRunwayMetres = 900.0;

        public static bool TryPlan(string iata, AircraftType type, bool international, SurfaceWind wind, string seed,
            IReadOnlyCollection<string> occupiedGateIds, out AirportArrival arrival, string airlineCode = null)
        {
            arrival = default;
            if (!AirportTemplates.TryFor(iata, out var airport) || type == null) return false;
            arrival = Plan(airport, type, international, wind, seed, occupiedGateIds, airlineCode);
            return true;
        }

        public static AirportArrival Plan(AirportTemplate airport, AircraftType type, bool international, SurfaceWind wind,
            string seed, IReadOnlyCollection<string> occupiedGateIds = null, string airlineCode = null)
        {
            if (airport == null) throw new ArgumentNullException(nameof(airport));
            if (type == null) throw new ArgumentNullException(nameof(type));
            var runway = type.IsRotorcraft ? default : ChooseRunway(airport, type, wind);
            if (type.IsRotorcraft) return new AirportArrival(airport, default, null, true);
            var gate = ChooseGate(airport, type, international, seed, occupiedGateIds, out var free, airlineCode);
            return new AirportArrival(airport, runway, gate, free);
        }

        /// <summary>The longest-enough sealed runway end facing the wind, steered by the airport's own policy.</summary>
        public static RunwayEnd ChooseRunway(AirportTemplate airport, AircraftType type, SurfaceWind wind)
        {
            var needed = Math.Max(MinimumRunwayMetres, AircraftPerformance.For(type).TakeoffRollMetres * RunwayMargin);
            var letter = AircraftCatalogue.CodeLetter(type);
            RunwayEnd best = default;
            var bestScore = double.NegativeInfinity;
            var calm = wind.Knots < CalmKnots;
            foreach (var runway in airport.Runways)
            {
                for (var high = 0; high < 2; high++)
                {
                    var end = runway.End(airport.Iata, high == 1);
                    var score = 0.0;
                    if (!runway.Sealed) score -= 10_000;
                    if (runway.LengthMetres < needed) score -= 1_000 + (needed - runway.LengthMetres);
                    if (letter > runway.SoftMaxCodeLetter) score -= 100;
                    if (calm)
                        score += end.Designator.StartsWith(airport.CalmWindRunwayEnd, StringComparison.Ordinal) ? 50 : 0;
                    else
                        score += Headwind(wind, end.HeadingDegrees);
                    score += runway.LengthMetres / 100_000.0; // tie-break: the longer runway
                    if (score > bestScore) { bestScore = score; best = end; }
                }
            }

            return best;
        }

        /// <summary>
        /// The smallest free gate that fits (so widebody gates stay open), of the right kind: international flights
        /// need an international or swing gate, domestic flights anything but an international-only one. Jets take an
        /// aerobridge when one is free. If everything suitable is taken the same rules apply ignoring occupancy.
        /// </summary>
        public static GateTemplate ChooseGate(AirportTemplate airport, AircraftType type, bool international, string seed,
            IReadOnlyCollection<string> occupiedGateIds, out bool free, string airlineCode = null)
        {
            var letter = AircraftCatalogue.CodeLetter(type);
            var terminalClass = !AircraftCatalogue.TryFor(type, out var spec) || spec.StandClass == StandClass.TerminalGate;
            for (var pass = 0; pass < 3; pass++)
            {
                var pool = new List<GateTemplate>();
                foreach (var gate in airport.AllGates)
                {
                    if (gate.MaxCodeLetter < letter) continue;
                    if (pass == 0 && occupiedGateIds != null && Contains(occupiedGateIds, gate.Id)) continue;
                    if (pass < 2 && !UseFits(gate.Use, international)) continue;
                    pool.Add(gate);
                }

                if (pool.Count == 0) continue;
                // An airline's own terminal first (Virgin at Melbourne T3), when any of its gates qualify.
                var own = pool.FindAll(gate => TerminalServes(airport, gate, airlineCode));
                if (own.Count > 0) pool = own;
                if (terminalClass && pool.Exists(gate => gate.Aerobridge))
                    pool.RemoveAll(gate => !gate.Aerobridge);
                var smallest = char.MaxValue;
                foreach (var gate in pool)
                    if (gate.MaxCodeLetter < smallest) smallest = gate.MaxCodeLetter;
                pool.RemoveAll(gate => gate.MaxCodeLetter != smallest);
                free = pass == 0 || occupiedGateIds == null;
                return pool[(int)(StableHash(seed ?? string.Empty) % (uint)pool.Count)];
            }

            // Nothing is big enough (a widebody at a small field): the largest stand there.
            GateTemplate largest = null;
            foreach (var gate in airport.AllGates)
                if (largest == null || gate.MaxCodeLetter > largest.MaxCodeLetter) largest = gate;
            free = false;
            return largest;
        }

        private static bool TerminalServes(AirportTemplate airport, GateTemplate gate, string airlineCode)
        {
            foreach (var terminal in airport.Terminals)
                if (terminal.Id == gate.TerminalId) return terminal.Serves(airlineCode);
            return false;
        }

        private static bool UseFits(GateUse use, bool international) =>
            use == GateUse.Swing || (international ? use == GateUse.International : use == GateUse.Domestic);

        private static bool Contains(IReadOnlyCollection<string> ids, string id)
        {
            foreach (var other in ids)
                if (string.Equals(other, id, StringComparison.Ordinal)) return true;
            return false;
        }

        private static double Headwind(SurfaceWind wind, int runwayHeading)
        {
            var radians = (wind.DirectionDegrees - runwayHeading) * Math.PI / 180.0;
            return wind.Knots * Math.Cos(radians);
        }

        /// <summary>FNV-1a, because <c>string.GetHashCode</c> differs between runs.</summary>
        private static uint StableHash(string text)
        {
            var hash = 2166136261u;
            foreach (var c in text)
            {
                hash ^= c;
                hash *= 16777619u;
            }

            return hash;
        }
    }
}
