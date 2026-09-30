using System;
using System.Globalization;

namespace Airside.Simulation
{
    /// <summary>
    /// Parses packaged-player overview framing flags and checks the live camera
    /// against them. Used so night-sky (and similar) stills fail closed when the
    /// pose never reached the CLI pitch/yaw/distance — a nose-down default overview
    /// must not look like a successful cruise-corridor capture.
    /// </summary>
    public static class ReviewOverviewFraming
    {
        public const string PitchFlag = "-airsideOverviewPitch";
        public const string YawFlag = "-airsideOverviewYaw";
        public const string DistanceFlag = "-airsideOverviewDistance";

        /// <summary>Pitch tolerance in degrees for overview stills that set an explicit CLI pitch.</summary>
        public const float PitchToleranceDegrees = 5f;

        /// <summary>Yaw tolerance in degrees (wrapped).</summary>
        public const float YawToleranceDegrees = 8f;

        /// <summary>Relative distance tolerance (fraction of expected).</summary>
        public const float DistanceToleranceFraction = 0.2f;

        public readonly struct Pose
        {
            public Pose(float pitchDegrees, float yawDegrees, float distanceMetres)
            {
                PitchDegrees = pitchDegrees;
                YawDegrees = yawDegrees;
                DistanceMetres = distanceMetres;
            }

            public float PitchDegrees { get; }
            public float YawDegrees { get; }
            public float DistanceMetres { get; }
        }

        /// <summary>
        /// True when the command line names at least one overview framing flag with a value.
        /// Follow stills that omit these flags are not constrained here.
        /// </summary>
        public static bool TryReadExpected(string[] args, out Pose expected)
        {
            expected = default;
            if (args == null || args.Length == 0)
                return false;

            var hasPitch = TryReadFloatAfter(args, PitchFlag, out var pitch);
            var hasYaw = TryReadFloatAfter(args, YawFlag, out var yaw);
            var hasDistance = TryReadFloatAfter(args, DistanceFlag, out var distance);
            if (!hasPitch && !hasYaw && !hasDistance)
                return false;

            expected = new Pose(
                hasPitch ? pitch : float.NaN,
                hasYaw ? yaw : float.NaN,
                hasDistance ? distance : float.NaN);
            return true;
        }

        /// <summary>
        /// True when every CLI-supplied axis is within tolerance of the live pose.
        /// Axes left unspecified (NaN in <paramref name="expected"/>) are ignored.
        /// </summary>
        public static bool Matches(Pose expected, Pose actual)
        {
            if (!float.IsNaN(expected.PitchDegrees)
                && Math.Abs(actual.PitchDegrees - expected.PitchDegrees) > PitchToleranceDegrees)
                return false;

            if (!float.IsNaN(expected.YawDegrees))
            {
                var delta = Math.Abs(((actual.YawDegrees - expected.YawDegrees + 540f) % 360f) - 180f);
                if (delta > YawToleranceDegrees)
                    return false;
            }

            if (!float.IsNaN(expected.DistanceMetres))
            {
                var expectedAbs = Math.Abs(expected.DistanceMetres);
                if (expectedAbs < 1f)
                    expectedAbs = 1f;
                var rel = Math.Abs(actual.DistanceMetres - expected.DistanceMetres) / expectedAbs;
                if (rel > DistanceToleranceFraction)
                    return false;
            }

            return true;
        }

        private static bool TryReadFloatAfter(string[] args, string flag, out float value)
        {
            value = 0f;
            var i = Array.IndexOf(args, flag);
            if (i < 0 || i + 1 >= args.Length)
                return false;
            return float.TryParse(
                args[i + 1],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }
    }
}
