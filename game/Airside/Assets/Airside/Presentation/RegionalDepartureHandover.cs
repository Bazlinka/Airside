using System;
using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// The hand-over from a regional runway departure to the aircraft's route (the return leg from an outstation).
    /// The departure model flies the ground roll and climb-out for <see cref="RegionalFlightPath.DepartureSeconds"/>;
    /// the route profile assumes the aircraft was already at climb speed from the first second, so at the hand-over the
    /// two disagree by several kilometres. That gap is closed by easing the departure's offset away. Closing it in a
    /// fixed two minutes lent the aircraft the closing speed on top of its route speed, and a 3 km gap took a turboprop
    /// to 308 kt IAS at 2,700 ft (finding 13). The ease now spends only part of the speed the type has in hand below its
    /// cap (250 kt calibrated below 10,000 ft, <see cref="FlightSpeedEnvelope.MaximumCasKnots"/>) and lasts as long as that
    /// needs. Position only: the leg's timing, its route and the arrival time are unchanged.
    /// </summary>
    public static class RegionalDepartureHandover
    {
        /// <summary>Shortest ease (the original fixed hand-over), seconds.</summary>
        public const double MinimumBlendSeconds = 120.0;

        /// <summary>The ease never runs past this many seconds after lift-off, whatever the gap.</summary>
        public const double MaximumEndSeconds = 3000.0;

        /// <summary>Stay a little under the cap so sampling between profile points never touches it.</summary>
        public const double CapMargin = 0.96;

        /// <summary>
        /// Shares of the headroom under the cap the ease may use, tried in turn: the gentlest that closes the gap in time wins.
        /// </summary>
        private static readonly double[] HeadroomShares = { 0.6, 0.8, 1.0 };

        /// <summary>
        /// The arrival at the home field bends the route onto the drawn final over its last tens of kilometres; the ease
        /// must be over this long before the route's profile ends, so the two never overlap.
        /// </summary>
        public const double ArrivalBlendReserveSeconds = 450.0;

        private const double Step = 5.0;
        private const double TaperSeconds = 30.0;
        private static readonly Dictionary<(string, double, double, long, long), Plan> Cache = new();

        /// <summary>The ease for this leg: how much of the departure's offset is still carried at each second.</summary>
        public static Plan PlanFor(AircraftType type, EnrouteProfile route, double lagSeconds, double offsetMetres)
        {
            var key = (type.Id, route.LegSeconds, route.LegMetres, (long)Math.Round(offsetMetres / 10.0), (long)Math.Round(lagSeconds));
            if (Cache.TryGetValue(key, out var found))
                return found;
            var plan = Build(type, route, lagSeconds, Math.Abs(offsetMetres));
            if (Cache.Count >= 64)
                Cache.Clear();
            Cache[key] = plan;
            return plan;
        }

        private static Plan Build(AircraftType type, EnrouteProfile route, double lagSeconds, double gap)
        {
            var start = RegionalFlightPath.DepartureSeconds;
            if (gap < 1.0)
                return new Plan(start, Step, new[] { 0.0, 0.0 }, MinimumBlendSeconds);
            var latest = Math.Min(MaximumEndSeconds, route.LegSeconds - ArrivalBlendReserveSeconds);
            latest = Math.Max(start + MinimumBlendSeconds, latest);
            var count = (int)Math.Floor((latest - start) / Step) + 1;
            var room = new double[count];
            for (var i = 0; i < count; i++)
            {
                var t = start + i * Step;
                room[i] = Math.Max(0.0, CapMetresPerSecond(type, route, lagSeconds, t)
                                        - route.GroundSpeedKnotsAt(t) * CircuitProfile.KnotsToMetresPerSecond);
            }

            // The gentlest share of the headroom that carries the gap away before the latest end.
            foreach (var share in HeadroomShares)
            {
                var headroom = new double[count];
                for (var i = 0; i < count; i++)
                    headroom[i] = room[i] * share;
                for (var end = (int)Math.Ceiling(MinimumBlendSeconds / Step); end < count; end++)
                {
                    var sums = Cumulative(headroom, end);
                    if (sums[end] >= gap)
                        return Finish(start, sums, end);
                }
            }

            // The headroom cannot carry it: spread it evenly over the whole window, the smallest peak added speed there is
            // (a long leg whose route already runs at the cap, never worse than the old two-minute ease).
            var even = new double[count];
            for (var i = 0; i < count; i++)
                even[i] = 1.0;
            return Finish(start, Cumulative(even, count - 1), count - 1);
        }

        private static Plan Finish(double start, double[] cumulative, int end)
        {
            var total = cumulative[end];
            var keep = new double[end + 1];
            for (var i = 0; i <= end; i++)
                keep[i] = 1.0 - cumulative[i] / total;
            keep[end] = 0.0;
            return new Plan(start, Step, keep, end * Step);
        }

        /// <summary>Running total of the headroom, tapered in over the first seconds and out over the last.</summary>
        private static double[] Cumulative(double[] headroom, int end)
        {
            var sums = new double[headroom.Length];
            var taper = (int)Math.Max(1, TaperSeconds / Step);
            for (var i = 1; i <= end && i < headroom.Length; i++)
            {
                var mid = Math.Min(headroom[i - 1], headroom[i]);
                var weight = Smooth(i / (double)taper) * Smooth((end - (i - 0.5)) / taper);
                sums[i] = sums[i - 1] + mid * weight * Step;
            }

            for (var i = end + 1; i < headroom.Length; i++)
                sums[i] = sums[end];
            return sums;
        }

        private static double Smooth(double value)
        {
            var u = Math.Max(0.0, Math.Min(1.0, value));
            return u * u * (3.0 - 2.0 * u);
        }

        /// <summary>The fastest ground speed the type may fly at this moment of the route, in metres per second.</summary>
        public static double CapMetresPerSecond(AircraftType type, EnrouteProfile route, double lagSeconds, double elapsedSeconds)
        {
            var feet = RegionalFlightPath.ClimbAltitudeFeet(route, elapsedSeconds, lagSeconds);
            var metres = FlightAtmosphere.AltitudeMetres(feet);
            // The rule at stake is the 250 kt calibrated cap below 10,000 ft. Above it a climbing aircraft may go faster
            // (jets accelerate to their climb speed), but a turboprop's own limit can sit below what its route already
            // flies, so never allow less than 250 there: the ease then has a little room instead of none.
            var cas = FlightSpeedEnvelope.MaximumCasKnots(type, metres * EnrouteProfile.FeetPerMetre);
            if (feet > EnrouteProfile.TransitionAltitudeFeet)
                cas = Math.Max(cas, FlightSpeedEnvelope.MaxCasBelowTenThousandKnots);
            return FlightAtmosphere.TrueKnots(cas, metres) * CapMargin * CircuitProfile.KnotsToMetresPerSecond;
        }

        /// <summary>How much of the departure's offset the route position still carries.</summary>
        public sealed class Plan
        {
            private readonly double _start, _step;
            private readonly double[] _keep;

            public Plan(double start, double step, double[] keep, double durationSeconds)
            {
                _start = start;
                _step = step;
                _keep = keep;
                DurationSeconds = durationSeconds;
            }

            /// <summary>Seconds after the departure ends until the offset is gone.</summary>
            public double DurationSeconds { get; }

            /// <summary>Seconds after lift-off when the ease is over.</summary>
            public double EndSeconds => _start + DurationSeconds;

            /// <summary>True while the route position still carries part of the departure's offset.</summary>
            public bool Active(double elapsedSeconds) => elapsedSeconds < EndSeconds;

            /// <summary>1 at the end of the departure, 0 when the ease is over (continuous, eased at both ends).</summary>
            public double Keep(double elapsedSeconds)
            {
                var u = (elapsedSeconds - _start) / _step;
                if (u <= 0.0)
                    return 1.0;
                var i = (int)Math.Floor(u);
                if (i >= _keep.Length - 1)
                    return 0.0;
                var f = u - i;
                return _keep[i] + (_keep[i + 1] - _keep[i]) * f;
            }
        }
    }
}
