using System.Collections.Generic;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Read-only HUD projections for the career objective, compact Operations and primary action.</summary>
    public sealed class OperationsSummaryTests
    {
        private static Destination Code(string code)
        {
            Assert.That(DestinationCatalogue.TryFind(code, out var destination), Is.True, code);
            return destination;
        }

        private static (ManualSimulationClock clock, AirlineOperations ops, FleetAircraft plane) PlayerOnly()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Soak Air", "#1F3A93");
            ops.AddAirline(player);
            var plane = ops.AddAircraft(player, "VH-PAX", AircraftType.Atr42, AirlineOperations.AdelaideRegionalBays[0]);
            return (clock, ops, plane);
        }

        [Test]
        public void Objective_UsesTheContractPlaceNameNotTheAuthoredId()
        {
            var (clock, ops, _) = PlayerOnly();
            Assert.That(ops.AcceptContract(RouteContractCatalogue.RegionalKingscoteIntro).Accepted, Is.True);

            var objective = OperationsSummary.Objective(ops.FleetOf(ops.PlayerAirline), clock.Now, ops.Clock, ops.CareerState);
            Assert.That(objective.Title, Is.EqualTo("Prove the Kingscote service"));
            Assert.That(objective.Title, Does.Not.Contain("REG-KGC"));
            Assert.That(objective.ProgressText, Does.Contain("0 of 5"));
            Assert.That(objective.Progress01, Is.EqualTo(0f));
            Assert.That(objective.NextLine, Does.Contain("VH-PAX"));
        }

        [Test]
        public void Objective_WithoutAContractPointsAtTheNextUsefulAction()
        {
            var (clock, ops, _) = PlayerOnly();
            var objective = OperationsSummary.Objective(ops.FleetOf(ops.PlayerAirline), clock.Now, ops.Clock,
                ops.CareerState, ops.MarketOffers());
            Assert.That(objective.Title, Does.Not.Contain("REG-"));
            Assert.That(objective.NextLine, Does.StartWith("Next:"));
        }

        [Test]
        public void CompactRows_ArePlayerOnlyAndShowPrepOrAvailability()
        {
            var (clock, ops, plane) = PlayerOnly();
            var rows = new List<OperationsRow>();
            OperationsSummary.FillPlayerRows(ops.FleetOf(ops.PlayerAirline), clock.Now, rows);
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0].Registration, Is.EqualTo("VH-PAX"));
            Assert.That(rows[0].State, Is.EqualTo("Available"));
            Assert.That(OperationsSummary.AvailableCount(ops.FleetOf(ops.PlayerAirline)), Is.EqualTo(1));

            var departAt = clock.Now.Advance(DeparturePrep.LeadSeconds(plane.Type));
            Assert.That(ops.ScheduleDeparture(plane, Code("KGC"), departAt).Accepted, Is.True);
            OperationsSummary.FillPlayerRows(ops.FleetOf(ops.PlayerAirline), clock.Now, rows);
            Assert.That(rows[0].Route, Is.EqualTo("KGC"));
            Assert.That(rows[0].State, Does.Contain("Fuelling").Or.Contain("Fuel"));
            Assert.That(OperationsSummary.AvailableCount(ops.FleetOf(ops.PlayerAirline)), Is.EqualTo(0));
        }

        [Test]
        public void PrimaryAction_MatchesAircraftState()
        {
            var (clock, ops, plane) = PlayerOnly();
            Assert.That(OperationsSummary.PrimaryAction(plane), Is.EqualTo(AircraftHudAction.PlanFlight));
            Assert.That(OperationsSummary.ActionLabel(AircraftHudAction.PlanFlight), Is.EqualTo("Plan flight"));

            var departAt = clock.Now.Advance(DeparturePrep.LeadSeconds(plane.Type));
            Assert.That(ops.ScheduleDeparture(plane, Code("KGC"), departAt).Accepted, Is.True);
            Assert.That(OperationsSummary.PrimaryAction(plane), Is.EqualTo(AircraftHudAction.ViewPlan));
        }

        [Test]
        public void FourPlayerAircraft_RemainIndividualRows()
        {
            var (clock, ops, _) = PlayerOnly();
            var player = ops.PlayerAirline;
            ops.AddAircraft(player, "VH-SUN", AircraftType.Atr42, AirlineOperations.AdelaideRegionalBays[1]);
            ops.AddAircraft(player, "VH-PLO", AircraftType.Atr42, AirlineOperations.AdelaideRegionalBays[2]);
            ops.AddAircraft(player, "VH-KGC", AircraftType.Atr42, AirlineOperations.AdelaideRegionalBays[3]);
            var rows = new List<OperationsRow>();
            OperationsSummary.FillPlayerRows(ops.FleetOf(player), clock.Now, rows);
            Assert.That(rows, Has.Count.EqualTo(4));
            Assert.That(OperationsSummary.AvailableCount(ops.FleetOf(player)), Is.EqualTo(4));
        }
    }
}
