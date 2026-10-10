using System;
using System.Collections.Generic;

namespace Airside.Domain
{
    /// <summary>Which kind of stand a type may use (ADR 0047).</summary>
    public enum StandClass
    {
        RegionalBay,
        TerminalGate,
        /// <summary>A helicopter pad spot: vertical take-off and landing, no taxiway (ADR 0207).</summary>
        Helipad
    }

    /// <summary>Whether the type has its own runtime model yet.</summary>
    public enum ModelStatus
    {
        /// <summary>Drawn with its own true-scale runtime model; has a generated thumbnail.</summary>
        Genuine,

        /// <summary>No model of its own yet — flies with a stand-in and must be labelled as such.</summary>
        Placeholder
    }

    /// <summary>Published MTOW bands used by Australian wake rules, independent of wingspan.</summary>
    public enum AircraftWeightBand { Light, MediumBelow25Tonnes, Medium, Heavy, Super }

    /// <summary>
    /// One aircraft type's facts in one place (ADR 0048): identity, role, real dimensions,
    /// planning performance, stand compatibility and art status. Dimensions and manufacturer
    /// figures are cited in docs/data/AIRCRAFT_SPECIFICATIONS.md; planning cruise and practical
    /// range are gameplay planning figures kept at or below the manufacturer's.
    /// </summary>
    public sealed class AircraftSpec
    {
        internal AircraftSpec(string id, string name, string role, double lengthMetres, double wingspanMetres,
            double heightMetres, double planningCruiseKmh, double practicalRangeKm, double manufacturerMaxCruiseKmh,
            double manufacturerRangeKm, string manufacturerRangeBasis, StandClass standClass, ModelStatus modelStatus,
            string runtimeModelPath, string thumbnailPath, string sourceId, AircraftWeightBand weightBand, bool rotorcraft = false)
        {
            Type = new AircraftType(id, name, planningCruiseKmh, practicalRangeKm, rotorcraft);
            Role = role;
            LengthMetres = lengthMetres;
            WingspanMetres = wingspanMetres;
            HeightMetres = heightMetres;
            ManufacturerMaxCruiseKmh = manufacturerMaxCruiseKmh;
            ManufacturerRangeKm = manufacturerRangeKm;
            ManufacturerRangeBasis = manufacturerRangeBasis;
            StandClass = standClass;
            ModelStatus = modelStatus;
            RuntimeModelPath = runtimeModelPath;
            ThumbnailPath = thumbnailPath;
            SourceId = sourceId;
            WeightBand = weightBand;
        }

        /// <summary>The simulation type these facts describe (planning cruise and practical range).</summary>
        public AircraftType Type { get; }
        public string Id => Type.Id;
        public string Name => Type.Name;
        public string Role { get; }
        public double LengthMetres { get; }
        public double WingspanMetres { get; }
        public double HeightMetres { get; }
        public double PlanningCruiseKmh => Type.CruiseKmh;
        public double PracticalRangeKm => Type.PracticalRangeKm;

        /// <summary>Manufacturer maximum cruise speed; 0 when no manufacturer figure is recorded.</summary>
        public double ManufacturerMaxCruiseKmh { get; }

        /// <summary>Manufacturer range; 0 when no manufacturer figure is recorded.</summary>
        public double ManufacturerRangeKm { get; }
        public string ManufacturerRangeBasis { get; }
        public StandClass StandClass { get; }
        public ModelStatus ModelStatus { get; }

        /// <summary>Art-relative runtime model; null for a placeholder.</summary>
        public string RuntimeModelPath { get; }

        /// <summary>Art-relative thumbnail rendered from <see cref="RuntimeModelPath"/>; null for a placeholder.</summary>
        public string ThumbnailPath { get; }

        /// <summary>Key into docs/data/AIRCRAFT_SPECIFICATIONS.md.</summary>
        public string SourceId { get; }

        /// <summary>MTOW band; sources and threshold distinctions in ADELAIDE_GROUND_PROTOCOLS.md.</summary>
        public AircraftWeightBand WeightBand { get; }

        public string StandClassLabel => StandClass == StandClass.TerminalGate ? "Terminal gate"
            : StandClass == StandClass.Helipad ? "Helipad" : "Regional bay";

        /// <summary>ICAO aerodrome reference code letter from wingspan (Annex 14): stand sizing.</summary>
        public char CodeLetter => AircraftCatalogue.CodeLetterForSpan(WingspanMetres);
    }

    /// <summary>Every aircraft type the game knows, in display order.</summary>
    public static class AircraftCatalogue
    {
        public static readonly AircraftSpec Atr42 = new(
            "ATR42", "ATR 42-600", "Regional turboprop · 48 seats",
            22.67, 24.57, 7.59,
            planningCruiseKmh: 510, practicalRangeKm: 1100,
            manufacturerMaxCruiseKmh: 556, manufacturerRangeKm: 1302, manufacturerRangeBasis: "703 NM with max passengers",
            StandClass.RegionalBay, ModelStatus.Genuine,
            "Models/Aircraft/mdl_atr42_starter_v03.gltf", "UI/Aircraft/thb_air_atr42_v01.png", "SPEC-ATR42-600", AircraftWeightBand.MediumBelow25Tonnes);

        // Rex's type (AIR-007). Saab publishes no range on its product page; 1,000 km stays a
        // planning assumption until a manufacturer figure is recorded.
        public static readonly AircraftSpec Saab340 = new(
            "SF34", "Saab 340B", "Regional turboprop · 34 seats",
            19.73, 21.44, 6.97,
            planningCruiseKmh: 500, practicalRangeKm: 1000,
            manufacturerMaxCruiseKmh: 524, manufacturerRangeKm: 0, manufacturerRangeBasis: "not published on the cited source",
            StandClass.RegionalBay, ModelStatus.Genuine,
            "Models/Aircraft/mdl_saab_340b_v01.gltf", "UI/Aircraft/thb_air_sf34_v01.png", "SPEC-SAAB-340B", AircraftWeightBand.MediumBelow25Tonnes);

        // QantasLink's type (AIR-006). Practical range 1,500 km sits below the
        // 1,596 km full-passenger range.
        public static readonly AircraftSpec Dash8Q400 = new(
            "DH8D", "Dash 8-400", "Regional turboprop · 82 seats",
            32.83, 28.42, 8.34,
            planningCruiseKmh: 620, practicalRangeKm: 1500,
            manufacturerMaxCruiseKmh: 667, manufacturerRangeKm: 1596, manufacturerRangeBasis: "full passenger range, 102 kg per passenger",
            StandClass.RegionalBay, ModelStatus.Genuine,
            "Models/Aircraft/mdl_dash8_q400_v01.gltf", "UI/Aircraft/thb_air_dh8d_v01.png", "SPEC-DASH8-400", AircraftWeightBand.Medium);

        public static readonly AircraftSpec EmbraerE190 = new(
            "E190", "Embraer E190", "Regional jet · 98–106 seats",
            36.24, 28.72, 10.55,
            planningCruiseKmh: 829, practicalRangeKm: 3500,
            manufacturerMaxCruiseKmh: 0, manufacturerRangeKm: 0, manufacturerRangeBasis: "performance varies by E190 weight variant",
            StandClass.TerminalGate, ModelStatus.Genuine,
            "Models/Aircraft/mdl_e190_v01.gltf", "UI/Aircraft/thb_air_e190_v01.png", "SPEC-EMBRAER-E190", AircraftWeightBand.Medium);

        public static readonly AircraftSpec AirbusA220300 = new(
            "A223", "Airbus A220-300", "Regional jet · 120–160 seats",
            38.70, 35.10, 11.50,
            planningCruiseKmh: 829, practicalRangeKm: 5500,
            manufacturerMaxCruiseKmh: 871, manufacturerRangeKm: 6297, manufacturerRangeBasis: "3,400 nm Airbus family figure",
            StandClass.TerminalGate, ModelStatus.Genuine,
            "Models/Aircraft/mdl_a220_300_v01.gltf", "UI/Aircraft/thb_air_a223_v01.png", "SPEC-AIRBUS-A220-300", AircraftWeightBand.Medium);

        public static readonly AircraftSpec AirbusA320200 = new(
            "A320", "Airbus A320-200", "Narrowbody jet · 180 seats",
            37.57, 35.80, 11.76,
            planningCruiseKmh: 830, practicalRangeKm: 5000,
            manufacturerMaxCruiseKmh: 871, manufacturerRangeKm: 6200, manufacturerRangeBasis: "representative A320 family figure",
            StandClass.TerminalGate, ModelStatus.Genuine,
            "Models/Aircraft/mdl_a320_200_v02.gltf", "UI/Aircraft/thb_air_a320_v01.png", "SPEC-AIRBUS-A320-200", AircraftWeightBand.Medium);

        public static readonly AircraftSpec Boeing737800 = new(
            "B738", "Boeing 737-800", "Narrowbody jet · 160–189 seats",
            39.47, 35.80, 12.50,
            planningCruiseKmh: 839, practicalRangeKm: 4800,
            manufacturerMaxCruiseKmh: 0, manufacturerRangeKm: 5190, manufacturerRangeBasis: "up to 2,800 nmi",
            StandClass.TerminalGate, ModelStatus.Genuine,
            "Models/Aircraft/mdl_737_800_v01.gltf", "UI/Aircraft/thb_air_b738_v01.png", "SPEC-BOEING-737-800", AircraftWeightBand.Medium);

        // Virgin Australia's type (AIR-005). Boeing lists no cruise speed on the cited page; 839 km/h
        // (about Mach 0.79) is the planning figure.
        public static readonly AircraftSpec Boeing7378 = new(
            "B38M", "Boeing 737-8", "Narrowbody jet · 160–180 seats",
            39.5, 35.9, 12.3,
            planningCruiseKmh: 839, practicalRangeKm: 5200,
            manufacturerMaxCruiseKmh: 0, manufacturerRangeKm: 6480, manufacturerRangeBasis: "up to 3,500 nmi",
            StandClass.TerminalGate, ModelStatus.Genuine,
            "Models/Aircraft/mdl_737_8_narrowbody_v01.gltf", "UI/Aircraft/thb_air_b38m_v01.png", "SPEC-BOEING-737-8", AircraftWeightBand.Medium);

        // Air New Zealand's trans-Tasman type (AIR-008). Airbus publishes M0.82 as
        // maximum cruise and 7,400 km as the advertised range; the lower figures are
        // representative schedule-planning values rather than dispatch limits.
        public static readonly AircraftSpec AirbusA321Neo = new(
            "A21N", "Airbus A321neo", "International narrowbody · 180–220 seats",
            44.51, 35.80, 11.76,
            planningCruiseKmh: 833, practicalRangeKm: 6000,
            manufacturerMaxCruiseKmh: 871, manufacturerRangeKm: 7400, manufacturerRangeBasis: "up to 4,000 nm",
            StandClass.TerminalGate, ModelStatus.Genuine,
            "Models/Aircraft/mdl_a321neo_v01.gltf", "UI/Aircraft/thb_air_a21n_v01.png", "SPEC-AIRBUS-A321NEO", AircraftWeightBand.Medium);

        public static readonly AircraftSpec AirbusA350900 = new(
            "A359", "Airbus A350-900", "Long-haul widebody · 300–350 seats",
            66.80, 64.75, 17.05,
            planningCruiseKmh: 903, practicalRangeKm: 13500,
            manufacturerMaxCruiseKmh: 903, manufacturerRangeKm: 15750, manufacturerRangeBasis: "Airbus key figures",
            StandClass.TerminalGate, ModelStatus.Genuine,
            "Models/Aircraft/mdl_a350_900_v01.gltf", "UI/Aircraft/thb_air_a359_v01.png", "SPEC-AIRBUS-A350-900", AircraftWeightBand.Heavy);

        public static readonly AircraftSpec Boeing78710 = new(
            "B78X", "Boeing 787-10", "Long-haul widebody · 300–375 seats",
            68.30, 60.12, 17.02,
            planningCruiseKmh: 903, practicalRangeKm: 11200,
            manufacturerMaxCruiseKmh: 0, manufacturerRangeKm: 13890, manufacturerRangeBasis: "up to 7,500 nmi",
            StandClass.TerminalGate, ModelStatus.Genuine,
            "Models/Aircraft/mdl_787_10_v01.gltf", "UI/Aircraft/thb_air_b78x_v01.png", "SPEC-BOEING-787-10", AircraftWeightBand.Heavy);

        public static readonly AircraftSpec AirbusA330900 = new(
            "A339", "Airbus A330-900neo", "Long-haul widebody · 260–300 seats",
            63.66, 64.00, 16.79,
            planningCruiseKmh: 871, practicalRangeKm: 12000,
            manufacturerMaxCruiseKmh: 871, manufacturerRangeKm: 13334, manufacturerRangeBasis: "7,200 nm Airbus family figure",
            StandClass.TerminalGate, ModelStatus.Genuine,
            "Models/Aircraft/mdl_a330_900neo_v01.gltf", "UI/Aircraft/thb_air_a339_v01.png", "SPEC-AIRBUS-A330-900", AircraftWeightBand.Heavy);

        public static readonly AircraftSpec Boeing7879 = new(
            "B789", "Boeing 787-9", "Long-haul widebody · 250–325 seats",
            62.81, 60.12, 17.02,
            planningCruiseKmh: 903, practicalRangeKm: 13500,
            manufacturerMaxCruiseKmh: 0, manufacturerRangeKm: 15370, manufacturerRangeBasis: "up to 8,300 nmi",
            StandClass.TerminalGate, ModelStatus.Genuine,
            "Models/Aircraft/mdl_787_9_v01.gltf", "UI/Aircraft/thb_air_b789_v01.png", "SPEC-BOEING-787-9", AircraftWeightBand.Heavy);

        public static readonly AircraftSpec Boeing7478 = new(
            "B748", "Boeing 747-8 Intercontinental", "Long-haul four-engine widebody · 410–467 seats",
            76.25, 68.40, 19.40,
            planningCruiseKmh: 903, practicalRangeKm: 13500,
            manufacturerMaxCruiseKmh: 0, manufacturerRangeKm: 14320, manufacturerRangeBasis: "7,730 nmi passenger planning reference",
            StandClass.TerminalGate, ModelStatus.Genuine,
            "Models/Aircraft/mdl_747_8_v01.gltf", "UI/Aircraft/thb_air_b748_v01.png", "SPEC-BOEING-747-8", AircraftWeightBand.Heavy);

        public static readonly AircraftSpec AirbusA380800 = new(
            "A388", "Airbus A380-800", "Long-haul double-deck widebody · 500–575 seats",
            72.73, 79.75, 24.09,
            planningCruiseKmh: 903, practicalRangeKm: 14000,
            manufacturerMaxCruiseKmh: 0, manufacturerRangeKm: 15000, manufacturerRangeBasis: "8,000 nmi Airbus reference, payload dependent",
            StandClass.TerminalGate, ModelStatus.Genuine,
            "Models/Aircraft/mdl_a380_800_v01.gltf", "UI/Aircraft/thb_air_a388_v01.png", "SPEC-AIRBUS-A380-800", AircraftWeightBand.Super);

        // Bell 412EP (AIR-017): the SA Ambulance rescue helicopter at Helipad West (ADR 0186, 0207). Bell's
        // published envelope is 17.1 m long with rotors turning, 14.0 m rotor, 4.6 m high, 226 km/h maximum
        // cruise and 358 nm range; planning cruise sits below that and the practical range allows reserves.
        public static readonly AircraftSpec Bell412 = new(
            "B412", "Bell 412EP", "Rescue / utility helicopter · 13 seats",
            17.10, 14.02, 4.60,
            planningCruiseKmh: 205, practicalRangeKm: 520,
            manufacturerMaxCruiseKmh: 226, manufacturerRangeKm: 663, manufacturerRangeBasis: "358 nm standard fuel",
            StandClass.Helipad, ModelStatus.Genuine,
            "Models/Aircraft/mdl_bell_412_rescue_v01.gltf", "UI/Aircraft/thb_air_b412_v01.png", "SPEC-BELL-412EP", AircraftWeightBand.Light,
            rotorcraft: true);

        /// <summary>
        /// Helicopters, kept apart from <see cref="All"/>: the airline fleet, purchase list and every
        /// runway/taxi rule iterate that list and must not see a type that has no runway (ADR 0207).
        /// </summary>
        public static IReadOnlyList<AircraftSpec> Rotorcraft { get; } = new[] { Bell412 };

        public static IReadOnlyList<AircraftSpec> All { get; } = new[]
        {
            Atr42, Saab340, Dash8Q400, EmbraerE190, AirbusA220300,
            AirbusA320200, Boeing737800, Boeing7378, AirbusA321Neo,
            AirbusA330900, AirbusA350900, Boeing7879, Boeing78710, Boeing7478, AirbusA380800
        };

        public static bool TryFor(AircraftType type, out AircraftSpec found)
        {
            found = null;
            if (type == null)
                return false;
            // IReadOnlyList foreach boxes the array enumerator; this lookup runs in frame polling.
            for (var i = 0; i < All.Count; i++)
            {
                var spec = All[i];
                if (spec.Id != type.Id)
                    continue;
                found = spec;
                return true;
            }
            for (var i = 0; i < Rotorcraft.Count; i++)
            {
                var spec = Rotorcraft[i];
                if (spec.Id != type.Id)
                    continue;
                found = spec;
                return true;
            }
            return false;
        }

        public static bool IsWidebody(AircraftType type) =>
            TryFor(type, out var spec) && spec.WingspanMetres >= 45.0;

        /// <summary>
        /// ICAO code letter by wingspan: A &lt; 15 m, B &lt; 24 m, C &lt; 36 m, D &lt; 52 m,
        /// E &lt; 65 m, F otherwise. A 737 or A321 is C; an A330, 787 or A350 is E (ADR 0110).
        /// </summary>
        public static char CodeLetterForSpan(double wingspanMetres) =>
            wingspanMetres < 15.0 ? 'A'
            : wingspanMetres < 24.0 ? 'B'
            : wingspanMetres < 36.0 ? 'C'
            : wingspanMetres < 52.0 ? 'D'
            : wingspanMetres < 65.0 ? 'E'
            : 'F';

        /// <summary>
        /// Typical seats from the catalogue role ("· 34 seats", "· 160–189 seats": the middle of
        /// a range). Drives how many passengers walk to a stand (ADR 0114); 150 if unknown.
        /// </summary>
        public static int TypicalSeats(AircraftType type)
        {
            if (!TryFor(type, out var spec) || spec.Role == null)
                return 150;
            var marker = spec.Role.IndexOf(" seats", StringComparison.Ordinal);
            if (marker < 0)
                return 150;
            var start = marker;
            while (start > 0 && (char.IsDigit(spec.Role[start - 1]) || spec.Role[start - 1] == '–' || spec.Role[start - 1] == '-'))
                start--;
            var numbers = spec.Role.Substring(start, marker - start).Split('–', '-');
            var total = 0;
            var count = 0;
            foreach (var number in numbers)
            {
                if (int.TryParse(number, out var value))
                {
                    total += value;
                    count++;
                }
            }

            return count == 0 ? 150 : total / count;
        }

        /// <summary>The type's ICAO code letter; unknown types are treated as code C.</summary>
        public static char CodeLetter(AircraftType type) =>
            TryFor(type, out var spec) ? spec.CodeLetter : 'C';

        public static AircraftSpec For(AircraftType type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            foreach (var spec in All)
                if (spec.Id == type.Id)
                    return spec;
            foreach (var spec in Rotorcraft)
                if (spec.Id == type.Id)
                    return spec;
            throw new ArgumentException($"{type.Id} is not in the aircraft catalogue.", nameof(type));
        }
    }
}
