using System;

namespace Airside.Simulation
{
    /// <summary>
    /// Where holding and go-around traffic sit in the Adelaide visual circuit — a right-hand
    /// racetrack south of runway 05, kept off the terminal. UnityEngine-free so the headless
    /// harness can lock the path; presentation only turns these metres into a drawn pose.
    ///
    /// The simulation still owns who is holding and who goes around. This file never
    /// reserves the runway; it only says where a visible aircraft is.
    /// </summary>
    public static class CircuitTraffic
    {
        /// <summary>One lap of the holding racetrack, matching <see cref="AirlineOperations.GoAroundCircuitSeconds"/>.</summary>
        public const double LoopSeconds = AirlineOperations.GoAroundCircuitSeconds;

        /// <summary>~1 000 ft AGL, a typical turboprop circuit height.</summary>
        public const double CircuitHeightMetres = 305.0;

        /// <summary>Radius of each 180° cap; the downwind leg sits 2× this south of the runway.</summary>
        public const double TurnRadiusMetres = 900.0;

        /// <summary>Fraction of the lap each holding aircraft is spaced behind the one in front.</summary>
        public const double SlotSpacing = 0.22;

        public const double WestX = CircuitProfile.ApproachStartX + 400.0;
        public const double EastX = -CircuitProfile.WestThresholdX + 400.0;

        public static double StraightMetres => EastX - WestX;
        public static double ArcMetres => Math.PI * TurnRadiusMetres;
        public static double LapMetres => 2.0 * StraightMetres + 2.0 * ArcMetres;

        /// <summary>0..1 around the racetrack for a holding aircraft, looping on the clock so a
        /// go-around that rejoins does not freeze at the end of a duration.</summary>
        public static double HoldingProgress(double nowSeconds, int queueSlot)
        {
            var phase = nowSeconds / LoopSeconds + Math.Max(0, queueSlot) * SlotSpacing;
            phase -= Math.Floor(phase);
            if (phase < 0)
                phase += 1;
            return phase;
        }

        /// <summary>0..1 through a go-around: one missed-approach climb then one circuit lap.</summary>
        public static double GoAroundProgress(double elapsedSeconds) =>
            Clamp01(elapsedSeconds / LoopSeconds);

        public static void Holding(double nowSeconds, int queueSlot, out double x, out double y, out double z) =>
            OnLap(HoldingProgress(nowSeconds, queueSlot) * LapMetres, CircuitHeightMetres, out x, out y, out z);

        /// <summary>
        /// Missed approach from short final: climb along the upwind, then fly the same
        /// racetrack the holders use. Starts on the glideslope, arrives at circuit height
        /// before the east turn. Coordinates are in the 05 circuit frame.
        /// </summary>
        public static void GoAround(double elapsedSeconds, out double x, out double y, out double z)
        {
            var t = GoAroundProgress(elapsedSeconds);
            var startDistance = DistanceAlongUpwind(CircuitProfile.ShortFinalX);
            var distance = startDistance + t * LapMetres;
            if (distance >= LapMetres)
                distance -= LapMetres;

            var climb = Clamp01(t / 0.28);
            var startHeight = CircuitProfile.ShortFinalHeight;
            var height = startHeight + (CircuitHeightMetres - startHeight) * Smooth(climb);
            OnLap(distance, height, out x, out y, out z);
        }

        /// <summary>
        /// Missed approach in world XZ for the assigned runway. 05/23 reuse the
        /// authored racetrack (23 flipped). 12/30 fly a compact right-hand circuit
        /// in cross-strip local metres — remapping the 05 south racetrack put
        /// regionals through the terminal.
        /// </summary>
        public static void GoAroundOnRunway(double elapsedSeconds, RunwayDirection runway,
            out double x, out double y, out double z)
        {
            if (runway == RunwayDirection.Runway23)
            {
                GoAround(elapsedSeconds, out x, out y, out z);
                x = -x;
                z = -z;
                return;
            }

            if (runway is RunwayDirection.Runway12 or RunwayDirection.Runway30)
            {
                GoAroundCrossLocal(elapsedSeconds, out var localX, out y, out var localZ);
                if (runway == RunwayDirection.Runway30)
                {
                    localX = -localX;
                    localZ = -localZ;
                }

                AdelaideCrossRoutes.LocalToWorld((float)localX, (float)localZ, out var wx, out var wz);
                x = wx;
                z = wz;
                return;
            }

            GoAround(elapsedSeconds, out x, out y, out z);
        }

        /// <summary>Compact right-hand circuit in 12-local metres (along +, lateral − away from apron).</summary>
        private static void GoAroundCrossLocal(double elapsedSeconds, out double along, out double y, out double lateral)
        {
            var t = GoAroundProgress(elapsedSeconds);
            var half = AdelaideCrossRoutes.HalfLength;
            var radius = Math.Min(TurnRadiusMetres, half * 0.45);
            // Upwind starts outside the arrival threshold so the missed approach
            // continues the short final just flown (native metres, not RemapAlong).
            var west = -half - 220.0;
            var east = half - 80.0;
            var straight = Math.Max(200.0, east - west);
            var arc = Math.PI * radius;
            var lap = 2.0 * straight + 2.0 * arc;

            var startDistance = 0.0;
            var distance = startDistance + t * lap;
            if (distance >= lap)
                distance -= lap;

            var climb = Clamp01(t / 0.28);
            var startHeight = CircuitProfile.ShortFinalHeight;
            y = startHeight + (CircuitHeightMetres - startHeight) * Smooth(climb);

            var upwind = straight;
            var eastArc = upwind + arc;
            var downwind = eastArc + straight;

            if (distance <= upwind)
            {
                var u = upwind <= 0 ? 0 : distance / upwind;
                along = west + u * straight;
                lateral = 0;
                return;
            }

            if (distance <= eastArc)
            {
                var u = (distance - upwind) / arc;
                var theta = u * Math.PI;
                along = east + radius * Math.Sin(theta);
                lateral = -radius + radius * Math.Cos(theta);
                return;
            }

            if (distance <= downwind)
            {
                var u = (distance - eastArc) / straight;
                along = east - u * straight;
                lateral = -2.0 * radius;
                return;
            }

            var westArc = (distance - downwind) / arc;
            var phi = Math.PI + westArc * Math.PI;
            along = west + radius * Math.Sin(phi);
            lateral = -radius + radius * Math.Cos(phi);
        }

        public static void OnLap(double distanceMetres, double heightMetres, out double x, out double y, out double z)
        {
            y = heightMetres;
            var d = distanceMetres % LapMetres;
            if (d < 0)
                d += LapMetres;

            var upwind = StraightMetres;
            var eastArc = upwind + ArcMetres;
            var downwind = eastArc + StraightMetres;

            if (d <= upwind)
            {
                var u = upwind <= 0 ? 0 : d / upwind;
                x = WestX + u * StraightMetres;
                z = 0;
                return;
            }

            if (d <= eastArc)
            {
                var u = (d - upwind) / ArcMetres;
                var theta = u * Math.PI;
                x = EastX + TurnRadiusMetres * Math.Sin(theta);
                z = -TurnRadiusMetres + TurnRadiusMetres * Math.Cos(theta);
                return;
            }

            if (d <= downwind)
            {
                var u = (d - eastArc) / StraightMetres;
                x = EastX - u * StraightMetres;
                z = -2.0 * TurnRadiusMetres;
                return;
            }

            var west = (d - downwind) / ArcMetres;
            var phi = Math.PI + west * Math.PI;
            x = WestX + TurnRadiusMetres * Math.Sin(phi);
            z = -TurnRadiusMetres + TurnRadiusMetres * Math.Cos(phi);
        }

        /// <summary>Distance from the west upwind start to a station on the upwind centreline.</summary>
        public static double DistanceAlongUpwind(double x)
        {
            var clamped = Math.Max(WestX, Math.Min(EastX, x));
            return clamped - WestX;
        }

        private static double Clamp01(double value) => value < 0 ? 0 : value > 1 ? 1 : value;

        private static double Smooth(double t)
        {
            var u = Clamp01(t);
            return u * u * (3 - 2 * u);
        }
    }
}
