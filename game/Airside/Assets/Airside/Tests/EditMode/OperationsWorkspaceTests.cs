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
            Assert.That(model.Attention[0].Text, Does.Not.Contain("Departed"));
        }

        [Test]
        public void Operations_ComingUpNeverShowsADepartedAircraft()
        {
            // Quiet COMING UP used to fall through to PriorityAircraft → FirstOrDefault(),
            // so an airborne player jet became "VH-PAX · Departed · Mount Gambier".
            var (clock, ops, plane) = HudTestAirline.Create();
            Assert.That(ops.ScheduleDeparture(plane, HudTestAirline.Code("MGB"), new SimulationTime(600)).Accepted,
                Is.True);
            var outboundAt = 600 + AirlineOperations.TaxiOutSecondsFrom(plane.Stand)
                             + AirlineOperations.TakeoffRunwaySeconds + 1;
            for (var t = 60L; t <= outboundAt; t += 60)
            {
                clock.Set(new SimulationTime(t));
                ops.Update();
            }

            Assert.That(plane.State, Is.EqualTo(FleetState.Outbound));

            var model = new OperationsWorkspaceModel();
            model.Rebuild(ops, clock.Now, OperationsBoardTab.Departures, null, null);

            Assert.That(model.Attention.Any(a => a.Text.Contains("Departed")), Is.False,
                "COMING UP must not advertise a flight that has already left");
            Assert.That(model.Attention.All(a => a.Severity >= StatusSeverity.Attention || !a.Text.Contains("Departed")),
                Is.True);
        }

        [Test]
        public void Operations_DepartedRowsDoNotShowDestinationEtaAsEst()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(11),
                Airline.Player("Soak Air", "#1F3A93"));
            var morning = ops.Clock.AtLocal(ops.Clock.LocalAt(clock.Now).Date.AddHours(7).AddMinutes(46));
            var afternoon = ops.Clock.AtLocal(ops.Clock.LocalAt(clock.Now).Date.AddHours(13).AddMinutes(55));
            DestinationCatalogue.TryFind("SIN", out var singapore);
            var sia = ops.Airlines.First(a => a.Id.Value == "SIA");
            ops.RestoreAircraft(
                "VH-SIN", sia, AircraftType.Boeing78710, FleetState.Outbound,
                morning, afternoon, default, default,
                singapore, null, 0);

            clock.Set(ops.Clock.AtLocal(ops.Clock.LocalAt(clock.Now).Date.AddHours(12).AddMinutes(45)));
            ops.Update();

            var model = new OperationsWorkspaceModel();
            model.Rebuild(ops, clock.Now, OperationsBoardTab.Departures, null, null);

            var row = model.Rows.First(r => r.Registration == "VH-SIN");
            Assert.That(row.Status, Is.EqualTo("Departed"));
            Assert.That(row.ShowsEstimate, Is.False,
                "destination ETA must not print as 'est' under a departed departure");
            Assert.That(row.HasProgress, Is.False,
                "enroute-to-destination progress is not a departures FIDS bar");
        }

        [Test]
        public void Operations_DayOnFieldCountMatchesDrawnMetalNotHiddenInbound()
        {
            // Opening traffic seeds a bank of Inbound aircraft that are off the map until
            // short final. Counting them as "on field" produced a busy caption over an
            // empty-looking apron (Bailey: "17 on field and I can't see any").
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(17),
                Airline.Player("Soak Air", "#1F3A93"));
            clock.Set(new SimulationTime(30));
            ops.Update();

            var expected = 0;
            foreach (var aircraft in ops.Fleet)
                if (FleetVisual.For(aircraft, clock.Now).Visible)
                    expected++;
            Assert.That(expected, Is.GreaterThan(0), "opening still parks some metal on stands");
            Assert.That(ops.Fleet.Count(a => a.State == FleetState.Inbound), Is.GreaterThan(0),
                "opening still seeds hidden inbound traffic");

            var model = new OperationsWorkspaceModel();
            model.Rebuild(ops, clock.Now, OperationsBoardTab.Arrivals, null, null);

            Assert.That(model.DayOnFieldCount, Is.EqualTo(expected));
            Assert.That(model.DayCaption, Does.Contain($"{expected} at the airport"));
            foreach (var row in model.Rows.Where(r =>
                         ops.Fleet.First(a => a.Registration == r.Registration).State == FleetState.Inbound))
                Assert.That(row.OnField, Is.False,
                    $"inbound {row.Registration} must not read as on-field metal");
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
                "15:00 should sit in the second half of a 06–23 operating day");
            Assert.That(model.DayCaption, Does.Contain("at the airport"));
            Assert.That(model.DayCaption, Does.Not.Contain("listed ahead"));
            Assert.That(model.DayOnFieldCount, Is.GreaterThan(0));
            Assert.That(model.DayListedAheadCount, Is.EqualTo(0));
            Assert.That(model.Subtitle, Does.Contain("/"),
                "subtitle names both active strip ends");
            Assert.That(model.DayDensity.Count, Is.EqualTo(
                AirportCurfew.ClosedFromHour - AirportCurfew.OpensAtHour + 1));
        }

        [Test]
        public void Operations_BoardShowsOnlyLiveAircraft()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            ops.AddMissingRegionalCarriers();
            ops.AddMissingTerminalOperators();
            clock.Set(new SimulationTime(60));
            ops.Update();

            var model = new OperationsWorkspaceModel();
            model.Rebuild(ops, clock.Now, OperationsBoardTab.Departures, null, null);

            var liveRegs = new HashSet<string>(ops.Fleet.Select(a => a.Registration));
            Assert.That(model.Rows, Is.Not.Empty, "the live AI fleet still fills the board");
            Assert.That(model.Rows.All(r => liveRegs.Contains(r.Registration)), Is.True,
                "timetable ghosts must not appear as departures");
            Assert.That(model.Rows.All(r => r.Status != "Listed" && r.Status != "Expected"), Is.True);

            var liveAtStand = model.Rows.Where(r => r.OnField && r.Stand != "—").ToList();
            Assert.That(liveAtStand, Is.Not.Empty,
                "at least one live aircraft should still show its real stand");
        }

        [Test]
        public void Operations_NowDividerSkipsPastLiveDepartures()
        {
            // Timetable ghosts used to fill the morning with Cancelled/Departed rows so NOW
            // sat in the afternoon. The board is live metal only now: a morning outbound
            // that already left must still mute, and NOW opens on a later live departure.
            var (clock, ops, _) = HudTestAirline.Create();
            var rival = Airline.Rex();
            ops.AddAirline(rival);
            var morning = ops.Clock.AtLocal(ops.Clock.LocalAt(clock.Now).Date.AddHours(9).AddMinutes(55));
            var afternoon = ops.Clock.AtLocal(ops.Clock.LocalAt(clock.Now).Date.AddHours(15));
            ops.RestoreAircraft(
                "VH-GONE", rival, AircraftType.Saab340, FleetState.Outbound,
                morning, afternoon.Advance(60 * 60), default, default,
                HudTestAirline.Code("PLO"), null, 0);
            var later = ops.AddAircraft(ops.PlayerAirline, "VH-LATE", AircraftType.Saab340,
                AirlineOperations.AdelaideRegionalBays[1]);
            ops.CancelDeparture(later);
            Assert.That(ops.ScheduleDeparture(later, HudTestAirline.Code("KGC"), afternoon.Advance(90 * 60)).Accepted,
                Is.True);

            clock.Set(afternoon);
            ops.Update();

            var model = new OperationsWorkspaceModel();
            model.Rebuild(ops, clock.Now, OperationsBoardTab.Departures, null, null);

            Assert.That(model.NowDividerRowIndex, Is.GreaterThan(0));
            var nowRow = model.Rows[model.NowDividerRowIndex];
            Assert.That(nowRow.IsPast, Is.False);
            Assert.That(model.Rows[model.NowDividerRowIndex - 1].IsPast, Is.True);
            var hour = int.Parse(nowRow.ScheduledTime.Substring(0, 2));
            Assert.That(hour, Is.GreaterThanOrEqualTo(12),
                $"NOW should open near the current clock, not on {nowRow.ScheduledTime}");
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
            Assert.That(model.SelectedPrep.Select(p => p.Done), Is.EqualTo(new[] { true, true, false, false }));
            Assert.That(model.SelectedPrep[2].Active, Is.True);
            Assert.That(model.SelectedPrep[2].Label, Does.StartWith("Baggage "));
            Assert.That(model.PrimaryAction, Is.EqualTo(AircraftHudAction.ViewPlan));
            Assert.That(model.CanCancel, Is.True);
        }

        [Test]
        public void Operations_FirstActiveRowSkipsMutedPastMovements()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            var rival = Airline.Rex();
            ops.AddAirline(rival);
            var morning = ops.Clock.AtLocal(ops.Clock.LocalAt(clock.Now).Date.AddHours(9).AddMinutes(55));
            var afternoon = ops.Clock.AtLocal(ops.Clock.LocalAt(clock.Now).Date.AddHours(15));
            ops.RestoreAircraft(
                "VH-GONE", rival, AircraftType.Saab340, FleetState.Outbound,
                morning, afternoon.Advance(60 * 60), default, default,
                HudTestAirline.Code("PLO"), null, 0);
            var later = ops.AddAircraft(ops.PlayerAirline, "VH-LATE", AircraftType.Saab340,
                AirlineOperations.AdelaideRegionalBays[1]);
            ops.CancelDeparture(later);
            Assert.That(ops.ScheduleDeparture(later, HudTestAirline.Code("KGC"), afternoon.Advance(90 * 60)).Accepted,
                Is.True);

            clock.Set(afternoon);
            ops.Update();

            var model = new OperationsWorkspaceModel();
            model.Rebuild(ops, clock.Now, OperationsBoardTab.Departures, null, null);

            Assert.That(model.FirstActiveRowIndex, Is.GreaterThan(0),
                "afternoon opens past the morning Departed rows");
            Assert.That(model.Rows[model.FirstActiveRowIndex].IsPast, Is.False);
            Assert.That(model.Rows[model.FirstActiveRowIndex - 1].IsPast, Is.True);
        }

        [Test]
        public void Operations_OffersNoCommandsOverAnotherOperatorsFlight()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            var rival = Airline.Rex();
            ops.AddAirline(rival);
            var rex = ops.AddAircraft(rival, "VH-ZRC", AircraftType.Saab340, AirlineOperations.AdelaideRegionalBays[2]);
            Assert.That(ops.ScheduleDeparture(rex, HudTestAirline.Code("PLO"), new SimulationTime(1800)).Accepted, Is.True);
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

        [Test]
        public void Operations_AllMovementsKeepsTheLongBoardAccessible()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(17),
                Airline.Player("Soak Air", "#1F3A93"));
            clock.Set(new SimulationTime(30));
            ops.Update();

            var model = new OperationsWorkspaceModel();
            model.Rebuild(ops, clock.Now, OperationsBoardTab.Departures, null, null);
            Assert.That(model.Rows.Count, Is.GreaterThan(5));

            var layout = OperationsWorkspaceLayout.Create(HudShell.WorkspaceSurface(1440f, 900f),
                model.Attention.Count);
            var list = new HudDrawList();
            OperationsWorkspacePainter.Paint(list, model, layout, null, 0);
            var compact = list.Commands.Count(c => c.Kind == HudDrawKind.Hotspot
                && c.Box.Y >= layout.Board.Y && c.Box.Bottom <= layout.Board.Bottom);
            Assert.That(list.Commands.Any(c => c.ActionId == HudAction.ToggleMovements), Is.True);

            OperationsWorkspacePainter.Paint(list, model, layout, null, 0, allMovements: true);
            var complete = list.Commands.Count(c => c.Kind == HudDrawKind.Hotspot
                && c.Box.Y >= layout.Board.Y && c.Box.Bottom <= layout.Board.Bottom);
            Assert.That(compact, Is.LessThanOrEqualTo(5));
            Assert.That(complete, Is.GreaterThan(compact));
            Assert.That(list.Commands.Any(c => c.Text == "LIVE APRON"
                && c.ActionId == HudAction.ToggleMovements), Is.True);
        }

    }
}
