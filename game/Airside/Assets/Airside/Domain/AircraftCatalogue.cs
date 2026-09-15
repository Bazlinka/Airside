using System;
using System.Collections.Generic;

namespace Airside.Domain
{
    /// <summary>Which kind of stand a type may use (ADR 0047).</summary>
    public enum StandClass
    {
        RegionalBay,
        TerminalGate
    }

    /// <summary>Whether the type has its own runtime model yet.</summary>
    public enum ModelStatus
    {
        /// <summary>Drawn with its own true-scale runtime model; has a generated thumbnail.</summary>
        Genuine,

        /// <summary>No model of its own yet — flies with a stand-in and must be labelled as such.</summary>
        Placeholder
    }

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
            string runtimeModelPath, string thumbnailPath, string sourceId)
        {
            Type = new AircraftType(id, name, planningCruiseKmh, practicalRangeKm);
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

        public string StandClassLabel => StandClass == StandClass.TerminalGate ? "Terminal gate" : "Regional bay";
    }

    /// <summary>Every aircraft type the game knows, in display order.</summary>
    public static class AircraftCatalogue
    {
        public static readonly AircraftSpec Atr42 = new(
            "ATR42", "ATR 42-600", "Regional turboprop · 48 seats",
            22.67, 24.57, 7.59,
            planningCruiseKmh: 556, practicalRangeKm: 1100,
            manufacturerMaxCruiseKmh: 556, manufacturerRangeKm: 1302, manufacturerRangeBasis: "703 NM with max passengers",
            StandClass.RegionalBay, ModelStatus.Genuine,
            "Models/Aircraft/mdl_atr42_starter_v02.gltf", "UI/Aircraft/thb_air_atr42_v01.png", "SPEC-ATR42-600");

        // Rex's type. Saab publishes no range on its product page; 1,000 km stays a planning
        // assumption until a manufacturer figure is recorded.
        public static readonly AircraftSpec Saab340 = new(
            "SF34", "Saab 340B", "Regional turboprop · 34 seats",
            19.73, 21.44, 6.97,
            planningCruiseKmh: 500, practicalRangeKm: 1000,
            manufacturerMaxCruiseKmh: 524, manufacturerRangeKm: 0, manufacturerRangeBasis: "not published on the cited source",
            StandClass.RegionalBay, ModelStatus.Placeholder,
            null, null, "SPEC-SAAB-340B");

        // QantasLink's type (AIR-006). Practical range 1,500 km sits below the
        // 1,596 km full-passenger range.
        public static readonly AircraftSpec Dash8Q400 = new(
            "DH8D", "Dash 8-400", "Regional turboprop · 82 seats",
            32.83, 28.42, 8.34,
            planningCruiseKmh: 667, practicalRangeKm: 1500,
            manufacturerMaxCruiseKmh: 667, manufacturerRangeKm: 1596, manufacturerRangeBasis: "full passenger range, 102 kg per passenger",
            StandClass.RegionalBay, ModelStatus.Genuine,
            "Models/Aircraft/mdl_dash8_q400_v01.gltf", "UI/Aircraft/thb_air_dh8d_v01.png", "SPEC-DASH8-400");

        // Wattlebird Jet's type (AIR-005). Boeing lists no cruise speed on the cited page; 839 km/h
        // (about Mach 0.79) is the planning figure.
        public static readonly AircraftSpec Boeing7378 = new(
            "B38M", "Boeing 737-8", "Narrowbody jet · 160–180 seats",
            39.5, 35.9, 12.3,
            planningCruiseKmh: 839, practicalRangeKm: 5200,
            manufacturerMaxCruiseKmh: 0, manufacturerRangeKm: 6480, manufacturerRangeBasis: "up to 3,500 nmi",
            StandClass.TerminalGate, ModelStatus.Genuine,
            "Models/Aircraft/mdl_737_8_narrowbody_v01.gltf", "UI/Aircraft/thb_air_b38m_v01.png", "SPEC-BOEING-737-8");

        public static IReadOnlyList<AircraftSpec> All { get; } = new[] { Atr42, Saab340, Dash8Q400, Boeing7378 };

        public static bool TryFor(AircraftType type, out AircraftSpec found)
        {
            found = null;
            if (type == null)
                return false;
            foreach (var spec in All)
                if (spec.Id == type.Id)
                    found = spec;
            return found != null;
        }

        public static AircraftSpec For(AircraftType type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            foreach (var spec in All)
                if (spec.Id == type.Id)
                    return spec;
            throw new ArgumentException($"{type.Id} is not in the aircraft catalogue.", nameof(type));
        }
    }
}
