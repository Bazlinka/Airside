using System.Collections.Generic;
using Airside.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    public sealed class AirportContinuitySerializationTests
    {
        [Test]
        public void FlatWeatherAndHoldingRecordsSurviveUnityJsonAndBlankOptionalListsStaySafe()
        {
            var data = new AirlineSaveData {
                Fleet = new List<AircraftRecord> { new AircraftRecord { ArrivalCommittedBeforeStorm = true } },
                WeatherObservations = new List<WeatherObservationRecord> { new WeatherObservationRecord {
                    FromSeconds = 100, UntilSeconds = 200, Kind = (int)WeatherKind.Storm,
                    Cover = 0.95f, Rain = 1f, Gloom = 0.55f, Visibility = 0.48f, Wetness = 0.72f,
                    WindDegrees = 230, WindKnots = 18 } },
                ArrivalViews = new List<ArrivalViewRecord> { new ArrivalViewRecord {
                    Registration = "VH-PAX", Active = true, Holding = true, HoldingStartedAt = 100.5,
                    LastTime = 150.75, EntryX = 450, EntryY = 800, ForwardZ = 1, Speed = 80 } } };
            var restored = JsonUtility.FromJson<AirlineSaveData>(JsonUtility.ToJson(data));
            Assert.That(restored.Fleet[0].ArrivalCommittedBeforeStorm, Is.True);
            Assert.That(restored.WeatherObservations[0].Kind, Is.EqualTo((int)WeatherKind.Storm));
            Assert.That(restored.WeatherObservations[0].UntilSeconds, Is.EqualTo(200));
            Assert.That(restored.ArrivalViews[0].HoldingStartedAt, Is.EqualTo(100.5));
            Assert.That(restored.ArrivalViews[0].EntryY, Is.EqualTo(800));
            Assert.That(restored.ArrivalViews[0].Active, Is.True);
            var blank = JsonUtility.FromJson<AirlineSaveData>("{\"Version\":22}");
            var timeline = new AirportWeatherTimeline();
            Assert.DoesNotThrow(() => timeline.Restore(blank.WeatherObservations));
            Assert.That(timeline.Records.Count, Is.Zero);
        }
    }
}
