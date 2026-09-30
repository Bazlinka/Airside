using System;
using System.Collections.Generic;
using System.Globalization;

namespace Airside.Simulation
{
    /// <summary>
    /// Parses one or more <c>-airsideReviewShot</c> entries from packaged-player
    /// launch args so a single soak can write several PNGs before quitting.
    /// Presentation-only: does not touch Domain or Simulation state.
    /// </summary>
    public sealed class ReviewShotSchedule
    {
        public const string ShotFlag = "-airsideReviewShot";
        public const string DelayFlag = "-airsideReviewDelay";
        public const string FollowZoomFlag = "-airsideReviewFollowZoom";
        public const string WeatherFlag = "-airsideReviewWeather";

        public readonly struct Entry
        {
            public Entry(string path, float delaySeconds, float? followZoom, string weatherToken)
            {
                Path = path;
                DelaySeconds = delaySeconds;
                FollowZoom = followZoom;
                WeatherToken = weatherToken;
            }

            public string Path { get; }
            public float DelaySeconds { get; }
            public float? FollowZoom { get; }
            /// <summary>Optional weather kind name (e.g. clear, storm); null keeps prior override.</summary>
            public string WeatherToken { get; }
        }

        private readonly Entry[] _entries;

        private ReviewShotSchedule(Entry[] entries)
        {
            _entries = entries ?? Array.Empty<Entry>();
        }

        public int Count => _entries.Length;

        public Entry this[int index] => _entries[index];

        public IReadOnlyList<Entry> Entries => _entries;

        public static bool TryParse(string[] args, out ReviewShotSchedule schedule)
        {
            schedule = null;
            if (args == null || args.Length == 0)
                return false;

            var shotIndexes = new List<int>();
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], ShotFlag, StringComparison.Ordinal)
                    && i + 1 < args.Length
                    && !string.IsNullOrWhiteSpace(args[i + 1])
                    && !args[i + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    shotIndexes.Add(i);
                }
            }

            if (shotIndexes.Count == 0)
                return false;

            var globalDelay = TryReadFloatAfter(args, DelayFlag) ?? 20f;
            var entries = new Entry[shotIndexes.Count];
            for (var s = 0; s < shotIndexes.Count; s++)
            {
                var start = shotIndexes[s];
                var end = s + 1 < shotIndexes.Count ? shotIndexes[s + 1] : args.Length;
                var path = args[start + 1];
                var delay = TryReadFloatInRange(args, start + 2, end, DelayFlag) ?? globalDelay;
                var zoom = TryReadFloatInRange(args, start + 2, end, FollowZoomFlag);
                var weather = TryReadTokenInRange(args, start + 2, end, WeatherFlag);
                entries[s] = new Entry(path, delay, zoom, weather);
            }

            schedule = new ReviewShotSchedule(entries);
            return true;
        }

        private static float? TryReadFloatAfter(string[] args, string flag)
        {
            var index = Array.IndexOf(args, flag);
            if (index < 0 || index + 1 >= args.Length)
                return null;
            return float.TryParse(args[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : (float?)null;
        }

        private static float? TryReadFloatInRange(string[] args, int start, int end, string flag)
        {
            for (var i = start; i + 1 < end; i++)
            {
                if (!string.Equals(args[i], flag, StringComparison.Ordinal))
                    continue;
                if (float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    return value;
            }

            return null;
        }

        private static string TryReadTokenInRange(string[] args, int start, int end, string flag)
        {
            for (var i = start; i + 1 < end; i++)
            {
                if (!string.Equals(args[i], flag, StringComparison.Ordinal))
                    continue;
                var token = args[i + 1];
                if (!string.IsNullOrWhiteSpace(token) && !token.StartsWith("-", StringComparison.Ordinal))
                    return token;
            }

            return null;
        }
    }
}
