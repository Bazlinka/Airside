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
        }

        public string Name { get; }
        public IReadOnlyList<StableId> SegmentIds { get; }
        public IReadOnlyList<TaxiPoint> Points { get; }
    }

    public sealed class AirportTaxiNetwork
    {
        /// <summary>
        /// How much larger the airfield is than the coordinates it was first laid out
        /// in. The paved surfaces, stand pitch and taxi distances were authored around
        /// a 5.6-unit proxy aircraft; the art kit's aircraft has a 15 wingspan, so the
        /// world is scaled to match it (REF-005 proportions: runway ~1.1x span,
        /// taxiway ~0.6x span, stand pitch ~1.25x span). Layout numbers below are
        /// written in the original units and multiplied by this once, so the airfield
        /// stays readable as a layout.
        /// </summary>
        public const float WorldScale = 2.68f;

        public static readonly StableId AlphaOne = new("TAXI-A1");
        public static readonly StableId AlphaTwo = new("TAXI-A2");
        public static readonly StableId StandOneLeadIn = new("LEAD-IN-1");
        public static readonly StableId StandTwoLeadIn = new("LEAD-IN-2");
        public static readonly StableId StandThreeLeadIn = new("LEAD-IN-3");

        // A single-file corridor covering the shared A1/A2 taxiway. Ground-traffic
        // aircraft reserve it for the whole time they are on A1 or A2, so only one
        // of them is ever on the corridor at once and they can never meet head-on.
        // The primary flight does not use it — it has priority on the segments
        // themselves.
        public static readonly StableId Corridor = new("TAXI-CORRIDOR");

        public TaxiRoute RouteTo(StableId stand)
        {
            if (stand.Equals(AirportSimulation.StandOne))
                return Create("A1 → A2 → Stand 1", StandOneLeadIn, StandZ(stand));
            if (stand.Equals(AirportSimulation.StandTwo))
                return Create("A1 → A2 → Stand 2", StandTwoLeadIn, StandZ(stand));
            if (stand.Equals(AirportSimulation.StandThree))
                return Create("A1 → A2 → Stand 3", StandThreeLeadIn, StandZ(stand));
            throw new ArgumentOutOfRangeException(nameof(stand), "Stand is not connected to the taxi network.");
        }

        /// <summary>Stand centres in layout units — pitch 7 is 18.8 world, comfortably
        /// clear of the aircraft's 15 wingspan.</summary>
        public const float StandOneLayoutZ = 14f;
        public const float StandPitch = 7f;

        public static float StandZ(StableId stand)
        {
            if (stand.Equals(AirportSimulation.StandOne)) return StandOneLayoutZ * WorldScale;
            if (stand.Equals(AirportSimulation.StandTwo)) return (StandOneLayoutZ + StandPitch) * WorldScale;
            if (stand.Equals(AirportSimulation.StandThree)) return (StandOneLayoutZ + StandPitch * 2f) * WorldScale;
            throw new ArgumentOutOfRangeException(nameof(stand));
        }

        public static StableId LeadInFor(StableId stand)
        {
            if (stand.Equals(AirportSimulation.StandOne)) return StandOneLeadIn;
            if (stand.Equals(AirportSimulation.StandTwo)) return StandTwoLeadIn;
            if (stand.Equals(AirportSimulation.StandThree)) return StandThreeLeadIn;
            throw new ArgumentOutOfRangeException(nameof(stand));
        }

        private static TaxiRoute Create(string name, StableId leadIn, float standZ)
        {
            const float s = WorldScale;
            return new TaxiRoute(
                name,
                new[] { AlphaOne, AlphaTwo, leadIn },
                new[]
                {
                    new TaxiPoint(-24f * s, 0f * s),
                    new TaxiPoint(-12f * s, 9f * s),
                    new TaxiPoint(8f * s, 9f * s),
                    new TaxiPoint(17f * s, standZ)   // standZ is already in world units
                });
        }
    }
}
