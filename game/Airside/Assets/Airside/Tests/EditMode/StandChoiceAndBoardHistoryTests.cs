using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// Player stand choice window + Operations Arrivals/Departures history (Bailey 2026-09-22).
    /// </summary>
    public sealed class StandChoiceAndBoardHistoryTests
    {
        [Test]
        public void Player_WaitsForStandChoiceBeforeAutoPark()
        {
            var (clock, ops, plane) = FlyHomeAwaitingStand();

            Assert.That(plane.State, Is.EqualTo(FleetState.AwaitingStand));
            var midWindow = plane.StateStartedAt.Advance(AirlineOperations.PlayerStandAutoSeconds / 2);
            clock.Set(midWindow);
            ops.Update();
            Assert.That(plane.State, Is.EqualTo(FleetState.AwaitingStand),
                "player must stay at the exit so they can pick a stand");

            var choices = ops.AssignableStands(plane);
            Assert.That(choices, Is.Not.Empty);
            Assert.That(ops.AssignStand(plane, choices[0]).Accepted, Is.True);
            Assert.That(plane.State, Is.EqualTo(FleetState.TaxiIn));
        }

        [Test]
        public void Player_AutoParksAfterDecisionWindow()
        {
            var (clock, ops, plane) = FlyHomeAwaitingStand();
            var autoAt = plane.StateStartedAt.Advance(AirlineOperations.PlayerStandAutoSeconds);
            // Advance onto a ground-control tick so PathClear / OnGrid can admit the taxi-in.
            clock.Set(GroundTraffic.NextGrid(autoAt));
            ops.Update();
            Assert.That(plane.State, Is.EqualTo(FleetState.TaxiIn).Or.EqualTo(FleetState.AtStand),
                "ADR 0056: missed choice still parks after the fixed deadline");
        }

        [Test]
        public void Operations_KeepsRecentDepartedAndLandedOnTheBoard()
        {
            var (clock, ops, plane) = FlyHomeAwaitingStand();
            var stand = ops.AssignableStands(plane).First();
            Assert.That(ops.AssignStand(plane, stand).Accepted, Is.True);

            // Finish taxi-in so an AtStand history event exists with a frozen route.
            for (var t = clock.Now.ElapsedSeconds; t < clock.Now.ElapsedSeconds + 3600; t += 30)
            {
                clock.Set(new SimulationTime(t));
                ops.Update();
                if (plane.State == FleetState.AtStand)
                    break;
            }

            Assert.That(plane.State, Is.EqualTo(FleetState.AtStand));

            var arrivals = new OperationsWorkspaceModel();
            arrivals.Rebuild(ops, clock.Now, OperationsBoardTab.Arrivals, null, null);
            Assert.That(arrivals.Rows.Any(r => r.Registration == plane.Registration && r.IsPast && r.Status == "Landed"),
                Is.True, "Arrivals must keep the recent landing after the aircraft parks");

            // Push a departure so Departures history has an Outbound / TakingOff event.
            Assert.That(ops.ScheduleDeparture(plane, HudTestAirline.Code("KGC"),
                clock.Now.Advance(600)).Accepted, Is.True);
            for (var t = clock.Now.ElapsedSeconds; t < clock.Now.ElapsedSeconds + 6 * 3600; t += 60)
            {
                clock.Set(new SimulationTime(t));
                ops.Update();
                if (plane.State is FleetState.Outbound or FleetState.AtDestination)
                    break;
            }

            var departures = new OperationsWorkspaceModel();
            departures.Rebuild(ops, clock.Now, OperationsBoardTab.Departures, null, null);
            Assert.That(departures.Rows.Any(r =>
                    r.Registration == plane.Registration
                    && (r.Status == "Departed" || r.IsPast || r.Status == "Departing")),
                Is.True, "Departures must keep the movement after it leaves the field");

            // With regional carriers also moving, the board should carry more than one row
            // across a multi-hour window — history plus live traffic.
            ops.AddMissingRegionalCarriers();
            clock.Set(clock.Now.Advance(90 * 60));
            ops.Update();
            departures.Rebuild(ops, clock.Now, OperationsBoardTab.Departures, null, null);
            Assert.That(departures.Rows.Count, Is.GreaterThan(1),
                "board should list live traffic plus recent history, not a near-empty strip");
        }

        [Test]
        public void Operations_EventHistoryShowsSeveralLines()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            ops.AddMissingRegionalCarriers();
            clock.Set(new SimulationTime(30 * 60));
            ops.Update();

            var lines = ops.RecentEvents
                .Where(e => e.State is FleetState.TaxiOut or FleetState.TakingOff or FleetState.Outbound
                    or FleetState.Landing or FleetState.AwaitingStand or FleetState.TaxiIn
                    or FleetState.AtStand or FleetState.HoldingForLanding or FleetState.Inbound)
                .TakeLast(OperationsWorkspaceModel.MaxEventHistoryLines)
                .Select(e => new OperationsEventLine("12:00", e.Registration))
                .ToList();

            var model = new OperationsWorkspaceModel();
            model.Rebuild(ops, clock.Now, OperationsBoardTab.Departures, null, lines);
            Assert.That(model.Events.Count, Is.EqualTo(lines.Count));
            Assert.That(model.Events.Count, Is.LessThanOrEqualTo(OperationsWorkspaceModel.MaxEventHistoryLines));
            Assert.That(OperationsWorkspaceModel.MaxEventHistoryLines, Is.GreaterThanOrEqualTo(5));
        }

        private static (ManualSimulationClock clock, AirlineOperations ops, FleetAircraft plane)
            FlyHomeAwaitingStand()
        {
            var (clock, ops, plane) = HudTestAirline.Create();
            Assert.That(ops.ScheduleDeparture(plane, HudTestAirline.Code("KGC"), new SimulationTime(600)).Accepted,
                Is.True);
            for (var t = 60L; t <= 6 * 3600; t += 30)
            {
                clock.Set(new SimulationTime(t));
                ops.Update();
                if (plane.State == FleetState.AwaitingStand)
                    return (clock, ops, plane);
            }

            Assert.Fail($"expected AwaitingStand, got {plane.State}");
            return (clock, ops, plane);
        }
    }
}
