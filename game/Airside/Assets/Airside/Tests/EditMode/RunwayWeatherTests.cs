using System;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class RunwayWeatherTests
    {
        [TestCase(50, 12, RunwayDirection.Runway05)]
        [TestCase(230, 12, RunwayDirection.Runway23)]
        [TestCase(230, 2, RunwayDirection.Runway05)]
        public void RunwayUsesTheBestHeadwindAndAStableCalmDefault(int direction, int knots, RunwayDirection expected)
        {
            Assert.That(RunwayWeather.Select(new SurfaceWind(direction, knots)), Is.EqualTo(expected));
        }

        [Test]
        public void JetToPerth_Takes23WhenTheWindIsClose()
        {
            Assert.That(DestinationCatalogue.TryFind("PER", out var perth), Is.True);
            var wind = new SurfaceWind(140, 8);
            Assert.That(RunwayWeather.Select(wind, AircraftType.Boeing78710, perth, DestinationCatalogue.Adelaide),
                Is.EqualTo(RunwayDirection.Runway23));
            Assert.That(RunwayWeather.AllowsCrossRunway(AircraftType.Boeing78710), Is.False);
            Assert.That(RunwayWeather.AllowsCrossRunway(AircraftType.Atr42), Is.True);
            Assert.That(RunwayWeather.Label(RunwayDirection.Runway12), Is.EqualTo("12"));
            Assert.That(RunwayWeather.Label(RunwayDirection.Runway30), Is.EqualTo("30"));
        }

        [Test]
        public void Regional_UsesTheCrossStrip_JetStaysOnTheLongStrip()
        {
            var wind12 = new SurfaceWind(123, 10);
            var wind30 = new SurfaceWind(303, 10);
            Assert.That(RunwayWeather.Select(wind12, AircraftType.Atr42, null, null),
                Is.EqualTo(RunwayDirection.Runway12));
            Assert.That(RunwayWeather.Select(wind30, AircraftType.Dash8Q400, null, null),
                Is.EqualTo(RunwayDirection.Runway30));
            Assert.That(RunwayWeather.Select(wind12, AircraftType.Boeing7378, null, null),
                Is.EqualTo(RunwayDirection.Runway05));
            Assert.That(RunwayWeather.Select(wind30, AircraftType.AirbusA321Neo, null, null),
                Is.EqualTo(RunwayDirection.Runway23));
            Assert.That(RunwayWeather.IsMainRunway(RunwayDirection.Runway05), Is.True);
            Assert.That(RunwayWeather.IsMainRunway(RunwayDirection.Runway12), Is.False);
        }

        [Test]
        public void ArrivalRunway_FollowsTheWindNotTheAwayCity()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(3), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Wind Air", "#123456");
            ops.AddAirline(player);
            Assert.That(DestinationCatalogue.TryFind("PLO", out var portLincoln), Is.True);

            var restore = typeof(AirlineOperations).GetMethod("RestoreAircraft",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            restore.Invoke(ops, new object[]
            {
                "VH-ARR", player, AircraftType.Atr42, FleetState.Inbound, new SimulationTime(0),
                new SimulationTime(60), default(StableId), default(StableId), portLincoln, null, 0
            });
            var inbound = ops.Fleet[0];
            var windOnly = RunwayWeather.Select(ops.Wind, AircraftType.Atr42, null, ops.Home);
            Assert.That(ops.RunwayFor(inbound), Is.EqualTo(windOnly),
                "arrivals must not take the departure-favoured end in light wind");
        }

        // Block 34 (122400-125999s) hashes to Storm; blocks 33 (Clear) and 35 (Fog) bracket it
        // (see Weather.At). A holding aircraft restored inside that window exercises the
        // ground stop (ADR 0058) without waiting on a live day's odds of a storm turning up.
        private static AirlineOperations HoldingForLandingDuringStorm(ManualSimulationClock clock, long stateStartedAt)
        {
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Storm Air", "#445566");
            ops.AddAirline(player);
            Assert.That(DestinationCatalogue.TryFind("PLO", out var portLincoln), Is.True);

            var restore = typeof(AirlineOperations).GetMethod("RestoreAircraft",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            restore.Invoke(ops, new object[]
            {
                "VH-STM", player, AircraftType.Atr42, FleetState.HoldingForLanding,
                new SimulationTime(stateStartedAt), null, default(StableId), default(StableId),
                portLincoln, null, 0
            });
            return ops;
        }

        [Test]
        public void Storm_HoldsTheClearanceUntilWeatherClears()
        {
            Assert.That(Weather.At(new SimulationTime(123000)), Is.EqualTo(WeatherKind.Storm));
            Assert.That(Weather.At(new SimulationTime(126000)), Is.Not.EqualTo(WeatherKind.Storm));

            var clock = new ManualSimulationClock(new SimulationTime(123000));
            var ops = HoldingForLandingDuringStorm(clock, 120000);
            var holder = ops.Fleet[0];

            ops.Update();
            Assert.That(holder.State, Is.EqualTo(FleetState.HoldingForLanding),
                "a storm withholds a new landing clearance even though the strip is free");
            Assert.That(ops.IsGroundStopped, Is.True);

            clock.Set(new SimulationTime(126000));
            ops.Update();
            Assert.That(holder.State, Is.EqualTo(FleetState.Landing),
                "the held aircraft lands as soon as the storm block ends");
            Assert.That(ops.IsGroundStopped, Is.False);
        }

        [Test]
        public void Storm_ReleaseIsIdenticalWhetherSteppedBySecondOrSkippedToTheNextEvent()
        {
            const long start = 120000;
            const long horizon = 130000;

            string Run(Func<ManualSimulationClock, AirlineOperations, bool> step)
            {
                var clock = new ManualSimulationClock(new SimulationTime(start));
                var ops = HoldingForLandingDuringStorm(clock, start);
                while (clock.Now.ElapsedSeconds < horizon && step(clock, ops))
                {
                }

                clock.Set(new SimulationTime(horizon));
                ops.Update();
                var holder = ops.Fleet[0];
                return $"{holder.State}@{holder.StateStartedAt.ElapsedSeconds}";
            }

            var bySecond = Run((c, o) => { c.Advance(1); o.Update(); return true; });
            var bySkipping = Run((c, o) =>
            {
                var next = o.NextEventAt();
                if (next == null || next.Value.ElapsedSeconds > horizon) return false;
                c.Set(next.Value);
                o.Update();
                return true;
            });

            Assert.That(bySkipping, Is.EqualTo(bySecond),
                "a storm's tower gate must answer the same way at the same simulated moment " +
                "no matter how big a jump catch-up takes to reach it");
        }

        [Test]
        public void HeavyAircraftReceiveLongerWakeSpacing()
        {
            Assert.That(AirlineOperations.WakeSeparationSeconds(Airside.Domain.AircraftType.AirbusA350900), Is.EqualTo(180));
            Assert.That(AirlineOperations.WakeSeparationSeconds(Airside.Domain.AircraftType.Boeing78710), Is.EqualTo(180));
            Assert.That(AirlineOperations.WakeSeparationSeconds(Airside.Domain.AircraftType.Atr42), Is.EqualTo(90));
        }

        [Test]
        public void MediumJetsGetTheMiddleWakeBand()
        {
            // WakeSeparationSeconds used to classify by a hand-picked list of type IDs - a
            // second, disconnected source of truth from the catalogue's own wingspan data
            // that would silently give any newly added heavy jet only the smallest 90 s
            // separation if its ID were never added to match. It is now derived from
            // AircraftCatalogue.WingspanMetres directly.
            Assert.That(AirlineOperations.WakeSeparationSeconds(Airside.Domain.AircraftType.Boeing7378), Is.EqualTo(120));
            Assert.That(AirlineOperations.WakeSeparationSeconds(Airside.Domain.AircraftType.AirbusA321Neo), Is.EqualTo(120));
            Assert.That(AirlineOperations.WakeSeparationSeconds(Airside.Domain.AircraftType.Saab340), Is.EqualTo(90));
            Assert.That(AirlineOperations.WakeSeparationSeconds(Airside.Domain.AircraftType.Dash8Q400), Is.EqualTo(90));
        }
    }
}
