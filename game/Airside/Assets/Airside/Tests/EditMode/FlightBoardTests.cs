using System.Collections.Generic;
using System.Reflection;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Flights board labels and sort order (presentation helpers only).</summary>
    public sealed class FlightBoardTests
    {
        private static Destination Code(string code)
        {
            Assert.That(DestinationCatalogue.TryFind(code, out var destination), Is.True, code);
            return destination;
        }

        private static (ManualSimulationClock clock, AirlineOperations ops, FleetAircraft plane) PlayerOnly(int aircraft = 1)
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(11), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Test Air", "#39708A");
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
        public void RouteText_UsesOutboundAndInboundArrows()
        {
            var (_, ops, aircraft) = PlayerOnly();
            ops.ScheduleDeparture(aircraft, Code("KGC"), new SimulationTime(600));

            Assert.That(FlightBoard.RouteText(aircraft), Is.EqualTo("ADL → KGC"));
            Assert.That(FlightBoard.PhaseLabel(aircraft), Is.EqualTo("Scheduled"));
            Assert.That(FlightBoard.TimeLabel(aircraft, t => $"T{t.ElapsedSeconds}"), Is.EqualTo("T600"));
            Assert.That(FlightBoard.TimeMeaning(aircraft), Is.EqualTo("DEPARTS"));
        }

        [Test]
        public void Sort_OrdersByNextInterestingTime_IdleLast()
        {
            var (_, ops, later) = PlayerOnly(aircraft: 3);
            var sooner = ops.Fleet[1];
            var parked = ops.Fleet[2];
            ops.ScheduleDeparture(later, Code("PLO"), new SimulationTime(1_800));
            ops.ScheduleDeparture(sooner, Code("KGC"), new SimulationTime(600));

            var list = new List<FleetAircraft> { later, parked, sooner };
            FlightBoard.Sort(list);

            Assert.That(list[0].Registration, Is.EqualTo(sooner.Registration));
            Assert.That(list[1].Registration, Is.EqualTo(later.Registration));
            Assert.That(list[2].Registration, Is.EqualTo(parked.Registration));
            Assert.That(FlightBoard.SortKeySeconds(sooner), Is.LessThan(FlightBoard.SortKeySeconds(later)));
            Assert.That(FlightBoard.SortKeySeconds(later), Is.LessThan(FlightBoard.SortKeySeconds(parked)));
        }

        [Test]
        public void PhaseLabel_CoversAwayAndArrivalStates()
        {
            var (clock, ops, aircraft) = PlayerOnly();
            ops.ScheduleDeparture(aircraft, Code("BHQ"), new SimulationTime(600));

            var airborne = ops.AirborneSeconds(aircraft, Code("BHQ"));
            var outboundAt = 600 + AirlineOperations.TaxiOutSecondsFrom(aircraft.Stand)
                             + AirlineOperations.TakeoffRunwaySeconds + 1;
            RunTo(clock, ops, outboundAt);
            Assert.That(aircraft.State, Is.EqualTo(FleetState.Outbound));
            Assert.That(FlightBoard.RouteText(aircraft), Is.EqualTo("ADL → BHQ"));
            Assert.That(FlightBoard.PhaseLabel(aircraft), Is.EqualTo("Departed"));
            Assert.That(FlightBoard.TimeMeaning(aircraft), Is.EqualTo("ARRIVES"));

            var inboundAt = outboundAt - 1 + airborne + AirlineOperations.DestinationTurnaroundSeconds + 1;
            RunTo(clock, ops, inboundAt);
            Assert.That(aircraft.State, Is.EqualTo(FleetState.Inbound));
            Assert.That(FlightBoard.RouteText(aircraft), Is.EqualTo("BHQ → ADL"));
            Assert.That(FlightBoard.PhaseLabel(aircraft), Is.EqualTo("Inbound"));
            Assert.That(FlightBoard.TimeMeaning(aircraft), Is.EqualTo("ETA"));
        }

        [TestCase(FleetState.HoldingShort, "HOLD SINCE")]
        [TestCase(FleetState.HoldingForLanding, "ON FINAL")]
        [TestCase(FleetState.Landing, "ON RUNWAY")]
        [TestCase(FleetState.TakingOff, "DEPARTING")]
        [TestCase(FleetState.TaxiIn, "ETA STAND")]
        [TestCase(FleetState.AwaitingStand, "WAIT SINCE")]
        public void TimeMeaning_DescribesTheActualMilestone(FleetState state, string expected)
        {
            Assert.That(FlightBoard.TimeMeaning(state), Is.EqualTo(expected));
        }

        [TestCase(FleetState.TaxiOut, "Taxiing")]
        [TestCase(FleetState.TaxiIn, "Taxiing")]
        [TestCase(FleetState.TakingOff, "Departing")]
        [TestCase(FleetState.Outbound, "Departed")]
        [TestCase(FleetState.AwaitingStand, "Landed")]
        public void PhaseLabel_MatchesTheMovementOnTheField(FleetState state, string expected)
        {
            var (_, _, aircraft) = PlayerOnly();
            var enter = typeof(FleetAircraft).GetMethod("Enter", BindingFlags.Instance | BindingFlags.NonPublic);
            enter.Invoke(aircraft, new object[] { state, new SimulationTime(0), (long?)60 });
            Assert.That(FlightBoard.PhaseLabel(aircraft), Is.EqualTo(expected));
            // TakingOff at t=0 is still lining up — the live chip says so.
            var compact = state == FleetState.TakingOff ? "Lining up" : expected;
            Assert.That(OperationsSummary.CompactState(aircraft, new SimulationTime(0)), Is.EqualTo(compact));
        }

        [Test]
        public void PhaseLabel_OnStandWhenNothingIsPlanned()
        {
            var (_, _, aircraft) = PlayerOnly();
            Assert.That(FlightBoard.PhaseLabel(aircraft), Is.EqualTo("On stand"));
            Assert.That(FlightBoard.RouteText(aircraft), Does.StartWith("Bay "));
            Assert.That(FlightBoard.SortKeySeconds(aircraft), Is.EqualTo(long.MaxValue));
        }

        [Test]
        public void IsArrival_ExcludesAircraftStillAwayAtDestination()
        {
            var (_, _, aircraft) = PlayerOnly();
            var enter = typeof(FleetAircraft).GetMethod("Enter", BindingFlags.Instance | BindingFlags.NonPublic);
            enter.Invoke(aircraft, new object[] { FleetState.AtDestination, new SimulationTime(0), (long?)60 });
            Assert.That(FlightBoard.IsArrival(aircraft), Is.False,
                "away aircraft must not clutter the Arrivals board");
            enter.Invoke(aircraft, new object[] { FleetState.Inbound, new SimulationTime(0), (long?)60 });
            Assert.That(FlightBoard.IsArrival(aircraft), Is.True);
        }

        [Test]
        public void DelayedDeparture_IsClearlyLabelledAfterOneMinute()
        {
            var (_, ops, aircraft) = PlayerOnly();
            ops.ScheduleDeparture(aircraft, Code("KGC"), new SimulationTime(600));
            var prepStart = aircraft.PrepStartedAt!.Value.ElapsedSeconds;

            Assert.That(FlightBoard.DepartureDelayMinutes(aircraft, new SimulationTime(659)), Is.Zero);
            Assert.That(FlightBoard.PhaseLabel(aircraft, new SimulationTime(prepStart + 45)), Is.EqualTo("Fuelling 50%"));
            Assert.That(FlightBoard.PhaseLabel(aircraft, new SimulationTime(659)), Is.EqualTo("Ready"));
            Assert.That(FlightBoard.DepartureDelayMinutes(aircraft, new SimulationTime(720)), Is.EqualTo(2));
            Assert.That(FlightBoard.PhaseLabel(aircraft, new SimulationTime(720)), Is.EqualTo("Gate hold"));
            Assert.That(FlightBoard.TimeMeaning(aircraft, new SimulationTime(720)), Is.EqualTo("LATE +2 MIN"));
        }

        [Test]
        public void DelayedAndCancelledBookings_ShowOnTheChip()
        {
            var (_, ops, aircraft) = PlayerOnly();
            ops.ScheduleDeparture(aircraft, Code("KGC"), new SimulationTime(600));
            aircraft.Scheduled = new ScheduledDeparture(Code("KGC"), new SimulationTime(900), 15);
            Assert.That(FlightBoard.PhaseLabel(aircraft, new SimulationTime(100)), Is.EqualTo("Delayed +15"));
            aircraft.Scheduled = new ScheduledDeparture(Code("KGC"), new SimulationTime(900), 0, cancelled: true);
            Assert.That(FlightBoard.PhaseLabel(aircraft, new SimulationTime(100)), Is.EqualTo("Cancelled"));
        }

        [Test]
        public void EstimatedTime_OnDeparturesNeverShowsDestinationEta()
        {
            // Bailey's FIDS lie: Departed SIA488 at 07:46 with "est 13:55" — that second
            // clock was Singapore arrival, mislabeled as a departure estimate.
            var (clock, ops, aircraft) = PlayerOnly();
            ops.ScheduleDeparture(aircraft, Code("BHQ"), new SimulationTime(600));
            var outboundAt = 600 + AirlineOperations.TaxiOutSecondsFrom(aircraft.Stand)
                             + AirlineOperations.TakeoffRunwaySeconds + 1;
            RunTo(clock, ops, outboundAt);
            Assert.That(aircraft.State, Is.EqualTo(FleetState.Outbound));
            Assert.That(aircraft.StateEndsAt.HasValue, Is.True, "sim still tracks destination ETA");

            string Clock(SimulationTime t) => $"T{t.ElapsedSeconds}";
            Assert.That(FlightBoard.EstimatedTime(aircraft, arrivals: false, Clock), Is.EqualTo("—"));
            Assert.That(FlightBoard.EstimatedTime(aircraft, arrivals: true, Clock),
                Is.EqualTo(Clock(aircraft.StateEndsAt.Value)),
                "arrivals may still surface the touchdown/away estimate");
        }
    }
}
