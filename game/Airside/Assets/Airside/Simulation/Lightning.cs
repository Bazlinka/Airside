using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// Deterministic storm lightning cadence (ADR 0059). Presentation only — a strike carries
    /// no simulation effect and needs no save state, so unlike <see cref="Weather"/> and the
    /// ADR 0058 ground stop it is never queried by the tower. It exists so a live view and an
    /// offline catch-up agree on where the strikes fall, the same discipline <see cref="Weather"/>
    /// itself follows: a pure hash of simulated time, no random source touched. It does keep a
    /// small internal memo of the last ladder position reached (see <see cref="StrikesAt"/>) as
    /// a single-threaded speed-up — that memo never changes what any call returns, only how
    /// much work it costs to get there.
    /// </summary>
    public static class Lightning
    {
        // Gaps land in [MinGapSeconds, MinGapSeconds + GapRangeSeconds) — frequent enough to
        // read as "an active storm," rare enough not to strobe.
        private const long MinGapSeconds = 5;
        private const long GapRangeSeconds = 9;

        // Ladder position memo: real play only ever asks StrikesAt with a non-decreasing
        // `now` (Update() advances the clock forward one second at a time), and most of those
        // seconds fall strictly between two strikes. _cachedFloor/_cachedCeiling are always a
        // pair of *adjacent* ladder points (no strike exists strictly between them, by
        // construction — ceiling is floor's own next ladder point), so once a query has
        // bracketed `now` between them, every further query in that same gap answers in O(1)
        // with no hashing at all; only crossing the ceiling costs the one hash to extend it.
        // Answers stay identical for any call order: a different block, or a target before
        // the cached floor (a test asking out of order does this), just rebuilds the bracket
        // from that block's own first second, same as if this cache did not exist.
        private static long _cachedBlockStart = long.MinValue;
        private static long _cachedFloor;
        private static long _cachedCeiling;

        /// <summary>
        /// True on the exact simulated second a strike lands. Only ever true during a
        /// <see cref="WeatherKind.Storm"/> block; the ladder always starts fresh at that
        /// block's own first second, so two non-adjacent storm hours never share a cadence.
        /// </summary>
        public static bool StrikesAt(SimulationTime now)
        {
            if (Weather.At(now) != WeatherKind.Storm)
                return false;

            var target = now.ElapsedSeconds;
            var blockStart = target / Weather.BlockSeconds * Weather.BlockSeconds;
            long floor, ceiling;
            if (blockStart == _cachedBlockStart && target >= _cachedFloor)
            {
                floor = _cachedFloor;
                ceiling = _cachedCeiling;
            }
            else
            {
                floor = blockStart;
                ceiling = blockStart;
            }

            while (ceiling < target)
            {
                floor = ceiling;
                ceiling += GapSeconds(ceiling);
            }

            _cachedBlockStart = blockStart;
            _cachedFloor = floor;
            _cachedCeiling = ceiling;
            return target == floor || target == ceiling;
        }

        /// <summary>
        /// 0 (directly overhead) .. 1 (on the horizon) for a strike landing at
        /// <paramref name="strikeAt"/> — brightens/dims the flash and delays thunder's arrival
        /// (sound lags light by roughly 3 s per kilometre).
        /// </summary>
        public static float DistanceFor(SimulationTime strikeAt) => Hash(strikeAt.ElapsedSeconds, 0x5bd1e995u) % 100u / 100f;

        /// <summary>Thunder's delay behind the flash, in seconds, for a strike at this distance.</summary>
        public static float ThunderDelaySeconds(float distance01) => 0.3f + distance01 * 7f;

        private static long GapSeconds(long at) =>
            MinGapSeconds + (long)(Hash(at, 0x9e3779b9u) % GapRangeSeconds);

        // Same xorshift-style mix as Weather.At, salted so the gap ladder and the distance
        // draw do not correlate.
        private static uint Hash(long seed, uint salt)
        {
            var h = (uint)((ulong)seed * 2654435761UL) ^ salt;
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            return h;
        }
    }
}
