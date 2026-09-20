using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// Deterministic storm lightning cadence (ADR 0059). Presentation only — a strike carries
    /// no simulation effect and needs no save state, so unlike <see cref="Weather"/> and the
    /// ADR 0058 ground stop it is never queried by the tower. It exists so a live view and an
    /// offline catch-up agree on where the strikes fall, the same discipline <see cref="Weather"/>
    /// itself follows: a pure hash of simulated time, no random source touched.
    /// </summary>
    public static class Lightning
    {
        // Gaps land in [MinGapSeconds, MinGapSeconds + GapRangeSeconds) — frequent enough to
        // read as "an active storm," rare enough not to strobe.
        private const long MinGapSeconds = 5;
        private const long GapRangeSeconds = 9;

        /// <summary>
        /// True on the exact simulated second a strike lands. Only ever true during a
        /// <see cref="WeatherKind.Storm"/> block; the ladder always starts fresh at that
        /// block's own first second, so two non-adjacent storm hours never share a cadence.
        /// </summary>
        public static bool StrikesAt(SimulationTime now)
        {
            if (Weather.At(now) != WeatherKind.Storm)
                return false;

            var blockStart = new SimulationTime(now.ElapsedSeconds / Weather.BlockSeconds * Weather.BlockSeconds);
            var at = blockStart;
            while (at.CompareTo(now) < 0)
                at = at.Advance(GapSeconds(at));
            return at.Equals(now);
        }

        /// <summary>
        /// 0 (directly overhead) .. 1 (on the horizon) for a strike landing at
        /// <paramref name="strikeAt"/> — brightens/dims the flash and delays thunder's arrival
        /// (sound lags light by roughly 3 s per kilometre).
        /// </summary>
        public static float DistanceFor(SimulationTime strikeAt) => Hash(strikeAt.ElapsedSeconds, 0x5bd1e995u) % 100u / 100f;

        /// <summary>Thunder's delay behind the flash, in seconds, for a strike at this distance.</summary>
        public static float ThunderDelaySeconds(float distance01) => 0.3f + distance01 * 7f;

        private static long GapSeconds(SimulationTime at) =>
            MinGapSeconds + (long)(Hash(at.ElapsedSeconds, 0x9e3779b9u) % GapRangeSeconds);

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
