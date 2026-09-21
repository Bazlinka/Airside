using System;
using System.Collections.Generic;

namespace Airside.Simulation
{
    /// <summary>
    /// A path with a speed profile: every point gets the fastest speed the limits allow
    /// — capped on the straight, slowed by the curve radius in turns, and bounded by
    /// acceleration from the previous point and braking to the next — and travel time
    /// follows from that. Durations and positions both come from here, so the time an
    /// aircraft is given for a taxi is exactly the time the drawn motion takes.
    /// </summary>
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

        private (float MinX, float MinZ, float MaxX, float MaxZ)? _bounds;

        /// <summary>Axis-aligned box around every point of the path.</summary>
        public (float MinX, float MinZ, float MaxX, float MaxZ) Bounds
        {
            get
            {
                if (_bounds.HasValue)
                    return _bounds.Value;
                float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
                for (var i = 0; i < _x.Length; i++)
                {
                    minX = Math.Min(minX, _x[i]);
                    maxX = Math.Max(maxX, _x[i]);
                    minZ = Math.Min(minZ, _z[i]);
                    maxZ = Math.Max(maxZ, _z[i]);
                }

                _bounds = (minX, minZ, maxX, maxZ);
                return _bounds.Value;
            }
        }
        public float TopSpeed => Max(_speed);
        public float StartX => _x[0];
        public float StartZ => _z[0];
        public float EndX => _x[_x.Length - 1];
        public float EndZ => _z[_z.Length - 1];

        /// <summary>Seconds needed to travel <paramref name="metres"/> along the path.</summary>
        public double SecondsAtDistance(float metres)
        {
            if (metres <= 0f)
                return 0.0;
            if (metres >= Length)
                return Seconds;
            var lo = 0.0;
            var hi = Seconds;
            for (var n = 0; n < 24; n++)
            {
                var mid = (lo + hi) * 0.5;
                if (DistanceAt(mid) < metres)
                    lo = mid;
                else
                    hi = mid;
            }

            return hi;
        }

        /// <summary>Metres travelled along the path after <paramref name="seconds"/>.</summary>
        public float DistanceAt(double seconds)
        {
            if (seconds <= 0)
                return 0f;
            if (seconds >= Seconds)
                return Length;
            var i = Array.BinarySearch(_time, seconds);
            if (i < 0)
                i = ~i;
            var segment = Math.Max(0, i - 1);
            var span = _time[segment + 1] - _time[segment];
            var length = _distance[segment + 1] - _distance[segment];
            var v0 = _speed[segment];
            var v1 = _speed[segment + 1];
            var tau = seconds - _time[segment];
            var a = span > 1e-6 ? (v1 - v0) / span : 0;
            var travelled = v0 * tau + 0.5 * a * tau * tau;
            if (v0 + v1 < 2 * MinimumSegmentSpeed)
                travelled = span > 1e-9 ? length * (tau / span) : length;
            return _distance[segment] + (float)Math.Min(length, Math.Max(0.0, travelled));
        }

        /// <summary>
        /// Point <paramref name="metres"/> along the path; beyond either end it continues in a
        /// straight line along the end segment (where a trailing wheel would still be).
        /// </summary>
        public (float x, float z) PointAtDistance(float metres)
        {
            var last = _x.Length - 1;
            if (metres < 0f)
            {
                var d = Direction(0);
                return (_x[0] + d.x * metres, _z[0] + d.z * metres);
            }

            if (metres > Length)
            {
                var d = Direction(last - 1);
                return (_x[last] + d.x * (metres - Length), _z[last] + d.z * (metres - Length));
            }

            var sample = SampleAtDistance(metres);
            return (sample.X, sample.Z);
        }

        private (float x, float z) Direction(int segment)
        {
            var dx = _x[segment + 1] - _x[segment];
            var dz = _z[segment + 1] - _z[segment];
            var length = Hypot(dx, dz);
            return length > 1e-6f ? (dx / length, dz / length) : (1f, 0f);
        }

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
                travelled = span > 1e-9 ? length * (tau / span) : length;
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
            var x = _x[segment] + dx * fraction;
            var z = _z[segment] + dz * fraction;
            var along = _distance[segment] + (len > 1e-6f ? fraction * len : 0f);
            // Point the nose a little ahead of the current tangent so a vertex
            // does not snap the heading. Sixteen metres is about one cockpit
            // glance down a real taxiway. The glance grows from the start the way
            // it already shrinks into the end, so a leg begins on its own tangent:
            // a full 16 m chord on the first metre of a curved pushback turned the
            // parked aircraft up to 30° the instant the tug started.
            var look = Math.Min(Length, along + Math.Min(16f, 1f + along));
            if (look > along + 0.4f)
            {
                var ahead = PointAtDistance(look);
                var lx = ahead.x - x;
                var lz = ahead.z - z;
                var lookLen = Hypot(lx, lz);
                if (lookLen > 0.4f)
                {
                    dx = lx;
                    dz = lz;
                    len = lookLen;
                }
            }

            return new GroundSample(
                x, z,
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
        public GroundLegPart(GroundPath path, bool tailFirst, double pauseBeforeSeconds = 0, float trackMetres = 0f)
        {
            Path = path;
            TailFirst = tailFirst;
            PauseBeforeSeconds = pauseBeforeSeconds;
            TrackMetres = trackMetres;
        }

        public GroundPath Path { get; }
        public bool TailFirst { get; }
        public double PauseBeforeSeconds { get; }

        /// <summary>
        /// When positive, the path is followed by the aircraft's nose datum and its main gear
        /// trails this far behind along the same path, so the body points from the mains to the
        /// nose instead of along the tangent at the nose. A 39 m jet steered off its nose tangent
        /// swings its tail across the grass in every turn; tracked like this it stays on the
        /// taxiway. Zero keeps the original tangent heading (regional turboprops).
        /// </summary>
        public float TrackMetres { get; }
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

        /// <summary>
        /// Seconds into the leg at which it is <paramref name="metresBeforeEnd"/> short of the
        /// end of its last part — where an aircraft stops behind a queue at the holding point.
        /// </summary>
        public double SecondsShortOfEnd(float metresBeforeEnd)
        {
            if (metresBeforeEnd <= 0f)
                return Seconds;
            var before = 0.0;
            for (var i = 0; i < _parts.Count - 1; i++)
                before += _parts[i].Seconds;
            var last = _parts[_parts.Count - 1];
            var along = Math.Max(0f, last.Path.Length - metresBeforeEnd);
            return before + last.PauseBeforeSeconds + last.Path.SecondsAtDistance(along);
        }

        private float[] _tableX;
        private float[] _tableZ;

        /// <summary>
        /// Where the leg is after <paramref name="seconds"/>, from a one-second table built on first
        /// use and interpolated. For ground-conflict checks, which ask this hundreds of thousands of
        /// times; a full <see cref="PoseAt"/> (tug turn, look-ahead) is far too dear for that.
        /// </summary>
        public (float X, float Z) PositionAt(double seconds)
        {
            if (_tableX == null)
            {
                var count = (int)Math.Ceiling(Seconds) + 2;
                var xs = new float[count];
                var zs = new float[count];
                for (var i = 0; i < count; i++)
                {
                    var pose = PoseAt(i);
                    xs[i] = pose.X;
                    zs[i] = pose.Z;
                }

                _tableZ = zs;
                _tableX = xs;
            }

            var t = Math.Max(0.0, Math.Min(seconds, _tableX.Length - 1));
            var index = (int)Math.Floor(t);
            if (index >= _tableX.Length - 1)
                return (_tableX[_tableX.Length - 1], _tableZ[_tableZ.Length - 1]);
            var f = (float)(t - index);
            return (_tableX[index] + (_tableX[index + 1] - _tableX[index]) * f,
                _tableZ[index] + (_tableZ[index + 1] - _tableZ[index]) * f);
        }

        /// <summary>Box around every part of the leg.</summary>
        public (float MinX, float MinZ, float MaxX, float MaxZ) Bounds
        {
            get
            {
                float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
                foreach (var part in _parts)
                {
                    var b = part.Path.Bounds;
                    minX = Math.Min(minX, b.MinX);
                    minZ = Math.Min(minZ, b.MinZ);
                    maxX = Math.Max(maxX, b.MaxX);
                    maxZ = Math.Max(maxZ, b.MaxZ);
                }

                return (minX, minZ, maxX, maxZ);
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
                    return TugTurnPose(part, 0, stopped: true, i, t, part.PauseBeforeSeconds);
                t -= part.PauseBeforeSeconds;
                if (t <= part.Path.Seconds || isLast)
                    return TugTurnPose(part, t, stopped: false, i, -1, 0);
                t -= part.Path.Seconds;
            }

            var final = _parts[_parts.Count - 1];
            return TugTurnPose(final, final.Path.Seconds, stopped: true, _parts.Count - 1, -1, 0);
        }

        /// <summary>
        /// A tail-first push that hands off to a nose-first taxi used to invert
        /// the heading at tug disconnect — the airframe faced the stand, then
        /// snapped 180° onto the taxilane. The tug now turns the nose onto the
        /// taxi heading through the last part of the push (and finishes during
        /// the disconnect pause), so the hand-off is a human turn, not a flip.
        /// The turn starts from whatever the nose is already doing, so it never steps.
        /// </summary>
        private GroundPose TugTurnPose(GroundLegPart part, double pathSeconds, bool stopped,
            int index, double pauseElapsed, double pauseTotal)
        {
            var pose = Pose(part, pathSeconds, stopped);
            if (!TryTugTurn(index, out var fromX, out var fromZ, out var toX, out var toZ))
                return pose;

            var blend = 0.0;
            if (part.TailFirst)
                blend = PushTurnBlend(part, pathSeconds);
            else if (stopped && pauseTotal > 1e-6)
                blend = 1.0;
            else
                return pose;
            if (blend <= 0.0)
                return pose;
            // Blend from the heading the airframe already has (the tail-first tangent while
            // pushing), not from the stand heading: on a curving push those differ by tens of
            // degrees, so starting from the stand heading snapped the nose back when the turn began.
            // The direction of the turn is fixed where the blend begins. Re-picking the shortest way
            // every frame flips it by 360° the moment the nose swings through the exact opposite of
            // the taxi heading, which is a real case (a push that curves ~180° from the taxilane).
            var toHeading = Math.Atan2(toX, toZ);
            var now = Math.Atan2(pose.NoseX, pose.NoseZ);
            var turn = toHeading - now;
            if (part.TailFirst)
            {
                var start = Pose(part, part.Path.Seconds * 0.25, false);
                var reference = WrapPi(toHeading - Math.Atan2(start.NoseX, start.NoseZ));
                turn += 2.0 * Math.PI * Math.Round((reference - turn) / (2.0 * Math.PI));
            }
            else
                turn = WrapPi(turn);
            var heading = now + turn * Math.Max(0.0, Math.Min(1.0, blend));
            var noseX = (float)Math.Sin(heading);
            var noseZ = (float)Math.Cos(heading);
            return new GroundPose(pose.X, pose.Z, noseX, noseZ, pose.Speed, part.TailFirst);
        }

        private bool TryTugTurn(int index, out float fromX, out float fromZ, out float toX, out float toZ)
        {
            fromX = fromZ = toX = toZ = 0f;
            if (index < 0 || index >= _parts.Count)
                return false;
            var part = _parts[index];
            GroundLegPart push;
            GroundLegPart taxi;
            if (part.TailFirst && index + 1 < _parts.Count && !_parts[index + 1].TailFirst)
            {
                push = part;
                taxi = _parts[index + 1];
            }
            else if (!part.TailFirst && index > 0 && _parts[index - 1].TailFirst)
            {
                push = _parts[index - 1];
                taxi = part;
            }
            else
                return false;

            var start = Pose(push, 0, true);
            var end = Pose(taxi, 0, true);
            fromX = start.NoseX;
            fromZ = start.NoseZ;
            toX = end.NoseX;
            toZ = end.NoseZ;
            return true;
        }

        /// <summary>Tug starts the turnout a quarter of the way through the push and finishes before it stops.</summary>
        private static double PushTurnBlend(GroundLegPart push, double pathSeconds)
        {
            var span = push.Path.Seconds;
            var u = span > 1e-6 ? pathSeconds / span : 1.0;
            return Smooth01((u - 0.25) / 0.67);
        }

        private static double Smooth01(double t)
        {
            if (t <= 0)
                return 0;
            if (t >= 1)
                return 1;
            return t * t * (3.0 - 2.0 * t);
        }

        private static double WrapPi(double angle)
        {
            while (angle > Math.PI)
                angle -= 2.0 * Math.PI;
            while (angle < -Math.PI)
                angle += 2.0 * Math.PI;
            return angle;
        }

        /// <summary>Parts in order, for tests and tools that need the individual paths.</summary>
        public IReadOnlyList<GroundLegPart> Parts => _parts;

        private static GroundPose Pose(GroundLegPart part, double seconds, bool stopped)
        {
            var sample = part.Path.SampleAt(seconds);
            var sign = part.TailFirst ? -1f : 1f;
            var noseX = sample.DirectionX * sign;
            var noseZ = sample.DirectionZ * sign;
            if (part.TrackMetres > 0f)
            {
                // Main gear on the path behind the nose: behind in travel when nose first,
                // ahead in travel when the tail leads a pushback.
                var along = part.Path.DistanceAt(seconds);
                var mains = part.Path.PointAtDistance(part.TailFirst ? along + part.TrackMetres : along - part.TrackMetres);
                var dx = sample.X - mains.x;
                var dz = sample.Z - mains.z;
                var length = (float)Math.Sqrt(dx * dx + dz * dz);
                if (length > 1e-3f)
                {
                    noseX = dx / length;
                    noseZ = dz / length;
                }
            }

            return new GroundPose(sample.X, sample.Z, noseX, noseZ, stopped ? 0f : sample.Speed, part.TailFirst);
        }
    }
}
