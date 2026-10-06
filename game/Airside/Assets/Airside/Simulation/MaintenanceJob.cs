using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    public enum MaintenancePhase
    {
        Preparing, Starting, WaitingOutbound, Taxiing, ShuttingDown, Positioning,
        Repairing, WaitingReturn, Returning, Parking
    }

    /// <summary>Saved v22 maintenance job. Derived paths are not persisted; phase boundaries belong to simulation.</summary>
    [Serializable]
    public sealed class MaintenanceJob
    {
        public string OriginStand;
        public string HangarId;
        public string ReturnStand;
        public MaintenancePhase Phase;
        public long RequestedAt;
        public long PhaseStartedAt;
        public long PhaseEndsAt;
        public long RepairSeconds;
        public bool RepairCompleted;
        [NonSerialized] public string WaitReason;
        [NonSerialized] private Movement _movement;

        public bool Waiting => Phase is MaintenancePhase.WaitingOutbound or MaintenancePhase.WaitingReturn;
        public string Label => Phase switch
        {
            MaintenancePhase.Preparing => "Preparing for maintenance",
            MaintenancePhase.Starting => "Starting engines",
            MaintenancePhase.WaitingOutbound => "Waiting for taxi clearance",
            MaintenancePhase.Taxiing => "Taxiing to maintenance",
            MaintenancePhase.ShuttingDown => "Shutting down at shed apron",
            MaintenancePhase.Positioning => "Positioning inside shed",
            MaintenancePhase.Repairing => "Under maintenance",
            MaintenancePhase.WaitingReturn => "Waiting to return to stand",
            MaintenancePhase.Returning => "Returning to stand",
            _ => "Parking after maintenance"
        };

        public MaintenanceJob Copy() => new()
        {
            OriginStand = OriginStand, HangarId = HangarId, ReturnStand = ReturnStand,
            Phase = Phase, RequestedAt = RequestedAt, PhaseStartedAt = PhaseStartedAt,
            PhaseEndsAt = PhaseEndsAt, RepairSeconds = RepairSeconds, RepairCompleted = RepairCompleted
        };

        public HangarTow.Plan Hangar(AircraftType type)
        {
            foreach (var option in HangarTow.Options(type, new StableId(OriginStand)))
                if (option.HangarId == HangarId) return option;
            return null;
        }

        public sealed class Movement
        {
            public HangarTow.Plan Hangar;
            public GroundLeg Outbound, Entry, Return;
            public double PushSeconds, ExitSeconds;
        }

        public Movement Paths(AircraftType type)
        {
            if (_movement != null && (_movement.Return != null) == !string.IsNullOrEmpty(ReturnStand)) return _movement;
            var plan = Hangar(type);
            if (plan == null) throw new InvalidOperationException("Maintenance shed no longer fits this aircraft.");
            var push = AdelaideGround.TaxiOut(new StableId(OriginStand), type).FirstPart;
            var p = push.Path.SampleAt(push.Path.Seconds);
            var wheelbase = AircraftPerformance.For(type).NoseToMainGearMetres;
            var extra = Math.Max(0f, plan.Length + HangarTow.ClearanceMetres - HangarTow.ApronMetres);
            var apronX = plan.ApronX - plan.InsideNoseX * extra;
            var apronZ = plan.ApronZ - plan.InsideNoseZ * extra;
            if (!AdelaideTaxiRouter.TryRoute(p.X, p.Z, apronX - plan.InsideNoseX * 40f, apronZ - plan.InsideNoseZ * 40f,
                    type, new StableId(OriginStand), out var route, apronAccessMetres: 180f))
                throw new InvalidOperationException("No permitted taxi route to the maintenance apron.");
            // Explicit stand/shed apron connectors may be outside the taxiway graph; the graph
            // itself still excludes forbidden taxiways and has no disconnected-route fallback.
            // Approach the doorway on its centreline, leaving the complete tail outside the shed.
            var approach = new List<float>(route);
            approach.Add(apronX); approach.Add(apronZ);
            var taxi = Path(approach.ToArray(), type);
            var entry = new GroundPath(new[] { apronX, apronZ, plan.InsideX, plan.InsideZ }, HangarTow.TowLimits);
            var movement = new Movement
            {
                Hangar = plan, PushSeconds = push.Seconds,
                Outbound = new GroundLeg(push, new GroundLegPart(taxi, false,
                    Math.Max(AdelaideGround.TugDisconnectSeconds, DepartureCountdown.JetLeftStartAfterPushSeconds + EngineStartSequence.SpoolSeconds), wheelbase)),
                Entry = new GroundLeg(new GroundLegPart(entry, false, trackMetres: wheelbase))
            };
            if (!string.IsNullOrEmpty(ReturnStand))
            {
                var reverse = new GroundPath(new[] { plan.InsideX, plan.InsideZ, apronX, apronZ }, HangarTow.TowLimits);
                // Join the existing stand approach, retaining its final heading and aircraft-specific gear tracking.
                var arrival = AdelaideGround.TaxiIn(new StableId(ReturnStand), type).FirstPart.Path;
                var from = Math.Max(0f, arrival.Length - Math.Max(60f, plan.Length * 2f));
                var join = arrival.PointAtDistance(from);
                if (!AdelaideTaxiRouter.TryRoute(apronX, apronZ, join.x, join.z, type, default, out var returningRoute, apronAccessMetres: 180f))
                    throw new InvalidOperationException("No permitted return route from the maintenance apron.");
                var back = new List<float>(returningRoute);
                for (var d = from; d < arrival.Length; d += 5f)
                {
                    var point = arrival.PointAtDistance(d); back.Add(point.x); back.Add(point.z);
                }
                var end = arrival.PointAtDistance(arrival.Length); back.Add(end.x); back.Add(end.z);
                movement.ExitSeconds = reverse.Seconds;
                movement.Return = new GroundLeg(new GroundLegPart(reverse, true, trackMetres: wheelbase,
                    turnAfterMetres: Math.Max(0f, reverse.Length - Math.Max(15f, plan.Length * .5f))),
                    new GroundLegPart(Path(back.ToArray(), type), false, 120, wheelbase));
            }
            return _movement = movement;
        }

        private static GroundPath Path(float[] route, AircraftType type) => new(
            GroundPathSmoothing.FilletAndDensify(route, Math.Max(12f, AircraftPerformance.For(type).NoseToMainGearMetres * 1.4f)),
            new GroundSpeedLimits(5.5f, .4f, .6f, .7f));

        public GroundLeg ActiveLeg(AircraftType type) => Phase switch
        {
            MaintenancePhase.Taxiing => Paths(type).Outbound,
            MaintenancePhase.Positioning => Paths(type).Entry,
            MaintenancePhase.Returning => Paths(type).Return,
            _ => null
        };

        public GroundPose Pose(AircraftType type, double seconds)
        {
            var paths = Paths(type);
            var elapsed = Math.Max(0, seconds - PhaseStartedAt);
            var moving = ActiveLeg(type);
            if (moving != null) return moving.PoseAt(elapsed);
            return Phase switch
            {
                MaintenancePhase.ShuttingDown => paths.Outbound.PoseAt(paths.Outbound.Seconds),
                MaintenancePhase.Repairing or MaintenancePhase.WaitingReturn => paths.Entry.PoseAt(paths.Entry.Seconds),
                MaintenancePhase.Parking => paths.Return.PoseAt(paths.Return.Seconds),
                _ => AdelaideGround.StandPose(new StableId(OriginStand))
            };
        }

        public EngineState Engines(AircraftType type, double seconds)
        {
            var elapsed = Math.Max(0, seconds - PhaseStartedAt);
            switch (Phase)
            {
                case MaintenancePhase.Starting:
                    return EngineStartSequence.Start(type, elapsed, duringPush: false);
                case MaintenancePhase.WaitingOutbound:
                    return AirlineOperations.NeedsTerminalGate(type) ? new EngineState(0, 0, true, 0f, 0f) : EngineState.Running;
                case MaintenancePhase.Taxiing:
                    return AirlineOperations.NeedsTerminalGate(type) ? EngineStartSequence.Start(type, elapsed, duringPush: true) : EngineState.Running;
                case MaintenancePhase.ShuttingDown:
                case MaintenancePhase.Parking:
                    return EngineStartSequence.Stop(elapsed);
                case MaintenancePhase.Returning:
                    var start = elapsed - Paths(type).ExitSeconds;
                    return start < 0 ? new EngineState(0, 0, false, 0f, 0f)
                        : EngineStartSequence.Start(type, start, duringPush: true);
                default:
                    return new EngineState(0, 0, false, 0f, 0f);
            }
        }
    }
}
