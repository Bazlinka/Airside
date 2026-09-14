using System;
using System.Collections.Generic;

namespace Airside.Simulation
{
    /// <summary>How an aircraft is allowed to move along a ground path.</summary>
    public readonly struct GroundSpeedLimits
    {
        public GroundSpeedLimits(float maxSpeed, float acceleration, float braking, float lateralAcceleration)
        {
            MaxSpeed = maxSpeed;
            Acceleration = acceleration;
            Braking = braking;
            LateralAcceleration = lateralAcceleration;
        }

        /// <summary>m/s on the straight.</summary>
        public float MaxSpeed { get; }

        /// <summary>m/s² when speeding up.</summary>
        public float Acceleration { get; }

        /// <summary>m/s² when slowing down.</summary>
        public float Braking { get; }

        /// <summary>m/s² allowed sideways in a turn; sets the cornering speed from the radius.</summary>
        public float LateralAcceleration { get; }

        /// <summary>
        /// ATR taxi: 15 kt on the straight, 0.5 m/s² sideways in turns — about 10 kt round a
        /// normal 45 m taxiway fillet, which is how they are flown. (0.35 m/s² crawled
        /// round every corner at ~6 kt.)
        /// </summary>
        public static GroundSpeedLimits Taxi => new(CircuitProfile.Knots(15f), 0.5f, 0.7f, 0.5f);

        /// <summary>Apron taxi lanes next to parked aircraft and people: 10 kt.</summary>
        public const float ApronKnots = 10f;

        /// <summary>Lead-in to a stand under guidance: walking pace, 5 kt.</summary>
        public const float StandLeadInKnots = 5f;

        /// <summary>How far out from the stop the lead-in pace starts.</summary>
        public const float StandLeadInMetres = 45f;

        /// <summary>Apron stretch leaving or entering the bays at <see cref="ApronKnots"/>.</summary>
        public const float ApronMetres = 160f;

        /// <summary>Tug pushback: 2 kt, very gentle starts and stops.</summary>
        public static GroundSpeedLimits Pushback => new(CircuitProfile.Knots(2f), 0.15f, 0.25f, 0.2f);

        /// <summary>Lining up: 8 kt onto the centreline, stopping at the takeoff point.</summary>
        public static GroundSpeedLimits Lineup => new(CircuitProfile.Knots(8f), 0.4f, 0.6f, 0.35f);
    }

    /// <summary>
    /// A path with a speed profile: every point gets the fastest speed the limits allow
    /// — capped on the straight, slowed by the curve radius in turns, and bounded by
    /// acceleration from the previous point and braking to the next — and travel time
    /// follows from that. Durations and positions both come from here, so the time an
    /// aircraft is given for a taxi is exactly the time the drawn motion takes.
    /// </summary>
    /// <summary>A stretch at one end of a path with a lower speed limit (apron, stand lead-in).</summary>
    public readonly struct GroundSpeedZone
    {
        public GroundSpeedZone(float metres, float maxSpeed)
        {
            Metres = metres;
            MaxSpeed = maxSpeed;
        }

        public float Metres { get; }
        public float MaxSpeed { get; }
        public bool IsSet => Metres > 0f;
    }

    public sealed class GroundPath
    {
        private const float MinimumSegmentSpeed = 0.25f;

        private readonly float[] _x;
        private readonly float[] _z;
        private readonly float[] _distance;
        private readonly float[] _speed;
        private readonly double[] _time;

        public GroundPath(float[] xz, GroundSpeedLimits limits, float entrySpeed = 0f, float exitSpeed = 0f)
            : this(xz, limits, entrySpeed, exitSpeed, default, default)
        {
        }

        /// <param name="startZones">Slower stretches measured from the start of the path.</param>
        /// <param name="endZones">Slower stretches measured back from the end of the path.</param>
        public GroundPath(float[] xz, GroundSpeedLimits limits, float entrySpeed, float exitSpeed,
            GroundSpeedZone[] startZones, GroundSpeedZone[] endZones)
        {
            if (xz == null || xz.Length < 4 || xz.Length % 2 != 0)
                throw new ArgumentException("A path needs at least two x,z points.", nameof(xz));
            xz = SplitAtZoneEdges(xz, startZones, endZones);

            var count = xz.Length / 2;
            _x = new float[count];
            _z = new float[count];
            for (var i = 0; i < count; i++)
            {
                _x[i] = xz[i * 2];
                _z[i] = xz[i * 2 + 1];
            }

            _distance = new float[count];
            for (var i = 1; i < count; i++)
                _distance[i] = _distance[i - 1] + Hypot(_x[i] - _x[i - 1], _z[i] - _z[i - 1]);

            _speed = new float[count];
            for (var i = 0; i < count; i++)
            {
                var v = limits.MaxSpeed;
                if (i > 0 && i < count - 1)
                {
                    var radius = TurnRadius(i);
                    if (radius < float.MaxValue)
                        v = Math.Min(v, (float)Math.Sqrt(limits.LateralAcceleration * radius));
                }

                if (startZones != null)
                    foreach (var zone in startZones)
                        if (zone.IsSet && _distance[i] <= zone.Metres + 0.01f)
                            v = Math.Min(v, zone.MaxSpeed);
                if (endZones != null)
                    foreach (var zone in endZones)
                        if (zone.IsSet && _distance[count - 1] - _distance[i] <= zone.Metres + 0.01f)
                            v = Math.Min(v, zone.MaxSpeed);

                _speed[i] = v;
            }

            _speed[0] = Math.Min(_speed[0], entrySpeed);
            _speed[count - 1] = Math.Min(_speed[count - 1], exitSpeed);
            for (var i = 1; i < count; i++)
            {
                var d = _distance[i] - _distance[i - 1];
                _speed[i] = Math.Min(_speed[i], (float)Math.Sqrt(_speed[i - 1] * _speed[i - 1] + 2f * limits.Acceleration * d));
            }

            for (var i = count - 2; i >= 0; i--)
            {
                var d = _distance[i + 1] - _distance[i];
                _speed[i] = Math.Min(_speed[i], (float)Math.Sqrt(_speed[i + 1] * _speed[i + 1] + 2f * limits.Braking * d));
            }

            _time = new double[count];
            for (var i = 1; i < count; i++)
            {
                var d = _distance[i] - _distance[i - 1];
                var mean = Math.Max(MinimumSegmentSpeed, (_speed[i - 1] + _speed[i]) * 0.5f);
                _time[i] = _time[i - 1] + d / mean;
            }
        }

        public float Length => _distance[_distance.Length - 1];
        public double Seconds => _time[_time.Length - 1];
        public float TopSpeed => Max(_speed);
        public float StartX => _x[0];
        public float StartZ => _z[0];
        public float EndX => _x[_x.Length - 1];
        public float EndZ => _z[_z.Length - 1];

        /// <summary>Position and travel direction <paramref name="metres"/> along the path (speed 0).</summary>
        public GroundSample SampleAtDistance(float metres)
        {
            var last = _x.Length - 1;
            if (metres <= 0f)
                return Sample(0, 0f, 0f);
            if (metres >= Length)
                return Sample(last - 1, 1f, 0f);
            var i = Array.BinarySearch(_distance, metres);
            if (i < 0)
                i = ~i;
            var segment = Math.Max(0, i - 1);
            var span = _distance[segment + 1] - _distance[segment];
            return Sample(segment, span > 1e-6f ? (metres - _distance[segment]) / span : 1f, 0f);
        }

        /// <summary>Position, travel direction (unit x,z) and speed after <paramref name="seconds"/>.</summary>
        public GroundSample SampleAt(double seconds)
        {
            var last = _x.Length - 1;
            if (seconds <= 0)
                return Sample(0, 0f, _speed[0]);
            if (seconds >= Seconds)
                return Sample(last - 1, 1f, _speed[last]);

            var i = Array.BinarySearch(_time, seconds);
            if (i < 0)
                i = ~i;
            var segment = Math.Max(0, i - 1);
            var span = _time[segment + 1] - _time[segment];
            var length = _distance[segment + 1] - _distance[segment];
            var v0 = _speed[segment];
            var v1 = _speed[segment + 1];
            var tau = seconds - _time[segment];

            // Uniform acceleration across the segment: s = v0·t + ½·a·t².
            var a = span > 1e-6 ? (v1 - v0) / span : 0;
            var travelled = v0 * tau + 0.5 * a * tau * tau;
            if (v0 + v1 < 2 * MinimumSegmentSpeed)
                travelled = length * (tau / span);
            var fraction = length > 1e-6 ? (float)Math.Min(1.0, Math.Max(0.0, travelled / length)) : 1f;
            var speed = (float)Math.Max(0.0, v0 + a * tau);
            return Sample(segment, fraction, speed);
        }

        private GroundSample Sample(int segment, float fraction, float speed)
        {
            var next = Math.Min(segment + 1, _x.Length - 1);
            var dx = _x[next] - _x[segment];
            var dz = _z[next] - _z[segment];
            var len = Hypot(dx, dz);
            return new GroundSample(
                _x[segment] + dx * fraction,
                _z[segment] + dz * fraction,
                len > 1e-6f ? dx / len : 1f,
                len > 1e-6f ? dz / len : 0f,
                speed);
        }

        /// <summary>Insert a point exactly where each zone begins or ends, so its limit holds from that metre.</summary>
        private static float[] SplitAtZoneEdges(float[] xz, GroundSpeedZone[] startZones, GroundSpeedZone[] endZones)
        {
            var total = 0f;
            for (var i = 2; i < xz.Length; i += 2)
                total += Hypot(xz[i] - xz[i - 2], xz[i + 1] - xz[i - 1]);

            var cuts = new List<float>();
            if (startZones != null)
                foreach (var zone in startZones)
                    if (zone.IsSet && zone.Metres < total)
                        cuts.Add(zone.Metres);
            if (endZones != null)
                foreach (var zone in endZones)
                    if (zone.IsSet && zone.Metres < total)
                        cuts.Add(total - zone.Metres);
            if (cuts.Count == 0)
                return xz;
            cuts.Sort();

            var result = new List<float> { xz[0], xz[1] };
            var travelled = 0f;
            var next = 0;
            for (var i = 2; i < xz.Length; i += 2)
            {
                float ax = xz[i - 2], az = xz[i - 1], bx = xz[i], bz = xz[i + 1];
                var length = Hypot(bx - ax, bz - az);
                while (next < cuts.Count && cuts[next] < travelled + length)
                {
                    var t = length > 1e-6f ? (cuts[next] - travelled) / length : 0f;
                    if (t > 0.01f && t < 0.99f)
                    {
                        result.Add(ax + (bx - ax) * t);
                        result.Add(az + (bz - az) * t);
                    }
                    next++;
                }

                result.Add(bx);
                result.Add(bz);
                travelled += length;
            }

            return result.ToArray();
        }

        private float TurnRadius(int i)
        {
            // Circumradius of the three neighbouring points; straight lines give infinity.
            float ax = _x[i - 1], az = _z[i - 1], bx = _x[i], bz = _z[i], cx = _x[i + 1], cz = _z[i + 1];
            var ab = Hypot(bx - ax, bz - az);
            var bc = Hypot(cx - bx, cz - bz);
            var ca = Hypot(ax - cx, az - cz);
            var cross = Math.Abs((bx - ax) * (cz - az) - (bz - az) * (cx - ax));
            return cross < 1e-4f ? float.MaxValue : ab * bc * ca / (2f * cross);
        }

        private static float Hypot(float x, float z) => (float)Math.Sqrt(x * x + z * z);

        private static float Max(float[] values)
        {
            var max = 0f;
            foreach (var v in values)
                max = Math.Max(max, v);
            return max;
        }
    }

    public readonly struct GroundSample
    {
        public GroundSample(float x, float z, float directionX, float directionZ, float speed)
        {
            X = x;
            Z = z;
            DirectionX = directionX;
            DirectionZ = directionZ;
            Speed = speed;
        }

        public float X { get; }
        public float Z { get; }

        /// <summary>Unit direction of travel (not necessarily where the nose points).</summary>
        public float DirectionX { get; }
        public float DirectionZ { get; }

        /// <summary>m/s along the path.</summary>
        public float Speed { get; }
    }

    /// <summary>One piece of a ground leg: a path, driven nose-first or tail-first, after an optional pause.</summary>
    public readonly struct GroundLegPart
    {
        public GroundLegPart(GroundPath path, bool tailFirst, double pauseBeforeSeconds = 0)
        {
            Path = path;
            TailFirst = tailFirst;
            PauseBeforeSeconds = pauseBeforeSeconds;
        }

        public GroundPath Path { get; }
        public bool TailFirst { get; }
        public double PauseBeforeSeconds { get; }
        public double Seconds => PauseBeforeSeconds + Path.Seconds;
    }

    /// <summary>Where an aircraft is on a ground leg: position, nose heading and speed.</summary>
    public readonly struct GroundPose
    {
        public GroundPose(float x, float z, float noseX, float noseZ, float speed, bool tailFirst)
        {
            X = x;
            Z = z;
            NoseX = noseX;
            NoseZ = noseZ;
            Speed = speed;
            TailFirst = tailFirst;
        }

        public float X { get; }
        public float Z { get; }

        /// <summary>Unit direction the nose points.</summary>
        public float NoseX { get; }
        public float NoseZ { get; }

        public float Speed { get; }
        public bool TailFirst { get; }
    }

    /// <summary>A sequence of parts, e.g. pushback, pause for the tug to disconnect, then taxi.</summary>
    public sealed class GroundLeg
    {
        private readonly List<GroundLegPart> _parts;

        public GroundLeg(params GroundLegPart[] parts)
        {
            if (parts == null || parts.Length == 0)
                throw new ArgumentException("A leg needs at least one part.", nameof(parts));
            _parts = new List<GroundLegPart>(parts);
        }

        public double Seconds
        {
            get
            {
                var total = 0.0;
                foreach (var part in _parts)
                    total += part.Seconds;
                return total;
            }
        }

        /// <summary>Whole simulated seconds the leg takes, rounded up so the motion always finishes.</summary>
        public long WholeSeconds => (long)Math.Ceiling(Seconds);

        public GroundPose PoseAt(double seconds)
        {
            var t = Math.Max(0.0, seconds);
            for (var i = 0; i < _parts.Count; i++)
            {
                var part = _parts[i];
                var isLast = i == _parts.Count - 1;
                if (t < part.PauseBeforeSeconds)
                    return Pose(part, part.Path.SampleAt(0), stopped: true);
                t -= part.PauseBeforeSeconds;
                if (t <= part.Path.Seconds || isLast)
                    return Pose(part, part.Path.SampleAt(t), stopped: false);
                t -= part.Path.Seconds;
            }

            var final = _parts[_parts.Count - 1];
            return Pose(final, final.Path.SampleAt(final.Path.Seconds), stopped: true);
        }

        private static GroundPose Pose(GroundLegPart part, GroundSample sample, bool stopped)
        {
            var sign = part.TailFirst ? -1f : 1f;
            return new GroundPose(sample.X, sample.Z, sample.DirectionX * sign, sample.DirectionZ * sign,
                stopped ? 0f : sample.Speed, part.TailFirst);
        }
    }
}
