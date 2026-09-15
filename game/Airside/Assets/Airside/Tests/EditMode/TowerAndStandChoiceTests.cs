using System.Reflection;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Runway fairness for long-holding departures and stand choice beside a Dash 8-400.</summary>
    public sealed class TowerAndStandChoiceTests
    {
        private static readonly StableId Bay50D = new("BAY-1");
        private static readonly StableId Bay50E = new("BAY-5");

        private static (ManualSimulationClock clock, AirlineOperations ops, Airline player, Airline other) Empty()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(9), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideStands);
            var player = Airline.Player("Test Air", "#39708A");
            var other = new Airline("OTH", "Other Air", "#C95D50", isPlayer: false);
            ops.AddAirline(player);
            ops.AddAirline(other);
            return (clock, ops, player, other);
        }

        private static Destination Kgc()
        {
            Assert.That(DestinationCatalogue.TryFind("KGC", out var kgc), Is.True);
            return kgc;
        }

        private static FleetAircraft Restore(AirlineOperations ops, string registration, Airline airline, AircraftType type,
            FleetState state, long startedAt, StableId stand, Destination? destination = null)
        {
            var method = typeof(AirlineOperations).GetMethod("RestoreAircraft", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(ops, new object[]
            {
                registration, airline, type, state, new SimulationTime(startedAt), null, stand, default(StableId),
                destination, null, 0
            });
            return ops.Fleet[ops.Fleet.Count - 1];
        }

        /// <summary>Runway occupied until <paramref name="seconds"/>, so the tower decides then.</summary>
        private static void RunwayBusyUntil(AirlineOperations ops, long seconds)
        {
            var method = typeof(AirlineOperations).GetMethod("RestoreTower", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(ops, new object[] { new SimulationTime(seconds), 0L });
        }

        private static void RunTo(ManualSimulationClock clock, AirlineOperations ops, long seconds)
        {
            clock.Set(new SimulationTime(seconds));
            ops.Update();
        }

        [Test]
        public void Tower_ReleasesADepartureThatHasHeldTooLong()
        {
            var (clock, ops, player, _) = Empty();
            var departure = Restore(ops, "VH-DEP", player, AircraftType.Atr42, FleetState.HoldingShort, 0, default, Kgc());
            var arrival = Restore(ops, "VH-ARR", player, AircraftType.Atr42, FleetState.HoldingForLanding, 60, default, Kgc());

            RunwayBusyUntil(ops, AirlineOperations.DepartureMaxHoldSeconds);
            RunTo(clock, ops, AirlineOperations.DepartureMaxHoldSeconds);

            Assert.That(departure.State, Is.EqualTo(FleetState.TakingOff));
            Assert.That(arrival.State, Is.EqualTo(FleetState.HoldingForLanding));
        }

        [Test]
        public void Tower_StillLandsFirstWhenTheDepartureHasNotHeldLong()
        {
            var (clock, ops, player, _) = Empty();
            var departure = Restore(ops, "VH-DEP", player, AircraftType.Atr42, FleetState.HoldingShort, 0, default, Kgc());
            var arrival = Restore(ops, "VH-ARR", player, AircraftType.Atr42, FleetState.HoldingForLanding, 30, default, Kgc());

            RunwayBusyUntil(ops, AirlineOperations.DepartureMaxHoldSeconds - 60);
            RunTo(clock, ops, AirlineOperations.DepartureMaxHoldSeconds - 60);

            Assert.That(arrival.State, Is.EqualTo(FleetState.Landing));
            Assert.That(departure.State, Is.EqualTo(FleetState.HoldingShort));
        }

        [Test]
        public void Tower_LandsAnArrivalThatHasCircledLongerThanTheDepartureHeld()
        {
            var (clock, ops, player, _) = Empty();
            var arrival = Restore(ops, "VH-ARR", player, AircraftType.Atr42, FleetState.HoldingForLanding, 0, default, Kgc());
            var departure = Restore(ops, "VH-DEP", player, AircraftType.Atr42, FleetState.HoldingShort, 10, default, Kgc());

            RunwayBusyUntil(ops, AirlineOperations.DepartureMaxHoldSeconds + 60);
            RunTo(clock, ops, AirlineOperations.DepartureMaxHoldSeconds + 60);

            Assert.That(arrival.State, Is.EqualTo(FleetState.Landing));
            Assert.That(departure.State, Is.EqualTo(FleetState.HoldingShort));
        }

        [Test]
        public void SuggestStand_AvoidsTheBayBesideAParkedDash8WhenAnotherIsFree()
        {
            var (_, ops, player, other) = Empty();
            Restore(ops, "VH-QQQ", other, AircraftType.Dash8Q400, FleetState.AtStand, 0, Bay50D);
            var waiting = Restore(ops, "VH-WAI", player, AircraftType.Atr42, FleetState.AwaitingStand, 0, default);

            Assert.That(ops.CrowdsNeighbour(AircraftType.Atr42, Bay50E), Is.True);
            var suggested = ops.SuggestStand(waiting);
            Assert.That(suggested.HasValue, Is.True);
            Assert.That(suggested.Value, Is.Not.EqualTo(Bay50E));

            // Among the rest it is still the shortest taxi in.
            foreach (var stand in ops.FreeStandsFor(AircraftType.Atr42))
                if (!ops.CrowdsNeighbour(AircraftType.Atr42, stand))
                    Assert.That(AirlineOperations.TaxiInSecondsTo(suggested.Value),
                        Is.LessThanOrEqualTo(AirlineOperations.TaxiInSecondsTo(stand)));
        }

        [Test]
        public void SuggestStand_StillUsesTheTightBayWhenItIsTheOnlyOneFree()
        {
            var (_, ops, player, other) = Empty();
            Restore(ops, "VH-QQQ", other, AircraftType.Dash8Q400, FleetState.AtStand, 0, Bay50D);
            var n = 0;
            foreach (var bay in AirlineOperations.AdelaideRegionalBays)
                if (!bay.Equals(Bay50D) && !bay.Equals(Bay50E))
                    Restore(ops, $"VH-P{n++}", player, AircraftType.Atr42, FleetState.AtStand, 0, bay);
            var waiting = Restore(ops, "VH-WAI", other, AircraftType.Saab340, FleetState.AwaitingStand, 0, default);

            Assert.That(ops.SuggestStand(waiting), Is.EqualTo((StableId?)Bay50E));
        }

        [Test]
        public void AiAircraft_TaxiToTheSuggestedStand()
        {
            var (clock, ops, _, other) = Empty();
            Restore(ops, "VH-QQQ", other, AircraftType.Dash8Q400, FleetState.AtStand, 0, Bay50D);
            var waiting = Restore(ops, "VH-AIW", other, AircraftType.Saab340, FleetState.AwaitingStand, 0, default);
            var expected = ops.SuggestStand(waiting);

            RunTo(clock, ops, 1);

            Assert.That(waiting.State, Is.EqualTo(FleetState.TaxiIn));
            Assert.That(waiting.Stand, Is.EqualTo(expected.Value));
            Assert.That(waiting.Stand, Is.Not.EqualTo(Bay50E));
        }
    }
}
