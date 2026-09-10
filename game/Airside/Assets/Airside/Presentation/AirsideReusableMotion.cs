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

        // ANM-AIR-003 cabin/cargo door
        public const float DoorOpenAtStand = 1f;
        public const float DoorClosed = 0f;

        // ANM-AIR-004 nav/beacon pulse
        public const float BeaconHz = 1.4f;
        public const float NavSteady = 1f;

        // ANM-VEH wheel spin scale (presentation)
        public const float VehicleWheelRpmTaxi = 180f;
        public const float VehicleWheelRpmService = 90f;

        // ANM-AIR tire roll on ground phases (degrees/sec base before phase scale)
        public const float AircraftTireRpmTaxi = 380f;

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

        public static float PropRpmForPhase(AircraftPhase phase) => phase switch
        {
            AircraftPhase.Takeoff => PropRpmTakeoff,
            AircraftPhase.Approach or AircraftPhase.Landing => PropRpmApproach,
            AircraftPhase.TaxiIn or AircraftPhase.TaxiOut or AircraftPhase.Pushback => PropRpmTaxi,
            AircraftPhase.AtStand or AircraftPhase.Departed => 0f,
            _ => PropRpmCruise
        };

        public static bool PropellersSpinning(AircraftPhase phase) =>
            phase is not (AircraftPhase.AtStand or AircraftPhase.Departed);

        /// <summary>
        /// Gear bias 0..1. Takeoff keeps gear down through the ground roll and starts
        /// retracting just after the wheels actually leave <see cref="AirsideFlightPath"/>.
        /// </summary>
        public static float GearRetractProgress =>
            Mathf.Min(0.97f, AirsideFlightPath.RotateProgress + 0.05f);

        public static float GearBias(AircraftPhase phase, float progress01 = 1f) => phase switch
        {
            AircraftPhase.Takeoff => progress01 < GearRetractProgress ? GearDeployed : GearRetracted,
            AircraftPhase.Approach => GearDeployed,
            AircraftPhase.Landing => GearDeployed,
            AircraftPhase.Departed => GearRetracted,
            _ => GearDeployed
        };

        public static float CabinDoorBias(AircraftPhase phase) =>
            phase == AircraftPhase.AtStand ? DoorOpenAtStand : DoorClosed;
    }
}
