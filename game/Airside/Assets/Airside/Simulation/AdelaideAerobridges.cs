using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// A jet's forward-left (L1) passenger door in its art-root frame (nose +Z at the nose
    /// stop, tyres at y 0): where an aerobridge cab docks. Generated from each runtime mesh
    /// by scripts/generate-aircraft-title-layout.py (ADR 0113).
    /// </summary>
    public readonly struct AircraftDoor
    {
        public AircraftDoor(float localX, float localZ, float sillY, float topY)
        {
            LocalX = localX;
            LocalZ = localZ;
            SillY = sillY;
            TopY = topY;
        }

        /// <summary>Door centre across the aircraft; negative is the left (L) side.</summary>
        public float LocalX { get; }
        /// <summary>Door centre along the aircraft; negative is aft of the nose stop.</summary>
        public float LocalZ { get; }
        /// <summary>Door sill height above the tyres' ground plane — the cab floor.</summary>
        public float SillY { get; }
        public float TopY { get; }
    }

    public static class AircraftDoors
    {
        /// <summary>The L1 door of a terminal-gate jet. Turboprops never use a bridge.</summary>
        public static AircraftDoor L1(AircraftType type)
        {
            // <generated door layout>
            if (Is(type, AircraftType.EmbraerE190))
                return new AircraftDoor(-1.38f, -5.52f, 2.52f, 3.99f);
            if (Is(type, AircraftType.AirbusA220300))
                return new AircraftDoor(-1.62f, -5.95f, 2.32f, 3.91f);
            if (Is(type, AircraftType.AirbusA320200))
                return new AircraftDoor(-1.91f, -4.94f, 2.55f, 4.23f);
            if (Is(type, AircraftType.Boeing737800))
                return new AircraftDoor(-1.82f, -5.19f, 2.75f, 4.54f);
            if (Is(type, AircraftType.Boeing7378))
                return new AircraftDoor(-1.82f, -5.19f, 2.73f, 4.51f);
            if (Is(type, AircraftType.AirbusA321Neo))
                return new AircraftDoor(-1.82f, -5.85f, 2.55f, 4.23f);
            if (Is(type, AircraftType.AirbusA350900))
                return new AircraftDoor(-2.70f, -6.30f, 4.67f, 6.56f);
            if (Is(type, AircraftType.AirbusA330900))
                return new AircraftDoor(-2.73f, -6.00f, 4.55f, 6.43f);
            if (Is(type, AircraftType.Boeing7879))
                return new AircraftDoor(-2.62f, -5.92f, 4.66f, 6.54f);
            if (Is(type, AircraftType.Boeing78710))
                return new AircraftDoor(-2.62f, -6.44f, 4.66f, 6.54f);
            // </generated door layout>
            return L1(AircraftType.Boeing7378);
        }

        private static bool Is(AircraftType type, AircraftType candidate) =>
            type != null && type.Id == candidate.Id;
    }

    /// <summary>
    /// One Terminal 1 aerobridge: a fixed rotunda by the building face and a telescoping
    /// tunnel whose cab drives out to a parked jet's L1 door (ADR 0113). Runway-frame metres.
    /// </summary>
    public readonly struct AerobridgeSite
    {
        public AerobridgeSite(StableId gate, float rotundaX, float rotundaZ, float parkedCabX, float parkedCabZ)
        {
            Gate = gate;
            RotundaX = rotundaX;
            RotundaZ = rotundaZ;
            ParkedCabX = parkedCabX;
            ParkedCabZ = parkedCabZ;
        }

        public StableId Gate { get; }
        public float RotundaX { get; }
        public float RotundaZ { get; }
        /// <summary>Where the cab waits, retracted and swung clear, when no jet is on the gate.</summary>
        public float ParkedCabX { get; }
        public float ParkedCabZ { get; }
    }

    /// <summary>
    /// Terminal 1's aerobridges (ADR 0113), derived from the same OSM gate stops and
    /// terminal footprint the pavement uses, so they cannot drift from the gates:
    ///
    /// * a rotunda stands <see cref="RotundaStandOffMetres"/> in front of the terminal face,
    ///   straight ahead of the stand's L1 door and <see cref="RotundaLateralMetres"/> further
    ///   to the aircraft's left, so the tunnel runs diagonally beside the nose, never over it;
    /// * a gate gets a bridge only if every jet's L1 door is within the tunnel's reach —
    ///   20R/22R (78 m out) stay bus stands, and 27–29 lie west of the terminal footprint.
    /// </summary>
    public static class AdelaideAerobridges
    {
        public const float RotundaStandOffMetres = 3.5f;
        public const float RotundaLateralMetres = 9f;
        /// <summary>Departures-level floor height where the tunnel leaves the rotunda.</summary>
        public const float RotundaFloorMetres = 5.0f;
        public const float MinTunnelMetres = 12f;
        public const float MaxTunnelMetres = 44f;
        /// <summary>Parked: retracted to this length, folded back along the terminal face.</summary>
        public const float ParkedTunnelMetres = 13f;
        /// <summary>How far the parked tunnel angles out from the face onto the apron.</summary>
        public const float ParkedApronDegrees = 20f;
        /// <summary>Furthest the terminal face may be ahead of a door for a bridge to be built.</summary>
        public const float MaxFaceDistanceMetres = 45f;

        private static readonly AircraftType[] GateTypes =
        {
            AircraftType.EmbraerE190, AircraftType.AirbusA220300, AircraftType.AirbusA320200,
            AircraftType.Boeing737800, AircraftType.Boeing7378, AircraftType.AirbusA321Neo,
            AircraftType.AirbusA350900, AircraftType.AirbusA330900, AircraftType.Boeing7879,
            AircraftType.Boeing78710
        };

        private static readonly Dictionary<string, AerobridgeSite> SitesByGate = Build();

        public static IEnumerable<AerobridgeSite> Sites => SitesByGate.Values;

        public static bool Serves(StableId stand) =>
            !string.IsNullOrEmpty(stand.Value) && SitesByGate.ContainsKey(stand.Value);

        public static bool TryGet(StableId stand, out AerobridgeSite site)
        {
            site = default;
            return !string.IsNullOrEmpty(stand.Value) && SitesByGate.TryGetValue(stand.Value, out site);
        }

        /// <summary>World (runway-frame) x, z of a type's L1 door on a gate, from its nose stop.</summary>
        public static (float X, float Z) DoorAt(AdelaideTerminalGate gate, AircraftType type)
        {
            var door = AircraftDoors.L1(type);
            return Local(gate, door.LocalX, door.LocalZ);
        }

        /// <summary>Docked tunnel length from the rotunda to <paramref name="type"/>'s L1 door.</summary>
        public static float DockedLength(AerobridgeSite site, AdelaideTerminalGate gate, AircraftType type)
        {
            var (x, z) = DoorAt(gate, type);
            return Distance(site.RotundaX, site.RotundaZ, x, z);
        }

        private static Dictionary<string, AerobridgeSite> Build()
        {
            var sites = new Dictionary<string, AerobridgeSite>(StringComparer.Ordinal);
            var face = TerminalFootprint();
            if (face == null)
                return sites;

            foreach (var gate in AdelaideLayout.TerminalGates)
            {
                var heading = gate.HeadingDegrees * Math.PI / 180.0;
                var fx = (float)Math.Sin(heading);
                var fz = (float)Math.Cos(heading);
                var (doorX, doorZ) = DoorAt(gate, AircraftType.Boeing7378);
                var ahead = DistanceToFace(face, doorX, doorZ, fx, fz);
                if (ahead < 0f)
                    continue;

                // Left of the nose direction is (-fz, fx) turned the other way in this frame:
                // for a gate heading 0 (nose +Z) the aircraft's left is -X.
                var lx = -fz;
                var lz = fx;
                var along = ahead - RotundaStandOffMetres;
                var rotundaX = doorX + fx * along + lx * RotundaLateralMetres;
                var rotundaZ = doorZ + fz * along + lz * RotundaLateralMetres;
                if (PointInPolygon(face, rotundaX, rotundaZ))
                    continue;

                var site = new AerobridgeSite(new StableId(gate.Id), rotundaX, rotundaZ, 0f, 0f);
                var reaches = true;
                foreach (var type in GateTypes)
                {
                    var length = DockedLength(site, gate, type);
                    if (length < MinTunnelMetres || length > MaxTunnelMetres)
                        reaches = false;
                }

                if (!reaches)
                    continue;

                // Park: retracted and folded back along the terminal face, pointing away from
                // the stand (the aircraft's left) and only slightly out onto the apron, so an
                // arriving jet's nose and wing never sweep through its own bridge.
                var parkedAngle = ParkedApronDegrees * Math.PI / 180.0;
                var parkedX = rotundaX + ParkedTunnelMetres * (float)(Math.Cos(parkedAngle) * lx - Math.Sin(parkedAngle) * fx);
                var parkedZ = rotundaZ + ParkedTunnelMetres * (float)(Math.Cos(parkedAngle) * lz - Math.Sin(parkedAngle) * fz);
                sites[gate.Id] = new AerobridgeSite(site.Gate, rotundaX, rotundaZ, parkedX, parkedZ);
            }

            return sites;
        }

        private static float[] TerminalFootprint()
        {
            foreach (var outline in AdelaideLayout.Terminals)
                if (outline.Name == "Domestic & International Terminal")
                    return outline.Xz;
            return null;
        }

        private static (float X, float Z) Local(AdelaideTerminalGate gate, float localX, float localZ)
        {
            var heading = gate.HeadingDegrees * Math.PI / 180.0;
            var cos = (float)Math.Cos(heading);
            var sin = (float)Math.Sin(heading);
            // Unity yaw, clockwise from +Z: local +X maps to (cos, -sin), local +Z to (sin, cos).
            return (gate.NoseX + localX * cos + localZ * sin, gate.NoseZ - localX * sin + localZ * cos);
        }

        private static float DistanceToFace(float[] polygon, float x, float z, float fx, float fz)
        {
            for (var step = 0; step <= MaxFaceDistanceMetres * 10f; step++)
            {
                var t = step * 0.1f;
                if (PointInPolygon(polygon, x + fx * t, z + fz * t))
                    return t;
            }

            return -1f;
        }

        private static bool PointInPolygon(float[] xz, float x, float z)
        {
            var inside = false;
            var n = xz.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var xi = xz[i * 2];
                var zi = xz[i * 2 + 1];
                var xj = xz[j * 2];
                var zj = xz[j * 2 + 1];
                if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi)
                    inside = !inside;
            }

            return inside;
        }

        private static float Distance(float ax, float az, float bx, float bz) =>
            (float)Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));
    }

    /// <summary>
    /// When a gate's aerobridge is docked, as a pure function of fleet state and time
    /// (ADR 0113) — presentation only, like <see cref="EngineStartSequence"/>; it never
    /// changes when anything happens. Follows ramp procedure:
    ///
    /// * arrival: the bridge may only move once the beacon is off
    ///   (<see cref="EngineStartSequence.BeaconOffAfterSeconds"/>), drives out over
    ///   <see cref="DockSeconds"/>, and the L1 door opens once it is on;
    /// * departure: the door closes, then the bridge pulls back over
    ///   <see cref="RetractSeconds"/>, finishing before the beacon comes on for the push.
    ///   A player aircraft still boarding keeps its bridge until boarding is done.
    /// </summary>
    public static class AerobridgeTimeline
    {
        public const double DockAfterParkSeconds = EngineStartSequence.BeaconOffAfterSeconds;
        public const double DockSeconds = 60;
        public const double DoorsOpenAfterParkSeconds = DockAfterParkSeconds + DockSeconds + 10;
        /// <summary>Retracting starts this long before the push and ends before the beacon comes on.</summary>
        public const double RetractBeforePushSeconds = EngineStartSequence.BeaconOnBeforeSeconds + RetractSeconds + 10;
        public const double RetractSeconds = 60;
        public const double DoorsCloseBeforePushSeconds = RetractBeforePushSeconds + 10;

        /// <summary>True when this aircraft is parked (or taxiing in) on a bridged gate.</summary>
        public static bool AtBridgedGate(FleetAircraft aircraft) =>
            aircraft != null && aircraft.State is FleetState.AtStand or FleetState.TaxiIn
            && AdelaideAerobridges.Serves(aircraft.Stand);

        /// <summary>0 = parked clear of the stand, 1 = docked at the aircraft's L1 door.</summary>
        public static float DockedFraction(FleetAircraft aircraft, double nowSeconds,
            PlayerBaseLevel baseLevel = PlayerBaseLevel.Starter)
        {
            if (aircraft == null || aircraft.State != FleetState.AtStand || !AdelaideAerobridges.Serves(aircraft.Stand))
                return 0f;

            var parked = nowSeconds - aircraft.StateStartedAt.ElapsedSeconds;
            var docked = Ramp((parked - DockAfterParkSeconds) / DockSeconds);

            var retractStart = RetractStartSeconds(aircraft, baseLevel);
            if (retractStart.HasValue)
                docked = Math.Min(docked, 1f - Ramp((nowSeconds - retractStart.Value) / RetractSeconds));
            return docked;
        }

        /// <summary>
        /// When the L1 door is open on a bridged gate; null when the aircraft is not on one
        /// (its doors follow <see cref="EngineStartSequence"/>'s own stair timing).
        /// </summary>
        public static bool? DoorsOpen(FleetAircraft aircraft, double nowSeconds,
            PlayerBaseLevel baseLevel = PlayerBaseLevel.Starter)
        {
            if (aircraft == null || aircraft.State != FleetState.AtStand || !AdelaideAerobridges.Serves(aircraft.Stand))
                return null;
            var parked = nowSeconds - aircraft.StateStartedAt.ElapsedSeconds;
            if (parked < DoorsOpenAfterParkSeconds)
                return false;
            var retractStart = RetractStartSeconds(aircraft, baseLevel);
            return !retractStart.HasValue || nowSeconds < retractStart.Value - 10;
        }

        private static double? RetractStartSeconds(FleetAircraft aircraft, PlayerBaseLevel baseLevel)
        {
            if (aircraft.Scheduled is not { Cancelled: false } departure)
                return null;
            var start = departure.DepartAt.ElapsedSeconds - RetractBeforePushSeconds;
            if (aircraft.Airline.IsPlayer)
            {
                // Boarding holds the bridge: pull back only once prep is complete.
                var total = DeparturePrep.TotalSeconds(aircraft.Type, baseLevel);
                var prepStart = aircraft.PrepStartedAt?.ElapsedSeconds ?? departure.DepartAt.ElapsedSeconds - total;
                start = Math.Max(start, prepStart + total);
            }

            return start;
        }

        private static float Ramp(double t)
        {
            if (t <= 0) return 0f;
            if (t >= 1) return 1f;
            return (float)(t * t * (3 - 2 * t));
        }
    }
}
