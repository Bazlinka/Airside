using System;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class RunwayWeatherTests
    {
        [Test]
        public void CoupledWind_FollowsTheWeather()
        {
            Assert.That(RunwayWeather.CoupledKnots(10, WeatherKind.Fog), Is.LessThanOrEqualTo(4));
            Assert.That(RunwayWeather.CoupledKnots(10, WeatherKind.Clear), Is.LessThan(RunwayWeather.CoupledKnots(10, WeatherKind.Rain)));
            Assert.That(RunwayWeather.CoupledKnots(10, WeatherKind.Rain), Is.LessThan(RunwayWeather.CoupledKnots(10, WeatherKind.Storm)));
            Assert.That(RunwayWeather.CoupledKnots(40, WeatherKind.Storm), Is.EqualTo(45));
            Assert.That(RunwayWeather.Coupled(new SurfaceWind(230, 10), WeatherKind.Storm).DirectionDegrees, Is.EqualTo(230));
        }

        [Test]
        public void ForecastWind_NeverStepsAcrossAnHourBoundary()
        {
            var timeline = new AirportWeatherTimeline();
            var clock = AirlineClock.Default;
            for (var hour = 30; hour < 150; hour++)
            {
                var edge = hour * 3600L;
                var before = timeline.WindAt(clock, new SimulationTime(edge - 1)).Knots;
                var after = timeline.WindAt(clock, new SimulationTime(edge)).Knots;
                Assert.That(Math.Abs(after - before), Is.LessThanOrEqualTo(2), "hour " + hour);
            }
        }

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
        public void Storm_LetsAnArrivalOnFinalLandAndCommittedDepartureContinue()
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
                "the departure still waits for the arrival and separation");
            Assert.That(ops.Why(departure).Kind, Is.Not.EqualTo(HoldKind.GroundStop));
            Assert.That(ops.IsGroundStopped, Is.True);

            clock.Set(new SimulationTime(897600));
            ops.Update();
            Assert.That(ops.IsGroundStopped, Is.True);
            Assert.That(departure.State, Is.Not.EqualTo(FleetState.HoldingShort),
                "the committed departure clears without waiting for the storm to end");
        }

        [Test]
        public void Storm_DoesNotPostponeAnInboundAlreadyOnTheExtendedFinal()
        {
            const long due = 896500; // Entered the displayed final in clear weather, before 896400.
            var clock = new ManualSimulationClock(new SimulationTime(896350));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Storm Air", "#445566");
            ops.AddAirline(player);
            Assert.That(DestinationCatalogue.TryFind("PLO", out var origin), Is.True);
            ops.RestoreAircraft("VH-FIN", player, AircraftType.Atr42, FleetState.Inbound,
                new SimulationTime(890000), new SimulationTime(due), default, default, origin, null, 0);
            var arrival = ops.Fleet[0];
            Assert.That(ApproachRules.EnteredFinalBeforeStorm(arrival, clock.Now), Is.True);

            clock.Set(new SimulationTime(896400));
            ops.Update();
            Assert.That(ops.ExpectedLandingQueueTime(arrival, out _).Value.ElapsedSeconds, Is.EqualTo(due));
            Assert.That(ops.Why(arrival).Kind, Is.Not.EqualTo(HoldKind.GroundStop));

            clock.Set(new SimulationTime(due));
            ops.Update();
            Assert.That(arrival.State, Is.EqualTo(FleetState.Landing));
            Assert.That(arrival.StateStartedAt.ElapsedSeconds, Is.EqualTo(due),
                "the inbound timer must not be extended to the end of the storm");
        }

        [Test]
        public void Storm_TaxiOutAlreadyUnderwayStillReceivesTakeoffClearance()
        {
            var clock = new ManualSimulationClock(new SimulationTime(896350));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Storm Air", "#445566");
            ops.AddAirline(player);
            Assert.That(DestinationCatalogue.TryFind("PLO", out var destination), Is.True);
            ops.RestoreAircraft("VH-TAX", player, AircraftType.Atr42, FleetState.TaxiOut,
                new SimulationTime(896300), new SimulationTime(896500), default,
                AirlineOperations.AdelaideRegionalBays[0], destination, null, 0);
            clock.Set(new SimulationTime(896500));
            ops.Update();
            Assert.That(ops.Fleet[0].State, Is.EqualTo(FleetState.TakingOff));
            Assert.That(ops.Fleet[0].StateStartedAt.ElapsedSeconds, Is.EqualTo(896500));
        }

        [Test]
        public void Storm_ReadyDepartureWaitsAtStandAndReleasesIdenticallyWhenSkipped()
        {
            long Run(bool skip)
            {
                var clock = new ManualSimulationClock(new SimulationTime(897000));
                var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                    AirlineOperations.AdelaideRegionalBays);
                var player = Airline.Player("Storm Air", "#445566");
                ops.AddAirline(player);
                Assert.That(DestinationCatalogue.TryFind("PLO", out var destination), Is.True);
                var plane = ops.AddAircraft(player, "VH-GAT", AircraftType.Atr42, AirlineOperations.AdelaideRegionalBays[0]);
                plane.Scheduled = new ScheduledDeparture(destination, clock.Now);
                plane.PrepStartedAt = new SimulationTime(890000);
                ops.Update();
                Assert.That(plane.State, Is.EqualTo(FleetState.AtStand));
                Assert.That(ops.Why(plane).Kind, Is.EqualTo(HoldKind.GroundStop));
                Assert.That(ops.NextEventAt().Value.ElapsedSeconds, Is.EqualTo(900000));
                while (plane.State == FleetState.AtStand && clock.Now.ElapsedSeconds < 900120)
                {
                    clock.Set(skip ? ops.NextEventAt().Value : clock.Now.Advance(1));
                    ops.Update();
                }
                Assert.That(plane.State, Is.EqualTo(FleetState.TaxiOut));
                var weatherSeconds = 0;
                Assert.That(plane.PushbackDelay.HasValue, Is.True);
                foreach (var part in plane.PushbackDelay.Value.Parts)
                    if (part.Cause == DelayCause.Weather) weatherSeconds += part.Seconds;
                Assert.That(weatherSeconds, Is.EqualTo(3000));
                return plane.StateStartedAt.ElapsedSeconds;
            }
            Assert.That(Run(true), Is.EqualTo(Run(false)));
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
        public void HeavyAircraftRequireTwoMinutesBeforeAMediumDeparture()
        {
            Assert.That(WakeSeparation.Seconds(Airside.Domain.AircraftType.AirbusA350900,
                Airside.Domain.AircraftType.Boeing7378, landing: false), Is.EqualTo(120));
            Assert.That(WakeSeparation.Seconds(Airside.Domain.AircraftType.Boeing78710,
                Airside.Domain.AircraftType.Atr42, landing: false), Is.EqualTo(120));
        }

        [Test]
        public void MediumAircraftDoNotRequireTimeWakeBeforeAnotherMedium()
        {
            Assert.That(WakeSeparation.Seconds(Airside.Domain.AircraftType.Boeing7378,
                Airside.Domain.AircraftType.Saab340, landing: false), Is.Zero);
            Assert.That(WakeSeparation.Seconds(Airside.Domain.AircraftType.Dash8Q400,
                Airside.Domain.AircraftType.Atr42, landing: true), Is.Zero);
        }
    }
}
