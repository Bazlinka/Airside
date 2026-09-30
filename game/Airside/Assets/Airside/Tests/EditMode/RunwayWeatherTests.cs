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

        // Block 249 (896400-899999s) is a storm; blocks 248 (Cloudy) and 250 (Rain) bracket it
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

        private static FleetAircraft RestoreHoldingShort(AirlineOperations ops, string registration, long stateStartedAt)
        {
            var player = ops.Fleet[0].Airline;
            Assert.That(DestinationCatalogue.TryFind("PLO", out var portLincoln), Is.True);
            ops.RestoreAircraft(
                registration, player, AircraftType.Atr42, FleetState.HoldingShort,
                new SimulationTime(stateStartedAt), null, default,
                AirlineOperations.AdelaideRegionalBays[0], portLincoln, null, 0);
            return ops.Fleet[ops.Fleet.Count - 1];
        }

        [Test]
        public void Storm_LetsAnArrivalAlreadyOnFinalLandAndHoldsTheDeparture()
        {
            Assert.That(Weather.At(new SimulationTime(897000)), Is.EqualTo(WeatherKind.Storm));
            Assert.That(Weather.At(new SimulationTime(900000)), Is.Not.EqualTo(WeatherKind.Storm));

            var clock = new ManualSimulationClock(new SimulationTime(897000));
            var ops = HoldingForLandingDuringStorm(clock, 894000);
            var arrival = ops.Fleet[0];
            var departure = RestoreHoldingShort(ops, "VH-DEP", 894000);
            var queued = ops.ExpectedLandingQueueTime(arrival, out _);

            Assert.That(queued.HasValue, Is.True);
            Assert.That(queued.Value.ElapsedSeconds, Is.LessThan(900000),
                "an aircraft already on final is not parked until the storm ends");

            ops.Update();
            Assert.That(arrival.State, Is.EqualTo(FleetState.Landing),
                "a storm does not withhold a landing that is already on final");
            Assert.That(departure.State, Is.EqualTo(FleetState.HoldingShort),
                "a departure still waits out the ground stop");
            Assert.That(ops.Why(departure).Kind, Is.EqualTo(HoldKind.GroundStop));
            Assert.That(ops.IsGroundStopped, Is.True);

            clock.Set(new SimulationTime(900000));
            ops.Update();
            Assert.That(ops.IsGroundStopped, Is.False);
            Assert.That(departure.State, Is.Not.EqualTo(FleetState.HoldingShort),
                "the held departure is released once the storm block ends");
        }

        [Test]
        public void Storm_HoldsAnArrivalThatHasNotReachedFinal()
        {
            Assert.That(Weather.At(new SimulationTime(897000)), Is.EqualTo(WeatherKind.Storm));
            var clock = new ManualSimulationClock(new SimulationTime(897000));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Storm Air", "#445566");
            ops.AddAirline(player);
            Assert.That(DestinationCatalogue.TryFind("PLO", out var portLincoln), Is.True);
            ops.RestoreAircraft(
                "VH-INB", player, AircraftType.Atr42, FleetState.Inbound,
                new SimulationTime(890000), new SimulationTime(897000),
                default, default, portLincoln, null, 0);
            var inbound = ops.Fleet[0];

            ops.Update();
            Assert.That(inbound.State, Is.EqualTo(FleetState.Inbound),
                "an arrival that has not reached final does not join the approach during a storm");
            Assert.That(inbound.StateEndsAt, Is.EqualTo(new SimulationTime(900000)));
            Assert.That(ops.Why(inbound).Kind, Is.EqualTo(HoldKind.GroundStop));

            clock.Set(new SimulationTime(900000));
            ops.Update();
            Assert.That(inbound.State, Is.Not.EqualTo(FleetState.Inbound),
                "it joins final once the storm block ends");
        }

        [Test]
        public void Storm_ReleaseIsIdenticalWhetherSteppedBySecondOrSkippedToTheNextEvent()
        {
            const long start = 897000;
            const long horizon = 904000;

            string Run(Func<ManualSimulationClock, AirlineOperations, bool> step)
            {
                var clock = new ManualSimulationClock(new SimulationTime(start));
                var ops = HoldingForLandingDuringStorm(clock, 894000);
                RestoreHoldingShort(ops, "VH-DEP", 894000);
                while (clock.Now.ElapsedSeconds < horizon && step(clock, ops))
                {
                }

                clock.Set(new SimulationTime(horizon));
                ops.Update();
                var arrival = ops.Fleet[0];
                var departure = ops.Fleet[1];
                return $"{arrival.State}@{arrival.StateStartedAt.ElapsedSeconds}|{departure.State}@{departure.StateStartedAt.ElapsedSeconds}";
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
