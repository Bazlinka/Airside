using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>ERSA YPAD, 3 September 2026. OSM's combined L label is conservatively treated as L1.</summary>
    public static class AdelaideTaxiPolicy
    {
        public static bool Allows(string reference, AircraftType type, StableId departureStand = default)
        {
            var code = AircraftCatalogue.TryFor(type, out var spec) ? spec.CodeLetter : 'E';
            if (reference == "R" && (spec?.WingspanMetres ?? double.MaxValue) > 18) return false;
            if (reference is "D1" or "E1" or "E2" or "H" or "F1" or "A2" && code > 'C') return false;
            if (reference == "F4" && code > 'D') return false;
            if (code > 'D' && !string.IsNullOrEmpty(reference)
                && reference is not ("A3" or "A4" or "A5" or "A6" or "B1" or "B2" or "F2" or "F3" or "F5" or "F6"
                    or "T1" or "T2" or "T3" or "K" or "L" or "L1" or "L2")) return false;
            if (code <= 'C' && AdelaideGround.TryTerminalGate(departureStand, out var gate))
            {
                var number = 0;
                foreach (var c in gate.Reference)
                {
                    if (!char.IsDigit(c)) break;
                    number = number * 10 + c - '0';
                }
                if (number is >= 15 and <= 27 && reference == "B1") return false;
                if (number is >= 22 and <= 27 && reference is "L" or "L1") return false;
            }
            return true;
        }

        internal static string Key(AircraftType type, StableId stand) =>
            (AircraftCatalogue.TryFor(type, out var spec) ? spec.CodeLetter : 'E') + "/" + stand.Value;
    }
}
