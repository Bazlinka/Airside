using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    public enum WeatherKind
    {
        Clear,
        Cloudy,
        Overcast,
        Rain,
        Fog,
        Storm
    }

    /// <summary>
    /// Deterministic weather. It changes every <see cref="BlockSeconds"/> and is a
    /// pure function of the simulated timeline (no random source), so it is
    /// identical under live play and offline catch-up and does not disturb the
    /// gameplay random sequence. Weather is informational plus a daily operating
    /// cost — it does not change flight timing.
    ///
    /// ADR 0143: it drifts instead of jumping. Each hour is a step of a Markov chain along
    /// Clear ↔ Cloudy ↔ Overcast ↔ Rain ↔ Storm: most hours stay put, most changes go to a
    /// neighbour, and about one change in twenty jumps anywhere. Fog forms only between 04:00 and
    /// 09:00 local from a clear or cloudy night, and lifts after. The chain restarts from a hashed
    /// state 48 hours before each local-day anchor, so any hour is computed in at most 72 steps and
    /// is the same whichever hour was asked first. Over time: about 37% clear, 28% cloudy, 17%
    /// overcast, 11% rain, 4% fog, 2.5% storm.
    /// </summary>
    public static class Weather
    {
        // A new sky every simulated hour. Five minutes suited the old 20-minute day; on a
        // real 24-hour day at 60x it flickered rain on and off every five seconds.
        public const long BlockSeconds = 3600;

        /// <summary>Start of the hour-long block that contains <paramref name="now"/>.</summary>
        public static SimulationTime BlockStart(SimulationTime now) =>
            new SimulationTime(FloorDiv(now.ElapsedSeconds, BlockSeconds) * BlockSeconds);

        /// <summary>First instant of the weather block after the one containing <paramref name="now"/>.</summary>
        public static SimulationTime NextBlock(SimulationTime now) =>
            BlockStart(now).Advance(BlockSeconds);

        /// <summary>Hours a chain runs before the day it answers for, so days join up.</summary>
        public const int LeadInBlocks = 48;

        public const int FogFromHour = 4;
        public const int FogUntilHour = 9;

        private static readonly WeatherKind[] Chain =
            { WeatherKind.Clear, WeatherKind.Cloudy, WeatherKind.Overcast, WeatherKind.Rain, WeatherKind.Storm };

        // Weights, in Chain order: where a jump lands, and which neighbour a step picks.
        private static readonly int[] JumpWeights = { 40, 25, 17, 12, 6 };
        private static readonly int[] NeighbourWeights = { 52, 16, 20, 18, 7 };
        // Percent chance each state stays for another hour.
        private static readonly int[] StayPercent = { 72, 52, 55, 50, 40 };
        private const int JumpPerMille = 50;
        private const int FogFormsPercent = 18;
        private const int FogStaysPerMille = 700;

        private static AirlineClock LocalClock = AirlineClock.Default;
        private static readonly Dictionary<long, WeatherKind[]> Days = new();

        public static WeatherKind At(SimulationTime now)
        {
            var block = FloorDiv(now.ElapsedSeconds, BlockSeconds);
            var anchor = FloorDiv(block, 24) * 24;
            var day = DayFrom(anchor);
            return day[block - anchor];
        }

        /// <summary>The next hour's weather from this one: the chain's single step (ADR 0143).</summary>
        public static WeatherKind Step(WeatherKind previous, long block)
        {
            var roll = Hash(block, 7) % 1000u;
            if (roll >= 1000 - JumpPerMille)
                return Draw(Hash(block, 17), JumpWeights);
            var hour = LocalHour(block);
            var fogHours = hour >= FogFromHour && hour < FogUntilHour;
            if (previous == WeatherKind.Fog)
            {
                if (fogHours && roll < FogStaysPerMille)
                    return WeatherKind.Fog;
                return roll % 5 == 0 ? WeatherKind.Clear : WeatherKind.Cloudy;
            }

            if ((previous == WeatherKind.Clear || previous == WeatherKind.Cloudy) && fogHours
                && Hash(block, 11) % 100u < FogFormsPercent)
                return WeatherKind.Fog;
            var index = Array.IndexOf(Chain, previous);
            if (roll / 10 < StayPercent[index])
                return previous;
            var down = index > 0 ? NeighbourWeights[index - 1] : 0;
            var up = index < Chain.Length - 1 ? NeighbourWeights[index + 1] : 0;
            var pick = Hash(block, 13) % (uint)(down + up);
            return pick < down ? Chain[index - 1] : Chain[index + 1];
        }

        /// <summary>
        /// Points the weather at the airline's own clock. Fog and its hours are local-time rules, so they must
        /// read the same local time the HUD and curfew show; a save with another epoch otherwise had fog (and
        /// the helicopter hold it causes) hours out of step. Cached days belong to the old clock and are dropped.
        /// </summary>
        public static void UseClock(AirlineClock clock)
        {
            clock ??= AirlineClock.Default;
            lock (Days)
            {
                if (LocalClock.EpochUtcTicks == clock.EpochUtcTicks)
                    return;
                LocalClock = clock;
                Days.Clear();
            }
        }

        private static WeatherKind[] DayFrom(long anchor)
        {
            lock (Days)
            {
                if (Days.TryGetValue(anchor, out var cached))
                    return cached;
                var start = anchor - LeadInBlocks;
                var state = Draw(Hash(start, 3), JumpWeights);
                var day = new WeatherKind[24];
                for (var block = start + 1; block < anchor + 24; block++)
                {
                    state = Step(state, block);
                    if (block >= anchor)
                        day[block - anchor] = state;
                }

                if (Days.Count > 256)
                    Days.Clear();
                Days[anchor] = day;
                return day;
            }
        }

        private static int LocalHour(long block) =>
            block >= 0
                ? LocalClock.LocalAt(new SimulationTime(block * BlockSeconds)).Hour
                // The lead-in reaches before time zero, which a SimulationTime cannot hold.
                : LocalClock.LocalAt(new SimulationTime(0)).AddHours(block).Hour;

        private static WeatherKind Draw(uint roll, int[] weights)
        {
            var total = 0;
            foreach (var w in weights)
                total += w;
            var t = (int)(roll % (uint)total);
            for (var i = 0; i < weights.Length; i++)
            {
                if (t < weights[i])
                    return Chain[i];
                t -= weights[i];
            }

            return WeatherKind.Clear;
        }

        private static uint Hash(long block, uint salt)
        {
            unchecked
            {
                var h = (uint)((ulong)block * 2654435761UL) + salt * 40503u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return h;
            }
        }

        private static long FloorDiv(long a, long b) => a >= 0 ? a / b : -((-a + b - 1) / b);

        /// <summary>
        /// ADR 0143: the look eased from the last hour's weather into this hour's over
        /// <see cref="BlendSeconds"/> of game time, so a change of sky is never a snap.
        /// </summary>
        public const long BlendSeconds = 15 * 60;

        public static WeatherLook LookAt(SimulationTime now)
        {
            var current = WeatherLook.For(At(now));
            var into = now.ElapsedSeconds - FloorDiv(now.ElapsedSeconds, BlockSeconds) * BlockSeconds;
            // The first hour of a career has no hour before it to ease from (and time zero cannot
            // step back a second), so a new game opens on its own sky.
            if (into >= BlendSeconds || now.ElapsedSeconds < BlockSeconds)
                return current;
            var previous = WeatherLook.For(At(new SimulationTime(now.ElapsedSeconds - into - 1)));
            var t = into / (float)BlendSeconds;
            return WeatherLook.Lerp(previous, current, t * t * (3f - 2f * t));
        }

        public static bool IsAdverse(WeatherKind kind) =>
            kind == WeatherKind.Rain || kind == WeatherKind.Fog || kind == WeatherKind.Storm;

        public static string Describe(WeatherKind kind) => kind switch
        {
            WeatherKind.Clear => "Clear",
            WeatherKind.Cloudy => "Cloudy",
            WeatherKind.Overcast => "Overcast",
            WeatherKind.Rain => "Rain",
            WeatherKind.Fog => "Fog",
            WeatherKind.Storm => "Storm",
            _ => "Clear"
        };

        /// <summary>Presentation knobs for a forecast. Does not change flight timing (ADR 0013).</summary>
        public static WeatherLook Look(WeatherKind kind) => WeatherLook.For(kind);
    }

    /// <summary>
    /// Shared look for cloudy / overcast / rain / fog / storm. Presentation reads
    /// these instead of scattering magic numbers. Timing stays informational.
    /// </summary>
    public readonly struct WeatherLook
    {
        public WeatherLook(float cloudCover, float precipitation, float gloom, float visibility, float wetness)
        {
            CloudCover = cloudCover;
            Precipitation = precipitation;
            Gloom = gloom;
            Visibility = visibility;
            Wetness = wetness;
        }

        /// <summary>0 clear sky … 1 a solid overcast sheet.</summary>
        public float CloudCover { get; }

        /// <summary>0 dry … 1 storm rain.</summary>
        public float Precipitation { get; }

        /// <summary>0 bright … 1 storm gloom (sun and ambient dim).</summary>
        public float Gloom { get; }

        /// <summary>1 unlimited … 0 socked-in fog.</summary>
        public float Visibility { get; }

        /// <summary>0 dry pavement … 1 soaked.</summary>
        public float Wetness { get; }

        public bool IsRaining => Precipitation > 0.05f;

        /// <summary>
        /// ADR 0143: how far you can see, metres. Clear air reaches past the 30 km far clip;
        /// thick fog is a few hundred metres.
        /// </summary>
        public float VisibilityMetres
        {
            get
            {
                var v = Visibility < 0f ? 0f : Visibility > 1f ? 1f : Visibility;
                // 0.30 (fog) → ~900 m, 0.48 (storm) → ~2.6 km, 0.68 (rain) → ~9 km, 1 (clear) → 60 km.
                return 150f * (float)Math.Pow(400.0, v);
            }
        }

        /// <summary>The 0..1 <see cref="Visibility"/> that reads back as <paramref name="metres"/> through <see cref="VisibilityMetres"/>.</summary>
        public static float VisibilityFromMetres(float metres)
        {
            var v = (float)(Math.Log(Math.Max(150f, metres) / 150.0) / Math.Log(400.0));
            return v < 0f ? 0f : v > 1f ? 1f : v;
        }

        /// <summary>
        /// Exponential-squared fog density that fades to 5% at <see cref="VisibilityMetres"/>:
        /// exp(−(d·D)²) = 0.05 gives D = √3 / visibility.
        /// </summary>
        public float FogDensity => 1.7320508f / Math.Max(1f, VisibilityMetres);

        public static WeatherLook Lerp(WeatherLook a, WeatherLook b, float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            static float L(float x, float y, float s) => x + (y - x) * s;
            return new WeatherLook(L(a.CloudCover, b.CloudCover, t), L(a.Precipitation, b.Precipitation, t),
                L(a.Gloom, b.Gloom, t), L(a.Visibility, b.Visibility, t), L(a.Wetness, b.Wetness, t));
        }

        public static WeatherLook For(WeatherKind kind) => kind switch
        {
            WeatherKind.Cloudy => new WeatherLook(0.45f, 0f, 0.16f, 0.92f, 0f),
            WeatherKind.Overcast => new WeatherLook(0.78f, 0f, 0.22f, 0.82f, 0f),
            WeatherKind.Rain => new WeatherLook(0.88f, 0.55f, 0.28f, 0.68f, 0.52f),
            WeatherKind.Fog => new WeatherLook(0.70f, 0f, 0.42f, 0.30f, 0.14f),
            WeatherKind.Storm => new WeatherLook(0.95f, 1f, 0.55f, 0.48f, 0.72f),
            _ => new WeatherLook(0.12f, 0f, 0f, 1f, 0f)
        };
    }
}
