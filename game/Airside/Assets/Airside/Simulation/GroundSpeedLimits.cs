using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// Verified ground-speed bands for the fleet types. Sources are recorded in
    /// <c>docs/data/AIRCRAFT_SPECIFICATIONS.md</c> (taxi section) and ADR 0045's
    /// ground-timing note. Simulation owns these numbers; presentation only reads them.
    /// </summary>
    public readonly struct GroundSpeedLimits
    {
        public GroundSpeedLimits(float maxSpeed, float acceleration, float braking, float lateralAcceleration)
        {
            MaxSpeed = maxSpeed;
            Acceleration = acceleration;
            Braking = braking;
            LateralAcceleration = lateralAcceleration;
        }

        /// <summary>m/s on the straight.</summary>
        public float MaxSpeed { get; }

        /// <summary>m/s² when speeding up (breakaway / start of taxi).</summary>
        public float Acceleration { get; }

        /// <summary>m/s² when slowing down.</summary>
        public float Braking { get; }

        /// <summary>m/s² allowed sideways in a turn; sets the cornering speed from the radius.</summary>
        public float LateralAcceleration { get; }

        // --- Straight taxi (verified) -------------------------------------------------

        /// <summary>
        /// ATR / Dash 8 / Saab straight taxiway. ATR 42/72-500 SOP (PIA, cited AIAA Journal
        /// of Aircraft 2023) max 25 kt straight; Saab 340 operator flow 25 kt taxiway;
        /// regional-turboprop ADS-B study mean/median top speed ≈ 20 kt with a 25 kt
        /// design target (AIAA).
        /// </summary>
        public const float TurbopropStraightKnots = 25f;

        /// <summary>
        /// Boeing 737 FCTM: normal taxi ≈ 20 kt; long clear straights up to 30 kt are
        /// acceptable. YPAD parallel routes are long enough that 25 kt is the working
        /// straight band (still under the 30 kt tiller caution).
        /// </summary>
        public const float JetStraightKnots = 25f;

        /// <summary>Boeing 737 FCTM "normal taxi speed is approximately 20 knots".</summary>
        public const float JetNormalTaxiKnots = 20f;

        // --- Turns / apron / stand / push / lineup ------------------------------------

        /// <summary>
        /// Industry / Boeing FCTM turn entry: ≈ 10 kt (5–10 kt for sharp / 180°). CAST and
        /// airline SOPs use the same figure. LateralAcceleration is sized so a typical
        /// 45 m taxiway fillet settles near this speed.
        /// </summary>
        public const float TurnKnots = 10f;

        /// <summary>Saab 340 operator flow: apron 15 kt. Used on regional-bay apron lanes.</summary>
        public const float ApronTurbopropKnots = 15f;

        /// <summary>Boeing 737 FCTM: ramp / apron entry 10 kt.</summary>
        public const float ApronJetKnots = 10f;

        /// <summary>Lead-in to a stand under guidance: walking pace.</summary>
        public const float StandLeadInKnots = 5f;

        /// <summary>How far out from the stop the lead-in pace starts.</summary>
        public const float StandLeadInMetres = 45f;

        /// <summary>Apron stretch leaving or entering the bays / gates.</summary>
        public const float ApronMetres = 160f;

        /// <summary>
        /// Tug pushback. Walking-pace tow; raised from 2→3 kt so the start of the push
        /// reads as motion without becoming a taxi.
        /// </summary>
        public const float PushbackKnots = 3f;

        /// <summary>
        /// Holding-point → runway centreline. Boeing: ≤10 kt into turns; once aligned and
        /// cleared, takeoff thrust follows — this leg still ends stopped at the roll start.
        /// </summary>
        public const float LineupKnots = 10f;

        /// <summary>
        /// Sideways accel that yields <see cref="TurnKnots"/> on a 45 m fillet:
        /// v²/r = (10 kt)² / 45 m ≈ 0.59 m/s².
        /// </summary>
        public const float TurnLateralMetresPerSecondSquared = 0.59f;

        /// <summary>
        /// Breakaway / taxi acceleration. Regional-turboprop ADS-B study average peak
        /// ≈ 0.5 m/s²; ETS sizing literature uses ≈ 0.4 m/s² to 15 kt.
        /// </summary>
        public const float TaxiAcceleration = 0.55f;

        public const float TaxiBraking = 0.8f;

        /// <summary>Regional turboprops (ATR 42, Saab 340, Dash 8-400) on bay routes.</summary>
        public static GroundSpeedLimits TaxiTurboprop => new(
            CircuitProfile.Knots(TurbopropStraightKnots),
            TaxiAcceleration,
            TaxiBraking,
            TurnLateralMetresPerSecondSquared);

        /// <summary>737-class jets on terminal-gate routes.</summary>
        public static GroundSpeedLimits TaxiJet => new(
            CircuitProfile.Knots(JetStraightKnots),
            TaxiAcceleration,
            TaxiBraking,
            TurnLateralMetresPerSecondSquared);

        /// <summary>
        /// Shared runway-exit / vacate band. Landing ends at
        /// <see cref="CircuitProfile.RunwayExitKnots"/>; this lets the aircraft settle to a
        /// normal taxi pace once clear of the strip.
        /// </summary>
        public static GroundSpeedLimits Vacate => new(
            CircuitProfile.Knots(JetNormalTaxiKnots),
            TaxiAcceleration,
            TaxiBraking,
            TurnLateralMetresPerSecondSquared);

        /// <summary>Tug pushback: gentle starts and stops.</summary>
        public static GroundSpeedLimits Pushback => new(
            CircuitProfile.Knots(PushbackKnots), 0.2f, 0.3f, 0.25f);

        /// <summary>Lining up onto the centreline, stopping at the takeoff point.</summary>
        public static GroundSpeedLimits Lineup => new(
            CircuitProfile.Knots(LineupKnots), 0.45f, 0.7f, 0.4f);

        /// <summary>Legacy alias — prefer <see cref="TaxiTurboprop"/> or <see cref="TaxiJet"/>.</summary>
        public static GroundSpeedLimits Taxi => TaxiTurboprop;

        /// <summary>Pick the straight-taxi profile for a stand class (bay vs terminal gate).</summary>
        public static GroundSpeedLimits TaxiFor(StandClass standClass) =>
            standClass == StandClass.TerminalGate ? TaxiJet : TaxiTurboprop;

        /// <summary>Each type uses its own normal straight-taxi target.</summary>
        public static GroundSpeedLimits TaxiFor(AircraftType type)
        {
            var profile = AircraftPerformance.For(type);
            return new GroundSpeedLimits(
                CircuitProfile.Knots(profile.TaxiStraightKnots),
                TaxiAcceleration,
                TaxiBraking,
                TurnLateralMetresPerSecondSquared);
        }

        public static float ApronKnotsFor(StandClass standClass) =>
            standClass == StandClass.TerminalGate ? ApronJetKnots : ApronTurbopropKnots;

        public static float ApronKnotsFor(AircraftType type) => AircraftPerformance.For(type).TaxiApronKnots;

        /// <summary>Cornering speed (kt) on a fillet of the given radius at the shared turn lateral limit.</summary>
        public static float TurnKnotsAtRadiusMetres(float radiusMetres)
        {
            if (radiusMetres <= 0f)
                return 0f;
            var metresPerSecond = (float)Math.Sqrt(TurnLateralMetresPerSecondSquared * radiusMetres);
            return CircuitProfile.ToKnots(metresPerSecond);
        }
    }

    /// <summary>A stretch at one end of a path with a lower speed limit (apron, stand lead-in).</summary>
    public readonly struct GroundSpeedZone
    {
        public GroundSpeedZone(float metres, float maxSpeed)
        {
            Metres = metres;
            MaxSpeed = maxSpeed;
        }

        public float Metres { get; }
        public float MaxSpeed { get; }
        public bool IsSet => Metres > 0f;
    }
}
