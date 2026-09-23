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
            Assert.That(ops.Airlines.Select(a => a.Name), Does.Contain("Rex").And.Contain("QantasLink").And.Contain("Royal Flying Doctor Service"));
            var regional = ops.Fleet.Where(a => !AirlineOperations.NeedsTerminalGate(a.Type)).ToList();
            Assert.That(regional.Count, Is.EqualTo(12),
                "player Saab plus six Rex, four QantasLink and RFDS (ADR 0111)");
            Assert.That(regional.Count(a => a.State == FleetState.AtStand), Is.GreaterThanOrEqualTo(6),
                "most regionals stay on the apron; only a short inbound bank flies in");
            Assert.That(regional.Where(a => a.State == FleetState.AtStand).All(a => AirlineOperations.AdelaideRegionalBays.Contains(a.Stand)), Is.True);
            Assert.That(ops.Fleet.Where(a => a.Airline.Name == "Rex").All(a => a.Type == AircraftType.Saab340), Is.True);
            Assert.That(ops.Fleet.Single(a => a.Airline.IsEmergency).State, Is.EqualTo(FleetState.AtStand),
                "RFDS always keeps a bay");
            Assert.That(regional.Count(a => a.State == FleetState.Inbound), Is.GreaterThanOrEqualTo(1));
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
        public void OldSave_GainsTheCarriersOnceParkingOnlyOnFreeStands()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Old Save Air", "#2E7D32");
            ops.AddAirline(player);
            ops.AddAircraft(player, "VH-PAX", AircraftType.Atr42, AirlineOperations.AdelaideRegionalBays[0]);
            var legacy = new Airline("LEG", "Legacy Regional", "#A66F32", isPlayer: false);
            ops.AddAirline(legacy);
            ops.AddAircraft(legacy, "VH-LEA", AircraftType.Atr42, AirlineOperations.AdelaideRegionalBays[1]);
            ops.AddAircraft(legacy, "VH-LEB", AircraftType.Atr42, AirlineOperations.AdelaideRegionalBays[2]);

            Assert.That(ops.AddMissingRegionalCarriers(), Is.EqualTo(10),
                "six Rex plus four Q400 join: parked where a bay is free, otherwise flying in");
            Assert.That(ops.AddMissingRegionalCarriers(), Is.EqualTo(0), "idempotent across reloads");
            var parked = ops.Fleet.Where(a => a.State == FleetState.AtStand).ToList();
            Assert.That(parked.Select(a => a.Stand).Distinct().Count(), Is.EqualTo(parked.Count), "no bay shared");
            Assert.That(parked.All(a => AirlineOperations.StandFits(a.Type, a.Stand)), Is.True);
            Assert.That(ops.Fleet.Where(a => a.State == FleetState.Inbound).All(a => string.IsNullOrEmpty(a.Stand.Value)),
                Is.True, "overflow is away, holding no stand");
        }

        [Test]
        public void Overflow_StartsAwayAndLandsOnAFreedBay()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Old Save Air", "#2E7D32");
            ops.AddAirline(player);
            for (var i = 0; i < 5; i++)
                ops.AddAircraft(player, $"VH-P{i}", AircraftType.Atr42, AirlineOperations.AdelaideRegionalBays[i]);

            Assert.That(ops.AddMissingRegionalCarriers(), Is.EqualTo(10));
            var away = ops.Fleet.Where(a => !a.Airline.IsPlayer && a.State == FleetState.Inbound).ToList();
            Assert.That(away, Is.Not.Empty, "no room for everyone: some start at their outstation");
            Assert.That(ops.Airlines.Select(a => a.Name), Does.Contain("QantasLink"));
            Assert.That(ops.AddMissingRegionalCarriers(), Is.EqualTo(0));
        }

        [Test]
        public void AddMissingRegionalCarriers_AvoidsTheBayBesideAParkedDash8WhenAnotherIsFree()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Old Save Air", "#2E7D32");
            ops.AddAirline(player);
            ops.AddAircraft(player, "VH-QQQ", AircraftType.Dash8Q400, new StableId("BAY-1"));

            Assert.That(ops.AddMissingRegionalCarriers(), Is.EqualTo(10));
            Assert.That(ops.Fleet.First(a => a.Registration == "VH-ZRC").Stand,
                Is.Not.EqualTo(new StableId("BAY-5")), "the first choice avoids the tight neighbour while alternatives exist");
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
