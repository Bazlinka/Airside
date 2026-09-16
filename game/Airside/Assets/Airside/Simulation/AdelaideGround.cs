using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// The ground legs fleet aircraft drive at Adelaide, built from the real routes in
    /// <see cref="AdelaideLayout"/> with verified ground-speed limits (ADR 0045, taxi
    /// section of <c>AIRCRAFT_SPECIFICATIONS.md</c>). Regional bays use the turboprop
    /// band (ATR / Saab / Q400); terminal gates use the 737 band. Every ground duration
    /// the airline simulation uses comes from here, so time on the ground is the time
    /// the motion really takes — nothing is a picked number.
    /// </summary>
    public static class AdelaideGround
    {
        /// <summary>Tug disconnect and taxi clearance between the pushback and moving off.</summary>
        public const double TugDisconnectSeconds = 25;

        /// <summary>Queue spacing back along the exit when more than one arrival waits for a stand.</summary>
        public const float AwaitingSpacingMetres = 60f;

        private static readonly Dictionary<string, AdelaideBay> BaysById = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, GroundLeg> TaxiOutLegs = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, GroundLeg> TaxiInLegs = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, AdelaideTerminalGate> GatesById = new(StringComparer.Ordinal);

        /// <summary>
        /// A jet's main gear trails its nose datum by about this much on a gate route (737-8
        /// nose to main gear ≈ 19 m), which is how its body is steered through the turns.
        /// </summary>
        public const float JetTrackMetres = 19f;

        /// <summary>The paved link and lead-in between T1/T2 and a terminal gate, reserved per gate.</summary>
        public static string LeadInResource(StableId gate) => gate.Value + "/lead-in";
        private static GroundPath _vacate;
        private static GroundLeg _vacateLeg;
        private static GroundLeg _lineupLeg;

        public static IReadOnlyList<AdelaideBay> Bays => AdelaideLayout.Bays;

        public static IReadOnlyList<AdelaideTerminalGate> TerminalGates => AdelaideLayout.TerminalGates;

        /// <summary>True for a terminal gate (a separate stand system from the regional bays).</summary>
        public static bool IsTerminalGate(StableId stand) => TryTerminalGate(stand, out _);

        public static bool TryTerminalGate(StableId stand, out AdelaideTerminalGate gate)
        {
            if (GatesById.Count == 0)
                foreach (var candidate in AdelaideLayout.TerminalGates)
                    GatesById[candidate.Id] = candidate;
            gate = default;
            return stand.Value != null && GatesById.TryGetValue(stand.Value, out gate);
        }

        /// <summary>
        /// Regional bay lookup. Unknown ids keep the historical fall back to the first bay, but a
        /// terminal gate is refused outright: a jet must never be drawn or routed on 50D.
        /// </summary>
        public static AdelaideBay Bay(StableId stand)
        {
            if (IsTerminalGate(stand))
                throw new InvalidOperationException($"{stand} is a terminal gate, not a regional bay.");
            if (BaysById.Count == 0)
                foreach (var bay in AdelaideLayout.Bays)
                    BaysById[bay.Id] = bay;
            return stand.Value != null && BaysById.TryGetValue(stand.Value, out var found) ? found : AdelaideLayout.Bays[0];
        }

        /// <summary>
        /// The player-facing name of a stand: "Bay 50C" for a real Adelaide regional bay,
        /// the raw id for anything else, "—" for none.
        /// </summary>
        public static string StandLabel(StableId stand)
        {
            if (string.IsNullOrEmpty(stand.Value))
                return "—";
            if (TryTerminalGate(stand, out var gate))
                return $"Gate {gate.Reference}";
            foreach (var bay in AdelaideLayout.Bays)
                if (bay.Id == stand.Value)
                    return $"Bay {bay.Reference}";
            return stand.Value;
        }

        /// <summary>Runway 05 rollout end → exit E2 → holding point clear of the runway.</summary>
        public static GroundLeg Vacate => _vacateLeg ??= new GroundLeg(new GroundLegPart(VacatePath, tailFirst: false));

        /// <summary>F6 holding point → centreline at the 05 takeoff start, stopped and ready to roll.</summary>
        public static GroundLeg Lineup => _lineupLeg ??= new GroundLeg(
            new GroundLegPart(new GroundPath(AdelaideLayout.Lineup, GroundSpeedLimits.Lineup), tailFirst: false));

        /// <summary>Where an aircraft parked on <paramref name="stand"/> stands: its stop and nose heading.</summary>
        public static GroundPose StandPose(StableId stand)
        {
            double heading;
            float x, z;
            if (TryTerminalGate(stand, out var gate))
            {
                x = gate.NoseX;
                z = gate.NoseZ;
                heading = gate.HeadingDegrees * Math.PI / 180.0;
            }
            else
            {
                var bay = Bay(stand);
                x = bay.StopX;
                z = bay.StopZ;
                heading = bay.HeadingDegrees * Math.PI / 180.0;
            }

            return new GroundPose(x, z, (float)Math.Sin(heading), (float)Math.Cos(heading), 0f, false);
        }

        /// <summary>
        /// Pushback tail-first onto T4, tug disconnect, then taxi to the runway 05 holding point.
        /// A terminal gate uses its own nose-datum routes: pushback tail-first onto T1, the tug
        /// disconnect, then forward along T1/T2 to runway 05.
        /// </summary>
        public static GroundLeg TaxiOut(StableId stand)
            => TaxiOut(stand, IsTerminalGate(stand) ? AircraftType.Boeing7378 : AircraftType.Atr42);

        public static GroundLeg TaxiOut(StableId stand, AircraftType type)
        {
            if (TryTerminalGate(stand, out var gate))
                return GateTaxiOut(gate, type);
            var bay = Bay(stand);
            var key = bay.Id + "/" + (type?.Id ?? "ATR42");
            if (!TaxiOutLegs.TryGetValue(key, out var leg))
            {
                var limits = GroundSpeedLimits.TaxiFor(type);
                leg = new GroundLeg(
                    new GroundLegPart(new GroundPath(bay.Pushback, GroundSpeedLimits.Pushback), tailFirst: true),
                    new GroundLegPart(new GroundPath(bay.TaxiOut, limits, 0f, 0f,
                        new[] { ApronZone(type) }, null), tailFirst: false, TugDisconnectSeconds));
                TaxiOutLegs[key] = leg;
            }

            return leg;
        }

        /// <summary>E2 holding point → nose into the assigned bay or terminal gate.</summary>
        public static GroundLeg TaxiIn(StableId stand)
            => TaxiIn(stand, IsTerminalGate(stand) ? AircraftType.Boeing7378 : AircraftType.Atr42);

        public static GroundLeg TaxiIn(StableId stand, AircraftType type)
        {
            if (TryTerminalGate(stand, out var gate))
                return GateTaxiIn(gate, type);
            var bay = Bay(stand);
            var key = bay.Id + "/" + (type?.Id ?? "ATR42");
            if (!TaxiInLegs.TryGetValue(key, out var leg))
            {
                var limits = GroundSpeedLimits.TaxiFor(type);
                leg = new GroundLeg(new GroundLegPart(new GroundPath(bay.TaxiIn, limits, 0f, 0f,
                    null, new[] { ApronZone(type), StandLeadInZone }), tailFirst: false));
                TaxiInLegs[key] = leg;
            }

            return leg;
        }

        /// <summary>
        /// Where arrival <paramref name="slot"/> waits for a stand: slot 0 at the E2 holding
        /// point, later ones queued back along the exit.
        /// </summary>
        public static GroundPose AwaitingPose(int slot)
        {
            var end = Vacate.PoseAt(Vacate.Seconds);
            if (slot <= 0)
                return new GroundPose(end.X, end.Z, end.NoseX, end.NoseZ, 0f, false);

            var back = VacatePath.SampleAtDistance(Math.Max(0f, VacatePath.Length - AwaitingSpacingMetres * slot));
            return new GroundPose(back.X, back.Z, back.DirectionX, back.DirectionZ, 0f, false);
        }

        /// <summary>
        /// Where departure <paramref name="slot"/> holds short after taxiing out from
        /// <paramref name="departureStand"/>: slot 0 at the runway 05 holding point, later ones
        /// queued back along their own taxi route.
        /// </summary>
        public static GroundPose HoldingShortPose(StableId departureStand, int slot)
        {
            var leg = TaxiOut(departureStand);
            var end = leg.PoseAt(leg.Seconds);
            if (slot <= 0)
                return new GroundPose(end.X, end.Z, end.NoseX, end.NoseZ, 0f, false);

            var taxi = leg.Parts[leg.Parts.Count - 1].Path;
            var back = taxi.SampleAtDistance(Math.Max(0f, taxi.Length - AwaitingSpacingMetres * slot));
            return new GroundPose(back.X, back.Z, back.DirectionX, back.DirectionZ, 0f, false);
        }

        private static GroundLeg GateTaxiOut(AdelaideTerminalGate gate, AircraftType type)
        {
            var key = gate.Id + "/" + (type?.Id ?? "B38M");
            if (!TaxiOutLegs.TryGetValue(key, out var leg))
            {
                var limits = GroundSpeedLimits.TaxiFor(type);
                leg = new GroundLeg(
                    new GroundLegPart(new GroundPath(gate.Pushback, GroundSpeedLimits.Pushback), tailFirst: true, trackMetres: JetTrackMetres),
                    new GroundLegPart(new GroundPath(gate.TaxiOut, limits, 0f, 0f, new[] { ApronZone(type) }, null),
                        tailFirst: false, TugDisconnectSeconds, JetTrackMetres));
                TaxiOutLegs[key] = leg;
            }

            return leg;
        }

        private static GroundLeg GateTaxiIn(AdelaideTerminalGate gate, AircraftType type)
        {
            var key = gate.Id + "/" + (type?.Id ?? "B38M");
            if (!TaxiInLegs.TryGetValue(key, out var leg))
            {
                var limits = GroundSpeedLimits.TaxiFor(type);
                leg = new GroundLeg(new GroundLegPart(new GroundPath(gate.TaxiIn, limits, 0f, 0f,
                    null, new[] { ApronZone(type), StandLeadInZone }), tailFirst: false, trackMetres: JetTrackMetres));
                TaxiInLegs[key] = leg;
            }

            return leg;
        }

        private static GroundSpeedZone ApronZone(StandClass standClass) =>
            new(GroundSpeedLimits.ApronMetres, CircuitProfile.Knots(GroundSpeedLimits.ApronKnotsFor(standClass)));

        private static GroundSpeedZone ApronZone(AircraftType type) =>
            new(GroundSpeedLimits.ApronMetres, CircuitProfile.Knots(GroundSpeedLimits.ApronKnotsFor(type)));

        /// <summary>5 kt for the last stretch onto the stand line.</summary>
        private static GroundSpeedZone StandLeadInZone =>
            new(GroundSpeedLimits.StandLeadInMetres, CircuitProfile.Knots(GroundSpeedLimits.StandLeadInKnots));

        /// <summary>Entered rolling at the runway exit speed the landing ends at, not from a stop.</summary>
        private static GroundPath VacatePath => _vacate ??= new GroundPath(AdelaideLayout.Vacate, GroundSpeedLimits.Vacate,
            entrySpeed: CircuitProfile.Knots(CircuitProfile.RunwayExitKnots));
    }
}
