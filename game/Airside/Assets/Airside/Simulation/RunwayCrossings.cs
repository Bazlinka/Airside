using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>When a taxi leg is on a runway it does not use: seconds into the leg, and which strip.</summary>
    public readonly struct RunwayCrossing
    {
        public RunwayCrossing(bool mainStrip, double enterSeconds, double exitSeconds)
        {
            MainStrip = mainStrip;
            EnterSeconds = enterSeconds;
            ExitSeconds = exitSeconds;
        }

        public bool MainStrip { get; }
        public double EnterSeconds { get; }
        public double ExitSeconds { get; }
    }

    /// <summary>
    /// ADR 0126 — where Adelaide's taxi routes cross a runway. Every 05/23 taxi-in and taxi-out to
    /// 05 crosses 12/30, and taxi-outs to 30 cross 05/23. The tower will not clear a landing or
    /// takeoff while a taxiing aircraft is due on that strip, and ground control will not release
    /// a taxi whose crossing would fall while the strip is busy. Pure and deterministic.
    /// </summary>
    public static class RunwayCrossings
    {
        /// <summary>Runway half-width plus the shoulder: inside this the aircraft is on the strip.</summary>
        public const float StripHalfWidthMetres = 30f;

        private const double ScanSeconds = 1.0;
        private static readonly Dictionary<(GroundLeg, float), CachedCrossings> Cache = new();

        public static bool OnStrip(float x, float z, bool mainStrip, float envelopeMetres = 0f)
        {
            if (mainStrip)
                return Math.Abs(z) < StripHalfWidthMetres + envelopeMetres && Math.Abs(x) < AdelaideLayout.MainRunwayLengthMetres * 0.5f + envelopeMetres;
            var yaw = AdelaideLayout.CrossRunwayYawDegrees * Math.PI / 180.0;
            var dx = x - AdelaideLayout.CrossRunwayCenterX;
            var dz = z - AdelaideLayout.CrossRunwayCenterZ;
            var along = Math.Cos(yaw) * dx - Math.Sin(yaw) * dz;
            var across = Math.Sin(yaw) * dx + Math.Cos(yaw) * dz;
            return Math.Abs(across) < StripHalfWidthMetres + envelopeMetres && Math.Abs(along) < AdelaideLayout.CrossRunwayLengthMetres * 0.5f + envelopeMetres;
        }

        /// <summary>
        /// Crossings of any strip other than <paramref name="ownRunway"/>'s own (a taxi-out ends at its own
        /// runway's holding point; that is not a crossing).
        /// </summary>
        public static IReadOnlyList<RunwayCrossing> For(GroundLeg leg, RunwayDirection ownRunway, AircraftType type = null, bool includeOwnRunway = false)
        {
            var own = RunwayWeather.IsMainRunway(ownRunway);
            // Ground pose is near the nose gear: include the full aft length, half-span and
            // planning margin. A centre-point crossing can free the strip with the tail still on it.
            var envelope = AircraftCatalogue.TryFor(type, out var spec)
                ? (float)Math.Max(spec.LengthMetres, spec.WingspanMetres * 0.5) + 3f : 0f;
            var crossings = All(leg, envelope);
            return includeOwnRunway ? crossings.All : own ? crossings.CrossStrip : crossings.MainStrip;
        }

        // The same taxi leg is queried repeatedly by tower clearance and hold-reason polling.
        // Keep both filtered views alongside the scan rather than allocating on every query.
        private sealed class CachedCrossings
        {
            public readonly RunwayCrossing[] All;
            public readonly RunwayCrossing[] MainStrip;
            public readonly RunwayCrossing[] CrossStrip;

            public CachedCrossings(RunwayCrossing[] all)
            {
                All = all;
                MainStrip = Filter(all, true);
                CrossStrip = Filter(all, false);
            }

            private static RunwayCrossing[] Filter(RunwayCrossing[] all, bool mainStrip)
            {
                var count = 0;
                foreach (var crossing in all)
                    if (crossing.MainStrip == mainStrip)
                        count++;
                if (count == all.Length)
                    return all;
                if (count == 0)
                    return Array.Empty<RunwayCrossing>();
                var result = new RunwayCrossing[count];
                var i = 0;
                foreach (var crossing in all)
                    if (crossing.MainStrip == mainStrip)
                        result[i++] = crossing;
                return result;
            }
        }

        private static CachedCrossings All(GroundLeg leg, float envelope)
        {
            lock (Cache)
            {
                if (Cache.TryGetValue((leg, envelope), out var cached))
                    return cached;
            }

            var found = new List<RunwayCrossing>();
            foreach (var main in new[] { true, false })
            {
                double? enter = null;
                for (var t = 0.0; t <= leg.Seconds + ScanSeconds; t += ScanSeconds)
                {
                    var (x, z) = leg.PositionAt(Math.Min(t, leg.Seconds));
                    var on = OnStrip(x, z, main, envelope);
                    if (on && !enter.HasValue)
                        enter = Math.Max(0, t - ScanSeconds);
                    else if (!on && enter.HasValue)
                    {
                        found.Add(new RunwayCrossing(main, enter.Value, t));
                        enter = null;
                    }
                }

                if (enter.HasValue)
                    found.Add(new RunwayCrossing(main, enter.Value, leg.Seconds));
            }

            var crossings = new CachedCrossings(found.ToArray());
            lock (Cache)
                Cache[(leg, envelope)] = crossings;
            return crossings;
        }
    }
}
