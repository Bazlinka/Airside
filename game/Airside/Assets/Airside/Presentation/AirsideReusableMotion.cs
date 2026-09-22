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
        // Blur the disc at taxi RPM and above. Discrete blades at 420 RPM strobe
        // against a 60 Hz frame (wagon-wheel), which read as broken rather than idle.
        // Slow spool / shutdown still shows the blades.
        public const float PropHighRpmThreshold = 380f;
        public const float PropBlurFadeStartRpm = 180f;
        public const float PropBlurFadeEndRpm = 520f;

        // ANM-AIR-001b turbofan presentation. These are fan RPMs (not N1 data):
        // deliberately modest visual values that make the 737 intake read alive at
        // overview distance without a noisy, strobing wheel of individual blades.
        public const float JetFanRpmTakeoff = 3600f;
        public const float JetFanRpmApproach = 2800f;
        public const float JetFanRpmTaxi = 1500f;
        public const float JetFanRpmCruise = 2400f;
        // Same idea as the props: taxi spool is already fast enough that individual
        // fan blades strobe; show the intake disc whenever the engine is at taxi-or-above.
        public const float JetFanHighRpmThreshold = 1400f;
        public const float JetFanBlurFadeStartRpm = 700f;
        public const float JetFanBlurFadeEndRpm = 1800f;

        /// <summary>
        /// Peak opacity of the propeller and turbofan blur discs. The blades are switched off
        /// once the blur is established, so the disc is the whole propeller at takeoff power —
        /// at the old 0.11 it was so close to invisible that a turboprop at full power read as
        /// having no propellers at all. Still translucent: the nacelle and the far wing stay
        /// visible through it.
        /// </summary>
        public const float PropDiscPeakAlpha = 0.30f;
        public const float JetFanDiscPeakAlpha = 0.26f;

        /// <summary>True when individual blades should hide behind the translucent disc.</summary>
        public static bool PropBlurActive(float rpm) => rpm >= PropHighRpmThreshold;

        /// <summary>True when the 737 intake should show its restrained fan disc.</summary>
        public static bool JetFanBlurActive(float rpm) => rpm >= JetFanHighRpmThreshold;

        /// <summary>0..1 overlap between visible blades and the motion-blur disc.</summary>
        public static float PropBlurBlend(float rpm) => SmoothBand(rpm, PropBlurFadeStartRpm, PropBlurFadeEndRpm);

        public static float JetFanBlurBlend(float rpm) => SmoothBand(rpm, JetFanBlurFadeStartRpm, JetFanBlurFadeEndRpm);

        private static float SmoothBand(float value, float from, float to)
        {
            var t = Mathf.Clamp01((value - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

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
        public const float StrobeCycleSeconds = 1.2f;

        /// <summary>Position lamps require a powered aircraft, not merely darkness.</summary>
        public static bool NavigationLightsOn(bool enginesRunning, bool beaconOn) =>
            enginesRunning || beaconOn;

        /// <summary>White strobes operate from runway entry until runway exit.</summary>
        public static bool StrobesOn(AircraftPhase phase) => phase is
            AircraftPhase.Takeoff or AircraftPhase.Departed or AircraftPhase.Approach
            or AircraftPhase.Landing or AircraftPhase.Circuit or AircraftPhase.GoAround;

        /// <summary>Two short white flashes per cycle, shared by both wingtips.</summary>
        public static float StrobeIntensity(AircraftPhase phase, float presentationSeconds)
        {
            if (!StrobesOn(phase))
                return 0f;
            var cycle = Mathf.Repeat(presentationSeconds, StrobeCycleSeconds);
            return cycle < 0.065f || cycle is >= 0.16f and < 0.225f ? 1f : 0f;
        }

        /// <summary>Soft, brief red anti-collision pulse instead of a square on/off blink.</summary>
        public static float BeaconIntensity(bool commandedOn, float presentationSeconds)
        {
            if (!commandedOn)
                return 0f;
            var wave = Mathf.Max(0f, Mathf.Sin(presentationSeconds * BeaconHz * Mathf.PI * 2f));
            return wave * wave * wave * wave;
        }

        /// <summary>
        /// Ackermann-style nose-wheel angle from the aircraft heading change over a short
        /// path sample. Wheelbase comes from the loaded model's gear pivots.
        /// </summary>
        public static float NoseWheelSteerDegrees(float currentNoseX, float currentNoseZ,
            float futureNoseX, float futureNoseZ, float speedMetresPerSecond, float lookAheadSeconds,
            float wheelbaseMetres, bool tailFirst)
        {
            if (speedMetresPerSecond < 0.2f || lookAheadSeconds <= 0f || wheelbaseMetres <= 0f)
                return 0f;
            var cross = currentNoseZ * futureNoseX - currentNoseX * futureNoseZ;
            var dot = Mathf.Clamp(currentNoseX * futureNoseX + currentNoseZ * futureNoseZ, -1f, 1f);
            var headingRadians = Mathf.Atan2(cross, dot);
            var arcMetres = Mathf.Max(0.5f, speedMetresPerSecond * lookAheadSeconds);
            var steer = Mathf.Atan(wheelbaseMetres * headingRadians / arcMetres) * Mathf.Rad2Deg;
            return Mathf.Clamp(tailFirst ? -steer : steer, -65f, 65f);
        }

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

        public static float JetFanRpmForPhase(AircraftPhase phase) => phase switch
        {
            AircraftPhase.Takeoff or AircraftPhase.Departed => JetFanRpmTakeoff,
            AircraftPhase.Approach or AircraftPhase.Landing => JetFanRpmApproach,
            AircraftPhase.TaxiIn or AircraftPhase.TaxiOut or AircraftPhase.Pushback => JetFanRpmTaxi,
            AircraftPhase.AtStand => AirportCircuit.SkipGroundTaxi ? JetFanRpmTaxi : 0f,
            _ => JetFanRpmCruise
        };

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
                case AircraftPhase.Circuit:
                case AircraftPhase.GoAround:
                    return GearRetracted;
                default:
                    return GearDeployed;
            }
        }

        /// <summary>
        /// Brief oleo squish after touchdown, then a small static compression on the ground.
        /// Zero once airborne. Presentation-only; never feeds simulation.
        /// </summary>
        /// <summary>
        /// Gear-door open amount 0..1. Doors open while the gear is in transit and
        /// close when the gear is locked up or locked down (presentation only).
        /// </summary>
        public static float GearDoorOpenBias(AircraftPhase phase, float progress01 = 1f)
        {
            var gear = GearBias(phase, progress01);
            // Fully retracted or fully deployed → closed over the wells.
            if (gear <= 0.02f || gear >= 0.98f)
                return 0f;
            // Peak open mid-travel.
            return Mathf.Sin(gear * Mathf.PI);
        }

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
        /// <param name="drawnOnGround">
        /// True for airline fleet aircraft, which really park and taxi at Adelaide. The
        /// "skipped ground phase" rule belongs to the demo circuit only: applied to the fleet
        /// it left every parked aircraft with its landing lights (90 m shadowed spot lights)
        /// burning all day.
        /// </param>
        public static bool LandingLightsOn(AircraftPhase phase, float progress01 = 1f, bool drawnOnGround = false)
        {
            if (phase is AircraftPhase.Approach or AircraftPhase.Landing
                or AircraftPhase.Circuit or AircraftPhase.GoAround)
                return true;
            if (phase is AircraftPhase.TaxiIn or AircraftPhase.TaxiOut or AircraftPhase.Pushback or AircraftPhase.AtStand)
                return !drawnOnGround && AirportCircuit.IsSkippedGroundPhase(phase);
            if (phase == AircraftPhase.Takeoff)
                return progress01 < GearRetractProgress;
            return false;
        }

        /// <param name="drawnOnGround">True for fleet aircraft: flaps are up on the stand and after landing, set for taxi-out.</param>
        public static float FlapDegrees(AircraftPhase phase, float progress01 = 1f, bool drawnOnGround = false)
        {
            var t = Mathf.Clamp01(progress01);
            if (drawnOnGround && phase is AircraftPhase.AtStand or AircraftPhase.TaxiIn)
                return 0f;
            if (AirportCircuit.IsSkippedGroundPhase(phase) || drawnOnGround && phase is AircraftPhase.TaxiOut or AircraftPhase.Pushback)
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
                AircraftPhase.Circuit => 15f,
                AircraftPhase.GoAround => t < 0.28f ? 10f : 5f,
                _ => 0f
            };
        }

        /// <summary>
        /// Small visual wing-load cue in degrees. Flex builds only after rotation,
        /// remains present while airborne, and unloads promptly once the mains settle.
        /// This is deliberately restrained: it should make the silhouette breathe,
        /// not turn the wing into rubber.
        /// </summary>
        public static float WingFlexDegrees(AircraftPhase phase, float progress01 = 1f)
        {
            var t = Mathf.Clamp01(progress01);
            return phase switch
            {
                AircraftPhase.Takeoff => 1.45f * Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(AirsideFlightPath.RotateProgress, 1f, t)),
                AircraftPhase.Departed => 1.25f,
                AircraftPhase.Approach => Mathf.Lerp(1.1f, 0.85f, t),
                AircraftPhase.Landing => t < AirsideFlightPath.TouchdownProgress
                    ? 0.85f
                    : Mathf.Lerp(0.85f, 0f, Mathf.InverseLerp(
                        AirsideFlightPath.TouchdownProgress,
                        Mathf.Min(1f, AirsideFlightPath.TouchdownProgress + 0.14f), t)),
                AircraftPhase.Circuit => 1.1f,
                AircraftPhase.GoAround => 1.2f,
                _ => 0f
            };
        }

        public static float CabinDoorBias(AircraftPhase phase) =>
            phase == AircraftPhase.AtStand && !AirportCircuit.SkipGroundTaxi
                ? DoorOpenAtStand
                : DoorClosed;
    }
}
