using System;
using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class WeatherTests
    {
        [Test]
        public void Weather_IsDeterministicForAGivenTime()
        {
            for (long t = 0; t < 5000; t += 137)
                Assert.That(Weather.At(new SimulationTime(t)), Is.EqualTo(Weather.At(new SimulationTime(t))));
        }

        [Test]
        public void Weather_HoldsForABlockThenCanChange()
        {
            var start = Weather.At(new SimulationTime(0));
            Assert.That(Weather.At(new SimulationTime(Weather.BlockSeconds - 1)), Is.EqualTo(start));

            var seen = new HashSet<WeatherKind>();
            for (long block = 0; block < 60; block++)
                seen.Add(Weather.At(new SimulationTime(block * Weather.BlockSeconds)));
            Assert.That(seen.Count, Is.GreaterThan(1), "weather should vary across the day");
        }

        [Test]
        public void Weather_FavoursMildConditions()
        {
            var mild = 0;
            const int samples = 400;
            for (long block = 0; block < samples; block++)
            {
                var kind = Weather.At(new SimulationTime(block * Weather.BlockSeconds));
                if (kind == WeatherKind.Clear || kind == WeatherKind.Cloudy)
                    mild++;
            }

            Assert.That(mild, Is.GreaterThan(samples / 2), "most of the time the weather is fine");
        }

        [Test]
        public void WeatherLook_ThickensFromClearToStormWithoutChangingTiming()
        {
            var clear = WeatherLook.For(WeatherKind.Clear);
            var cloudy = WeatherLook.For(WeatherKind.Cloudy);
            var overcast = WeatherLook.For(WeatherKind.Overcast);
            var rain = WeatherLook.For(WeatherKind.Rain);
            var storm = WeatherLook.For(WeatherKind.Storm);

            Assert.That(clear.IsRaining, Is.False);
            Assert.That(cloudy.CloudCover, Is.GreaterThan(clear.CloudCover));
            Assert.That(overcast.CloudCover, Is.GreaterThan(cloudy.CloudCover));
            Assert.That(overcast.Gloom, Is.GreaterThan(cloudy.Gloom));
            Assert.That(rain.IsRaining, Is.True);
            Assert.That(rain.Wetness, Is.GreaterThan(0.4f));
            Assert.That(storm.Precipitation, Is.GreaterThan(rain.Precipitation));
            Assert.That(storm.Gloom, Is.GreaterThan(rain.Gloom));
            Assert.That(storm.Visibility, Is.LessThan(clear.Visibility));
            Assert.That(Weather.IsAdverse(WeatherKind.Cloudy), Is.False);
            Assert.That(Weather.Look(WeatherKind.Storm).Gloom, Is.EqualTo(storm.Gloom));
        }
    

        private static readonly WeatherKind[] Chain =
            { WeatherKind.Clear, WeatherKind.Cloudy, WeatherKind.Overcast, WeatherKind.Rain, WeatherKind.Storm };

        [Test]
        public void Weather_DriftsToNeighbours_MostOfTheTime()
        {
            // ADR 0143: Clear ↔ Cloudy ↔ Overcast ↔ Rain ↔ Storm, with about one change in twenty a jump.
            var changes = 0;
            var jumps = 0;
            var previous = Weather.At(new SimulationTime(0));
            for (long block = 1; block < 8_000; block++)
            {
                var kind = Weather.At(new SimulationTime(block * Weather.BlockSeconds));
                if (kind != previous && kind != WeatherKind.Fog && previous != WeatherKind.Fog)
                {
                    changes++;
                    if (Math.Abs(Array.IndexOf(Chain, kind) - Array.IndexOf(Chain, previous)) > 1)
                        jumps++;
                }

                previous = kind;
            }

            Assert.That(changes, Is.GreaterThan(1_000));
            Assert.That(jumps / (double)changes, Is.LessThan(0.08), "most changes are to a neighbouring sky");
        }

        [Test]
        public void Weather_KeepsARealisticMix()
        {
            var counts = new Dictionary<WeatherKind, int>();
            const int hours = 20_000;
            for (long block = 0; block < hours; block++)
            {
                var kind = Weather.At(new SimulationTime(block * Weather.BlockSeconds));
                counts[kind] = counts.TryGetValue(kind, out var n) ? n + 1 : 1;
            }

            double Share(WeatherKind k) => counts.TryGetValue(k, out var n) ? n / (double)hours : 0;
            Assert.That(Share(WeatherKind.Clear) + Share(WeatherKind.Cloudy), Is.InRange(0.55, 0.75));
            Assert.That(Share(WeatherKind.Storm), Is.InRange(0.01, 0.05));
            Assert.That(Share(WeatherKind.Fog), Is.InRange(0.02, 0.07));
            Assert.That(Share(WeatherKind.Rain), Is.InRange(0.06, 0.16));
        }

        [Test]
        public void Fog_OnlyFormsOnMorningsAndLifts()
        {
            var clock = AirlineClock.Default;
            for (long block = 0; block < 10_000; block++)
            {
                var at = new SimulationTime(block * Weather.BlockSeconds);
                if (Weather.At(at) != WeatherKind.Fog)
                    continue;
                var hour = clock.LocalAt(at).Hour;
                Assert.That(hour, Is.InRange(Weather.FogFromHour, Weather.FogUntilHour - 1), $"fog at {hour}:00");
            }
        }

        [Test]
        public void Weather_IsTheSameWhicheverHourIsAskedFirst()
        {
            var forward = new List<WeatherKind>();
            for (long block = 0; block < 200; block++)
                forward.Add(Weather.At(new SimulationTime(block * Weather.BlockSeconds)));
            for (long block = 199; block >= 0; block--)
                Assert.That(Weather.At(new SimulationTime(block * Weather.BlockSeconds + 1799)), Is.EqualTo(forward[(int)block]));
        }

        [Test]
        public void TheLook_EasesBetweenHours_AndIsTheSameAtAnyStep()
        {
            // Find an hour where the weather changes.
            long block = 1;
            while (Weather.At(new SimulationTime(block * Weather.BlockSeconds))
                   == Weather.At(new SimulationTime((block - 1) * Weather.BlockSeconds)))
                block++;
            var start = block * Weather.BlockSeconds;
            var before = WeatherLook.For(Weather.At(new SimulationTime(start - 1)));
            var after = WeatherLook.For(Weather.At(new SimulationTime(start)));
            Assert.That(Weather.LookAt(new SimulationTime(start)).CloudCover, Is.EqualTo(before.CloudCover).Within(1e-4f));
            Assert.That(Weather.LookAt(new SimulationTime(start + Weather.BlendSeconds)).CloudCover,
                Is.EqualTo(after.CloudCover).Within(1e-4f));
            var previous = Weather.LookAt(new SimulationTime(start)).CloudCover;
            for (var t = start; t <= start + Weather.BlendSeconds; t += 10)
            {
                var cover = Weather.LookAt(new SimulationTime(t)).CloudCover;
                Assert.That(Math.Abs(cover - previous), Is.LessThan(0.02f), "no snap");
                previous = cover;
            }
        }

        [Test]
        public void TheLook_InTheFirstHour_IsThatHoursWeather()
        {
            // A new career starts at time zero, where the ease reached back to second -1 and
            // threw every frame for the first quarter hour.
            var first = WeatherLook.For(Weather.At(new SimulationTime(0)));
            for (long t = 0; t <= Weather.BlendSeconds; t += 30)
                Assert.That(Weather.LookAt(new SimulationTime(t)).CloudCover,
                    Is.EqualTo(first.CloudCover).Within(1e-4f));
        }

        [Test]
        public void FogDensity_FollowsVisibility()
        {
            var clear = WeatherLook.For(WeatherKind.Clear);
            var fog = WeatherLook.For(WeatherKind.Fog);
            Assert.That(clear.VisibilityMetres, Is.GreaterThan(30_000f), "a clear day sees past the far clip");
            Assert.That(fog.VisibilityMetres, Is.InRange(400f, 1_500f));
            // exp(-(d·D)²) is 5% at the visibility distance.
            foreach (var look in new[] { clear, fog, WeatherLook.For(WeatherKind.Rain) })
            {
                var d = look.VisibilityMetres * look.FogDensity;
                Assert.That(Math.Exp(-d * d), Is.EqualTo(0.05).Within(0.001));
            }
        }
    }
}
