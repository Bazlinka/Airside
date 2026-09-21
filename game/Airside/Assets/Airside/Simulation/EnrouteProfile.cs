using System;
using Airside.Domain;

namespace Airside.Simulation
{
    public enum EnroutePhase
    {
        Climb,
        Cruise,
        Descent
    }

    /// <summary>
    /// Height, speed and ground covered on an away leg, for showing where a flight is —
    /// never for deciding when it lands. The leg's duration still comes from
    /// <c>LegTiming</c>; this shapes a realistic ATR 42 profile inside that time:
    /// climb at ~1 200 ft/min and ~65 % of cruise ground speed, cruise at a level picked
    /// from the leg length (short hops stay low, FL250 at most), descend at ~1 500 ft/min
    /// and ~80 % speed. The cruise speed is solved so the distance flown is exactly the
    /// leg, which keeps the map position honest.
    ///
    /// Starts at the height the field departure leaves at and ends at the height the
    /// approach joins at, so there is no jump between the airfield and the map.
    /// </summary>
    public readonly struct EnrouteProfile
    {
        public const double FeetPerMetre = 3.28084;
        public const double ClimbFeetPerMinute = 1200;
        public const double DescentFeetPerMinute = 1500;
        public const double ClimbSpeedFraction = 0.65;
        public const double DescentSpeedFraction = 0.8;
        public const double MinCruiseFeet = 8000;
        public const double MaxCruiseFeet = 25000;

        /// <summary>Australian transition altitude: feet at or below it, flight levels above.</summary>
        public const double TransitionAltitudeFeet = 10000;

        /// <summary>Climb plus descent may take at most this share of the leg; short hops level off lower.</summary>
        private const double MaxClimbDescentShare = 0.85;

        public EnrouteProfile(double legKm, double legSeconds)
            : this(legKm, legSeconds, AircraftType.Atr42)
        {
        }

        public EnrouteProfile(double legKm, double legSeconds, AircraftType type)
        {
            var performance = AircraftPerformance.For(type);
            LegMetres = Math.Max(0.0, legKm * 1000.0);
            LegSeconds = Math.Max(1.0, legSeconds);
            StartFeet = performance.DepartedEndHeight * FeetPerMetre;
            EndFeet = CircuitProfile.ApproachStartHeight * FeetPerMetre;

            var wanted = PlannedCruiseFeet(legKm, type);
            var climbPerSecond = performance.ClimbFeetPerMinute / 60.0;
            var descentPerSecond = performance.DescentFeetPerMinute / 60.0;
            // Highest level whose climb and descent fit in the allowed share of the leg.
            var fits = (MaxClimbDescentShare * LegSeconds + StartFeet / climbPerSecond + EndFeet / descentPerSecond)
                       / (1.0 / climbPerSecond + 1.0 / descentPerSecond);
            CruiseFeet = Math.Max(Math.Max(StartFeet, EndFeet), Math.Min(wanted, fits));

            ClimbSeconds = Math.Max(0.0, (CruiseFeet - StartFeet) / climbPerSecond);
            DescentSeconds = Math.Max(0.0, (CruiseFeet - EndFeet) / descentPerSecond);
            CruiseSeconds = Math.Max(0.0, LegSeconds - ClimbSeconds - DescentSeconds);

            var weighted = ClimbSpeedFraction * ClimbSeconds + CruiseSeconds + DescentSpeedFraction * DescentSeconds;
            CruiseMetresPerSecond = weighted > 1e-6 ? LegMetres / weighted : 0.0;
            ClimbRateFeetPerMinute = performance.ClimbFeetPerMinute;
            DescentRateFeetPerMinute = performance.DescentFeetPerMinute;
        }

        public double LegMetres { get; }
        public double LegSeconds { get; }
        public double StartFeet { get; }
        public double EndFeet { get; }
        public double CruiseFeet { get; }
        public double ClimbSeconds { get; }
        public double CruiseSeconds { get; }
        public double DescentSeconds { get; }
        public double CruiseMetresPerSecond { get; }
        public double ClimbRateFeetPerMinute { get; }
        public double DescentRateFeetPerMinute { get; }

        /// <summary>Cruise level an ATR 42 would plan for a leg: ~6 000 ft + 25 ft/km, to the nearest 1 000 ft.</summary>
        public static double PlannedCruiseFeet(double legKm)
            => PlannedCruiseFeet(legKm, AircraftType.Atr42);

        public static double PlannedCruiseFeet(double legKm, AircraftType type)
        {
            var performance = AircraftPerformance.For(type);
            // Every authored jet climbs like a jet, not just the 737 — A321neo, A350 and
            // 787 fell through to the turboprop formula and planned a widebody
            // international service at ~22,000 ft on a medium leg instead of the
            // ~33,000 ft a jet would actually plan, which showed up directly in the HUD's
            // "FLxxx" readout.
            var jet = AircraftCatalogue.TryFor(type, out var spec)
                      && spec.StandClass == StandClass.TerminalGate;
            var raw = (jet ? 8000 : 6000) + (jet ? 38 : 25) * Math.Max(0.0, legKm);
            var rounded = Math.Round(raw / 1000.0) * 1000.0;
            return Math.Max(MinCruiseFeet, Math.Min(performance.MaxCruiseFeet, rounded));
        }

        public EnroutePhase PhaseAt(double seconds)
        {
            if (seconds < ClimbSeconds)
                return EnroutePhase.Climb;
            return seconds < ClimbSeconds + CruiseSeconds ? EnroutePhase.Cruise : EnroutePhase.Descent;
        }

        public double AltitudeFeetAt(double seconds)
        {
            var t = Clamp(seconds, 0, LegSeconds);
            if (t < ClimbSeconds)
                return StartFeet + (CruiseFeet - StartFeet) * (t / ClimbSeconds);
            var descentStart = ClimbSeconds + CruiseSeconds;
            if (t < descentStart || DescentSeconds <= 0)
                return CruiseFeet;
            return CruiseFeet + (EndFeet - CruiseFeet) * Math.Min(1.0, (t - descentStart) / DescentSeconds);
        }

        public double VerticalSpeedFeetPerMinuteAt(double seconds) => PhaseAt(Clamp(seconds, 0, LegSeconds)) switch
        {
            EnroutePhase.Climb => ClimbSeconds > 0 ? ClimbRateFeetPerMinute : 0,
            EnroutePhase.Descent => DescentSeconds > 0 ? -DescentRateFeetPerMinute : 0,
            _ => 0
        };

        public double GroundSpeedKnotsAt(double seconds)
        {
            var fraction = PhaseAt(Clamp(seconds, 0, LegSeconds)) switch
            {
                EnroutePhase.Climb => ClimbSpeedFraction,
                EnroutePhase.Descent => DescentSpeedFraction,
                _ => 1.0
            };
            return CruiseMetresPerSecond * fraction / CircuitProfile.KnotsToMetresPerSecond;
        }

        /// <summary>Share of the leg's distance flown after <paramref name="seconds"/>.</summary>
        public double DistanceFractionAt(double seconds)
        {
            if (LegMetres <= 0)
                return 1.0;
            var t = Clamp(seconds, 0, LegSeconds);
            var v = CruiseMetresPerSecond;
            var climb = Math.Min(t, ClimbSeconds);
            var metres = climb * v * ClimbSpeedFraction;
            var cruise = Math.Min(Math.Max(0, t - ClimbSeconds), CruiseSeconds);
            metres += cruise * v;
            var descent = Math.Max(0, t - ClimbSeconds - CruiseSeconds);
            metres += descent * v * DescentSpeedFraction;
            return Clamp(metres / LegMetres, 0, 1);
        }

        /// <summary>"9 000 ft" at or below the transition altitude, "FL220" above it.</summary>
        public static string AltitudeText(double feet)
        {
            if (feet > TransitionAltitudeFeet)
                return $"FL{Math.Round(feet / 100.0):000}";
            var rounded = Math.Round(feet / 100.0) * 100.0;
            return $"{rounded:#,0} ft";
        }

        private static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;
    }
}
