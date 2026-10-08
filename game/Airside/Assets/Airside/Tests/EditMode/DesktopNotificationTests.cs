using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class DesktopNotificationTests
    {
        [Test]
        public void OnlyPlayerArrivalsThatRequireAStandProduceNotices()
        {
            var at = new SimulationTime(100);
            var mine = new FleetAircraft("VH-MAC", Airline.Player("Mac Air", "#123456"), AircraftType.Saab340, default, at);
            var other = new FleetAircraft("VH-AIA", new Airline("AI", "Other Air", "#123456", false), AircraftType.Saab340, default, at);
            Assert.That(DesktopNotificationPolicy.Arrival(new FleetEvent(at, mine, FleetState.AwaitingStand)).HasValue, Is.True);
            Assert.That(DesktopNotificationPolicy.Arrival(new FleetEvent(at, other, FleetState.AwaitingStand)).HasValue, Is.False);
            foreach (var state in new[] { FleetState.Outbound, FleetState.AtDestination, FleetState.HoldingForLanding, FleetState.AtStand })
                Assert.That(DesktopNotificationPolicy.Arrival(new FleetEvent(at, mine, state)).HasValue, Is.False, state.ToString());
        }

        [Test]
        public void RoutinePaymentsAndNewsStayQuietButContractsAndMilestonesNotify()
        {
            var id = new SettlementId("VH-MAC", 1);
            Assert.That(DesktopNotificationPolicy.Settlement(new FlightSettlement(id, "", 100, 0, 1, false)).HasValue, Is.False);
            Assert.That(DesktopNotificationPolicy.Settlement(new FlightSettlement(id, "contract", 100, 1, 1, true)).Value.Title,
                Is.EqualTo("Contract completed"));
            Assert.That(DesktopNotificationPolicy.Settlement(new FlightSettlement(id, "", 100, 0, 1, false,
                new DelayBreakdown(600, new DelayPart[0]))).Value.Title, Is.EqualTo("Flight delay"));
            foreach (var kind in new[] { CareerEventKind.News, CareerEventKind.DailyReport })
                Assert.That(DesktopNotificationPolicy.Career(new CareerEvent(kind, OperatingTier.Provisional, "News")).HasValue, Is.False);
            foreach (var kind in new[] { CareerEventKind.ContractExpired, CareerEventKind.Milestone, CareerEventKind.TierReached, CareerEventKind.Finale })
                Assert.That(DesktopNotificationPolicy.Career(new CareerEvent(kind, OperatingTier.Provisional, "Important")).HasValue, Is.True);
        }

        [Test]
        public void BurstsAreGroupedAndRepeatEventsAreSuppressed()
        {
            var buffer = new DesktopNotificationBuffer();
            var first = new DesktopNotice("one", "Arrival", "Choose a stand.");
            buffer.Enqueue(first, 0f);
            Assert.That(buffer.Take(0f).Value.Title, Is.EqualTo("Arrival"));
            buffer.Enqueue(first, 1f);
            Assert.That(buffer.Take(10f).HasValue, Is.False);
            for (var i = 0; i < 12; i++) buffer.Enqueue(new DesktopNotice("burst" + i, "Update", "Flight " + i), 11f);
            var batch = buffer.Take(11f).Value;
            Assert.That(batch.Title, Is.EqualTo("12 airline updates"));
            Assert.That(batch.Body, Does.Contain("9 more updates"));
            buffer.Enqueue(new DesktopNotice("later", "Contract", "Completed"), 12f);
            Assert.That(buffer.Take(12f).HasValue, Is.False, "avoid a notification sound on every event");
            Assert.That(buffer.Take(21f).Value.Title, Is.EqualTo("Contract"));
        }

        [Test]
        public void ReturningToAirsideDropsUndeliveredBackgroundMessages()
        {
            var buffer = new DesktopNotificationBuffer();
            buffer.Enqueue(new DesktopNotice("one", "Arrival", "Choose a stand"), 0f);
            buffer.ClearPending();
            Assert.That(buffer.Take(1f).HasValue, Is.False);
        }
    }
}
