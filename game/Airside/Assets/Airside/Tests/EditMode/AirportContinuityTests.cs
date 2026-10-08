using System;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AirportContinuityTests
    {
        private static LiveWeatherSnapshot Sample(WeatherKind kind) => new(kind, Weather.Look(kind),
            new SurfaceWind(230, 18), 20, 70, 1, 3000, 0.95f);

        [TestCase(3599, false)] [TestCase(3600, true)] [TestCase(11000, true)]
        public void ParkedActivityUsesTwoHourPublishedWindowAndRetainsOverdueFlights(long now, bool active)
        {
            var (_, _, aircraft) = HudTestAirline.Create();
            aircraft.Scheduled = new ScheduledDeparture(HudTestAirline.Code("PLO"), new SimulationTime(18000),
                publishedAt: new SimulationTime(10800));
            Assert.That(AircraftPresence.IsActive(aircraft, new SimulationTime(now)), Is.EqualTo(active));
            Assert.That(FleetVisual.For(aircraft, new SimulationTime(now)).Visible, Is.True);
        }
        [Test]
        public void MovingAircraftStayActiveAndIdleOrCancelledParkedAircraftDoNot()
        {
            var (_, _, aircraft) = HudTestAirline.Create();
            Assert.That(AircraftPresence.IsActive(aircraft, new SimulationTime(0)), Is.False);
            aircraft.Scheduled = new ScheduledDeparture(HudTestAirline.Code("PLO"), new SimulationTime(0), cancelled: true);
            Assert.That(AircraftPresence.IsActive(aircraft, new SimulationTime(0)), Is.False);
            foreach (var state in new[] { FleetState.TaxiOut, FleetState.HoldingShort, FleetState.Outbound, FleetState.Inbound, FleetState.GoAround })
            {
                aircraft.Restore(state, aircraft.StateStartedAt, null);
                Assert.That(AircraftPresence.IsActive(aircraft, new SimulationTime(0)), Is.True, state.ToString());
            }
        }
        [Test]
        public void StormObservationAtTheOpeningTimestampDoesNotEraseAnEnteredFinal()
        {
            var (clock, ops, aircraft) = HudTestAirline.Create();
            aircraft.CurrentDestination = HudTestAirline.Code("PLO");
            aircraft.Restore(FleetState.Inbound, clock.Now, new SimulationTime(10));
            ops.WeatherTimeline.Observe(clock.Now, Sample(WeatherKind.Clear));
            ops.ObserveWeather(Sample(WeatherKind.Storm));
            Assert.That(ApproachRules.EnteredFinalBeforeStorm(aircraft, clock.Now, ops.WeatherAt), Is.True);
            var restored = AirlineSave.Restore(AirlineSave.Capture(ops), clock);
            Assert.That(ApproachRules.EnteredFinalBeforeStorm(restored.Fleet[0], clock.Now, restored.WeatherAt), Is.True);
            aircraft.Enter(FleetState.AtStand, clock.Now, null);
            Assert.That(aircraft.ArrivalCommittedBeforeStorm, Is.False);
        }
        [Test]
        public void InboundCommitmentUsesTheRecordedWeatherAtFinalEntry()
        {
            var (_, _, aircraft) = HudTestAirline.Create();
            var speed = CircuitProfile.Knots(AircraftPerformance.For(aircraft.Type).ApproachKnots);
            var end = 50 + (long)Math.Ceiling(ApproachRules.ExtendedFinalMetres / speed);
            aircraft.Restore(FleetState.Inbound, new SimulationTime(0), new SimulationTime(end));
            var timeline = new AirportWeatherTimeline();
            timeline.Observe(new SimulationTime(0), Sample(WeatherKind.Clear));
            timeline.Observe(new SimulationTime(100), Sample(WeatherKind.Storm));
            Assert.That(ApproachRules.EnteredFinalBeforeStorm(aircraft, new SimulationTime(200), timeline.At), Is.True);
            var replay = new AirportWeatherTimeline(); replay.Restore(timeline.Records);
            Assert.That(ApproachRules.EnteredFinalBeforeStorm(aircraft, new SimulationTime(200), replay.At), Is.True);
        }
        [Test]
        public void ParkedModelsStayOnFieldWhenTheActiveCountChanges()
        {
            var (_, ops, aircraft) = HudTestAirline.Create();
            aircraft.Scheduled = new ScheduledDeparture(HudTestAirline.Code("PLO"), new SimulationTime(10800));
            var model = new OperationsWorkspaceModel();
            model.Rebuild(ops, new SimulationTime(3599), OperationsBoardTab.Departures, null, null);
            Assert.That(model.DayActiveCount, Is.Zero); Assert.That(model.DayOnFieldCount, Is.EqualTo(1));
            model.Rebuild(ops, new SimulationTime(3600), OperationsBoardTab.Departures, null, null);
            Assert.That(model.DayActiveCount, Is.EqualTo(1)); Assert.That(model.DayOnFieldCount, Is.EqualTo(1));
        }
        [Test]
        public void RecordedWeatherHasExactStartChangeAndExpiryBoundaries()
        {
            var timeline = new AirportWeatherTimeline();
            timeline.Observe(new SimulationTime(100), Sample(WeatherKind.Storm));
            Assert.That(timeline.At(new SimulationTime(99)), Is.EqualTo(Weather.At(new SimulationTime(99))));
            Assert.That(timeline.At(new SimulationTime(100)), Is.EqualTo(WeatherKind.Storm));
            timeline.Observe(new SimulationTime(300), Sample(WeatherKind.Clear));
            Assert.That(timeline.At(new SimulationTime(299)), Is.EqualTo(WeatherKind.Storm));
            Assert.That(timeline.At(new SimulationTime(300)), Is.EqualTo(WeatherKind.Clear));
            var end = new SimulationTime(300 + LiveWeather.StaleSeconds);
            Assert.That(timeline.At(end), Is.EqualTo(Weather.At(end)));
            Assert.That(timeline.WindAt(AirlineClock.Default, new SimulationTime(100)).Knots, Is.EqualTo(18));
        }
        [Test]
        public void ReusingAnOlderSampleDoesNotGrantAnotherTwoHoursOfValidity()
        {
            var timeline = new AirportWeatherTimeline();
            timeline.Observe(new SimulationTime(100), Sample(WeatherKind.Storm), validSeconds: 60);
            Assert.That(timeline.At(new SimulationTime(159)), Is.EqualTo(WeatherKind.Storm));
            Assert.That(timeline.At(new SimulationTime(160)), Is.EqualTo(Weather.At(new SimulationTime(160))));
        }
        [Test]
        public void WeatherSaveRestoresHistoryWithoutMutatingCapturedDataAndOlderSavesUseForecast()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            ops.WeatherTimeline.Observe(clock.Now, Sample(WeatherKind.Storm));
            var save = AirlineSave.Capture(ops);
            ops.WeatherTimeline.EndLiveAt(clock.Now);
            var restored = AirlineSave.Restore(save, clock);
            Assert.That(restored.CurrentWeather, Is.EqualTo(WeatherKind.Storm));
            Assert.That(restored.IsGroundStopped, Is.True);
            restored.WeatherTimeline.EndLiveAt(clock.Now);
            Assert.That(save.WeatherObservations[0].UntilSeconds, Is.EqualTo(LiveWeather.StaleSeconds));
            save.Version = 22;
            Assert.That(AirlineSave.Restore(save, clock).CurrentWeather, Is.EqualTo(Weather.At(clock.Now)));
        }
        [Test]
        public void SavedHoldingPoseCannotResurrectAnAircraftChangedByCatchUp()
        {
            var (_, _, aircraft) = HudTestAirline.Create();
            aircraft.Restore(FleetState.Inbound, aircraft.StateStartedAt, null);
            aircraft.CurrentDestination = HudTestAirline.Code("PLO");
            var record = new ArrivalViewRecord { Registration = aircraft.Registration, TypeId = aircraft.Type.Id,
                DestinationCode = "PLO", FleetState = (int)FleetState.Inbound,
                StateStartedAt = aircraft.StateStartedAt.ElapsedSeconds, Runway = (int)RunwayDirection.Runway05,
                Active = true, Holding = true, Speed = 80, ForwardX = 1, LastTime = 100, HoldingStartedAt = 20 };
            Assert.That(ArrivalViewContinuity.Matches(record, aircraft, 100), Is.True);
            aircraft.Restore(FleetState.AtStand, aircraft.StateStartedAt, null);
            Assert.That(ArrivalViewContinuity.Matches(record, aircraft, 100), Is.False);
            aircraft.Restore(FleetState.Inbound, aircraft.StateStartedAt, null);
            aircraft.Restore(FleetState.Inbound, new SimulationTime(50), null);
            Assert.That(ArrivalViewContinuity.Matches(record, aircraft, 100), Is.False);
            record.X = float.NaN;
            Assert.That(ArrivalViewContinuity.Valid(record, 100), Is.False);
        }
    }
}
