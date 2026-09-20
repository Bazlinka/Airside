using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// The Operations workspace (ADR 0057): the movement board, the pinned player
    /// exceptions and the selected flight's actionable detail.
    /// </summary>
    public sealed class OperationsWorkspaceTests
    {
        [Test]
        public void Operations_BoardReadsInTheOrderOfTheTimeColumnItPrints()
        {
            var (clock, ops, plane) = HudTestAirline.Create();
            var second = ops.AddAircraft(ops.PlayerAirline, "VH-SUN", AircraftType.Atr42,
                AirlineOperations.AdelaideRegionalBays[1]);
            Assert.That(ops.ScheduleDeparture(second, HudTestAirline.Code("KGC"), new SimulationTime(3600)).Accepted, Is.True);
            Assert.That(ops.ScheduleDeparture(plane, HudTestAirline.Code("PLO"), new SimulationTime(1800)).Accepted, Is.True);
            clock.Set(new SimulationTime(600));
            ops.Update();

            var model = new OperationsWorkspaceModel();
            model.Rebuild(ops, clock.Now, OperationsBoardTab.Departures, null, null);

            var times = model.Rows.Select(r => r.ScheduledTime).ToList();
            Assert.That(times, Is.EqualTo(times.OrderBy(t => t).ToList()),
                "a timetable that sorts on something it does not print looks shuffled");
            Assert.That(model.Rows.Select(r => r.Registration), Is.EqualTo(new[] { "VH-PAX", "VH-SUN" }));
        }

        [Test]
        public void Operations_SubtitleNamesTheWeatherAndFlagsAGroundStop()
        {
            // Block 34 (122400-125999s) hashes to Storm; block 35 (Fog) follows it (see
            // Weather.At / RunwayWeatherTests) — used here instead of a live day's odds.
            var (clock, ops, _) = HudTestAirline.Create();
            clock.Set(new SimulationTime(123000));
            ops.Update();

            var model = new OperationsWorkspaceModel();
            model.Rebuild(ops, clock.Now, OperationsBoardTab.Departures, null, null);

            Assert.That(model.GroundStopped, Is.True);
            Assert.That(model.Subtitle, Does.Contain("Storm"));
            Assert.That(model.Subtitle, Does.Contain("GROUND STOP"));

            clock.Set(new SimulationTime(126000));
            ops.Update();
            model.Rebuild(ops, clock.Now, OperationsBoardTab.Departures, null, null);

            Assert.That(model.GroundStopped, Is.False);
            Assert.That(model.Subtitle, Does.Contain("Fog"));
            Assert.That(model.Subtitle, Does.Not.Contain("GROUND STOP"));
        }

        [Test]
        public void Operations_PinsPlayerExceptionsAboveTheBoard()
        {
            // A real exception, flown into rather than fabricated: every bay is taken while
            // the player's aircraft is away, so it lands with nowhere to park.
            var (clock, ops, plane) = HudTestAirline.Create();
            var rival = Airline.Rex();
            ops.AddAirline(rival);
            for (var i = 1; i < AirlineOperations.AdelaideRegionalBays.Count; i++)
                HudTestAirline.Park(ops, rival, $"VH-ZR{(char)('A' + i)}", AirlineOperations.AdelaideRegionalBays[i]);
            Assert.That(ops.ScheduleDeparture(plane, HudTestAirline.Code("KGC"), new SimulationTime(600)).Accepted, Is.True);

            var blockedTheLastBay = false;
            for (var t = 60L; t <= 4 * 3600 && plane.State != FleetState.AwaitingStand; t += 60)
            {
                clock.Set(new SimulationTime(t));
                ops.Update();
                HudTestAirline.HoldEveryBay(ops, rival);
                if (blockedTheLastBay || plane.State != FleetState.Outbound)
                    continue;
                // Its own bay freed on pushback; fill that too so the field really is full.
                HudTestAirline.Park(ops, rival, "VH-ZRZ", AirlineOperations.AdelaideRegionalBays[0]);
                blockedTheLastBay = true;
            }

            Assert.That(plane.State, Is.EqualTo(FleetState.AwaitingStand), "it should be stuck with no bay");

            var model = new OperationsWorkspaceModel();
            model.Rebuild(ops, clock.Now, OperationsBoardTab.Arrivals, null, null);

            Assert.That(model.Attention, Is.Not.Empty);
            Assert.That(model.Attention[0].Registration, Is.EqualTo("VH-PAX"));
            Assert.That(model.Attention[0].Severity, Is.EqualTo(StatusSeverity.Warning));
            Assert.That(model.Attention[0].Text, Does.Contain("parking"));
        }

        [Test]
        public void Operations_ShowsTheNextCommitmentWhenNothingIsWrong()
        {
            var (clock, ops, plane) = HudTestAirline.Create();
            Assert.That(ops.ScheduleDeparture(plane, HudTestAirline.Code("KGC"), new SimulationTime(3600)).Accepted, Is.True);
            clock.Set(new SimulationTime(60));
            ops.Update();

            var model = new OperationsWorkspaceModel();
            model.Rebuild(ops, clock.Now, OperationsBoardTab.Departures, null, null);

            Assert.That(model.Attention, Has.Count.EqualTo(1));
            Assert.That(model.Attention[0].Severity, Is.EqualTo(StatusSeverity.Normal));
            Assert.That(model.Attention[0].Text, Does.Contain("VH-PAX"));
        }

        [Test]
        public void Operations_DayStripShowsWhereWeAreInTheOperatingDay()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            ops.AddMissingRegionalCarriers();
            ops.AddMissingTerminalOperators();
            // Mid-afternoon Adelaide so the caret sits past the morning bank.
            var afternoon = ops.Clock.AtLocal(ops.Clock.LocalAt(clock.Now).Date.AddHours(15));
            clock.Set(afternoon);
            ops.Update();

            var model = new OperationsWorkspaceModel();
            model.Rebuild(ops, clock.Now, OperationsBoardTab.Departures, null, null);

            Assert.That(model.DayProgress01, Is.InRange(0.45f, 0.75f),
                "15:00 should sit in the second half of a 05–23 operating day");
            Assert.That(model.DayCaption, Does.Contain("done"));
            Assert.That(model.DayCaption, Does.Contain("to go"));
            Assert.That(model.DayDoneCount + model.DayActiveCount + model.DayUpcomingCount,
                Is.GreaterThan(10));
            Assert.That(model.Subtitle, Does.Contain("/"),
                "subtitle names both active strip ends");
            Assert.That(model.DayDensity.Count, Is.EqualTo(
                AirlineOperations.AiLastDepartureHour - AirlineOperations.AiFirstDepartureHour + 1));
        }

        [Test]
        public void Operations_IdleParkedAircraftStayOffTheDeparturesBoard()
        {
            var (clock, ops, plane) = HudTestAirline.Create();
            clock.Set(new SimulationTime(60));
            ops.Update();

            var model = new OperationsWorkspaceModel();
            model.Rebuild(ops, clock.Now, OperationsBoardTab.Departures, null, null);

            Assert.That(model.Rows.Any(r => r.Registration == plane.Registration), Is.False,
                "an idle stand with no booking is not a departure");
        }

        [Test]
        public void Operations_SelectionCarriesTheRealPrepAndTheOneActionThatFits()
        {
            var (clock, ops, plane) = HudTestAirline.Create();
            var departAt = new SimulationTime(DeparturePrep.TotalSeconds(plane.Type) + 60);
            Assert.That(ops.ScheduleDeparture(plane, HudTestAirline.Code("KGC"), departAt).Accepted, Is.True);
            clock.Set(new SimulationTime(plane.PrepStartedAt!.Value.ElapsedSeconds
                + DeparturePrep.FuelSeconds + DeparturePrep.CateringSeconds + 60));
            ops.Update();

            var model = new OperationsWorkspaceModel();
            model.Rebuild(ops, clock.Now, OperationsBoardTab.Departures, "VH-PAX", null);

            Assert.That(model.HasSelection, Is.True);
            Assert.That(model.SelectedPrep.Select(p => p.Done), Is.EqualTo(new[] { true, true, false }));
            Assert.That(model.SelectedPrep[2].Active, Is.True);
            Assert.That(model.SelectedPrep[2].Label, Does.StartWith("Boarding "));
            Assert.That(model.PrimaryAction, Is.EqualTo(AircraftHudAction.ViewPlan));
            Assert.That(model.CanCancel, Is.True);
        }

        [Test]
        public void Operations_FirstActiveRowSkipsMutedPastMovements()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            ops.AddMissingRegionalCarriers();
            ops.AddMissingTerminalOperators();
            var afternoon = ops.Clock.AtLocal(ops.Clock.LocalAt(clock.Now).Date.AddHours(15));
            clock.Set(afternoon);
            ops.Update();

            var model = new OperationsWorkspaceModel();
            model.Rebuild(ops, clock.Now, OperationsBoardTab.Departures, null, null);

            Assert.That(model.FirstActiveRowIndex, Is.GreaterThan(0),
                "afternoon opens past the morning Departed rows");
            Assert.That(model.Rows[model.FirstActiveRowIndex].IsPast, Is.False);
            if (model.FirstActiveRowIndex > 0)
                Assert.That(model.Rows[model.FirstActiveRowIndex - 1].IsPast, Is.True);
        }

        [Test]
        public void Operations_OffersNoCommandsOverAnotherOperatorsFlight()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            var rival = Airline.Rex();
            ops.AddAirline(rival);
            ops.AddAircraft(rival, "VH-ZRC", AircraftType.Saab340, AirlineOperations.AdelaideRegionalBays[2]);
            clock.Set(new SimulationTime(60));
            ops.Update();

            var model = new OperationsWorkspaceModel();
            model.Rebuild(ops, clock.Now, OperationsBoardTab.Departures, "VH-ZRC", null);

            Assert.That(model.HasSelection, Is.True);
            Assert.That(model.SelectedIsPlayer, Is.False);
            Assert.That(model.CanCancel, Is.False);
            Assert.That(model.SelectedPrep, Is.Empty);
            Assert.That(model.Rows.Any(r => r.Registration == "VH-ZRC" && !r.IsPlayer), Is.True,
                "other operators stay on the board");
        }

        [Test]
        public void Operations_LayoutKeepsEveryRegionInsideTheSurfaceAndApart()
        {
            foreach (var (width, height) in HudTestAirline.Viewports)
            foreach (var attention in new[] { 0, 1, 3 })
            {
                var surface = HudShell.WorkspaceSurface(width, height);
                var layout = OperationsWorkspaceLayout.Create(surface, attention);
                var label = $"{width}x{height} attention={attention}";

                Assert.That(layout.Board.Bottom, Is.LessThanOrEqualTo(surface.Bottom + 0.01f), label);
                Assert.That(layout.Board.Right, Is.LessThanOrEqualTo(surface.Right + 0.01f), label);
                Assert.That(layout.Board.Overlaps(layout.Footer), Is.False, label);
                Assert.That(layout.Board.Overlaps(layout.Header), Is.False, label);
                Assert.That(layout.DayStrip.Overlaps(layout.Header), Is.False, label);
                Assert.That(layout.DayStrip.Overlaps(layout.Board), Is.False, label);
                Assert.That(layout.DayStrip.Height, Is.EqualTo(OperationsWorkspaceLayout.DayStripHeight), label);
                if (!layout.Detail.IsEmpty)
                {
                    Assert.That(layout.Board.Overlaps(layout.Detail), Is.False, label);
                    Assert.That(layout.Detail.Right, Is.LessThanOrEqualTo(surface.Right + 0.01f), label);
                }

                for (var c = 1; c < OperationsWorkspaceLayout.ColumnLabels.Length; c++)
                    Assert.That(layout.ColumnX(c), Is.GreaterThan(layout.ColumnX(c - 1)), label);
                Assert.That(layout.ColumnX(OperationsWorkspaceLayout.ColumnLabels.Length - 1),
                    Is.LessThan(layout.Board.Right), label);
                if (attention > 0)
                    Assert.That(layout.AttentionRow(attention - 1).Bottom,
                        Is.LessThanOrEqualTo(layout.Attention.Bottom + 0.01f), label);
            }
        }

    }
}
