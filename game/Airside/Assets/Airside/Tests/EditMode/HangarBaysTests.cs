using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0188: hangars hold only so many aircraft, so checks queue for space.</summary>
    public sealed class HangarBaysTests
    {
        private static (AirlineOperations ops, List<FleetAircraft> planes) Fleet(int count)
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Soak Air", "#1F3A93");
            ops.AddAirline(player);
            var planes = new List<FleetAircraft>();
            for (var i = 0; i < count; i++)
                planes.Add(ops.AddAircraft(player, $"VH-T{i:00}", AircraftType.Saab340, AirlineOperations.AdelaideRegionalBays[i]));
            ops.RestoreCareerState(1_000_000 * FlightCostModel.LegacySaveMoneyScale, 90, nameof(OperatingTier.Provisional), null, 0, 0, Array.Empty<string>(),
                Array.Empty<string>(), 0);
            return (ops, planes);
        }

        [Test]
        public void EachHangarHoldsAtLeastOne_AndNoMoreThanTheMaximum()
        {
            var options = HangarTow.Options(AircraftType.Saab340, AirlineOperations.AdelaideRegionalBays[0]);
            Assert.That(options.Count, Is.GreaterThan(0));
            foreach (var option in options)
                Assert.That(option.Capacity, Is.InRange(1, HangarTow.MaxBerths), option.HangarName);
        }

        [Test]
        public void ChecksReserveDistinctSheds_UntilEveryFittingHangarIsOccupied()
        {
            var options = HangarTow.Options(AircraftType.Saab340, AirlineOperations.AdelaideRegionalBays[0]);
            var total = options.Sum(o => o.Capacity);
            var (ops, planes) = Fleet(Math.Min(total + 2, AirlineOperations.AdelaideRegionalBays.Count));
            var accepted = 0;
            foreach (var plane in planes)
            {
                var result = ops.StartCheck(plane);
                if (!result.Accepted)
                {
                    StringAssert.Contains("occupied", result.Reason);
                    break;
                }

                accepted++;
            }

            Assert.That(accepted, Is.GreaterThanOrEqualTo(1), "new jobs conservatively reserve a complete shed");
            TestContext.WriteLine($"{accepted} of {planes.Count} accepted; best hangar {options[0].HangarName} holds {options[0].Capacity}; all hangars {total}");

            var berths = planes.Where(p => p.CheckUntil.HasValue)
                .Select(p => HangarBays.Of(ops.Fleet, p, ops.CareerState.BaseLevel)).ToList();
            var keys = planes.Where(p => p.CheckUntil.HasValue)
                .Select((p, i) => (HangarTow.Options(p.Type, p.Stand)[berths[i].Hangar].HangarId, berths[i].Slot)).ToList();
            Assert.That(keys.Distinct().Count(), Is.EqualTo(keys.Count), "no two aircraft share a berth");
        }

        [Test]
        public void ALaterCheckNeverMovesAnEarlierOne()
        {
            var (ops, planes) = Fleet(3);
            Assert.That(ops.StartCheck(planes[0]).Accepted, Is.True);
            var before = HangarBays.Of(ops.Fleet, planes[0], ops.CareerState.BaseLevel);
            ops.StartCheck(planes[1]);
            ops.StartCheck(planes[2]);
            var after = HangarBays.Of(ops.Fleet, planes[0], ops.CareerState.BaseLevel);
            Assert.That((after.Hangar, after.Slot), Is.EqualTo((before.Hangar, before.Slot)));
        }

        [Test]
        public void WhenEveryFittingHangarIsFull_TheNextCheckIsToldWhenOneFrees()
        {
            var stand = AirlineOperations.AdelaideRegionalBays[0];
            var options = HangarTow.Options(AircraftType.Saab340, stand);
            var total = options.Sum(o => o.Capacity);
            var player = Airline.Player("Soak Air", "#1F3A93");
            var fleet = new List<FleetAircraft>();
            for (var i = 0; i < total; i++)
            {
                var plane = new FleetAircraft($"VH-F{i:00}", player, AircraftType.Saab340, stand, new SimulationTime(0));
                plane.CheckUntil = new SimulationTime(7200 + i * 60);
                fleet.Add(plane);
            }

            var next = new FleetAircraft("VH-NXT", player, AircraftType.Saab340, stand, new SimulationTime(0));
            var berth = HangarBays.Assign(fleet, next, 3600, 10800, PlayerBaseLevel.Starter);
            Assert.That(berth.Full, Is.True);
            Assert.That(berth.FreeAtSeconds, Is.EqualTo(7200), "the earliest check to end frees the first berth");
            var later = HangarBays.Assign(fleet, next, 7300, 14500, PlayerBaseLevel.Starter);
            Assert.That(later.Full, Is.False);
        }
    }
}
