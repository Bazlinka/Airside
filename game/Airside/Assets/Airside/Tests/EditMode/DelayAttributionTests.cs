using System;
using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0128 — every late pushback says what made it late, and the parts add up.</summary>
    public sealed class DelayAttributionTests
    {
        private static Airline Player() => Airline.Player("Southern Cross Regional", "#C8102E");

        private static void RunTo(ManualSimulationClock clock, AirlineOperations ops, long seconds)
        {
            clock.Set(new SimulationTime(seconds));
            ops.Update();
        }

        private static int Sum(DelayBreakdown delay) => delay.Parts.Sum(p => p.Seconds);

        [Test]
        public void Ledger_GivesEachIntervalToWhatWasBlockingBeforeIt()
        {
            var ledger = new DelayLedger(departAtSeconds: 1000, startSeconds: 1000, DelayCause.ApronBusy);
            ledger.Sample(1005, DelayCause.ApronBusy);
            ledger.Sample(1010, DelayCause.RunwayCrossing);
            ledger.Sample(1010, DelayCause.RunwayCrossing); // same instant twice: adds nothing
            ledger.Sample(1060, DelayCause.Taxiway);
            var delay = DelayLedger.Close(ledger, 1000, 1000, 1070);

            Assert.That(delay.LatenessSeconds, Is.EqualTo(70));
            Assert.That(Sum(delay), Is.EqualTo(70));
            Assert.That(delay.Parts[0].Cause, Is.EqualTo(DelayCause.RunwayCrossing));
            Assert.That(delay.Parts[0].Seconds, Is.EqualTo(50));
            Assert.That(delay.Parts.Single(p => p.Cause == DelayCause.ApronBusy).Seconds, Is.EqualTo(10));
            Assert.That(delay.Parts.Single(p => p.Cause == DelayCause.Taxiway).Seconds, Is.EqualTo(10));
            Assert.That(delay.TopCause, Is.EqualTo(DelayCause.RunwayCrossing));
        }

        [Test]
        public void Close_CountsTurnaroundOverrunAndUnsampledTimeAsOther()
        {
            // Ready 100 s late; the ledger only began 20 s after that (as after a reload).
            var ledger = new DelayLedger(1000, 1120, DelayCause.Taxiway);
            var delay = DelayLedger.Close(ledger, 1000, 1100, 1150);
            Assert.That(Sum(delay), Is.EqualTo(150));
            Assert.That(delay.Parts.Single(p => p.Cause == DelayCause.Turnaround).Seconds, Is.EqualTo(100));
            Assert.That(delay.Parts.Single(p => p.Cause == DelayCause.Taxiway).Seconds, Is.EqualTo(30));
            Assert.That(delay.Parts.Single(p => p.Cause == DelayCause.Other).Seconds, Is.EqualTo(20));

            var stale = new DelayLedger(500, 500, DelayCause.Taxiway);
            Assert.That(DelayLedger.Close(stale, 1000, 1000, 1030).Parts.Single().Cause, Is.EqualTo(DelayCause.Other),
                "a ledger from an earlier booking is ignored");
        }

        [Test]
        public void Breakdown_RoundTripsThroughItsSaveForm()
        {
            var delay = DelayBreakdown.Parse(240, "RunwayCrossing:180;Turnaround:60");
            Assert.That(delay.Serialize(), Is.EqualTo("RunwayCrossing:180;Turnaround:60"));
            Assert.That(DelayBreakdown.Parse(240, delay.Serialize()).Serialize(), Is.EqualTo(delay.Serialize()));

            var old = DelayBreakdown.Parse(90, null);
            Assert.That(old.Parts.Single().Cause, Is.EqualTo(DelayCause.Other), "pre-v16: all unattributed");
            Assert.That(old.TopCause, Is.Null);

            var messy = DelayBreakdown.Parse(100, "Warp:40;Taxiway:500;junk;LeadIn:-5");
            Assert.That(Sum(messy), Is.EqualTo(100), "overshoot is trimmed, bad items dropped");
            Assert.That(messy.Parts[0].Cause, Is.EqualTo(DelayCause.Taxiway));
            Assert.That(DelayBreakdown.Parse(0, "Taxiway:30").Parts, Is.Empty, "on time to the second");
        }

        [Test]
        public void LatePrep_IsAttributedToTheTurnaround()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Player();
            ops.AddAirline(player);
            var plane = ops.AddAircraft(player, "VH-PAA", AircraftType.Saab340, AirlineOperations.AdelaideRegionalBays[0]);
            var departAt = DeparturePrep.LeadSeconds(plane.Type) + 600;
            Assert.That(DestinationCatalogue.TryFind("KGC", out var kingscote), Is.True);
            Assert.That(ops.ScheduleDeparture(plane, kingscote, new SimulationTime(departAt)).Accepted, Is.True);
            // The turnaround started 200 s later than planned, so it finishes 200 s after the booked time.
            plane.PrepStartedAt = plane.PrepStartedAt.Value.Advance(200);
            for (var t = departAt - 5; t <= departAt + 400 && plane.State == FleetState.AtStand; t++)
                RunTo(clock, ops, t);

            Assert.That(plane.State, Is.Not.EqualTo(FleetState.AtStand));
            Assert.That(plane.PushbackDelay, Is.Not.Null);
            var delay = plane.PushbackDelay.Value;
            Assert.That(delay.LatenessSeconds, Is.EqualTo(plane.PushbackLatenessSeconds));
            Assert.That(Sum(delay), Is.EqualTo(delay.LatenessSeconds));
            Assert.That(delay.TopCause, Is.EqualTo(DelayCause.Turnaround));
            Assert.That(delay.Parts.Single(p => p.Cause == DelayCause.Turnaround).Seconds, Is.EqualTo(200));
        }

        /// <summary>
        /// A player departure ready while other traffic taxis across its path: the wait is put down to a
        /// ground cause, the parts add up, and stepping the clock second by second or event by event
        /// tells the same story.
        /// </summary>
        [Test]
        public void GroundHold_IsNamedAndTheSameForAnyStepSize()
        {
            string Run(bool fine, out FleetAircraft plane)
            {
                var clock = new ManualSimulationClock(new SimulationTime(0));
                var ops = new AirlineOperations(clock, new SeededRandomSource(4), DestinationCatalogue.Adelaide,
                    AirlineOperations.AdelaideStands);
                var player = Player();
                var other = new Airline("OTH", "Other Air", "#C95D50", isPlayer: false);
                ops.AddAirline(player);
                ops.AddAirline(other);
                var bays = AirlineOperations.AdelaideRegionalBays;
                plane = ops.AddAircraft(player, "VH-PAA", AircraftType.Atr42, bays[0]);
                var departAt = DeparturePrep.LeadSeconds(plane.Type);
                // Traffic taxiing in to the next bays, timed to be on the move as the player is ready.
                for (var i = 1; i <= 3; i++)
                {
                    var taxiIn = AdelaideGround.TaxiIn(bays[i], AircraftType.Atr42, RunwayDirection.Runway05);
                    var start = departAt - (long)(taxiIn.WholeSeconds * 0.5) + i * 20;
                    ops.RestoreAircraft($"VH-TX{i}", other, AircraftType.Atr42, FleetState.TaxiIn, new SimulationTime(start),
                        new SimulationTime(start + taxiIn.WholeSeconds), bays[i], default, HudTestAirline.Code("KGC"), null, 0);
                    ops.Fleet.Last().AssignedRunway = RunwayDirection.Runway05;
                }

                Assert.That(ops.ScheduleDeparture(plane, HudTestAirline.Code("KGC"), new SimulationTime(departAt)).Accepted,
                    Is.True);
                if (fine)
                {
                    for (var t = 1L; t <= departAt + 1800 && plane.State == FleetState.AtStand; t++)
                        RunTo(clock, ops, t);
                }
                else
                {
                    // Uneven, coarse steps: 7 s, 61 s, 13 s … never lining up with the grid.
                    var steps = new[] { 7L, 61L, 13L, 29L };
                    var t = 0L;
                    for (var i = 0; t <= departAt + 1800 && plane.State == FleetState.AtStand; i++)
                    {
                        t += steps[i % steps.Length];
                        RunTo(clock, ops, t);
                    }
                }

                Assert.That(plane.PushbackDelay, Is.Not.Null, "pushed back");
                return plane.PushbackDelay.Value.Serialize();
            }

            var fineStory = Run(true, out var finePlane);
            var coarseStory = Run(false, out _);
            var delay = finePlane.PushbackDelay.Value;
            Assert.That(Sum(delay), Is.EqualTo(delay.LatenessSeconds));
            Assert.That(coarseStory, Is.EqualTo(fineStory), "the same breakdown whatever the step size");
            if (delay.LatenessSeconds <= GroundTraffic.GridSeconds)
                Assert.Inconclusive($"the traffic did not hold the departure ({delay.LatenessSeconds}s)");
            Assert.That(delay.TopCause, Is.Not.Null.And.Not.EqualTo(DelayCause.Turnaround), fineStory);
        }

        [Test]
        public void PendingBreakdown_SurvivesASave_AndReachesTheSettlement()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Player();
            ops.AddAirline(player);
            var plane = ops.AddAircraft(player, "VH-PAA", AircraftType.Saab340, AirlineOperations.AdelaideRegionalBays[0]);
            var departAt = DeparturePrep.LeadSeconds(plane.Type);
            Assert.That(ops.ScheduleDeparture(plane, HudTestAirline.Code("KGC"), new SimulationTime(departAt)).Accepted, Is.True);
            RunTo(clock, ops, departAt);
            Assert.That(plane.State, Is.EqualTo(FleetState.TaxiOut));
            plane.PushbackLatenessSeconds = 420;
            plane.PushbackDelay = DelayBreakdown.Parse(420, "RunwayCrossing:300;ApronBusy:120");

            AirlineSaveData Fresh() => AirlineSave.Capture(ops);
            var data = Fresh();
            Assert.That(data.Version, Is.EqualTo(16));
            var restoredClock = new ManualSimulationClock(clock.Now);
            var restored = AirlineSave.Restore(data, restoredClock);
            var again = restored.Fleet.Single(a => a.Registration == "VH-PAA");
            Assert.That(again.PushbackDelay?.Serialize(), Is.EqualTo("RunwayCrossing:300;ApronBusy:120"));

            // v15 saves had no breakdown: the lateness loads as unattributed.
            var v15 = Fresh();
            v15.Version = 15;
            var old = AirlineSave.Restore(v15, new ManualSimulationClock(clock.Now))
                .Fleet.Single(a => a.Registration == "VH-PAA");
            Assert.That(old.PushbackDelay?.Serialize(), Is.EqualTo("Other:420"));

            // Fly it home: the settlement carries the story, and the aircraft forgets it.
            var settled = restored.TotalSettlements;
            for (var t = clock.Now.ElapsedSeconds; t < clock.Now.ElapsedSeconds + 6 * 3600 && restored.TotalSettlements == settled; t += 5)
            {
                restoredClock.Set(new SimulationTime(t));
                restored.Update();
            }

            Assert.That(restored.TotalSettlements, Is.GreaterThan(settled));
            var settlement = restored.RecentSettlements.Last();
            Assert.That(settlement.Delay?.Serialize(), Is.EqualTo("RunwayCrossing:300;ApronBusy:120"));
            Assert.That(again.PushbackDelay, Is.Null);
        }
        [Test]
        public void Wording_SaysHowLateAndWhy()
        {
            var late = DelayBreakdown.Parse(250, "RunwayCrossing:190;Turnaround:45;Other:15");
            Assert.That(DelayText.Summary(late), Is.EqualTo("4 min late: 3 min runway crossings, 1 min turnaround"));
            Assert.That(DelayText.Tip(late), Does.Contain("crossing"));
            Assert.That(DelayText.Summary(DelayBreakdown.Parse(90, "Taxiway:90")), Is.EqualTo("on time"), "inside the grace");
            Assert.That(DelayText.Summary(DelayBreakdown.Parse(600, null)), Is.EqualTo("10 min late"));
            Assert.That(DelayText.Minutes(3900), Is.EqualTo("1 h 5 min"));

            var id = new SettlementId("VH-PAA", 3);
            var onTime = new FlightSettlement(id, null, 12_400, 1, 1, false, DelayBreakdown.Parse(30, null));
            Assert.That(DelayText.SettlementToast(onTime, 7), Is.EqualTo("VH-PAA earned $12,400 · on time, 7 in a row."));
            var lateSettlement = new FlightSettlement(id, null, 12_400, 0, 1, false, late);
            Assert.That(DelayText.SettlementToast(lateSettlement, 0), Does.StartWith("VH-PAA earned $12,400 · 4 min late: 3 min runway crossings"));
            Assert.That(DelayText.Tone(lateSettlement), Is.EqualTo(HudTone.Caution));
            var veryLate = new FlightSettlement(id, null, 1, -2, 1, false, DelayBreakdown.Parse(FlightEconomics.HardLateSeconds + 60, null));
            Assert.That(DelayText.Tone(veryLate), Is.EqualTo(HudTone.Negative));
            Assert.That(DelayText.SettlementToast(new FlightSettlement(id, null, 500, 0, 1, false), 0),
                Is.EqualTo("VH-PAA earned $500."), "AI and old saves: no delay story");
        }
        [Test]
        public void HoldLine_IsALinkOnlyWhenThereIsSomethingToFollow()
        {
            var card = new SelectionCardData
            {
                Registration = "VH-PAA", TypeName = "ATR 72", RouteLine = "Adelaide → Kingscote",
                HoldLine = "Waiting to push back — Qantas 737-8 is on the taxi route", IsPlayer = true, PrimaryLabel = "View plan"
            };
            var slot = HudShell.Layout(1440f, 900f).SelectedCard;
            var box = slot.SliceBottom(SelectionCardPainter.HeightFor(card));
            var draw = new HudDrawList();
            SelectionCardPainter.Paint(draw, box, card);
            Assert.That(draw.Commands.Any(c => c.ActionId.StartsWith(HudAction.SelectPrefix)), Is.False);

            card.HoldAction = HudAction.Select("VH-QFA");
            card.BackRegistration = "VH-XYZ";
            draw.Clear();
            SelectionCardPainter.Paint(draw, box, card);
            Assert.That(draw.Commands.Count(c => c.ActionId == HudAction.Select("VH-QFA")), Is.EqualTo(1));
            Assert.That(draw.Commands.Count(c => c.ActionId == HudAction.Select("VH-XYZ")), Is.EqualTo(1), "the way back");
            foreach (var command in draw.Commands)
                Assert.That(command.Box.X >= box.X - 0.5f && command.Box.Right <= box.Right + 0.5f, Is.True, command.ActionId);
        }
    }
}
