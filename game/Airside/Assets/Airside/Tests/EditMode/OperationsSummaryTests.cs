using System;
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
            var plane = ops.AddAircraft(player, "VH-PAX", AircraftType.Saab340, AirlineOperations.AdelaideRegionalBays[0]);
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
            Assert.That(objective.NextLine.ToLowerInvariant(), Does.Contain("schedule"));
            Assert.That(objective.NextLine, Does.Contain("Kingscote"));
        }

        [Test]
        public void Objective_WithoutAContractPointsAtTheNextUsefulAction()
        {
            var (clock, ops, _) = PlayerOnly();
            var objective = OperationsSummary.Objective(ops.FleetOf(ops.PlayerAirline), clock.Now, ops.Clock,
                ops.CareerState, ops.MarketOffers());
            Assert.That(objective.Title, Does.Not.Contain("REG-"));
            Assert.That(objective.NextLine, Does.StartWith("Next:"));
            Assert.That(objective.NextLine.ToLowerInvariant(), Does.Contain("accept"));
            Assert.That(objective.ProgressText, Does.Contain("chapter goals"));
            Assert.That(objective.ProgressText, Does.Contain("Regional starter base"));
            Assert.That(objective.Progress01, Is.EqualTo(1f / 3f).Within(0.001f),
                "fresh Chapter 1 already satisfies its reliability goal");
        }

        [Test]
        public void Objective_WithoutOffersShowsHangarSaveGoal()
        {
            var (clock, ops, _) = PlayerOnly();
            // Empty market + every authored intro already completed → hangar owns the title.
            ops.RestoreCareerState(ops.CareerState.Funds, ops.CareerState.Reliability,
                nameof(OperatingTier.Provisional), null, 0, 0,
                Array.Empty<string>(),
                new[]
                {
                    RouteContractCatalogue.RegionalKingscoteIntro.Id,
                    RouteContractCatalogue.RegionalPortLincolnIntro.Id,
                    RouteContractCatalogue.RegionalWhyallaIntro.Id,
                    RouteContractCatalogue.DomesticMelbourneIntro.Id
                },
                0);
            // Drain market by advancing? MarketOffers still draws from owned Saab.
            // Pass null market and rely on completed intros.
            var objective = OperationsSummary.Objective(ops.FleetOf(ops.PlayerAirline), clock.Now, ops.Clock,
                ops.CareerState, Array.Empty<RouteContractDefinition>());
            Assert.That(objective.Title, Does.Contain("ATR 42"));
            Assert.That(objective.ProgressText, Does.Contain($"${AirlineCareerState.StartingFunds:N0}"));
            Assert.That(objective.NextLine.ToLowerInvariant(), Does.Contain("fly").Or.Contain("earn"));
        }

        [Test]
        public void Objective_PrepIsAnImperativeNotAStatusReadout()
        {
            var (clock, ops, plane) = PlayerOnly();
            Assert.That(ops.AcceptContract(RouteContractCatalogue.RegionalKingscoteIntro).Accepted, Is.True);
            var departAt = clock.Now.Advance(DeparturePrep.LeadSeconds(plane.Type));
            Assert.That(ops.ScheduleDeparture(plane, Code("KGC"), departAt).Accepted, Is.True);

            var objective = OperationsSummary.Objective(ops.FleetOf(ops.PlayerAirline), clock.Now, ops.Clock,
                ops.CareerState);
            Assert.That(objective.NextLine, Does.StartWith("Next: Finish"));
            Assert.That(objective.NextLine, Does.Contain("VH-PAX"));
            Assert.That(objective.NextLine, Does.Not.Contain("%"),
                "the next line is the action, not a fuelling percent");
        }

        [Test]
        public void Objective_ReadyAircraftPointsAtPushback()
        {
            var (clock, ops, plane) = PlayerOnly();
            Assert.That(ops.AcceptContract(RouteContractCatalogue.RegionalKingscoteIntro).Accepted, Is.True);
            var total = DeparturePrep.TotalSeconds(plane.Type);
            var departAt = clock.Now.Advance(total + 120);
            Assert.That(ops.ScheduleDeparture(plane, Code("KGC"), departAt).Accepted, Is.True);
            // Sit at the booked push without Update — Update would already start TaxiOut.
            clock.Set(departAt);
            Assert.That(plane.State, Is.EqualTo(FleetState.AtStand));
            Assert.That(DeparturePrep.For(plane, clock.Now).Ready, Is.True, "prep must be finished for this case");

            var objective = OperationsSummary.Objective(ops.FleetOf(ops.PlayerAirline), clock.Now, ops.Clock,
                ops.CareerState);
            Assert.That(objective.NextLine.ToLowerInvariant(), Does.Contain("follow"));
            Assert.That(objective.NextLine, Does.Contain("pushback"));
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
