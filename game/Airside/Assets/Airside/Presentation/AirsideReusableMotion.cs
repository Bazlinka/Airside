using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Batch F4 ANM-AIR / ANM-VEH — reusable phase→motion rates for presentation.
    /// Simulation timing remains authoritative; these values only drive visuals
    /// (propeller RPM, gear bias, door open, light pulse). Extracted from the
    /// prior inline Batch D behaviour so clips/controllers can map to the same
    /// numbers without changing outcomes.
    /// </summary>
    public static class AirsideReusableMotion
    {
        // ANM-AIR-001 propeller
        public const float PropRpmTakeoff = 1400f;
        public const float PropRpmApproach = 1100f;
        public const float PropRpmTaxi = 420f;
        public const float PropRpmCruise = 720f;
        public const float PropHighRpmThreshold = 1000f;

        // ANM-AIR-002 gear (visual bias only)
        public const float GearDeployed = 1f;
        public const float GearRetracted = 0f;
        /// <summary>Seconds of phase progress over which gear eases after rotate.</summary>
        public const float GearTransitionProgress = 0.12f;

        // ANM-AIR-003 cabin/cargo door
        public const float DoorOpenAtStand = 1f;
        public const float DoorClosed = 0f;

        // ANM-AIR-004 nav/beacon pulse
        public const float BeaconHz = 1.4f;
        public const float NavSteady = 1f;

        // ANM-VEH wheel spin scale (presentation)
        public const float VehicleWheelRpmTaxi = 180f;
        public const float VehicleWheelRpmService = 90f;

        // ANM-AIR tire radii from mdl_atr42_starter_v01 (metres).
        public const float MainTireRadiusMetres = 0.37f;
        public const float NoseTireRadiusMetres = 0.31f;
        /// <summary>Half-track of the main gear used for touchdown puff spacing.</summary>
        public const float MainGearHalfTrackMetres = 2.05f;

        // Shared presentation pulse rates (Hz) — beacon family + ALS/REIL
        public const float ServicePulseHz = 2.5f;
        public const float HeatPulseHz = 1.1f;
        public const float ArffLightbarHz = 1.05f;
        public const float AlsChaseHz = 3.1f;
        public const float ReilFlashHz = 1.9f;

        // Environmental life motion (presentation only)
        public const float WindsockSwayHz = 0.38f;
        public const float WindsockRippleHz = 0.54f;
        public const float FlagFlapHz = 0.67f;
        public const float FlagRippleHz = 1.13f;
        public const float ApronWalkerHz = 0.045f;
        public const float ApronIdleSwayHz = 0.09f;
        public const float ApronWaveHz = 0.64f;
        public const float ApronStrideHz = 5.5f;
        public const float BirdOrbitHz = 0.035f;
        public const float BirdFlapHz = 1.6f;
        public const float CoastBobHz = 0.85f;
        public const float CoastYawHz = 0.35f;
        public const float CoastPitchHz = 0.7f;
        public const float CoastRollHz = 0.55f;
        public const float FoamPulseHz = 1.6f;
        public const float FoamAlphaHz = 1.4f;
        public const float FloodFlickerHz = 0.33f;
        public const float StarTwinkleHz = 1.7f;
        // UI / chrome accent pulse
        public const float UiPulseHz = 0.51f;
        public const float WindowFlickerHz = 0.27f;

        // Oleo / settling (presentation metres on the motion root).
        public const float OleoStaticMetres = 0.02f;
        public const float OleoTouchdownMetres = 0.11f;
        public const float OleoSettleProgress = 0.18f;

        public static float PropRpmForPhase(AircraftPhase phase) => phase switch
        {
            AircraftPhase.Takeoff or AircraftPhase.Departed => PropRpmTakeoff,
            AircraftPhase.Approach or AircraftPhase.Landing => PropRpmApproach,
            AircraftPhase.TaxiIn or AircraftPhase.TaxiOut or AircraftPhase.Pushback => PropRpmTaxi,
            AircraftPhase.AtStand => AirportCircuit.SkipGroundTaxi ? PropRpmTaxi : 0f,
            _ => PropRpmCruise
        };

        public static bool PropellersSpinning(AircraftPhase phase) =>
            PropRpmForPhase(phase) > 0f;

        /// <summary>
        /// Gear bias 0..1. Takeoff keeps gear down through the ground roll and eases
        /// retract after the wheels leave <see cref="AirsideFlightPath"/>.
        /// </summary>
        public static float GearRetractProgress =>
            Mathf.Min(0.97f, AirsideFlightPath.RotateProgress + 0.05f);

        public static float GearBias(AircraftPhase phase, float progress01 = 1f)
        {
            var t = Mathf.Clamp01(progress01);
            switch (phase)
            {
                case AircraftPhase.Takeoff:
                {
                    var start = GearRetractProgress;
                    var end = Mathf.Min(1f, start + GearTransitionProgress);
                    if (t <= start)
                        return GearDeployed;
                    if (t >= end)
                        return GearRetracted;
                    return Mathf.Lerp(GearDeployed, GearRetracted,
                        Mathf.SmoothStep(0f, 1f, (t - start) / (end - start)));
                }
                case AircraftPhase.Approach:
                    // Ease down over the first part of final rather than popping at phase entry.
                    return Mathf.Lerp(0.15f, GearDeployed, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t / 0.22f)));
                case AircraftPhase.Landing:
                    return GearDeployed;
                case AircraftPhase.Departed:
                    return GearRetracted;
                default:
                    return GearDeployed;
            }
        }

        /// <summary>
        /// Brief oleo squish after touchdown, then a small static compression on the ground.
        /// Zero once airborne. Presentation-only; never feeds simulation.
        /// </summary>
        public static float OleoCompressionMetres(AircraftPhase phase, float progress01)
        {
            var t = Mathf.Clamp01(progress01);
            switch (phase)
            {
                case AircraftPhase.Landing:
                    if (t < AirsideFlightPath.TouchdownProgress)
                        return 0f;
                    var since = (t - AirsideFlightPath.TouchdownProgress) / OleoSettleProgress;
                    if (since < 1f)
                    {
                        // Ease in fast, ease out to static — no bounce past zero.
                        var peak = Mathf.Sin(Mathf.Clamp01(since) * Mathf.PI);
                        return Mathf.Lerp(OleoStaticMetres, OleoTouchdownMetres, peak);
                    }

                    return OleoStaticMetres;
                case AircraftPhase.Takeoff:
                    return t < AirsideFlightPath.RotateProgress ? OleoStaticMetres : 0f;
                case AircraftPhase.TaxiIn:
                case AircraftPhase.TaxiOut:
                case AircraftPhase.Pushback:
                case AircraftPhase.AtStand:
                    return OleoStaticMetres;
                default:
                    return 0f;
            }
        }

        public static float TireRadiusMetres(string tireName)
        {
            if (string.IsNullOrEmpty(tireName))
                return MainTireRadiusMetres;
            return tireName.IndexOf("nose", System.StringComparison.OrdinalIgnoreCase) >= 0
                ? NoseTireRadiusMetres
                : MainTireRadiusMetres;
        }

        /// <summary>
        /// Landing lamps follow the circuit, not night. Daylight is pinned, so these
        /// stay on through approach, landing, the skipped ground wait, and the takeoff
        /// roll, then go out once the gear comes up.
        /// </summary>
        public static bool LandingLightsOn(AircraftPhase phase, float progress01 = 1f)
        {
            if (phase is AircraftPhase.Approach or AircraftPhase.Landing)
                return true;
            if (AirportCircuit.IsSkippedGroundPhase(phase))
                return true;
            if (phase == AircraftPhase.Takeoff)
                return progress01 < GearRetractProgress;
            return false;
        }

        public static float FlapDegrees(AircraftPhase phase, float progress01 = 1f)
        {
            var t = Mathf.Clamp01(progress01);
            if (AirportCircuit.IsSkippedGroundPhase(phase))
                return 12f;
            return phase switch
            {
                AircraftPhase.Takeoff => t < AirsideFlightPath.RotateProgress
                    ? 12f
                    : Mathf.Lerp(12f, 0f, Mathf.InverseLerp(
                        AirsideFlightPath.RotateProgress, 1f, t)),
                AircraftPhase.Approach => Mathf.Lerp(8f, 22f, t),
                AircraftPhase.Landing => t < 0.6f
                    ? 22f
                    : Mathf.Lerp(22f, 0f, Mathf.InverseLerp(0.6f, 1f, t)),
                _ => 0f
            };
        }

        public static float CabinDoorBias(AircraftPhase phase) =>
            phase == AircraftPhase.AtStand && !AirportCircuit.SkipGroundTaxi
                ? DoorOpenAtStand
                : DoorClosed;
    }
}
