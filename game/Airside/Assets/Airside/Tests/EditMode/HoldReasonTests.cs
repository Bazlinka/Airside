using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// ADR 0124 — every hold explains itself. <see cref="AirlineOperations.Why"/> re-asks the same
    /// questions as the pushback, stand and tower decisions without changing anything.
    /// </summary>
    public sealed class HoldReasonTests
    {
        private static (ManualSimulationClock clock, AirlineOperations ops, Airline player, Airline other) Empty(
            bool regionalOnly = false)
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(9), DestinationCatalogue.Adelaide,
                regionalOnly ? AirlineOperations.AdelaideRegionalBays : AirlineOperations.AdelaideStands);
            var player = Airline.Player("Test Air", "#39708A");
            var other = new Airline("OTH", "Other Air", "#C95D50", isPlayer: false);
            ops.AddAirline(player);
            ops.AddAirline(other);
            return (clock, ops, player, other);
        }

        private static Destination Kgc() => HudTestAirline.Code("KGC");

        private static FleetAircraft Restore(AirlineOperations ops, string registration, Airline airline, FleetState state,
            long startedAt, StableId stand = default, ScheduledDeparture? scheduled = null)
        {
            var departureStand = AirlineOperations.RequiresDepartureStand(state) ? AirlineOperations.AdelaideRegionalBays[0] : default;
            ops.RestoreAircraft(registration, airline, AircraftType.Atr42, state, new SimulationTime(startedAt), null,
                stand, departureStand, state == FleetState.AtStand ? null : Kgc(), scheduled, 0);
            return ops.Fleet[ops.Fleet.Count - 1];
        }

        private static void RunTo(ManualSimulationClock clock, AirlineOperations ops, long seconds)
        {
            clock.Set(new SimulationTime(seconds));
            ops.Update();
        }

        private static long FirstTime(System.Func<long, bool> test)
        {
            for (long t = 3600; t < 60L * 86400; t += 600)
                if (test(t))
                    return t;
            Assert.Fail("no such time in the first 60 days");
            return 0;
        }

        [Test]
        public void SecondDepartureInTheQueue_SaysItsNumberAndWhoIsAhead()
        {
            var (_, ops, player, _) = Empty();
            var first = Restore(ops, "VH-ONE", player, FleetState.HoldingShort, 0);
            var second = Restore(ops, "VH-TWO", player, FleetState.HoldingShort, 20);
            var reason = ops.Why(second);
            if (reason.Kind == HoldKind.GroundStop)
                Assert.Inconclusive("storm at t=0 in this seed");
            Assert.That(reason.Kind, Is.EqualTo(HoldKind.Queued));
            Assert.That(reason.Position, Is.EqualTo(2));
            Assert.That(reason.Blocker, Is.SameAs(first));
            var text = HoldReasonText.Long(second, reason, new SimulationTime(0));
            Assert.That(text, Does.StartWith("Number 2 for"));
            Assert.That(text, Does.Contain(FlightNumber.OrRegistration(first)));
        }

        [Test]
        public void HoldingShort_WhileAnArrivalGoesFirst_NamesTheArrival()
        {
            var (_, ops, player, _) = Empty();
            var departure = Restore(ops, "VH-DEP", player, FleetState.HoldingShort, 0);
            var arrival = Restore(ops, "VH-ARR", player, FleetState.HoldingForLanding, 0);
            var reason = ops.Why(departure);
            Assert.That(reason.Kind, Is.EqualTo(HoldKind.ArrivalFirst).Or.EqualTo(HoldKind.GroundStop));
            if (reason.Kind == HoldKind.ArrivalFirst)
            {
                Assert.That(reason.Blocker, Is.SameAs(arrival));
                Assert.That(HoldReasonText.Long(departure, reason, new SimulationTime(0)), Does.Contain("lands first"));
                Assert.That(HoldReasonText.Short(reason), Does.StartWith("hold ·"));
            }
        }

        [Test]
        public void HoldingShort_WhileTheRunwayIsBusy_NamesTheMovementOrItsWake()
        {
            var (clock, ops, player, _) = Empty();
            var departure = Restore(ops, "VH-DEP", player, FleetState.HoldingShort, 0);
            var arrival = Restore(ops, "VH-ARR", player, FleetState.HoldingForLanding, 0);
            if (Weather.At(new SimulationTime(0)) == WeatherKind.Storm)
                Assert.Inconclusive("storm at t=0 in this seed");
            var t = 0L;
            while (arrival.State != FleetState.Landing && t < 120)
                RunTo(clock, ops, ++t);
            Assert.That(arrival.State, Is.EqualTo(FleetState.Landing), "the arrival is cleared first");
            Assert.That(departure.State, Is.EqualTo(FleetState.HoldingShort));
            var reason = ops.Why(departure);
            Assert.That(reason.Kind, Is.EqualTo(HoldKind.RunwayOccupied).Or.EqualTo(HoldKind.WakeSeparation));
            Assert.That(reason.Blocker, Is.SameAs(arrival));
            Assert.That(reason.Until.HasValue, Is.True);
            Assert.That(HoldReasonText.Long(departure, reason, new SimulationTime(t)), Does.Contain(FlightNumber.OrRegistration(arrival)));
        }

        [Test]
        public void Storm_IsAGroundStop()
        {
            var storm = FirstTime(s => Weather.At(new SimulationTime(s)) == WeatherKind.Storm);
            var clock = new ManualSimulationClock(new SimulationTime(storm));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Test Air", "#39708A");
            ops.AddAirline(player);
            var departure = Restore(ops, "VH-DEP", player, FleetState.HoldingShort, storm);
            Assert.That(ops.Why(departure).Kind, Is.EqualTo(HoldKind.GroundStop));
            Assert.That(HoldReasonText.Long(departure, ops.Why(departure), new SimulationTime(storm)), Does.Contain("storm"));
        }

        [Test]
        public void HeldPushbackOnABusyApron_NamesTheTaxiingAircraft()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Test Air", "#39708A");
            ops.AddAirline(player);
            var planes = Enumerable.Range(0, 3).Select(i => ops.AddAircraft(player, $"VH-PA{(char)('A' + i)}",
                AircraftType.Saab340, AirlineOperations.AdelaideRegionalBays[i])).ToList();
            foreach (var plane in planes)
                ops.ScheduleDeparture(plane, Kgc(), new SimulationTime(600));
            RunTo(clock, ops, 600);
            // Swept-path control releases the safe first push and explains the holds behind it.
            var waiting = planes.First(p => p.State == FleetState.AtStand);
            var taxiing = planes.Where(p => p.State == FleetState.TaxiOut).ToList();
            Assert.That(taxiing.Count, Is.EqualTo(1));
            var reason = ops.Why(waiting);
            Assert.That(reason.Kind, Is.EqualTo(HoldKind.TaxiwayBlocked));
            Assert.That(reason.Blocker, Is.SameAs(taxiing[0]));
            Assert.That(reason.Until.HasValue, Is.False, "swept-path holds clear when the blocker moves");
            var text = HoldReasonText.Long(waiting, reason, new SimulationTime(600));
            Assert.That(text, Does.Contain("taxi"));
            Assert.That(text, Does.Contain(taxiing[0].Registration).Or.Contain(FlightNumber.OrRegistration(taxiing[0])));
        }

        [Test]
        public void LandedWithEveryBayTaken_SaysNoStandIsFree()
        {
            var (_, ops, player, other) = Empty(regionalOnly: true);
            var i = 0;
            foreach (var bay in AirlineOperations.AdelaideRegionalBays)
            {
                // A bay too small for an ATR 42 cannot take the landed one either.
                try { Restore(ops, $"VH-X{i++:00}", other, FleetState.AtStand, 0, bay); }
                catch (System.FormatException) { }
            }
            var landed = Restore(ops, "VH-LND", player, FleetState.AwaitingStand, 0);
            var reason = ops.Why(landed);
            Assert.That(reason.Kind, Is.EqualTo(HoldKind.NoStandFree));
            Assert.That(HoldReasonText.Long(landed, reason, new SimulationTime(0)), Does.StartWith("Waiting for a stand"));
        }

        [Test]
        public void LandedPlayerAircraft_IsWaitingForThePlayersStandChoice()
        {
            var (_, ops, player, _) = Empty();
            ops.RestoreCareerState(20_000, 100, nameof(OperatingTier.Provisional), null, 0, 0,
                System.Array.Empty<string>(), System.Array.Empty<string>(), 0,
                baseLevel: PlayerBaseLevel.ExpandedRegional);
            var landed = Restore(ops, "VH-LND", player, FleetState.AwaitingStand, 0);
            var reason = ops.Why(landed);
            Assert.That(reason.Kind, Is.EqualTo(HoldKind.ChooseStand));
            Assert.That(reason.Until.Value.ElapsedSeconds, Is.EqualTo(AirlineOperations.PlayerStandAutoSeconds));
        }

        [Test]
        public void CommercialDepartureDuringCurfew_WaitsForTheMorning()
        {
            var (clock, ops, _, other) = Empty();
            var night = FirstTime(s => AirportCurfew.IsClosed(new SimulationTime(s), ops.Clock)
                                       && AirportCurfew.IsClosed(new SimulationTime(s - 60), ops.Clock));
            var aircraft = Restore(ops, "VH-NGT", other, FleetState.AtStand, 0, AirlineOperations.AdelaideRegionalBays[0],
                new ScheduledDeparture(Kgc(), new SimulationTime(night - 60)));
            RunTo(clock, ops, night);
            var reason = ops.Why(aircraft);
            Assert.That(reason.Kind, Is.EqualTo(HoldKind.Curfew));
            Assert.That(HoldReasonText.Long(aircraft, reason, new SimulationTime(night), ops.Clock), Does.StartWith("Curfew"));
        }

        [Test]
        public void AskingWhy_ChangesNothing()
        {
            var (_, ops, player, _) = Empty();
            var first = Restore(ops, "VH-ONE", player, FleetState.HoldingShort, 0);
            var second = Restore(ops, "VH-TWO", player, FleetState.HoldingForLanding, 10);
            var before = ops.Fleet.Select(a => (a.State, a.StateStartedAt, a.StateEndsAt)).ToList();
            for (var n = 0; n < 3; n++)
            {
                ops.Why(first);
                ops.Why(second);
            }
            Assert.That(ops.Fleet.Select(a => (a.State, a.StateStartedAt, a.StateEndsAt)).ToList(), Is.EqualTo(before));
            Assert.That(ops.RunwayFreeAt, Is.EqualTo(new SimulationTime(0)));
        }

        [Test]
        public void EveryHoldKind_HasWordsForTagsAndCards()
        {
            foreach (HoldKind kind in System.Enum.GetValues(typeof(HoldKind)))
            {
                if (kind is HoldKind.None or HoldKind.NotYetDue)
                    continue;
                var reason = new HoldReason(kind, runway: RunwayDirection.Runway23, until: new SimulationTime(120),
                    position: 2, detail: "Fuelling");
                Assert.That(HoldReasonText.Short(reason), Is.Not.Empty, kind.ToString());
                Assert.That(HoldReasonText.Long(null, reason, new SimulationTime(0)), Is.Not.Empty, kind.ToString());
            }
        }
    }
}
