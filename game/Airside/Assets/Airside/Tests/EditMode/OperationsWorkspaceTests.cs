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
            Assert.That(model.DayCaption, Does.Contain("on field"));
            Assert.That(model.DayCaption, Does.Contain("listed ahead"));
            Assert.That(model.DayOnFieldCount + model.DayListedAheadCount,
                Is.GreaterThan(0));
            Assert.That(model.Subtitle, Does.Contain("/"),
                "subtitle names both active strip ends");
            Assert.That(model.DayDensity.Count, Is.EqualTo(
                AirlineOperations.AiLastDepartureHour - AirlineOperations.AiFirstDepartureHour + 1));
        }

        [Test]
        public void Operations_PublishedDayPlanRowsDoNotClaimAStand()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            ops.AddMissingRegionalCarriers();
            ops.AddMissingTerminalOperators();
            clock.Set(new SimulationTime(60));
            ops.Update();

            var model = new OperationsWorkspaceModel();
            model.Rebuild(ops, clock.Now, OperationsBoardTab.Departures, null, null);

            var published = model.Rows.Where(r => !r.OnField && !r.IsPast).ToList();
            Assert.That(published, Is.Not.Empty, "the day plan still fills the board");
            Assert.That(published.All(r => r.Stand == "—"), Is.True,
                "a listed timetable slot must not invent Gate 13 / Bay 50C occupancy");
            Assert.That(published.All(r => r.Status == "Listed" || r.Status.StartsWith("Delayed")
                                           || r.Status == "Cancelled"), Is.True);

            var liveAtStand = model.Rows.Where(r => r.OnField && r.Stand != "—").ToList();
            Assert.That(liveAtStand, Is.Not.Empty,
                "at least one live aircraft should still show its real stand");
        }

        [Test]
        public void Operations_NowDividerSkipsPastCancellations()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            ops.AddMissingRegionalCarriers();
            ops.AddMissingTerminalOperators();
            var afternoon = ops.Clock.AtLocal(ops.Clock.LocalAt(clock.Now).Date.AddHours(15));
            clock.Set(afternoon);
            ops.Update();

            var model = new OperationsWorkspaceModel();
            model.Rebuild(ops, clock.Now, OperationsBoardTab.Departures, null, null);

            Assert.That(model.NowDividerRowIndex, Is.GreaterThan(0));
            var nowRow = model.Rows[model.NowDividerRowIndex];
            Assert.That(nowRow.IsPast, Is.False);
            if (model.NowDividerRowIndex > 0)
                Assert.That(model.Rows[model.NowDividerRowIndex - 1].IsPast, Is.True,
                    "everything above NOW must be muted past, including morning cancellations");
            // The active row's printed time should sit near or after midday, not at 08:00.
            var time = nowRow.ScheduledTime;
            Assert.That(time.Length, Is.GreaterThanOrEqualTo(4));
            var hour = int.Parse(time.Substring(0, 2));
            Assert.That(hour, Is.GreaterThanOrEqualTo(12),
                $"NOW should open near the current clock, not on {time}");
        }

        [Test]
        public void Operations_NowDividerSkipsLiveOutboundThatAlreadyLeft()
        {
            // Reproduce Bailey's "stuck at 09:55 when it is 11:55": an Outbound that pushed
            // mid-morning stays on the departures board for the whole flight, and used to keep
            // IsPast=false so NOW never walked past it.
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(11),
                Airline.Player("Soak Air", "#1F3A93"));
            var morning = ops.Clock.AtLocal(ops.Clock.LocalAt(clock.Now).Date.AddHours(9).AddMinutes(55));
            var lateMorning = ops.Clock.AtLocal(ops.Clock.LocalAt(clock.Now).Date.AddHours(11).AddMinutes(55));
            DestinationCatalogue.TryFind("MEL", out var melbourne);
            var voz = ops.Airlines.First(a => a.Id.Value == "VOZ");
            ops.RestoreAircraft(
                "VH-STUCK", voz, AircraftType.Boeing7378, FleetState.Outbound,
                morning, lateMorning.Advance(60 * 60), default, default,
                melbourne, null, 0);

            clock.Set(lateMorning);
            ops.Update();

            var model = new OperationsWorkspaceModel();
            model.Rebuild(ops, clock.Now, OperationsBoardTab.Departures, null, null);

            var liveGone = model.Rows.First(r => r.Registration == "VH-STUCK");
            Assert.That(liveGone.IsPast, Is.True,
                "an aircraft that left at 09:55 must not own NOW at 11:55");
            Assert.That(liveGone.OnField, Is.False);

            var nowRow = model.Rows[model.NowDividerRowIndex];
            Assert.That(nowRow.IsPast, Is.False);
            Assert.That(nowRow.Registration, Is.Not.EqualTo("VH-STUCK"));
            var hour = int.Parse(nowRow.ScheduledTime.Substring(0, 2));
            Assert.That(hour, Is.GreaterThanOrEqualTo(11),
                $"NOW should sit near 11:55, not on {nowRow.ScheduledTime}");
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
