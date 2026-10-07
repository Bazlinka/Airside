using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>The lighting fit shared by a group of airframes; the profile table is keyed by this.</summary>
    public enum AircraftLightingFamily
    {
        Generic,
        Turboprop,
        RegionalJet,
        Narrowbody,
        Widebody,
        Helicopter
    }

    /// <summary>
    /// What each aircraft type's lamps look like and how they behave: beam widths and reach, how the
    /// landing lamps are aimed, whether the nose lamp doubles as the takeoff light, strobe pattern and
    /// whether the tail carries a strobe, beacon rate. Pure data and maths, no UnityEngine, so the
    /// headless harness checks it. The lamp nodes themselves come from each model; this decides how
    /// they are lit. Values are visual approximations of typical installations, not manufacturer data
    /// (see docs/testing/aircraft-lighting-2026-10-07/README.md).
    /// </summary>
    public sealed class AircraftLightingProfile
    {
        public AircraftLightingFamily Family { get; private set; }

        // Landing lamps (wing roots; the helicopter's nose searchlight uses the same slot).
        public float LandingSpotAngle { get; private set; } = 48f;
        public float LandingInnerAngle { get; private set; } = 22f;
        public float LandingRange { get; private set; } = 90f;
        public float LandingIntensityNight { get; private set; } = 7.5f;
        public float LandingIntensityDay { get; private set; } = 9.5f;
        /// <summary>Lamps are aimed a little below the fuselage axis so the beam lands on the runway ahead.</summary>
        public float LandingPitchDownDegrees { get; private set; }
        /// <summary>Wing-root lamps toe outwards (left lamp left, right lamp right).</summary>
        public float LandingToeOutDegrees { get; private set; }

        // Nose-gear taxi lamp.
        public float TaxiSpotAngle { get; private set; } = 55f;
        public float TaxiInnerAngle { get; private set; } = 28f;
        public float TaxiRange { get; private set; } = 18f;
        public float TaxiIntensity { get; private set; } = 2.4f;
        public float TaxiPitchDownDegrees { get; private set; }
        /// <summary>The nose-gear lamp is also the takeoff light: lit for the roll and the landing, gear down.</summary>
        public bool TaxiLightDoublesAsTakeoff { get; private set; }

        // Anti-collision strobes: wingtips, plus the tail on the jets.
        public int StrobeFlashes { get; private set; } = 2;
        public float StrobeCycleSeconds { get; private set; } = 1.2f;
        public float StrobeFlashSeconds { get; private set; } = 0.065f;
        public float StrobeSecondFlashAt { get; private set; } = 0.16f;
        public bool TailStrobe { get; private set; }
        public float StrobeRange { get; private set; } = 18f;
        public float StrobeIntensity { get; private set; } = 12f;

        // Position lamps and the red beacon.
        public float NavRange { get; private set; } = 8f;
        public float BeaconHz { get; private set; } = 1.4f;
        public float BeaconRange { get; private set; } = 10f;

        /// <summary>What an unknown or unspecified aircraft gets: the original fleet-wide lamp behaviour.</summary>
        public static AircraftLightingProfile Generic { get; } = new AircraftLightingProfile();

        private static readonly AircraftLightingProfile TurbopropProfile = new AircraftLightingProfile
        {
            Family = AircraftLightingFamily.Turboprop,
            LandingSpotAngle = 44f, LandingInnerAngle = 20f, LandingRange = 70f,
            LandingIntensityNight = 6.5f, LandingIntensityDay = 8.5f,
            LandingPitchDownDegrees = 3f, LandingToeOutDegrees = 0f,
            TaxiSpotAngle = 66f, TaxiInnerAngle = 32f, TaxiRange = 32f, TaxiIntensity = 2.4f,
            TaxiPitchDownDegrees = 4f,
            StrobeFlashes = 1, StrobeCycleSeconds = 1.0f, StrobeFlashSeconds = 0.08f,
            StrobeRange = 16f, StrobeIntensity = 11f,
            NavRange = 9f, BeaconHz = 1.1f, BeaconRange = 9f
        };

        private static readonly AircraftLightingProfile RegionalJetProfile = new AircraftLightingProfile
        {
            Family = AircraftLightingFamily.RegionalJet,
            LandingSpotAngle = 36f, LandingInnerAngle = 16f, LandingRange = 100f,
            LandingIntensityNight = 7.5f, LandingIntensityDay = 9.5f,
            LandingPitchDownDegrees = 3f, LandingToeOutDegrees = 2f,
            TaxiSpotAngle = 62f, TaxiInnerAngle = 30f, TaxiRange = 45f, TaxiIntensity = 2.7f,
            TaxiPitchDownDegrees = 4.5f, TaxiLightDoublesAsTakeoff = true,
            TailStrobe = true, StrobeRange = 20f, StrobeIntensity = 12f,
            NavRange = 11f, BeaconHz = 1.4f, BeaconRange = 10f
        };

        private static readonly AircraftLightingProfile NarrowbodyProfile = new AircraftLightingProfile
        {
            Family = AircraftLightingFamily.Narrowbody,
            LandingSpotAngle = 32f, LandingInnerAngle = 14f, LandingRange = 120f,
            LandingIntensityNight = 8f, LandingIntensityDay = 10f,
            LandingPitchDownDegrees = 3f, LandingToeOutDegrees = 2.5f,
            TaxiSpotAngle = 62f, TaxiInnerAngle = 30f, TaxiRange = 55f, TaxiIntensity = 2.8f,
            TaxiPitchDownDegrees = 4.5f, TaxiLightDoublesAsTakeoff = true,
            TailStrobe = true, StrobeRange = 22f, StrobeIntensity = 12f,
            NavRange = 13f, BeaconHz = 1.4f, BeaconRange = 11f
        };

        private static readonly AircraftLightingProfile WidebodyProfile = new AircraftLightingProfile
        {
            Family = AircraftLightingFamily.Widebody,
            LandingSpotAngle = 28f, LandingInnerAngle = 12f, LandingRange = 150f,
            LandingIntensityNight = 9f, LandingIntensityDay = 11.5f,
            LandingPitchDownDegrees = 2.5f, LandingToeOutDegrees = 3f,
            TaxiSpotAngle = 58f, TaxiInnerAngle = 28f, TaxiRange = 70f, TaxiIntensity = 3.2f,
            TaxiPitchDownDegrees = 4.5f, TaxiLightDoublesAsTakeoff = true,
            TailStrobe = true, StrobeRange = 28f, StrobeIntensity = 14f,
            NavRange = 16f, BeaconHz = 1.3f, BeaconRange = 13f
        };

        // A nose searchlight angled well down on the slot the landing lamp uses; no nose-gear lamp.
        private static readonly AircraftLightingProfile HelicopterProfile = new AircraftLightingProfile
        {
            Family = AircraftLightingFamily.Helicopter,
            LandingSpotAngle = 18f, LandingInnerAngle = 8f, LandingRange = 110f,
            LandingIntensityNight = 10f, LandingIntensityDay = 12f,
            LandingPitchDownDegrees = 25f, LandingToeOutDegrees = 0f,
            StrobeFlashes = 1, StrobeCycleSeconds = 1.0f, StrobeFlashSeconds = 0.07f,
            StrobeRange = 14f, StrobeIntensity = 10f,
            NavRange = 7f, BeaconHz = 1.6f, BeaconRange = 8f
        };

        /// <summary>The profile for a type; <see cref="Generic"/> for null or a type the table does not know.</summary>
        public static AircraftLightingProfile For(AircraftType type)
        {
            if (type == null)
                return Generic;
            return ForFamily(FamilyOf(type));
        }

        public static AircraftLightingFamily FamilyOf(AircraftType type)
        {
            if (type == null)
                return AircraftLightingFamily.Generic;
            if (type.IsRotorcraft)
                return AircraftLightingFamily.Helicopter;
            switch (type.Id)
            {
                case "ATR42":
                case "SF34":
                case "DH8D":
                    return AircraftLightingFamily.Turboprop;
                case "E190":
                case "A223":
                    return AircraftLightingFamily.RegionalJet;
                case "A320":
                case "A21N":
                case "B738":
                case "B38M":
                    return AircraftLightingFamily.Narrowbody;
                case "A359":
                case "A339":
                case "B78X":
                case "B789":
                    return AircraftLightingFamily.Widebody;
                default:
                    return AircraftLightingFamily.Generic;
            }
        }

        public static AircraftLightingProfile ForFamily(AircraftLightingFamily family)
        {
            switch (family)
            {
                case AircraftLightingFamily.Turboprop: return TurbopropProfile;
                case AircraftLightingFamily.RegionalJet: return RegionalJetProfile;
                case AircraftLightingFamily.Narrowbody: return NarrowbodyProfile;
                case AircraftLightingFamily.Widebody: return WidebodyProfile;
                case AircraftLightingFamily.Helicopter: return HelicopterProfile;
                default: return Generic;
            }
        }

        /// <summary>
        /// Distance ahead, on level ground, where the axis of a lamp <paramref name="heightMetres"/> up and
        /// pitched <paramref name="pitchDownDegrees"/> below the fuselage axis meets the ground; infinity when
        /// the lamp is not aimed down. The beam only reaches it if that is inside the lamp's range.
        /// </summary>
        public static float AimGroundHitMetres(float heightMetres, float pitchDownDegrees)
        {
            if (pitchDownDegrees <= 0f)
                return float.PositiveInfinity;
            return heightMetres / (float)System.Math.Tan(pitchDownDegrees * System.Math.PI / 180.0);
        }

        /// <summary>Where this profile's landing lamps put the middle of their beam on the ground ahead.</summary>
        public float LandingAimGroundHitMetres(float lampHeightMetres) =>
            AimGroundHitMetres(lampHeightMetres, LandingPitchDownDegrees);

        /// <summary>Where this profile's nose-gear lamp puts the middle of its beam on the ground ahead.</summary>
        public float TaxiAimGroundHitMetres(float lampHeightMetres) =>
            AimGroundHitMetres(lampHeightMetres, TaxiPitchDownDegrees);

        /// <summary>Strobe brightness 0..1 at a presentation time: one or two short flashes per cycle.</summary>
        public float StrobeLevel(float presentationSeconds)
        {
            var cycle = StrobeCycleSeconds > 0f ? StrobeCycleSeconds : 1f;
            var t = presentationSeconds - (float)System.Math.Floor(presentationSeconds / cycle) * cycle;
            if (t < StrobeFlashSeconds)
                return 1f;
            if (StrobeFlashes >= 2 && t >= StrobeSecondFlashAt && t < StrobeSecondFlashAt + StrobeFlashSeconds)
                return 1f;
            return 0f;
        }

        /// <summary>Red beacon pulse for this type's rate (the same soft shape the fleet always used).</summary>
        public float BeaconLevel(bool commandedOn, float presentationSeconds)
        {
            if (!commandedOn)
                return 0f;
            var wave = (float)System.Math.Sin(presentationSeconds * BeaconHz * System.Math.PI * 2.0);
            if (wave <= 0f)
                return 0f;
            var squared = wave * wave;
            return squared * squared;
        }

        /// <summary>
        /// Whether the nose-gear lamp is lit. Always while taxiing forward under power (dark on the
        /// tail-first push and while stopped, ADR 0126); on types whose nose lamp is also the takeoff
        /// light it stays lit through the takeoff roll and the approach and landing, gear down.
        /// </summary>
        public bool TaxiLampOn(AircraftPhase phase, bool airborne, bool enginesOn, bool movingForwardOnGround)
        {
            if (!airborne && enginesOn
                && (phase == AircraftPhase.TaxiIn || phase == AircraftPhase.TaxiOut || phase == AircraftPhase.Pushback)
                && movingForwardOnGround)
                return true;
            if (!TaxiLightDoublesAsTakeoff)
                return false;
            if (phase == AircraftPhase.Landing || phase == AircraftPhase.Approach)
                return true;
            return phase == AircraftPhase.Takeoff && !airborne;
        }
    }
}
