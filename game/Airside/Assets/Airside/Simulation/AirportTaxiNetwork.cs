using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;

namespace Airside.Simulation
{
    public readonly struct TaxiPoint
    {
        public TaxiPoint(float x, float z) { X = x; Z = z; }
        public float X { get; }
        public float Z { get; }

        public float DistanceTo(TaxiPoint other)
        {
            var dx = other.X - X;
            var dz = other.Z - Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }

    public sealed class TaxiRoute
    {
        public TaxiRoute(string name, IEnumerable<StableId> segmentIds, IEnumerable<TaxiPoint> points)
        {
            Name = name;
            SegmentIds = segmentIds.ToArray();
            Points = points.ToArray();
            if (SegmentIds.Count == 0 || Points.Count != SegmentIds.Count + 1)
                throw new ArgumentException("A taxi route needs one more point than segment.");

            var lengths = new float[SegmentIds.Count];
            var total = 0f;
            for (var i = 0; i < SegmentIds.Count; i++)
            {
                lengths[i] = Math.Max(0.001f, Points[i].DistanceTo(Points[i + 1]));
                total += lengths[i];
            }

            SegmentLengths = lengths;
            TotalLength = total;
        }

        public string Name { get; }
        public IReadOnlyList<StableId> SegmentIds { get; }
        public IReadOnlyList<TaxiPoint> Points { get; }
        public IReadOnlyList<float> SegmentLengths { get; }
        public float TotalLength { get; }

        /// <summary>
        /// Maps 0..1 phase progress to a segment index using chord length so short
        /// lead-ins are not as slow as long Alpha legs.
        ///
        /// Reverse travel covers the same chords from the other end, so its distance is
        /// measured from the far end of the route. Mirroring only the index made an
        /// outbound aircraft spend the long Alpha leg's share of the phase on the short
        /// lead-in, then cover Alpha in the lead-in's share — a tenfold speed swing, and
        /// segment reservations that did not line up with where the aircraft was.
        /// </summary>
        public int SegmentIndexAt(double progress, bool reverse)
        {
            return ForwardSegmentIndex(reverse ? 1d - progress : progress);
        }

        public int ForwardSegmentIndex(double progress)
        {
            var count = SegmentIds.Count;
            var clamped = Math.Max(0d, Math.Min(0.999999d, progress));
            var distance = clamped * TotalLength;
            var accrued = 0f;
            for (var forward = 0; forward < count; forward++)
            {
                accrued += SegmentLengths[forward];
                if (distance < accrued)
                    return forward;
            }

            return count - 1;
        }

        /// <summary>0..1 progress within the current forward distance window.</summary>
        public float LocalT(double progress)
        {
            var count = SegmentIds.Count;
            var clamped = Math.Max(0d, Math.Min(0.999999d, progress));
            var distance = (float)(clamped * TotalLength);
            var accrued = 0f;
            for (var forward = 0; forward < count; forward++)
            {
                var next = accrued + SegmentLengths[forward];
                if (distance < next || forward == count - 1)
                    return Math.Max(0f, Math.Min(1f, (distance - accrued) / SegmentLengths[forward]));

                accrued = next;
            }

            return 1f;
        }
    }

    public sealed class AirportTaxiNetwork
    {
        public static readonly StableId AlphaOne = new("TAXI-A1");
        public static readonly StableId AlphaTwo = new("TAXI-A2");
        public static readonly StableId StandOneLeadIn = new("LEAD-IN-1");
        public static readonly StableId StandTwoLeadIn = new("LEAD-IN-2");
        public static readonly StableId StandThreeLeadIn = new("LEAD-IN-3");

        // Shared apron throat covering the Alpha→stand dogleg so lead-ins cannot
        // clip each other or a parked neighbour without a reservation conflict.
        public static readonly StableId ApronThroat = new("APRON-THROAT");

        // Off-centreline run-up bay used by GT-202 — not the live A2 centreline.
        public static readonly StableId RunUpBay = new("RUN-UP-BAY");

        // A single-file corridor covering the shared A1/A2 taxiway. Ground-traffic
        // and commercial taxi both claim it so opposite-direction meetings cannot
        // happen on Alpha.
        public static readonly StableId Corridor = new("TAXI-CORRIDOR");

        // Canonical waypoints shared by commercial routes and ground-traffic circuits.
        public static readonly TaxiPoint RunwayEnd = new(-24f, 0f);
        public static readonly TaxiPoint Junction = new(-12f, 9f);
        public static readonly TaxiPoint AlphaEnd = new(8f, 9f);
        public static readonly TaxiPoint OffFieldExit = new(-36f, -4f);
        public static readonly TaxiPoint AwayHold = new(-60f, -30f);
        public static readonly TaxiPoint RunUpBayPoint = new(5f, 13f);

        /// <summary>
        /// Distance from the runway centreline to the A1 holding position, in metres.
        /// An arrival has not vacated the runway until it is past this point, and a
        /// departure waits here for its clearance. Presentation reads the same number so
        /// the painted hold-short bar and the reservation boundary cannot drift apart.
        /// </summary>
        public const float RunwayHoldingPositionZ = 6.5f;

        /// <summary>Seconds GT spends on each Alpha leg — shared pacing with commercials.</summary>
        public const long AlphaLegSeconds = 14;
        public const long LeadInLegSeconds = 10;
        public const long RunUpHoldSeconds = 18;

        public TaxiRoute RouteTo(StableId stand)
        {
            if (stand.Equals(AirportSimulation.StandOne))
                return Create("A1 → A2 → throat → Stand 1", StandOneLeadIn, StandZ(stand));
            if (stand.Equals(AirportSimulation.StandTwo))
                return Create("A1 → A2 → throat → Stand 2", StandTwoLeadIn, StandZ(stand));
            if (stand.Equals(AirportSimulation.StandThree))
                return Create("A1 → A2 → throat → Stand 3", StandThreeLeadIn, StandZ(stand));
            throw new ArgumentOutOfRangeException(nameof(stand), "Stand is not connected to the taxi network.");
        }

        /// <summary>
        /// Stand centres are ≥10 m apart so turboprop half-spans (~3.9 m) do not
        /// wingtip-interpenetrate when dual commercials / GT park adjacent.
        /// </summary>
        public static float StandZ(StableId stand)
        {
            if (stand.Equals(AirportSimulation.StandOne)) return 14f;
            if (stand.Equals(AirportSimulation.StandTwo)) return 24f;
            if (stand.Equals(AirportSimulation.StandThree)) return 34f;
            throw new ArgumentOutOfRangeException(nameof(stand));
        }

        public static StableId LeadInFor(StableId stand)
        {
            if (stand.Equals(AirportSimulation.StandOne)) return StandOneLeadIn;
            if (stand.Equals(AirportSimulation.StandTwo)) return StandTwoLeadIn;
            if (stand.Equals(AirportSimulation.StandThree)) return StandThreeLeadIn;
            throw new ArgumentOutOfRangeException(nameof(stand));
        }

        /// <summary>
        /// Forward route progress at which the aircraft crosses the runway holding
        /// position on the first segment. Inbound, this is where the runway is finally
        /// vacated; outbound, it is where a departure stops and waits.
        /// </summary>
        public static float RunwayHoldingProgress(TaxiRoute route)
        {
            if (route == null)
                throw new ArgumentNullException(nameof(route));

            var entry = route.Points[0];
            var next = route.Points[1];
            var span = next.Z - entry.Z;
            var f = Math.Abs(span) < 0.0001f
                ? 1f
                : (RunwayHoldingPositionZ - entry.Z) / span;
            f = Math.Max(0f, Math.Min(1f, f));
            return Math.Min(1f, f * route.SegmentLengths[0] / route.TotalLength);
        }

        public static TaxiPoint StandPoint(StableId stand) => new(17f, StandZ(stand));

        public static TaxiPoint ThroatPoint(StableId stand) => new(12f, StandZ(stand));

        private static TaxiRoute Create(string name, StableId leadIn, float standZ)
        {
            // Dogleg: remain on Alpha to (8,9), then north/south to the stand Z at
            // x=12 before entering the stand box at x=17 — clears neighbouring stands.
            return new TaxiRoute(
                name,
                new[] { AlphaOne, AlphaTwo, ApronThroat, leadIn },
                new[]
                {
                    RunwayEnd,
                    Junction,
                    AlphaEnd,
                    new TaxiPoint(12f, standZ),
                    new TaxiPoint(17f, standZ)
                });
        }
    }
}
