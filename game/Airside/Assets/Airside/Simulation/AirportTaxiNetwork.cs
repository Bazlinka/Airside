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
        public static readonly StableId AlphaOne = new("TAXI-A1");
        public static readonly StableId AlphaTwo = new("TAXI-A2");
        public static readonly StableId StandOneLeadIn = new("LEAD-IN-1");
        public static readonly StableId StandTwoLeadIn = new("LEAD-IN-2");

        public TaxiRoute RouteTo(StableId stand)
        {
            if (stand.Equals(AirportSimulation.StandOne))
                return Create("A1 → A2 → Stand 1", StandOneLeadIn, 14f);
            if (stand.Equals(AirportSimulation.StandTwo))
                return Create("A1 → A2 → Stand 2", StandTwoLeadIn, 20f);
            throw new ArgumentOutOfRangeException(nameof(stand), "Stand is not connected to the taxi network.");
        }

        private static TaxiRoute Create(string name, StableId leadIn, float standZ)
        {
            return new TaxiRoute(
                name,
                new[] { AlphaOne, AlphaTwo, leadIn },
                new[] { new TaxiPoint(-24f, 0f), new TaxiPoint(-12f, 9f), new TaxiPoint(8f, 9f), new TaxiPoint(17f, standZ) });
        }
    }
}
