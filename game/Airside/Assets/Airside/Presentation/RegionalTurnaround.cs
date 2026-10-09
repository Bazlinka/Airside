using System;

namespace Airside.Presentation
{
    /// <summary>Which part of an outstation turnaround an aircraft is in.</summary>
    public enum TurnaroundLeg
    {
        TaxiIn,
        Parked,
        TaxiOut
    }

    /// <summary>Where an aircraft is during its turnaround at a regional outstation, in the field's world frame.</summary>
    public readonly struct TurnaroundPose
    {
        public TurnaroundPose(double x, double z, double yawDegrees, TurnaroundLeg leg, double progress01, double speedMetresPerSecond)
        {
            X = x;
            Z = z;
            YawDegrees = yawDegrees;
            Leg = leg;
            Progress01 = progress01;
            SpeedMetresPerSecond = speedMetresPerSecond;
        }

        public double X { get; }
        public double Z { get; }

        /// <summary>Unity yaw: 0 faces +Z, 90 faces +X.</summary>
        public double YawDegrees { get; }
        public TurnaroundLeg Leg { get; }

        /// <summary>How far through the current taxi leg (0..1); 1 while parked.</summary>
        public double Progress01 { get; }
        public double SpeedMetresPerSecond { get; }
    }

    /// <summary>
    /// A point on a mapped apron where an outstation turnaround is spent, and the way the nose faces there.
    /// </summary>
    public readonly struct TurnaroundSpot
    {
        public TurnaroundSpot(double x, double z, double yawDegrees)
        {
            X = x;
            Z = z;
            YawDegrees = yawDegrees;
        }

        public double X { get; }
        public double Z { get; }
        public double YawDegrees { get; }
    }

    /// <summary>
    /// The pose of an aircraft turning round at a regional outstation: after the landing roll it taxis to the
    /// mapped apron, waits there, and taxis back to the runway to start its departure roll where the departure
    /// path begins. A pure function of time into the turnaround (no clock reads, no randomness, no saved state),
    /// so it is frame-rate independent and identical after a save/load. Presentation only: the simulation's
    /// AtDestination timing is untouched.
    /// </summary>
    public static class RegionalTurnaround
    {
        /// <summary>Ground speed on the taxi legs on average (about 10 kt).</summary>
        public const double TaxiMetresPerSecond = 5.0;
        public const double MinimumTaxiSeconds = 20.0;
        public const double MaximumTaxiSeconds = 240.0;

        /// <summary>Each taxi leg may take at most this share of the turnaround, so a short stay still parks.</summary>
        public const double MaximumLegShare = 0.3;

        /// <summary>Tightest turn on the taxi legs, about a turboprop's nose-wheel limit at taxi speed.</summary>
        public const double TurningRadiusMetres = 22.0;

        /// <summary>Seconds a taxi leg of <paramref name="metres"/> takes inside a turnaround of <paramref name="duration"/> seconds.</summary>
        public static double TaxiSeconds(double metres, double duration)
        {
            var wanted = Math.Max(MinimumTaxiSeconds, metres / TaxiMetresPerSecond);
            return Math.Max(0.0, Math.Min(Math.Min(wanted, MaximumTaxiSeconds), duration * MaximumLegShare));
        }

        /// <summary>A stay shorter than this is not worth a taxi: the aircraft just stays where it stopped.</summary>
        public const double MinimumStaySeconds = 120.0;

        /// <summary>
        /// Pose <paramref name="seconds"/> into a turnaround lasting <paramref name="duration"/>.
        /// <paramref name="stopYaw"/> is the way the nose faces when the landing roll ends at (stopX, stopZ);
        /// <paramref name="departYaw"/> is the way it must face at (startX, startZ), where the departure roll begins.
        /// </summary>
        public static TurnaroundPose At(double stopX, double stopZ, double stopYaw, double startX, double startZ,
            double departYaw, TurnaroundSpot spot, double seconds, double duration)
        {
            var span = Math.Max(1.0, duration);
            var t = Math.Max(0.0, Math.Min(seconds, span));
            var inPath = Path(stopX, stopZ, stopYaw, spot.X, spot.Z, spot.YawDegrees);
            var outPath = Path(spot.X, spot.Z, spot.YawDegrees, startX, startZ, departYaw);
            var inSeconds = TaxiSeconds(inPath.Length, span);
            var outSeconds = TaxiSeconds(outPath.Length, span);
            if (inSeconds + outSeconds > span * (2 * MaximumLegShare))
            {
                var scale = span * (2 * MaximumLegShare) / (inSeconds + outSeconds);
                inSeconds *= scale;
                outSeconds *= scale;
            }

            if (inSeconds > 0 && t < inSeconds)
                return Along(inPath, t / inSeconds, inSeconds, TurnaroundLeg.TaxiIn);
            if (outSeconds > 0 && t > span - outSeconds)
                return Along(outPath, (t - (span - outSeconds)) / outSeconds, outSeconds, TurnaroundLeg.TaxiOut);
            return new TurnaroundPose(spot.X, spot.Z, spot.YawDegrees, TurnaroundLeg.Parked, 1.0, 0.0);
        }

        /// <summary>The same path position at a progress value in 0..1 along an eased (smoothstep) taxi.</summary>
        private static TurnaroundPose Along(TaxiPath path, double u, double seconds, TurnaroundLeg leg)
        {
            var eased = u * u * (3.0 - 2.0 * u);
            var speed = path.Length * 6.0 * u * (1.0 - u) / seconds;
            path.PointAtDistance(eased * path.Length, out var x, out var z, out var yaw);
            return new TurnaroundPose(x, z, yaw, leg, u, speed);
        }

        /// <summary>
        /// Chooses where on an apron to park: its centre, the nose toward the nearest terminal point when there
        /// is one (nose-in), otherwise parallel to the runway the aircraft landed on.
        /// </summary>
        public static TurnaroundSpot Spot(double[] apronX, double[] apronZ, double[] terminalX, double[] terminalZ,
            double fallbackYaw)
        {
            if (apronX == null || apronZ == null || apronX.Length == 0 || apronX.Length != apronZ.Length)
                throw new ArgumentException("An apron needs at least one point.");
            double cx = 0, cz = 0;
            for (var i = 0; i < apronX.Length; i++)
            {
                cx += apronX[i];
                cz += apronZ[i];
            }

            cx /= apronX.Length;
            cz /= apronX.Length;
            var yaw = fallbackYaw;
            if (terminalX != null && terminalZ != null && terminalX.Length > 0 && terminalX.Length == terminalZ.Length)
            {
                var best = double.MaxValue;
                for (var i = 0; i < terminalX.Length; i++)
                {
                    var dx = terminalX[i] - cx;
                    var dz = terminalZ[i] - cz;
                    var d = dx * dx + dz * dz;
                    if (d < best && d > 1.0)
                    {
                        best = d;
                        yaw = YawOf(dx, dz);
                    }
                }
            }

            return new TurnaroundSpot(cx, cz, yaw);
        }

        /// <summary>
        /// The headings of a regional strip as <see cref="RegionalFlightPath"/> flies it: the landing roll runs from the
        /// threshold nearest the home field to the far end, and the departure roll runs back the other way.
        /// </summary>
        public static void RunwayYaws(RegionalRunway runway, out double landingYaw, out double departureYaw)
        {
            double ax = runway.Ax, az = runway.Az, bx = runway.Bx, bz = runway.Bz;
            if (bx * bx + bz * bz < ax * ax + az * az)
            {
                (ax, bx) = (bx, ax);
                (az, bz) = (bz, az);
            }

            landingYaw = YawOf(bx - ax, bz - az);
            departureYaw = YawOf(ax - bx, az - bz);
        }

        public static double YawOf(double dx, double dz) => Normalise(Math.Atan2(dx, dz) * 180.0 / Math.PI);

        private static double Normalise(double degrees)
        {
            var value = degrees % 360.0;
            return value < 0 ? value + 360.0 : value;
        }

        private static TaxiPath Path(double ax, double az, double ayaw, double bx, double bz, double byaw) =>
            new TaxiPath(ax, az, ayaw, bx, bz, byaw);

        /// <summary>
        /// The shortest path between two poses that never turns tighter than <see cref="TurningRadiusMetres"/>
        /// (Dubins: an arc, a straight, an arc, or three arcs when the poses are close), so the nose swings at a
        /// believable rate and a U-turn is a real turn rather than a cusp. Headings are Unity yaws.
        /// </summary>
        private sealed class TaxiPath
        {
            private readonly double _x0, _z0, _theta0;
            private readonly char[] _kind;
            private readonly double[] _length;

            public TaxiPath(double ax, double az, double ayaw, double bx, double bz, double byaw)
            {
                _x0 = ax;
                _z0 = az;
                // Work in the maths plane (angle from +x toward +z); a Unity yaw is 90 degrees minus that.
                _theta0 = (90.0 - ayaw) * Math.PI / 180.0;
                var theta1 = (90.0 - byaw) * Math.PI / 180.0;
                var r = TurningRadiusMetres;
                var dx = bx - ax;
                var dz = bz - az;
                var d = Math.Sqrt(dx * dx + dz * dz) / r;
                var theta = Math.Atan2(dz, dx);
                var alpha = Mod2Pi(_theta0 - theta);
                var beta = Mod2Pi(theta1 - theta);
                var sa = Math.Sin(alpha);
                var sb = Math.Sin(beta);
                var ca = Math.Cos(alpha);
                var cb = Math.Cos(beta);
                var cab = Math.Cos(alpha - beta);
                var best = double.MaxValue;
                _kind = new[] { 'L', 'S', 'L' };
                _length = new double[3];

                void Try(string word, double t, double p, double q)
                {
                    if (double.IsNaN(t) || double.IsNaN(p) || double.IsNaN(q))
                        return;
                    var total = t + p + q;
                    if (total >= best)
                        return;
                    best = total;
                    _kind[0] = word[0];
                    _kind[1] = word[1];
                    _kind[2] = word[2];
                    _length[0] = t * r;
                    _length[1] = p * r;
                    _length[2] = q * r;
                }

                // LSL
                var tmp = Math.Atan2(cb - ca, d + sa - sb);
                var psq = 2 + d * d - 2 * cab + 2 * d * (sa - sb);
                if (psq >= 0)
                    Try("LSL", Mod2Pi(-alpha + tmp), Math.Sqrt(psq), Mod2Pi(beta - tmp));
                // RSR
                tmp = Math.Atan2(ca - cb, d - sa + sb);
                psq = 2 + d * d - 2 * cab + 2 * d * (sb - sa);
                if (psq >= 0)
                    Try("RSR", Mod2Pi(alpha - tmp), Math.Sqrt(psq), Mod2Pi(-beta + tmp));
                // LSR
                psq = -2 + d * d + 2 * cab + 2 * d * (sa + sb);
                if (psq >= 0)
                {
                    var p = Math.Sqrt(psq);
                    tmp = Math.Atan2(-ca - cb, d + sa + sb) - Math.Atan2(-2.0, p);
                    Try("LSR", Mod2Pi(-alpha + tmp), p, Mod2Pi(-beta + tmp));
                }

                // RSL
                psq = d * d - 2 + 2 * cab - 2 * d * (sa + sb);
                if (psq >= 0)
                {
                    var p = Math.Sqrt(psq);
                    tmp = Math.Atan2(ca + cb, d - sa - sb) - Math.Atan2(2.0, p);
                    Try("RSL", Mod2Pi(alpha - tmp), p, Mod2Pi(beta - tmp));
                }

                // RLR
                tmp = (6.0 - d * d + 2 * cab + 2 * d * (sa - sb)) / 8.0;
                if (Math.Abs(tmp) <= 1.0)
                {
                    var p = Mod2Pi(2 * Math.PI - Math.Acos(tmp));
                    var t = Mod2Pi(alpha - Math.Atan2(ca - cb, d - sa + sb) + p / 2.0);
                    Try("RLR", t, p, Mod2Pi(alpha - beta - t + p));
                }

                // LRL
                tmp = (6.0 - d * d + 2 * cab + 2 * d * (sb - sa)) / 8.0;
                if (Math.Abs(tmp) <= 1.0)
                {
                    var p = Mod2Pi(2 * Math.PI - Math.Acos(tmp));
                    var t = Mod2Pi(-alpha - Math.Atan2(ca - cb, d + sa - sb) + p / 2.0);
                    Try("LRL", t, p, Mod2Pi(Mod2Pi(beta) - alpha - t + p));
                }

                if (best == double.MaxValue)
                {
                    // Cannot happen for finite poses; fall back to a straight line between them.
                    _kind[0] = 'S'; _kind[1] = 'S'; _kind[2] = 'S';
                    _length[0] = 0; _length[1] = d * r; _length[2] = 0;
                }
            }

            public double Length => _length[0] + _length[1] + _length[2];

            public void PointAtDistance(double metres, out double x, out double z, out double yaw)
            {
                var remaining = Math.Max(0.0, Math.Min(metres, Length));
                x = _x0;
                z = _z0;
                var theta = _theta0;
                var r = TurningRadiusMetres;
                for (var i = 0; i < 3; i++)
                {
                    var s = Math.Min(remaining, _length[i]);
                    remaining -= s;
                    if (_kind[i] == 'S')
                    {
                        x += Math.Cos(theta) * s;
                        z += Math.Sin(theta) * s;
                    }
                    else
                    {
                        var sign = _kind[i] == 'L' ? 1.0 : -1.0;
                        var turned = sign * s / r;
                        // Centre of the turning circle sits r to the left (or right) of the heading.
                        var cx = x - sign * r * Math.Sin(theta);
                        var cz = z + sign * r * Math.Cos(theta);
                        theta += turned;
                        x = cx + sign * r * Math.Sin(theta);
                        z = cz - sign * r * Math.Cos(theta);
                    }
                }

                yaw = Normalise(90.0 - theta * 180.0 / Math.PI);
            }

            private static double Mod2Pi(double angle)
            {
                var value = angle % (2 * Math.PI);
                return value < 0 ? value + 2 * Math.PI : value;
            }
        }
    }
}
