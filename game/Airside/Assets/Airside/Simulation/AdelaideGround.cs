using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// The ground legs fleet aircraft drive at Adelaide, built from the real routes in
    /// <see cref="AdelaideLayout"/> with ATR ground-speed limits (ADR 0045). Every ground
    /// duration the airline simulation uses comes from here, so time on the ground is
    /// the time the motion really takes — nothing is a picked number.
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
        private static GroundPath _vacate;
        private static GroundLeg _vacateLeg;
        private static GroundLeg _lineupLeg;

        public static IReadOnlyList<AdelaideBay> Bays => AdelaideLayout.Bays;

        public static AdelaideBay Bay(StableId stand)
        {
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

        /// <summary>Pushback tail-first onto T4, tug disconnect, then taxi to the runway 05 holding point.</summary>
        public static GroundLeg TaxiOut(StableId stand)
        {
            var bay = Bay(stand);
            if (!TaxiOutLegs.TryGetValue(bay.Id, out var leg))
            {
                leg = new GroundLeg(
                    new GroundLegPart(new GroundPath(bay.Pushback, GroundSpeedLimits.Pushback), tailFirst: true),
                    new GroundLegPart(new GroundPath(bay.TaxiOut, GroundSpeedLimits.Taxi, 0f, 0f,
                        new[] { ApronZone }, null), tailFirst: false, TugDisconnectSeconds));
                TaxiOutLegs[bay.Id] = leg;
            }

            return leg;
        }

        /// <summary>E2 holding point → nose into the assigned bay.</summary>
        public static GroundLeg TaxiIn(StableId stand)
        {
            var bay = Bay(stand);
            if (!TaxiInLegs.TryGetValue(bay.Id, out var leg))
            {
                leg = new GroundLeg(new GroundLegPart(new GroundPath(bay.TaxiIn, GroundSpeedLimits.Taxi, 0f, 0f,
                    null, new[] { ApronZone, StandLeadInZone }), tailFirst: false));
                TaxiInLegs[bay.Id] = leg;
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

        /// <summary>10 kt on the apron lane beside the bays.</summary>
        private static GroundSpeedZone ApronZone =>
            new(GroundSpeedLimits.ApronMetres, CircuitProfile.Knots(GroundSpeedLimits.ApronKnots));

        /// <summary>5 kt for the last stretch onto the stand line.</summary>
        private static GroundSpeedZone StandLeadInZone =>
            new(GroundSpeedLimits.StandLeadInMetres, CircuitProfile.Knots(GroundSpeedLimits.StandLeadInKnots));

        /// <summary>Entered rolling at the runway exit speed the landing ends at, not from a stop.</summary>
        private static GroundPath VacatePath => _vacate ??= new GroundPath(AdelaideLayout.Vacate, GroundSpeedLimits.Taxi,
            entrySpeed: CircuitProfile.Knots(CircuitProfile.RunwayExitKnots));
    }
}
