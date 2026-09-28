using System;
using System.Collections.Generic;

namespace Airside.Simulation
{
    /// <summary>What a ramp worker is doing, which decides where they stand.</summary>
    public enum RampRole
    {
        /// <summary>Beside the vehicle currently working the aircraft.</summary>
        Attending,

        /// <summary>At the aircraft door or hold the vehicle is serving.</summary>
        Receiving,

        /// <summary>Marshalling clear of the wingtip while a vehicle manoeuvres.</summary>
        Marshalling
    }

    /// <summary>The visible job a ramp worker performs. It selects animation and equipment only.</summary>
    public enum RampTask
    {
        MarshalArrival,
        PlaceSafetyEquipment,
        FuelPanel,
        FuelCoupling,
        CateringLoader,
        CateringDoor,
        BaggageHold,
        BaggageCart,
        BoardingSupervision,
        PushbackHeadset,
        WingWalk
    }

    /// <summary>Whole-turn activities used for arrivals, ambient AI turns and pushback.</summary>
    public enum RampActivity
    {
        None,
        Arrival,
        Fuel,
        Catering,
        Baggage,
        Boarding,
        Pushback
    }

    /// <summary>One hi-vis worker to draw, in stand-local metres.</summary>
    public readonly struct RampCrewMember
    {
        public RampCrewMember(RampRole role, float alongMetres, float acrossMetres, float facingDegrees)
            : this(role, RampTask.BoardingSupervision, alongMetres, acrossMetres, facingDegrees, 0f)
        {
        }

        public RampCrewMember(RampRole role, RampTask task, float alongMetres, float acrossMetres,
            float facingDegrees, float progress01)
        {
            Role = role;
            Task = task;
            AlongMetres = alongMetres;
            AcrossMetres = acrossMetres;
            FacingDegrees = facingDegrees;
            Progress01 = Math.Max(0f, Math.Min(1f, progress01));
        }

        public RampRole Role { get; }
        public RampTask Task { get; }
        public float Progress01 { get; }

        /// <summary>Metres ahead of the stand stop along the aircraft nose direction; negative is aft.</summary>
        public float AlongMetres { get; }

        /// <summary>Metres to the aircraft's right of the centreline; negative is left.</summary>
        public float AcrossMetres { get; }

        /// <summary>Heading in degrees clockwise from the aircraft nose.</summary>
        public float FacingDegrees { get; }
    }

    /// <summary>
    /// Places the hi-vis ramp workers around a turnaround.
    ///
    /// ADR 0114 imported two ramp characters (<c>chr_ramp_m_worker</c>,
    /// <c>chr_ramp_f_worker</c>) and never placed them, so the only people ever visible were
    /// boarding passengers — and those are drawn for stairs boarding only. On an aerobridge
    /// gate, which is where the player's jets park, the apron had nobody on it at all.
    ///
    /// Crew appear for whichever vehicle is actually working, so the apron is populated
    /// through fuelling, catering and baggage rather than only during boarding.
    ///
    /// Pure function of the prep stage; no UnityEngine types, so the headless harness checks it.
    /// </summary>
    public static class RampCrew
    {
        /// <summary>The most workers drawn for one aircraft. Two characters are available.</summary>
        public const int MaxPerAircraft = 2;

        /// <summary>
        /// Crew for the stage <paramref name="prep"/> is in, appended to <paramref name="into"/>.
        /// Idle and Ready produce none: the aircraft is not being worked.
        /// </summary>
        public static void For(DeparturePrepStatus prep, GroundServiceKind? activeVehicle,
            List<RampCrewMember> into)
        {
            if (into == null)
                return;
            into.Clear();

            switch (prep.Stage)
            {
                case DeparturePrepStage.Fuel:
                    ForActivity(RampActivity.Fuel, prep.StageProgress, into);
                    break;

                case DeparturePrepStage.Catering:
                    ForActivity(RampActivity.Catering, prep.StageProgress, into);
                    break;

                case DeparturePrepStage.Baggage:
                    ForActivity(RampActivity.Baggage, prep.StageProgress, into);
                    break;

                case DeparturePrepStage.Boarding:
                    ForActivity(RampActivity.Boarding, prep.StageProgress, into);
                    break;

                default:
                    return;
            }

            if (activeVehicle is null && into.Count > 1)
                into.RemoveAt(0);
        }

        /// <summary>
        /// Places a small, readable crew team for an operational activity. The positions are
        /// stand-local and the progress is deterministic, so presentation can sample animation
        /// directly from simulation time without autonomous NPC state.
        /// </summary>
        public static void ForActivity(RampActivity activity, double progress01, List<RampCrewMember> into)
        {
            if (into == null)
                return;
            into.Clear();
            var progress = (float)Math.Max(0.0, Math.Min(1.0, progress01));
            switch (activity)
            {
                case RampActivity.Arrival:
                    into.Add(new RampCrewMember(RampRole.Marshalling, RampTask.MarshalArrival,
                        10.0f, 7.5f, 200f, progress));
                    into.Add(new RampCrewMember(RampRole.Receiving, RampTask.PlaceSafetyEquipment,
                        3.2f, -3.4f, 175f, progress));
                    break;
                case RampActivity.Fuel:
                    into.Add(new RampCrewMember(RampRole.Attending, RampTask.FuelPanel,
                        -11.0f, 6.2f, 250f, progress));
                    into.Add(new RampCrewMember(RampRole.Receiving, RampTask.FuelCoupling,
                        -8.5f, 3.6f, 90f, progress));
                    break;
                case RampActivity.Catering:
                    into.Add(new RampCrewMember(RampRole.Attending, RampTask.CateringLoader,
                        -6.0f, -6.2f, 110f, progress));
                    into.Add(new RampCrewMember(RampRole.Receiving, RampTask.CateringDoor,
                        -3.2f, -3.4f, 270f, progress));
                    break;
                case RampActivity.Baggage:
                    into.Add(new RampCrewMember(RampRole.Receiving, RampTask.BaggageHold,
                        -14.0f, -3.8f, 270f, progress));
                    into.Add(new RampCrewMember(RampRole.Attending, RampTask.BaggageCart,
                        -16.5f, -6.8f, 300f, progress));
                    break;
                case RampActivity.Boarding:
                    into.Add(new RampCrewMember(RampRole.Marshalling, RampTask.BoardingSupervision,
                        9.0f, 7.5f, 200f, progress));
                    break;
                case RampActivity.Pushback:
                    into.Add(new RampCrewMember(RampRole.Receiving, RampTask.PushbackHeadset,
                        5.0f, 3.5f, 180f, progress));
                    into.Add(new RampCrewMember(RampRole.Marshalling, RampTask.WingWalk,
                        -8.0f, -9.0f, 210f, progress));
                    break;
            }
        }

        /// <summary>The vehicle working during a stage, or null when none is.</summary>
        public static GroundServiceKind? VehicleFor(DeparturePrepStage stage) => stage switch
        {
            DeparturePrepStage.Fuel => GroundServiceKind.Fuel,
            DeparturePrepStage.Catering => GroundServiceKind.Catering,
            DeparturePrepStage.Baggage => GroundServiceKind.Baggage,
            _ => null
        };

        /// <summary>
        /// Which character to use for a crew slot. Alternating by index keeps both imported
        /// workers in play rather than showing the same person twice beside one aircraft.
        /// </summary>
        public static bool IsFemale(int index) => index % 2 == 1;
    }
}
