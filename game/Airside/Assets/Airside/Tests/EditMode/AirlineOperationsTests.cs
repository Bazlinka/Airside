using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Player airline, AI airline and the tower at Adelaide (ADR 0045).</summary>
    public sealed class AirlineOperationsTests
    {
        private static Destination Code(string code)
        {
            Assert.That(DestinationCatalogue.TryFind(code, out var destination), Is.True, code);
            return destination;
        }

        private static Airline Player() => Airline.Player("Southern Cross Regional", "#C8102E");

        /// <summary>Home with only the player's airline, so nothing moves unless asked.</summary>
        private static (ManualSimulationClock clock, AirlineOperations ops, FleetAircraft plane) PlayerOnly(int aircraft = 1)
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Player();
            ops.AddAirline(player);
            FleetAircraft first = null;
            for (var i = 0; i < aircraft; i++)
            {
                var added = ops.AddAircraft(player, $"VH-PA{(char)('A' + i)}", AircraftType.Atr42,
                    AirlineOperations.AdelaideRegionalBays[i]);
                first ??= added;
            }

            return (clock, ops, first);
        }

        private static void RunTo(ManualSimulationClock clock, AirlineOperations ops, long seconds)
        {
            clock.Set(new SimulationTime(seconds));
            ops.Update();
        }

        [Test]
        public void Adelaide_IsTheDefaultLocation()
        {
            Assert.That(AirportLocation.Default, Is.EqualTo(AirportLocation.Adelaide));
            Assert.That(AirportLocation.FromId("adl").Name, Is.EqualTo("Adelaide"));
        }

        [TestCase("KGC", 110, 130)]
        [TestCase("PLO", 240, 270)]
        [TestCase("MGB", 350, 400)]
        [TestCase("MEL", 630, 670)]
        [TestCase("SYD", 1140, 1190)]
        [TestCase("PER", 2100, 2150)]
        [TestCase("DRW", 2590, 2660)]
        public void LegDistances_MatchRealGreatCircleFigures(string code, double minKm, double maxKm)
        {
            var km = DestinationCatalogue.Adelaide.DistanceKmTo(Code(code));
            Assert.That(km, Is.InRange(minKm, maxKm));
        }

        [Test]
        public void Atr42_ReachesRegionalAndSouthEastButNotTheFarCities()
        {
            var (_, ops, plane) = PlayerOnly();
            foreach (var code in new[] { "KGC", "PLO", "WYA", "MGB", "CED", "CPD", "MQL", "BHQ", "MEL", "CBR" })
                Assert.That(ops.CanReach(plane, Code(code)), Is.True, code);
            foreach (var code in new[] { "SYD", "HBA", "ASP", "BNE", "OOL", "CNS", "DRW", "PER" })
                Assert.That(ops.CanReach(plane, Code(code)), Is.False, code);
            Assert.That(ops.MapDestinations().Any(d => d.Equals(DestinationCatalogue.Adelaide)), Is.False);
        }

        [Test]
        public void LegTiming_IsRealLength()
        {
            var (_, ops, plane) = PlayerOnly();
            Assert.That(ops.AirborneSeconds(plane, Code("KGC")) / 60.0, Is.InRange(20, 26));
            Assert.That(ops.AirborneSeconds(plane, Code("MEL")) / 60.0, Is.InRange(75, 85));
        }

        [Test]
        public void PlayerRoundTrip_GoesOutAndBackThenWaitsForAStand()
        {
            var (clock, ops, plane) = PlayerOnly();
            var kingscote = Code("KGC");
            Assert.That(ops.ScheduleDeparture(plane, kingscote, new SimulationTime(600)).Accepted, Is.True);

            RunTo(clock, ops, 599);
            Assert.That(plane.State, Is.EqualTo(FleetState.AtStand));
            Assert.That(ops.IsStandFree(AirlineOperations.AdelaideRegionalBays[0]), Is.False);

            RunTo(clock, ops, 600);
            Assert.That(plane.State, Is.EqualTo(FleetState.TaxiOut));
            Assert.That(ops.IsStandFree(AirlineOperations.AdelaideRegionalBays[0]), Is.True, "stand released at pushback");

            var takeoffAt = 600 + AirlineOperations.TaxiOutSecondsFrom(AirlineOperations.AdelaideRegionalBays[0]);
            RunTo(clock, ops, takeoffAt);
            Assert.That(plane.State, Is.EqualTo(FleetState.TakingOff), "empty runway: no hold");

            var outboundAt = takeoffAt + AirlineOperations.TakeoffRunwaySeconds;
            RunTo(clock, ops, outboundAt + 1);
            Assert.That(plane.State, Is.EqualTo(FleetState.Outbound));
            Assert.That(plane.IsOffMap, Is.True);
            Assert.That(plane.CurrentDestination, Is.EqualTo(kingscote));

            var airborne = ops.AirborneSeconds(plane, kingscote);
            var landingAt = outboundAt + airborne + AirlineOperations.DestinationTurnaroundSeconds + airborne;
            RunTo(clock, ops, landingAt);
            Assert.That(plane.State, Is.EqualTo(FleetState.Landing));

            RunTo(clock, ops, landingAt + AirlineOperations.LandingRunwaySeconds + 3600);
            Assert.That(plane.State, Is.EqualTo(FleetState.AwaitingStand), "player aircraft wait for the player");
            Assert.That(ops.NextEventAt(), Is.Null);

            var bay = AirlineOperations.AdelaideRegionalBays[3];
            Assert.That(ops.AssignStand(plane, bay).Accepted, Is.True);
            RunTo(clock, ops, clock.Now.ElapsedSeconds + AirlineOperations.TaxiInSecondsTo(bay));
            Assert.That(plane.State, Is.EqualTo(FleetState.AtStand));
            Assert.That(plane.Stand, Is.EqualTo(bay));
            Assert.That(plane.CompletedTrips, Is.EqualTo(1));
            Assert.That(plane.CurrentDestination, Is.Null);
        }

        [Test]
        public void Commands_RefuseWhatTheAirportWouldNotAllow()
        {
            var (clock, ops, plane) = PlayerOnly(aircraft: 2);
            var other = ops.Fleet[1];

            Assert.That(ops.ScheduleDeparture(plane, Code("PER"), new SimulationTime(60)).Accepted, Is.False, "out of range");
            Assert.That(ops.ScheduleDeparture(plane, DestinationCatalogue.Adelaide, new SimulationTime(60)).Accepted, Is.False, "home");
            Assert.That(ops.AssignStand(plane, AirlineOperations.AdelaideRegionalBays[2]).Accepted, Is.False, "not awaiting a stand");

            RunTo(clock, ops, 100);
            Assert.That(ops.ScheduleDeparture(plane, Code("KGC"), new SimulationTime(50)).Accepted, Is.False, "in the past");

            ops.ScheduleDeparture(plane, Code("KGC"), new SimulationTime(100));
            RunTo(clock, ops, 101);
            Assert.That(ops.ScheduleDeparture(plane, Code("MEL"), new SimulationTime(200)).Accepted, Is.False, "already taxiing");
            Assert.That(ops.CancelDeparture(plane).Accepted, Is.False);

            Assert.That(ops.ScheduleDeparture(other, Code("MEL"), new SimulationTime(5000)).Accepted, Is.True);
            Assert.That(ops.CancelDeparture(other).Accepted, Is.True);
            Assert.That(other.Scheduled, Is.Null);

            // Fly the first home and try to park it on the occupied bay.
            RunTo(clock, ops, 20000);
            Assert.That(plane.State, Is.EqualTo(FleetState.AwaitingStand));
            Assert.That(ops.AssignStand(plane, other.Stand).Accepted, Is.False, "occupied");
            Assert.That(ops.AssignStand(plane, new StableId("BAY-99")).Accepted, Is.False, "not a stand");
        }

        [Test]
        public void Tower_SequencesSimultaneousDeparturesWithSeparation()
        {
            var (clock, ops, first) = PlayerOnly(aircraft: 2);
            var second = ops.Fleet[1];
            // Stagger the pushbacks so both reach the holding point in the same second.
            var holdingAt = 1000 + AirlineOperations.TaxiOutSecondsFrom(first.Stand);
            ops.ScheduleDeparture(first, Code("KGC"), new SimulationTime(1000));
            ops.ScheduleDeparture(second, Code("PLO"), new SimulationTime(holdingAt - AirlineOperations.TaxiOutSecondsFrom(second.Stand)));

            RunTo(clock, ops, holdingAt);
            Assert.That(first.State, Is.EqualTo(FleetState.TakingOff));
            Assert.That(second.State, Is.EqualTo(FleetState.HoldingShort));

            var released = holdingAt + AirlineOperations.TakeoffRunwaySeconds + AirlineOperations.RunwaySeparationSeconds;
            RunTo(clock, ops, released - 1);
            Assert.That(second.State, Is.EqualTo(FleetState.HoldingShort));
            RunTo(clock, ops, released);
            Assert.That(second.State, Is.EqualTo(FleetState.TakingOff));
        }

        [Test]
        public void Tower_LandsArrivalsBeforeReleasingDepartures()
        {
            var (clock, ops, arriving) = PlayerOnly(aircraft: 2);
            var departing = ops.Fleet[1];
            ops.ScheduleDeparture(arriving, Code("KGC"), new SimulationTime(0));
            var airborne = ops.AirborneSeconds(arriving, Code("KGC"));
            var backInCircuit = AirlineOperations.TaxiOutSecondsFrom(arriving.Stand) + AirlineOperations.TakeoffRunwaySeconds
                                + airborne + AirlineOperations.DestinationTurnaroundSeconds + airborne;

            // The departure reaches the holding point at the same second the arrival does.
            ops.ScheduleDeparture(departing, Code("PLO"),
                new SimulationTime(backInCircuit - AirlineOperations.TaxiOutSecondsFrom(departing.Stand)));
            RunTo(clock, ops, backInCircuit);

            Assert.That(arriving.State, Is.EqualTo(FleetState.Landing));
            Assert.That(departing.State, Is.EqualTo(FleetState.HoldingShort));
        }

        [Test]
        public void Timeline_IsIdenticalForAnyStepSizeOrSkipping()
        {
            var horizon = 36 * 3600L;

            string Run(Func<ManualSimulationClock, AirlineOperations, bool> step)
            {
                var clock = new ManualSimulationClock(new SimulationTime(0));
                var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(2026), Player());
                var mine = ops.FleetOf(ops.PlayerAirline).Single();
                ops.ScheduleDeparture(mine, Code("MEL"), new SimulationTime(1800));
                // The player's plane is left waiting for a stand once home: a stand choice
                // made "when noticed" would differ by step size, which is the player's
                // doing rather than the simulation's.
                while (clock.Now.ElapsedSeconds < horizon && step(clock, ops))
                {
                }

                clock.Set(new SimulationTime(horizon));
                ops.Update();
                return Snapshot(ops);
            }

            var bySecond = Run((c, o) => { c.Advance(1); o.Update(); return true; });
            var byOddSteps = Run((c, o) => { c.Advance(Math.Min(horizon - c.Now.ElapsedSeconds, c.Now.ElapsedSeconds % 7 == 0 ? 613 : 37)); o.Update(); return true; });
            var bySkipping = Run((c, o) =>
            {
                var next = o.NextEventAt();
                if (next == null || next.Value.ElapsedSeconds > horizon) return false;
                c.Set(next.Value);
                o.Update();
                return true;
            });

            Assert.That(byOddSteps, Is.EqualTo(bySecond));
            Assert.That(bySkipping, Is.EqualTo(bySecond));
        }

        [Test]
        public void AiAirline_FliesAllDayWithoutDoubleBookingStandsOrRunway()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(11), Player());
            var emu = ops.Airlines.Single(a => a.Name == "Emu Air");

            Assert.That(ops.FleetOf(emu).Count(), Is.EqualTo(2));
            Assert.That(ops.FleetOf(emu).All(a => a.Scheduled.HasValue), Is.True, "AI schedules itself");

            for (var steps = 0; steps < 10000; steps++)
            {
                var next = ops.NextEventAt();
                if (next == null || next.Value.ElapsedSeconds > 48 * 3600)
                    break;
                clock.Set(next.Value);
                ops.Update();

                var onRunway = ops.Fleet.Count(a => a.State is FleetState.TakingOff or FleetState.Landing);
                Assert.That(onRunway, Is.LessThanOrEqualTo(1));
                var held = ops.Fleet.Where(a => a.State is FleetState.AtStand or FleetState.TaxiIn).Select(a => a.Stand).ToList();
                Assert.That(held.Distinct().Count(), Is.EqualTo(held.Count));
            }

            Assert.That(ops.FleetOf(emu).Sum(a => a.CompletedTrips), Is.GreaterThanOrEqualTo(10));
            Assert.That(ops.FleetOf(ops.PlayerAirline).Single().State, Is.EqualTo(FleetState.AtStand), "player plane untouched");
        }

        [Test]
        public void Airline_ValidatesLiveryAndSinglePlayer()
        {
            Assert.Throws<ArgumentException>(() => Airline.Player("X", "red"));
            Assert.That(Airline.Player("X", "#00ff80").LiveryRgb(), Is.EqualTo(((byte)0, (byte)255, (byte)128)));

            var (_, ops, _) = PlayerOnly();
            Assert.Throws<InvalidOperationException>(() => ops.AddAirline(Airline.Player("Another", "#000000")));
        }

        private static string Snapshot(AirlineOperations ops)
        {
            var text = new StringBuilder();
            foreach (var a in ops.Fleet)
            {
                text.Append(a.Registration).Append(' ').Append(a.State)
                    .Append(" since ").Append(a.StateStartedAt.ElapsedSeconds)
                    .Append(" stand ").Append(a.Stand.Value)
                    .Append(" to ").Append(a.CurrentDestination?.Code)
                    .Append(" next ").Append(a.Scheduled?.Destination.Code).Append('@').Append(a.Scheduled?.DepartAt.ElapsedSeconds)
                    .Append(" trips ").Append(a.CompletedTrips).AppendLine();
            }

            return text.ToString();
        }
    }
}
