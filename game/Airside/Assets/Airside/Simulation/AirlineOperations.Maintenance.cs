using System;
using Airside.Domain;

namespace Airside.Simulation
{
    public sealed partial class AirlineOperations
    {
        private bool AdvanceMaintenance(FleetAircraft aircraft, SimulationTime now)
        {
            var job = aircraft.MaintenanceJob;
            if (job == null) return false;
            if (job.Phase == MaintenancePhase.Taxiing && !string.IsNullOrEmpty(aircraft.Stand.Value)
                && now.ElapsedSeconds >= job.PhaseStartedAt + (long)Math.Ceiling(job.Paths(aircraft.Type).PushSeconds) + 60)
            {
                aircraft.Stand = default;
                return true;
            }
            if (job.PhaseEndsAt > now.ElapsedSeconds) return false;
            var paths = job.Paths(aircraft.Type);
            void Phase(MaintenancePhase phase, long seconds)
            {
                job.Phase = phase; job.PhaseStartedAt = now.ElapsedSeconds;
                job.PhaseEndsAt = now.ElapsedSeconds + seconds; job.WaitReason = string.Empty;
            }
            switch (job.Phase)
            {
                case MaintenancePhase.Preparing:
                    Phase(MaintenancePhase.Starting, AirlineOperations.NeedsTerminalGate(aircraft.Type) ? 30 : 130);
                    break;
                case MaintenancePhase.Starting:
                    Phase(MaintenancePhase.WaitingOutbound, 0);
                    break;
                case MaintenancePhase.WaitingOutbound:
                    if (!GroundTraffic.OnGrid(now)) return false;
                    if (AdelaideGround.IsTerminalGate(aircraft.Stand) && !IsLeadInFree(aircraft.Stand, aircraft))
                    {
                        job.WaitReason = "Waiting for gate lead-in clearance";
                        return false;
                    }
                    if (!GroundTraffic.PathClear(_fleet, aircraft, paths.Outbound, aircraft.AssignedRunway, false, now,
                            true, out var blocker))
                    {
                        job.WaitReason = blocker == null ? "Waiting for taxiway traffic" : "Waiting for " + blocker.Registration;
                        return false;
                    }
                    if (CrossingIntoBusyStrip(paths.Outbound, aircraft.AssignedRunway, now).HasValue)
                    {
                        job.WaitReason = "Waiting for runway crossing clearance";
                        return false;
                    }
                    Phase(MaintenancePhase.Taxiing, paths.Outbound.WholeSeconds);
                    break;
                case MaintenancePhase.Taxiing:
                    // Origin protection has cleared; the aircraft is now stopped on the shed apron.
                    aircraft.Stand = default;
                    Phase(MaintenancePhase.ShuttingDown, (long)EngineStartSequence.BeaconOffAfterSeconds);
                    break;
                case MaintenancePhase.ShuttingDown:
                    Phase(MaintenancePhase.Positioning, paths.Entry.WholeSeconds);
                    break;
                case MaintenancePhase.Positioning:
                    Phase(MaintenancePhase.Repairing, job.RepairSeconds);
                    aircraft.CheckUntil = now.Advance(job.RepairSeconds);
                    break;
                case MaintenancePhase.Repairing:
                    if (!job.RepairCompleted)
                    {
                        aircraft.RotationsSinceCheck = 0;
                        job.RepairCompleted = true;
                    }
                    Phase(MaintenancePhase.WaitingReturn, 0);
                    break;
                case MaintenancePhase.WaitingReturn:
                    if (!GroundTraffic.OnGrid(now)) return false;
                    if (string.IsNullOrEmpty(job.ReturnStand))
                    {
                        foreach (var stand in _stands)
                        {
                            if (!StandFits(aircraft.Type, stand) || !IsStandFree(stand)
                                || !PlayerBase.CanUseStand(CareerState.BaseLevel, aircraft.Type, stand)) continue;
                            job.ReturnStand = stand.Value; aircraft.Stand = stand;
                            break;
                        }
                    }
                    if (string.IsNullOrEmpty(job.ReturnStand))
                    {
                        job.WaitReason = "Waiting for a compatible free stand";
                        return false;
                    }
                    paths = job.Paths(aircraft.Type);
                    if (!GroundTraffic.PathClear(_fleet, aircraft, paths.Return, aircraft.AssignedRunway, false, now,
                            true, out var returningBlocker))
                    {
                        job.WaitReason = returningBlocker == null ? "Waiting for return taxiway traffic" : "Waiting for " + returningBlocker.Registration;
                        return false;
                    }
                    if (CrossingIntoBusyStrip(paths.Return, aircraft.AssignedRunway, now).HasValue)
                    {
                        job.WaitReason = "Waiting for runway crossing clearance";
                        return false;
                    }
                    Phase(MaintenancePhase.Returning, paths.Return.WholeSeconds);
                    break;
                case MaintenancePhase.Returning:
                    Phase(MaintenancePhase.Parking, (long)EngineStartSequence.BeaconOffAfterSeconds);
                    break;
                case MaintenancePhase.Parking:
                    aircraft.MaintenanceJob = null; aircraft.CheckUntil = null;
                    Transition(aircraft, FleetState.AtStand, now, null);
                    return true;
            }
            return true;
        }

        internal void RestoreMaintenanceJob(string registration, MaintenanceJob job)
        {
            var aircraft = _fleet.Find(a => a.Registration == registration);
            if (aircraft == null || !aircraft.Airline.IsPlayer || aircraft.Type.IsRotorcraft
                || aircraft.State != FleetState.Maintenance || job == null
                || !Enum.IsDefined(typeof(MaintenancePhase), job.Phase)
                || job.RequestedAt < 0 || job.PhaseStartedAt < job.RequestedAt
                || job.PhaseStartedAt > _processedTo.ElapsedSeconds || job.PhaseEndsAt < job.PhaseStartedAt
                || job.RepairSeconds <= 0 || job.Hangar(aircraft.Type) == null
                || aircraft.Scheduled.HasValue
                || job.RepairCompleted != (job.Phase is MaintenancePhase.WaitingReturn or MaintenancePhase.Returning or MaintenancePhase.Parking)
                || job.Phase is MaintenancePhase.Returning or MaintenancePhase.Parking && string.IsNullOrEmpty(job.ReturnStand)
                || job.Phase is MaintenancePhase.Preparing or MaintenancePhase.Starting or MaintenancePhase.WaitingOutbound
                    && aircraft.Stand.Value != job.OriginStand)
                throw new FormatException("Invalid maintenance job for " + registration + ".");
            foreach (var other in _fleet)
                if (other.MaintenanceJob?.HangarId == job.HangarId)
                    throw new FormatException("Maintenance shed is reserved twice: " + job.HangarId);
            if (!string.IsNullOrEmpty(job.ReturnStand) && (!StandFits(aircraft.Type, new StableId(job.ReturnStand))
                || aircraft.Stand.Value != job.ReturnStand))
                throw new FormatException("Invalid maintenance return stand for " + registration + ".");
            aircraft.MaintenanceJob = job.Copy();
            _ = aircraft.MaintenanceJob.Paths(aircraft.Type);
        }
    }
}
