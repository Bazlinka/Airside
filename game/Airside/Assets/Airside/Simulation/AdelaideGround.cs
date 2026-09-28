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

        /// <summary>The paved link and lead-in between T1/T2 and a terminal gate, reserved per gate.</summary>
        public static string LeadInResource(StableId gate) => gate.Value + "/lead-in";
        private static readonly Dictionary<string, GroundPath> VacatePaths = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, GroundLeg> VacateLegs = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, GroundLeg> LineupLegs = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, float> TakeoffRollInByType = new(StringComparer.Ordinal);

        public static IReadOnlyList<AdelaideBay> Bays => AdelaideLayout.Bays;

        public static IReadOnlyList<AdelaideTerminalGate> TerminalGates => AdelaideGateAlignment.Gates;

        /// <summary>True for a terminal gate (a separate stand system from the regional bays).</summary>
        public static bool IsTerminalGate(StableId stand) => TryTerminalGate(stand, out _);

        public static bool TryTerminalGate(StableId stand, out AdelaideTerminalGate gate)
        {
            if (GatesById.Count == 0)
                foreach (var candidate in AdelaideGateAlignment.Gates)
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
        public static GroundLeg Vacate => VacateFor(AircraftType.Atr42);

        public static GroundLeg VacateFor(AircraftType type)
            => VacateFor(type, RunwayDirection.Runway05);

        public static GroundLeg VacateFor(AircraftType type, RunwayDirection runway)
        {
            var key = (type?.Id ?? "ATR42") + "/" + runway;
            if (!VacateLegs.TryGetValue(key, out var leg))
            {
                var wheelbase = AircraftPerformance.For(type).NoseToMainGearMetres;
                leg = new GroundLeg(new GroundLegPart(VacatePathFor(type, runway), tailFirst: false,
                    trackMetres: wheelbase));
                VacateLegs[key] = leg;
            }
            return leg;
        }

        /// <summary>True when both runway ends vacate to the same waiting point (05/23 → E2).</summary>
        private static readonly Dictionary<(RunwayDirection, RunwayDirection), bool> SameExitCache = new();

        public static bool SameArrivalExit(RunwayDirection a, RunwayDirection b)
        {
            if (a == b)
                return true;
            // Asked for every pair of aircraft in every queue check: work each pair out once.
            if (SameExitCache.TryGetValue((a, b), out var known))
                return known;
            return SameExitCache[(a, b)] = SameExitUncached(a, b);
        }

        private static bool SameExitUncached(RunwayDirection a, RunwayDirection b)
        {
            var ea = VacateFor(AircraftType.Atr42, a);
            var eb = VacateFor(AircraftType.Atr42, b);
            var pa = ea.PoseAt(ea.Seconds);
            var pb = eb.PoseAt(eb.Seconds);
            var dx = pa.X - pb.X;
            var dz = pa.Z - pb.Z;
            return dx * dx + dz * dz < 25f;
        }

        /// <summary>
        /// Seconds until the aircraft is clear of the strip during vacate — when the
        /// tower may clear the next movement. For 05/23 that is the full E2 vacate.
        /// For 12/30 the long taxi toward E2 continues after the strip is free.
        /// </summary>
        public static long ClearOfRunwaySeconds(AircraftType type, RunwayDirection runway)
        {
            var vacate = VacateFor(type, runway);
            if (RunwayWeather.IsMainRunway(runway))
                return vacate.WholeSeconds;

            // Cross strip: free once well clear of the pavement and past the shared
            // exit conflict zone — not after the full kilometre to E2, but later than
            // a short 280 m hop that let the next landing meet the vacating aircraft.
            const float clearMetres = 480f;
            if (vacate.Parts.Count == 0)
                return Math.Max(30, vacate.WholeSeconds / 3);
            var path = vacate.Parts[0].Path;
            var metres = Math.Min(clearMetres, path.Length * 0.55f);
            var seconds = path.SecondsAtDistance(metres);
            return Math.Max(30L, (long)Math.Ceiling(seconds));
        }

        /// <summary>F6 holding point → centreline at the 05 takeoff start, stopped and ready to roll.</summary>
        public static GroundLeg Lineup => LineupFor(RunwayDirection.Runway05, AircraftType.Atr42);

        public static GroundLeg LineupFor(RunwayDirection runway) => LineupFor(runway, AircraftType.Atr42);

        /// <summary>
        /// Metres past the shared takeoff point where this type actually begins the roll
        /// on <paramref name="runway"/>. A long wheelbase is still yawing when the nose
        /// first reaches the centreline; the extra straight lets the main gear trail into
        /// line before the brakes come off. The takeoff path starts at the same point.
        /// </summary>
        public static float TakeoffRollInMetres(AircraftType type, RunwayDirection runway = RunwayDirection.Runway05)
        {
            type ??= AircraftType.Atr42;
            var key = type.Id + "/" + runway;
            if (TakeoffRollInByType.TryGetValue(key, out var metres))
                return metres;
            var xz = NominalLineup(runway, type, out var fx, out var fz);
            metres = MetresUntilAligned(xz, fx, fz, AircraftPerformance.For(type).NoseToMainGearMetres);
            TakeoffRollInByType[key] = metres;
            return metres;
        }

        /// <summary>Holding point onto the runway, steering the visible main gear for this type.</summary>
        public static GroundLeg LineupFor(RunwayDirection runway, AircraftType type)
        {
            type ??= AircraftType.Atr42;
            var key = type.Id + "/" + runway;
            if (LineupLegs.TryGetValue(key, out var leg))
                return leg;
            var xz = NominalLineup(runway, type, out var fx, out var fz);
            var rollIn = TakeoffRollInMetres(type, runway);
            if (rollIn > 0.5f)
                xz = Extend(xz, fx, fz, rollIn);
            leg = new GroundLeg(new GroundLegPart(new GroundPath(xz, GroundSpeedLimits.Lineup),
                tailFirst: false, trackMetres: AircraftPerformance.For(type).NoseToMainGearMetres, slipFree: true));
            LineupLegs[key] = leg;
            return leg;
        }

        /// <summary>Hold to the shared takeoff point, on this type's arc, before the alignment run.</summary>
        private static float[] NominalLineup(RunwayDirection runway, AircraftType type, out float fx, out float fz)
        {
            var xz = runway switch
            {
                RunwayDirection.Runway23 => AdelaideLayout.Lineup23,
                RunwayDirection.Runway12 => AdelaideCrossRoutes.Lineup(RunwayDirection.Runway12),
                RunwayDirection.Runway30 => AdelaideCrossRoutes.Lineup(RunwayDirection.Runway30),
                _ => AdelaideLayout.Lineup
            };
            var wheelbase = AircraftPerformance.For(type).NoseToMainGearMetres;
            RunwayFrame.Forward(runway, out fx, out fz);
            // Wide enough that the turn is not a hook, and small enough that centreline
            // remains for the trailed gear to finish straight.
            var radius = Math.Max(20f, Math.Min(42f, wheelbase * 2.2f));
            if (LineupGeometry.TryBest(xz, fx, fz, radius, wheelbase, out var steered))
                xz = steered;
            // Leave the holding point the way the taxi route arrived at it. The authored 05 and cross-
            // runway lineups set off about 8° to one side, so every departure swivelled on the spot as
            // it was cleared. Only the first metres bend; the turn and the takeoff point are unchanged.
            if (TryHoldApproachDirection(runway, out var inX, out var inZ))
                xz = LineupGeometry.AlignEntry(xz, inX, inZ, LineupEntryBlendMetres);
            return xz;
        }

        /// <summary>Distance over which a lineup blends from the taxi heading into its authored line.</summary>
        public const float LineupEntryBlendMetres = 45f;

        /// <summary>
        /// Unit direction the taxi-out routes arrive at <paramref name="runway"/>'s holding point:
        /// the last dozen metres of the first bay's route, which every stand's route shares.
        /// </summary>
        public static bool TryHoldApproachDirection(RunwayDirection runway, out float dx, out float dz)
        {
            dx = dz = 0f;
            var bays = AdelaideLayout.Bays;
            if (bays == null || bays.Length == 0)
                return false;
            var path = TaxiOutPath(bays[0], runway);
            if (path == null || path.Length < 4)
                return false;
            var n = path.Length;
            float ex = path[n - 2], ez = path[n - 1];
            for (var i = n - 4; i >= 0; i -= 2)
            {
                var x = ex - path[i];
                var z = ez - path[i + 1];
                var length = (float)Math.Sqrt(x * x + z * z);
                if (length < 12f && i > 0)
                    continue;
                if (length < 1e-3f)
                    return false;
                dx = x / length;
                dz = z / length;
                return true;
            }

            return false;
        }

        /// <summary>How much more centreline the trailed gear needs before it is within 2° of the runway.</summary>
        private static float MetresUntilAligned(float[] xz, float fx, float fz, float wheelbase)
        {
            const double aligned = 0.9993908270190958; // cos(2°)
            if (Aligned(xz, fx, fz, wheelbase, aligned))
                return 0f;
            var low = 0f;
            var high = 8f;
            while (high < 160f && !Aligned(Extend(xz, fx, fz, high), fx, fz, wheelbase, aligned))
                high *= 2f;
            for (var i = 0; i < 6; i++)
            {
                var mid = (low + high) * 0.5f;
                if (Aligned(Extend(xz, fx, fz, mid), fx, fz, wheelbase, aligned))
                    high = mid;
                else
                    low = mid;
            }

            return (float)Math.Ceiling(high);
        }

        private static bool Aligned(float[] xz, float fx, float fz, float wheelbase, double alignedDot)
        {
            var leg = new GroundLeg(new GroundLegPart(new GroundPath(xz, GroundSpeedLimits.Lineup),
                tailFirst: false, trackMetres: wheelbase, slipFree: true));
            var end = leg.PoseAt(leg.Seconds);
            return end.NoseX * fx + end.NoseZ * fz >= alignedDot;
        }

        private static float[] Extend(float[] xz, float fx, float fz, float metres)
        {
            var copy = new float[xz.Length + 2];
            Array.Copy(xz, copy, xz.Length);
            copy[^2] = xz[^2] + fx * metres;
            copy[^1] = xz[^1] + fz * metres;
            return copy;
        }

        /// <summary>
        /// ADR 0145: a bay's parked heading is the way its lead-in arrives, so the aircraft does not
        /// turn on the spot after stopping. The walk-out stands 10A and 10C were 9–11° off; there the
        /// heading splits the difference with the line it leaves on, halving both turns.
        /// </summary>
        public static double ParkedHeadingDegrees(AdelaideBay bay)
        {
            var ti = bay.TaxiIn;
            if (ti == null || ti.Length < 8)
                return bay.HeadingDegrees;
            var n = ti.Length;
            var arrive = Math.Atan2(ti[n - 2] - ti[n - 8], ti[n - 1] - ti[n - 7]) * 180.0 / Math.PI;
            if (!IsWalkOut(bay) || bay.Pushback == null || bay.Pushback.Length < 8)
                return arrive;
            var leave = Math.Atan2(bay.Pushback[0] - bay.Pushback[6], bay.Pushback[1] - bay.Pushback[7]) * 180.0 / Math.PI;
            var diff = ((leave - arrive) % 360.0 + 540.0) % 360.0 - 180.0;
            return arrive + diff * 0.5;
        }

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
                heading = ParkedHeadingDegrees(bay) * Math.PI / 180.0;
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
            => TaxiOut(stand, type, RunwayDirection.Runway05);

        public static GroundLeg TaxiOut(StableId stand, AircraftType type, RunwayDirection runway)
        {
            if (TryTerminalGate(stand, out var gate))
                return GateTaxiOut(gate, type, runway);
            var bay = Bay(stand);
            var key = bay.Id + "/" + (type?.Id ?? "ATR42") + "/" + runway;
            if (!TaxiOutLegs.TryGetValue(key, out var leg))
            {
                var limits = GroundSpeedLimits.TaxiFor(type);
                var wheelbase = AircraftPerformance.For(type).NoseToMainGearMetres;
                var (push, taxi) = IsWalkOut(bay)
                    ? (BayPushback(bay, type), CleanTaxiOut(TaxiOutPath(bay, runway)))
                    : PushAndTaxi(bay.StopX, bay.StopZ, bay.HeadingDegrees, BayPushback(bay, type),
                        CleanTaxiOut(TaxiOutPath(bay, runway)), CleanTaxiOut(bay.TaxiOut), type);
                leg = new GroundLeg(
                    new GroundLegPart(new GroundPath(push, GroundSpeedLimits.Pushback),
                        tailFirst: true, trackMetres: wheelbase),
                    new GroundLegPart(new GroundPath(Drivable(taxi, type), limits, 0f, 0f,
                        new[] { ApronZone(type) }, null), tailFirst: false, TugDisconnectSeconds, wheelbase));
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
                var wheelbase = AircraftPerformance.For(type).NoseToMainGearMetres;
                leg = new GroundLeg(new GroundLegPart(new GroundPath(BayTaxiIn(bay, bay.TaxiIn, type), limits, 0f, 0f,
                    null, new[] { ApronZone(type), StandLeadInZone }), tailFirst: false, trackMetres: wheelbase));
                TaxiInLegs[key] = leg;
            }

            return leg;
        }

        /// <summary>
        /// Taxi-in for an arrival off <paramref name="runway"/>. A 12/30 vacate ends partway
        /// along the E2 → bays corridor, so its taxi-in starts there rather than back at E2.
        /// </summary>
        public static GroundLeg TaxiIn(StableId stand, AircraftType type, RunwayDirection runway)
        {
            if (!AdelaideCrossRoutes.TryArrivalJoin(runway, out _, out _))
                return TaxiIn(stand, type);
            var key = stand.Value + "/" + (type?.Id ?? "ATR42") + "/" + runway;
            if (TaxiInLegs.TryGetValue(key, out var leg))
                return leg;

            var limits = GroundSpeedLimits.TaxiFor(type);
            if (TryTerminalGate(stand, out var gate))
            {
                var wheelbase = AircraftPerformance.For(type).NoseToMainGearMetres;
                leg = new GroundLeg(new GroundLegPart(new GroundPath(
                    Drivable(AdelaideCrossRoutes.TrimToArrivalJoin(gate.TaxiIn, runway), type), limits, 0f, 0f,
                    null, new[] { ApronZone(type), StandLeadInZone }), tailFirst: false, trackMetres: wheelbase));
            }
            else
            {
                var bay = Bay(stand);
                var wheelbase = AircraftPerformance.For(type).NoseToMainGearMetres;
                leg = new GroundLeg(new GroundLegPart(new GroundPath(
                    BayTaxiIn(bay, AdelaideCrossRoutes.TrimToArrivalJoin(bay.TaxiIn, runway), type), limits, 0f, 0f,
                    null, new[] { ApronZone(type), StandLeadInZone }), tailFirst: false, trackMetres: wheelbase));
            }

            TaxiInLegs[key] = leg;
            return leg;
        }

        /// <summary>
        /// Where arrival <paramref name="slot"/> waits for a stand: slot 0 at the end of
        /// that runway's vacate, later ones queued back along the same exit.
        /// </summary>
        public static GroundPose AwaitingPose(int slot)
            => AwaitingPose(slot, AircraftType.Atr42, RunwayDirection.Runway05);

        public static GroundPose AwaitingPose(int slot, AircraftType type, RunwayDirection runway)
        {
            // Wait where vacate ends so Landing → AwaitingStand does not teleport. Cross
            // vacates finish just onto the E2 → bays corridor, where their taxi-in starts;
            // ClearOfRunwaySeconds frees the strip earlier. QueueSlot is per AssignedRunway so 05 and 12 do not share
            // a queue even when both exit toward E2.
            var vacate = VacateFor(type, runway);
            var end = vacate.PoseAt(vacate.Seconds);
            if (slot <= 0)
                return new GroundPose(end.X, end.Z, end.NoseX, end.NoseZ, 0f, false);

            var path = vacate.Parts[vacate.Parts.Count - 1].Path;
            // A busy day can queue more arrivals for a stand than this taxiway is long.
            // SampleAtDistance clamps to the path's start, so every arrival past that point
            // used to collapse onto the exact same spot instead of getting its own place to
            // wait — visually stuck stacked aircraft rather than a real queue. PointAtDistance
            // keeps extending the line in a straight line past the start instead (direction
            // only needs the same clamp — it stays constant along that extrapolated stretch).
            var distance = path.Length - AwaitingSpacingMetres * slot;
            var (px, pz) = path.PointAtDistance(distance);
            var direction = path.SampleAtDistance(Math.Max(0f, distance));
            return new GroundPose(px, pz, direction.DirectionX, direction.DirectionZ, 0f, false);
        }

        /// <summary>
        /// Where departure <paramref name="slot"/> holds short after taxiing out from
        /// <paramref name="departureStand"/>: slot 0 at the runway 05 holding point, later ones
        /// queued back along their own taxi route.
        /// </summary>
        public static GroundPose HoldingShortPose(StableId departureStand, int slot,
            RunwayDirection runway = RunwayDirection.Runway05, AircraftType type = null)
        {
            // A missing departure stand must not teleport the aircraft onto bay 50D's hold.
            if (string.IsNullOrEmpty(departureStand.Value))
            {
                float hx, hz, fx, fz;
                if (runway == RunwayDirection.Runway12)
                {
                    hx = AdelaideCrossRoutes.Hold12[0];
                    hz = AdelaideCrossRoutes.Hold12[1];
                    RunwayFrame.Forward(runway, out fx, out fz);
                }
                else if (runway == RunwayDirection.Runway30)
                {
                    hx = AdelaideCrossRoutes.Hold30[0];
                    hz = AdelaideCrossRoutes.Hold30[1];
                    RunwayFrame.Forward(runway, out fx, out fz);
                }
                else if (runway == RunwayDirection.Runway23)
                {
                    // Start of the 23 lineup path — the F6-side hold mirrored for 23.
                    var lineup = AdelaideLayout.Lineup23;
                    hx = lineup[0];
                    hz = lineup[1];
                    RunwayFrame.Forward(runway, out fx, out fz);
                }
                else
                {
                    hx = AdelaideLayout.Runway05Hold[0];
                    hz = AdelaideLayout.Runway05Hold[1];
                    RunwayFrame.Forward(runway, out fx, out fz);
                }

                if (slot <= 0)
                    return new GroundPose(hx, hz, fx, fz, 0f, false);
                var back = AwaitingSpacingMetres * slot;
                return new GroundPose(hx - fx * back, hz - fz * back, fx, fz, 0f, false);
            }

            type ??= IsTerminalGate(departureStand) ? AircraftType.Boeing7378 : AircraftType.Atr42;
            var leg = TaxiOut(departureStand, type, runway);
            var end = leg.PoseAt(leg.Seconds);
            if (slot <= 0)
                return new GroundPose(end.X, end.Z, end.NoseX, end.NoseZ, 0f, false);

            var taxi = leg.Parts[leg.Parts.Count - 1].Path;
            // Same overflow fix as AwaitingPose above: a deep departure queue used to collapse
            // onto the taxiway's start once it ran out of taxiway to queue back along.
            var distance = taxi.Length - AwaitingSpacingMetres * slot;
            var (bx, bz) = taxi.PointAtDistance(distance);
            var direction = taxi.SampleAtDistance(Math.Max(0f, distance));
            return new GroundPose(bx, bz, direction.DirectionX, direction.DirectionZ, 0f, false);
        }

        private static GroundLeg GateTaxiOut(AdelaideTerminalGate gate, AircraftType type, RunwayDirection runway)
        {
            var key = gate.Id + "/" + (type?.Id ?? "B38M") + "/" + runway;
            if (!TaxiOutLegs.TryGetValue(key, out var leg))
            {
                var limits = GroundSpeedLimits.TaxiFor(type);
                // Each jet steers its main gear (not its nose) through the turns, trailing
                // the nose datum by its own actual wheelbase — a 737 and an A350 do not
                // track a corner the same way. See AircraftPerformanceProfile.NoseToMainGearMetres.
                var wheelbase = AircraftPerformance.For(type).NoseToMainGearMetres;
                var (push, taxi) = PushAndTaxi(gate.NoseX, gate.NoseZ, gate.HeadingDegrees,
                    DrivablePushback(gate.Pushback, type), CleanTaxiOut(TaxiOutPath(gate, runway)),
                    CleanTaxiOut(gate.TaxiOut), type);
                leg = new GroundLeg(
                    new GroundLegPart(new GroundPath(push, GroundSpeedLimits.Pushback),
                        tailFirst: true, trackMetres: wheelbase),
                    new GroundLegPart(new GroundPath(Drivable(taxi, type),
                            limits, 0f, 0f, new[] { ApronZone(type) }, null),
                        tailFirst: false, TugDisconnectSeconds, wheelbase));
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
                var wheelbase = AircraftPerformance.For(type).NoseToMainGearMetres;
                leg = new GroundLeg(new GroundLegPart(new GroundPath(Drivable(gate.TaxiIn, type), limits, 0f, 0f,
                    null, new[] { ApronZone(type), StandLeadInZone }), tailFirst: false, trackMetres: wheelbase));
                TaxiInLegs[key] = leg;
            }

            return leg;
        }

        private static GroundSpeedZone ApronZone(AircraftType type) =>
            new(GroundSpeedLimits.ApronMetres, CircuitProfile.Knots(GroundSpeedLimits.ApronKnotsFor(type)));

        /// <summary>5 kt for the last stretch onto the stand line.</summary>
        private static GroundSpeedZone StandLeadInZone =>
            new(GroundSpeedLimits.StandLeadInMetres, CircuitProfile.Knots(GroundSpeedLimits.StandLeadInKnots));

        private static float[] CleanTaxiOut(float[] xz) => TaxiPathCleanup.WithoutInitialHook(xz);

        /// <summary>
        /// ADR 0146: a tug push built for this runway's taxi route (straight back, the tail swung away
        /// from the taxi direction on the aircraft's own turning radius), and the taxi-out that starts
        /// where it ends. Falls back to the baked pair where the geometry does not suit.
        /// </summary>
        private static (float[] Push, float[] Taxi) PushAndTaxi(float stopX, float stopZ, float heading,
            float[] bakedPush, float[] bakedTaxi, float[] hint, AircraftType type)
        {
            var radius = Math.Max(12f, AircraftPerformance.For(type).NoseToMainGearMetres * 1.4f);
            return PushbackGeometry.TryBuild(stopX, stopZ, heading, bakedTaxi, hint, radius, out var push, out var taxi)
                ? (push, taxi)
                : (bakedPush, bakedTaxi);
        }

        /// <summary>
        /// The baked apron routes contain tight hooks, spurs and loops (a 28 m out-and-back at the
        /// start of a gate route, a loop just before a gate) that a nose-steered turboprop can swallow
        /// but a wheelbase-tracked widebody turns into a 150° swing in two seconds. Round them off to
        /// what the airframe can actually drive: no tighter than about its own wheelbase.
        /// </summary>
        /// <summary>
        /// Walk-out stands (AIP PADAP02 10A–10D, 2A): the painted line ends in a tight U-turn
        /// so the Saab stops facing out. Relaxing that loop to the taxi turn radius cut it off,
        /// so the aircraft arrived 20–100° off its parked heading and snapped round on the stand,
        /// and again when the pushback began. Their stand line is followed as painted.
        /// </summary>
        private static bool IsWalkOut(AdelaideBay bay) =>
            bay.Reference is "10A" or "10B" or "10C" or "10D" or "2A";

        private static float[] BayTaxiIn(AdelaideBay bay, float[] taxiIn, AircraftType type)
        {
            if (!IsWalkOut(bay) || bay.Pushback.Length < 4)
                return Drivable(taxiIn, type);

            // The pushback is the stand line reversed: walk back from the stop while the
            // taxi-in is still on it. The route may end a point short of the line's start.
            var split = taxiIn.Length / 2;
            for (var i = taxiIn.Length / 2 - 1; i >= 0; i--)
            {
                if (AdelaideCrossRoutes.DistanceToPolyline(bay.Pushback, taxiIn[i * 2], taxiIn[i * 2 + 1], out _) > 0.5f)
                    break;
                split = i;
            }

            if (split < 1 || split >= taxiIn.Length / 2 - 1)
                return Drivable(taxiIn, type);
            var route = new float[(split + 1) * 2];
            Array.Copy(taxiIn, route, route.Length);
            var standLine = new float[taxiIn.Length - split * 2];
            Array.Copy(taxiIn, split * 2, standLine, 0, standLine.Length);
            return GroundPathSmoothing.Join(Drivable(route, type), standLine);
        }

        private static float[] BayPushback(AdelaideBay bay, AircraftType type) =>
            IsWalkOut(bay) ? bay.Pushback : DrivablePushback(bay.Pushback, type);

        private static float[] Drivable(float[] xz, AircraftType type)
        {
            var wheelbase = AircraftPerformance.For(type).NoseToMainGearMetres;
            return GroundPathSmoothing.RelaxTightTurns(xz, Math.Max(20f, wheelbase * 0.9f));
        }

        /// <summary>
        /// Pushback is slow (3 kt) but still hits authored OSM corners that are sharper than a
        /// tug can turn. Soften them without the taxi min-radius — a 20 m floor would erase the
        /// real push arc onto the taxiway. Endpoints stay put so the stand stop and taxi join hold.
        /// </summary>
        private static float[] DrivablePushback(float[] xz, AircraftType type)
        {
            var wheelbase = AircraftPerformance.For(type).NoseToMainGearMetres;
            return GroundPathSmoothing.RelaxTightTurns(xz, Math.Max(12f, wheelbase * 0.55f));
        }

        private static float[] TaxiOutPath(AdelaideBay bay, RunwayDirection runway) =>
            runway switch
            {
                RunwayDirection.Runway23 => bay.TaxiOut23,
                RunwayDirection.Runway12 => AdelaideCrossRoutes.TaxiOutFrom(bay.TaxiOut[0], bay.TaxiOut[1],
                    RunwayDirection.Runway12),
                RunwayDirection.Runway30 => AdelaideCrossRoutes.TaxiOutFrom(bay.TaxiOut[0], bay.TaxiOut[1],
                    RunwayDirection.Runway30),
                _ => bay.TaxiOut
            };

        private static float[] TaxiOutPath(AdelaideTerminalGate gate, RunwayDirection runway) =>
            runway switch
            {
                RunwayDirection.Runway23 => gate.TaxiOut23,
                RunwayDirection.Runway12 => AdelaideCrossRoutes.TaxiOutFrom(gate.TaxiOut[0], gate.TaxiOut[1],
                    RunwayDirection.Runway12),
                RunwayDirection.Runway30 => AdelaideCrossRoutes.TaxiOutFrom(gate.TaxiOut[0], gate.TaxiOut[1],
                    RunwayDirection.Runway30),
                _ => gate.TaxiOut
            };

        private static float[] VacatePolyline(RunwayDirection runway) => runway switch
        {
            RunwayDirection.Runway23 => AdelaideLayout.Vacate23,
            RunwayDirection.Runway12 => AdelaideCrossRoutes.Vacate(RunwayDirection.Runway12),
            RunwayDirection.Runway30 => AdelaideCrossRoutes.Vacate(RunwayDirection.Runway30),
            _ => AdelaideLayout.Vacate
        };

        /// <summary>Entered rolling at the runway exit speed the landing ends at, not from a stop.</summary>
        private static GroundPath VacatePath => VacatePathFor(AircraftType.Atr42, RunwayDirection.Runway05);

        private static GroundPath VacatePathFor(AircraftType type)
            => VacatePathFor(type, RunwayDirection.Runway05);

        private static GroundPath VacatePathFor(AircraftType type, RunwayDirection runway)
        {
            var key = (type?.Id ?? "ATR42") + "/" + runway;
            if (!VacatePaths.TryGetValue(key, out var path))
            {
                var performance = AircraftPerformance.For(type);
                path = new GroundPath(VacatePolyline(runway),
                    GroundSpeedLimits.TaxiFor(type),
                    entrySpeed: CircuitProfile.Knots(performance.RunwayExitKnots));
                VacatePaths[key] = path;
            }
            return path;
        }
    }
}
