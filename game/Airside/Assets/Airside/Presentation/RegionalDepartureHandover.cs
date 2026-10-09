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
        public const double CapMargin = 0.97;

        /// <summary>Share of the headroom under the cap the ease may use.</summary>
        public const double HeadroomShare = 0.7;

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
            // The landing terminal blend begins this long before touchdown; the ease must be over before it.
            var latest = Math.Min(MaximumEndSeconds, route.LegSeconds - RegionalFlightPath.TerminalSeconds - 420.0);
            latest = Math.Max(start + MinimumBlendSeconds, latest);
            var count = (int)Math.Ceiling((latest - start) / Step) + 1;
            var headroom = new double[count];
            for (var i = 0; i < count; i++)
            {
                var t = start + i * Step;
                var room = CapMetresPerSecond(type, route, lagSeconds, t) - route.GroundSpeedKnotsAt(t) * CircuitProfile.KnotsToMetresPerSecond;
                headroom[i] = Math.Max(0.0, room) * HeadroomShare;
            }

            if (gap < 1.0)
                return new Plan(start, Step, new[] { 0.0, 0.0 }, MinimumBlendSeconds);

            // Smallest end time whose tapered headroom adds up to the gap; failing that, the latest end, scaled to close.
            var best = -1;
            double[] cumulative = null;
            for (var end = (int)Math.Ceiling(MinimumBlendSeconds / Step); end < count; end++)
            {
                var sums = Cumulative(headroom, end);
                if (sums[end] >= gap)
                {
                    best = end;
                    cumulative = sums;
                    break;
                }
            }

            if (best < 0)
            {
                best = count - 1;
                cumulative = Cumulative(headroom, best);
                if (cumulative[best] < 1e-6)
                {
                    // No headroom at all: close at an even pace rather than never.
                    for (var i = 0; i < count; i++)
                        cumulative[i] = gap * i / best;
                }
            }

            var total = cumulative[best];
            var keep = new double[best + 1];
            for (var i = 0; i <= best; i++)
                keep[i] = 1.0 - cumulative[i] / total;
            keep[best] = 0.0;
            return new Plan(start, Step, keep, best * Step);
        }

        /// <summary>Running total of the headroom, tapered in over the first seconds and out over the last.</summary>
        private static double[] Cumulative(double[] headroom, int end)
        {
            var sums = new double[headroom.Length];
            var taper = (int)Math.Max(1, TaperSeconds / Step);
            for (var i = 1; i <= end && i < headroom.Length; i++)
            {
                var mid = 0.5 * (headroom[i - 1] + headroom[i]);
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
            var cas = FlightSpeedEnvelope.MaximumCasKnots(type, metres * EnrouteProfile.FeetPerMetre);
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
