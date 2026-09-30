using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// Freight as a second way to earn (ADR 0194). A player aircraft can be converted to a freighter: it
    /// flies with no passengers, is paid from the tonnes the route offers rather than the seats it
    /// fills, and wears a cargo livery. These numbers are planning figures, like route demand: a market
    /// estimate, not a load the simulation carries. Pure functions; nothing here touches the clock.
    /// </summary>
    public static class FreightRates
    {
        /// <summary>Revenue floor as a share of the flight's base pay: parcel contracts are guaranteed.</summary>
        public const double BasePayFloor = 0.55;

        /// <summary>What a full hold adds on top of the floor (passengers add 0.90 to a 0.35 floor).</summary>
        public const double FillGain = 0.95;

        /// <summary>Payload of a package freighter conversion of each type, in tonnes.</summary>
        public static double CapacityTonnes(AircraftType type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            var id = type.Id;
            if (id == AircraftType.Saab340.Id) return 3.5;
            if (id == AircraftType.Atr42.Id) return 5.0;
            if (id == AircraftType.Dash8Q400.Id) return 8.0;
            if (id == AircraftType.EmbraerE190.Id) return 12.0;
            if (id == AircraftType.AirbusA220300.Id) return 14.0;
            if (id == AircraftType.AirbusA320200.Id) return 20.0;
            if (id == AircraftType.Boeing737800.Id || id == AircraftType.Boeing7378.Id) return 22.0;
            if (id == AircraftType.AirbusA321Neo.Id) return 27.0;
            if (id == AircraftType.AirbusA330900.Id) return 55.0;
            if (id == AircraftType.Boeing7879.Id) return 45.0;
            if (id == AircraftType.Boeing78710.Id) return 48.0;
            if (id == AircraftType.AirbusA350900.Id) return 50.0;
            return AircraftCatalogue.IsWidebody(type) ? 45.0 : 15.0;
        }

        /// <summary>
        /// Tonnes offered per departure to a destination. Thin passenger routes can be freight-heavy:
        /// Kangaroo Island produce, Coober Pedy opal and mining stores, Alice Springs and Darwin supply runs.
        /// </summary>
        public static double DemandTonnes(string code) => code switch
        {
            "KGC" => 2.4, "PLO" => 2.0, "WYA" => 2.6, "MGB" => 3.0,
            "CED" => 1.6, "CPD" => 2.8, "MQL" => 3.2, "BHQ" => 3.0,
            "MEL" => 14.0, "CBR" => 6.0, "SYD" => 16.0, "HBA" => 5.0,
            "BNE" => 12.0, "OOL" => 4.0, "ASP" => 7.0, "PER" => 14.0,
            "CNS" => 5.0, "DRW" => 7.0, "AKL" => 9.0, "CHC" => 5.0,
            "NAN" => 3.0, "DPS" => 6.0, "SIN" => 22.0, "KUL" => 16.0,
            "HKG" => 24.0, "DOH" => 18.0, "DXB" => 20.0,
            _ => 4.0
        };

        /// <summary>
        /// What it costs to convert an aircraft to a freighter or back to passengers: a fixed refit, so a
        /// player does not flip roles for free. Turboprops are cheap; widebodies are a real investment.
        /// </summary>
        public static long ConversionCost(AircraftType type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (AircraftCatalogue.For(type).StandClass == StandClass.RegionalBay) return 400;
            if (AircraftCatalogue.IsWidebody(type)) return 9_000;
            return 2_400;
        }

        /// <summary>The share of the hold the route fills, 0..1.</summary>
        public static double Fill(AircraftType type, string destinationCode) =>
            Math.Min(1.0, DemandTonnes(destinationCode) / CapacityTonnes(type));
    }
}
