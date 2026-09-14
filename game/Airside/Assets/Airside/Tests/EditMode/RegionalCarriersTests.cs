using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class RegionalCarriersTests
    {
        private static AirlineOperations NewGame(out ManualSimulationClock clock)
        {
            clock = new ManualSimulationClock(new SimulationTime(0));
            return AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(5), Airline.Player("Test Air", "#1F3A93"));
        }

        [Test]
        public void NewGame_HasRexAndQantasLinkOnTheExtraBays()
        {
            var ops = NewGame(out _);
            Assert.That(ops.Airlines.Select(a => a.Name), Does.Contain("Rex").And.Contain("QantasLink").And.Contain("Emu Air"));
            Assert.That(ops.Fleet.Count, Is.EqualTo(AirlineOperations.AdelaideRegionalBays.Count),
                "one aircraft per bay: nobody can be left without a stand");
            Assert.That(ops.Fleet.Select(a => a.Stand).Distinct().Count(), Is.EqualTo(ops.Fleet.Count));
            Assert.That(ops.Fleet.Where(a => a.Airline.Name == "Rex").All(a => a.Type == AircraftType.Saab340), Is.True);
            Assert.That(ops.Fleet.Where(a => !a.Airline.IsPlayer).All(a => a.Scheduled.HasValue), Is.True);
        }

        [Test]
        public void EachCarrier_FliesOnlyItsOwnNetwork()
        {
            var ops = NewGame(out var clock);
            for (var t = 0; t < 3 * 24 * 3600; t += 60)
            {
                clock.Advance(60);
                ops.Update();
                foreach (var aircraft in ops.Fleet.Where(a => !a.Airline.IsPlayer))
                {
                    var destination = aircraft.CurrentDestination ?? aircraft.Scheduled?.Destination;
                    if (!destination.HasValue)
                        continue;
                    var network = AirlineOperations.AiNetworkFor(aircraft.Airline).Select(n => n.Code);
                    Assert.That(network, Does.Contain(destination.Value.Code), $"{aircraft.Registration} to {destination.Value.Code}");
                    Assert.That(ops.CanReach(aircraft, destination.Value), Is.True);
                }
            }
        }

        [Test]
        public void OldSave_GainsTheCarriersOnceAndOnlyOnFreeStands()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Old Save Air", "#2E7D32");
            ops.AddAirline(player);
            ops.AddAircraft(player, "VH-PAX", AircraftType.Atr42, AirlineOperations.AdelaideRegionalBays[0]);
            var emu = Airline.EmuAir();
            ops.AddAirline(emu);
            ops.AddAircraft(emu, "VH-EMA", AircraftType.Atr42, AirlineOperations.AdelaideRegionalBays[1]);
            ops.AddAircraft(emu, "VH-EMB", AircraftType.Atr42, AirlineOperations.AdelaideRegionalBays[2]);

            Assert.That(ops.AddMissingRegionalCarriers(), Is.EqualTo(3));
            Assert.That(ops.AddMissingRegionalCarriers(), Is.EqualTo(0), "idempotent across reloads");
            Assert.That(ops.Fleet.Select(a => a.Stand).Distinct().Count(), Is.EqualTo(ops.Fleet.Count));
        }

        [Test]
        public void NewTypes_RoundTripThroughTheirSaveIds()
        {
            foreach (var type in new[] { AircraftType.Atr42, AircraftType.Saab340, AircraftType.Dash8Q400 })
            {
                Assert.That(AircraftType.TryFromId(type.Id, out var found), Is.True);
                Assert.That(found, Is.SameAs(type));
            }
        }
    }
}
