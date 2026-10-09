using System;
using System.Collections.Generic;
using Airside.Domain;

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
        WingWalk,
        EquipmentRunner
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
        /// <summary>The largest service team: four bag carriers and a hold attendant.</summary>
        public const int MaxPerAircraft = 5;

        /// <summary>
        /// Crew for the stage <paramref name="prep"/> is in, appended to <paramref name="into"/>.
        /// Idle and Ready produce none: the aircraft is not being worked.
        /// </summary>
        public static void For(DeparturePrepStatus prep, GroundServiceKind? activeVehicle,
            List<RampCrewMember> into) =>
            For(prep, activeVehicle, AircraftLayout.For(AircraftType.Boeing7378), into);

        /// <summary>As above, placed round <paramref name="layout"/>.</summary>
        public static void For(DeparturePrepStatus prep, GroundServiceKind? activeVehicle, AircraftLayout layout,
            List<RampCrewMember> into)
        {
            if (into == null)
                return;
            into.Clear();

            switch (prep.Stage)
            {
                case DeparturePrepStage.Fuel:
                    ForActivity(RampActivity.Fuel, prep.StageProgress, layout, into);
                    break;

                case DeparturePrepStage.Catering:
                    ForActivity(RampActivity.Catering, prep.StageProgress, layout, into);
                    break;

                case DeparturePrepStage.Baggage:
                    ForActivity(RampActivity.Baggage, prep.StageProgress, layout, into);
                    break;

                case DeparturePrepStage.Boarding:
                    ForActivity(RampActivity.Boarding, prep.StageProgress, layout, into);
                    break;

                default:
                    return;
            }

            if (activeVehicle is null && VehicleFor(prep.Stage).HasValue && into.Count > 1)
                into.RemoveAt(0);
        }

        /// <summary>
        /// Places a small, readable crew team for an operational activity round a 737-sized
        /// aircraft. Presentation uses the overload that takes the aircraft's own layout.
        /// </summary>
        public static void ForActivity(RampActivity activity, double progress01, List<RampCrewMember> into) =>
            ForActivity(activity, progress01, AircraftLayout.For(AircraftType.Boeing7378), into);

        /// <summary>
        /// Places a small, readable crew team for an operational activity, from where the parts
        /// they work on actually are on this type (<see cref="AircraftLayout"/>): fuel at the
        /// right-wing coupling, bags at the hold door, catering at the service door — or by hand
        /// at the passenger door of a turboprop, which has no hi-loader — and the marshaller
        /// ahead of the nose. Every position is kept out of the fuselage, the nacelles and the
        /// propeller arcs. Deterministic, so presentation samples it straight from sim time.
        /// </summary>
        public static void ForActivity(RampActivity activity, double progress01, AircraftLayout layout,
            List<RampCrewMember> into)
        {
            if (into == null)
                return;
            into.Clear();
            layout ??= AircraftLayout.For(AircraftType.Boeing7378);
            var progress = (float)Math.Max(0.0, Math.Min(1.0, progress01));
            var nose = layout.NoseZ;
            var width = layout.HalfWidth;
            switch (activity)
            {
                case RampActivity.Arrival:
                    // Marshaller on the centreline ahead of the nose, facing the aircraft; the
                    // wing-side worker waits at the nose gear with the chocks and cones.
                    Add(RampRole.Marshalling, RampTask.MarshalArrival, 0f, nose + (layout.IsTurboprop ? 8f : 12f), 0f, nose);
                    Add(RampRole.Receiving, RampTask.PlaceSafetyEquipment, -(width + 1.2f), nose - 2.2f, 0f, nose - 2.2f);
                    Add(RampRole.Receiving, RampTask.PlaceSafetyEquipment, width + 1.2f, nose - 2.2f, 0f, nose - 2.2f);
                    break;
                case RampActivity.Fuel:
                {
                    var truck = layout.FuelTruck;
                    var coupling = layout.FuelCoupling;
                    Add(RampRole.Attending, RampTask.FuelPanel, truck.X - 1.8f, truck.Z + 1.2f, truck.X, truck.Z);
                    Add(RampRole.Receiving, RampTask.FuelCoupling, coupling.X, coupling.Z, coupling.X - 1f, coupling.Z + 0.5f);
                    Add(RampRole.Attending, RampTask.EquipmentRunner, truck.X + 2.6f, truck.Z - 2f, truck.X, truck.Z);
                    break;
                }
                case RampActivity.Catering:
                    if (layout.CateringTruck is { } hiLoader && layout.CateringDoor is { } service)
                    {
                        var side = AircraftLayout.SideOf(service);
                        Add(RampRole.Attending, RampTask.CateringLoader, hiLoader.X + side * 2.2f, hiLoader.Z - 2.6f,
                            hiLoader.X, hiLoader.Z);
                        Add(RampRole.Receiving, RampTask.CateringDoor, service.X + side * 1.6f, service.Z + 2.0f,
                            service.X, service.Z);
                    }
                    else
                    {
                        // A turboprop is catered by hand: a trolley pushed to the airstair.
                        var door = layout.PassengerDoor;
                        var side = AircraftLayout.SideOf(door);
                        Add(RampRole.Attending, RampTask.CateringLoader, door.X + side * 2.6f, door.Z + 2.2f, door.X, door.Z);
                        Add(RampRole.Receiving, RampTask.CateringDoor, door.X + side * 1.5f, door.Z - 1.8f, door.X, door.Z);
                    }
                    var supply = layout.CateringTruck ?? layout.PassengerDoor;
                    Add(RampRole.Attending, RampTask.EquipmentRunner, supply.X + AircraftLayout.SideOf(supply) * 3.8f,
                        supply.Z + 3.5f, supply.X, supply.Z);
                    break;
                case RampActivity.Baggage:
                {
                    var hold = layout.CargoDoor;
                    var side = AircraftLayout.SideOf(hold);
                    Add(RampRole.Receiving, RampTask.BaggageHold, hold.X + side * 1.3f, hold.Z, hold.X, hold.Z);
                    for (var lane = 0; lane < (layout.IsTurboprop ? 2 : layout.HalfSpan > 25f ? 4 : 3); lane++)
                        Add(RampRole.Attending, RampTask.BaggageCart, hold.X + side * (6.3f + lane * 0.6f),
                            hold.Z + 0.9f + lane * 0.85f, hold.X, hold.Z);
                    break;
                }
                case RampActivity.Boarding:
                {
                    var door = layout.PassengerDoor;
                    var side = AircraftLayout.SideOf(door);
                    Add(RampRole.Marshalling, RampTask.BoardingSupervision, door.X + side * 3.4f, door.Z + 2.6f, door.X, door.Z);
                    if (layout.IsTurboprop)
                    {
                        // Roller bags are left planeside at the stairs; a handler puts them in the hold.
                        var hold = layout.CargoDoor;
                        var holdSide = AircraftLayout.SideOf(hold);
                        Add(RampRole.Receiving, RampTask.BaggageHold, hold.X + holdSide * 1.3f, hold.Z, hold.X, hold.Z);
                    }
                    Add(RampRole.Marshalling, RampTask.BoardingSupervision, door.X + side * 5.0f, door.Z + 5.2f, door.X, door.Z);
                    break;
                }
                case RampActivity.Pushback:
                    Add(RampRole.Receiving, RampTask.PushbackHeadset, -(width + 1.6f), nose - 1.5f, 0f, nose - 1.5f);
                    Add(RampRole.Marshalling, RampTask.WingWalk, -(layout.HalfSpan + 1.5f), layout.WingMidZ,
                        -(layout.HalfSpan + 1.5f), layout.WingMidZ - 10f);
                    Add(RampRole.Marshalling, RampTask.WingWalk, layout.HalfSpan + 1.5f, layout.WingMidZ,
                        layout.HalfSpan + 1.5f, layout.WingMidZ - 10f);
                    break;
            }

            void Add(RampRole role, RampTask task, float x, float z, float lookX, float lookZ)
            {
                var (cx, cz) = layout.Clear(x, z, 0.7f, forVehicles: false);
                var facing = (float)(Math.Atan2(lookX - cx, lookZ - cz) * 180.0 / Math.PI);
                into.Add(new RampCrewMember(role, task, cz, cx, facing, progress));
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
