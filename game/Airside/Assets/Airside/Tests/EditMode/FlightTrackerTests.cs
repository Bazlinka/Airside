using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class FlightTrackerTests
    {
        private static readonly List<OperationsRow> Scratch = new();

        private static List<FlightTrackerRow> Rows(AirlineOperations ops, SimulationTime now, string selected = null)
        {
            var rows = new List<FlightTrackerRow>();
            FlightTracker.Fill(ops, now, selected, rows, Scratch);
            return rows;
        }

        [Test]
        public void ParkedAircraftWithNothingBooked_IsNotTracked()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            Assert.That(Rows(ops, clock.Now), Is.Empty);
        }

        [Test]
        public void BookingAFlight_PutsItOnTheTrackerAtStepOne()
        {
            var (clock, ops, plane) = HudTestAirline.Create();
            Assert.That(ops.ScheduleDeparture(plane, HudTestAirline.Code("KGC"), clock.Now.Advance(1800)).Accepted, Is.True);
            var row = Rows(ops, clock.Now).Single();
            Assert.That(row.Registration, Is.EqualTo(plane.Registration));
            Assert.That(row.Step, Is.EqualTo(0));
            Assert.That(row.StepLabel, Is.EqualTo("BOOKED"));
            Assert.That(row.Time, Does.StartWith("Departs"));
            Assert.That(row.Route, Does.Contain("Kingscote"));
        }

        [Test]
        public void TheTrackerAdvancesThroughEveryStepOfARealFlight_AndEndsWhenParked()
        {
            var (clock, ops, plane) = HudTestAirline.Create();
            HudTestAirline.HoldEveryBay(ops, Airline.Rex());
            ops.ScheduleDeparture(plane, HudTestAirline.Code("KGC"), clock.Now.Advance(600));
            var seen = new SortedSet<int>();
            var guard = 0;
            while (guard++ < 20000)
            {
                var rows = Rows(ops, clock.Now);
                if (rows.Count > 0)
                    seen.Add(rows[0].Step);
                else if (plane.CompletedTrips > 0)
                    break;
                if (plane.State == FleetState.AwaitingStand)
                {
                    var stand = ops.SuggestStand(plane);
                    if (stand.HasValue)
                        ops.AssignStand(plane, stand.Value);
                }

                var next = ops.NextEventAt();
                Assert.That(next, Is.Not.Null);
                clock.Set(next.Value);
                ops.Update();
            }

            Assert.That(plane.CompletedTrips, Is.GreaterThan(0));
            Assert.That(seen, Does.Contain(0), "booked");
            Assert.That(seen, Does.Contain(2), "taxi");
            Assert.That(seen, Does.Contain(3), "flying");
            Assert.That(seen.Max(), Is.GreaterThanOrEqualTo(4), "landing or arrival is shown");
            Assert.That(Rows(ops, clock.Now), Is.Empty, "once parked with nothing booked the flight leaves the tracker");
        }

        [Test]
        public void TheSelectedAircraftLeads_AndRowsAreCapped()
        {
            var (clock, ops, plane) = HudTestAirline.Create();
            ops.RestoreCareerState(500_000 * FlightCostModel.LegacySaveMoneyScale, 95, nameof(OperatingTier.Domestic), null, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), 100, baseLevel: PlayerBaseLevel.ExpandedRegional);
            var more = new List<FleetAircraft> { plane };
            for (var i = 1; i < 5; i++)
                more.Add(ops.AddAircraft(ops.PlayerAirline, "VH-T0" + i, AircraftType.Saab340,
                    AirlineOperations.AdelaideRegionalBays[i]));
            foreach (var aircraft in more)
                ops.ScheduleDeparture(aircraft, HudTestAirline.Code("KGC"), clock.Now.Advance(1800));
            var rows = Rows(ops, clock.Now, "VH-T04");
            Assert.That(rows.Count, Is.EqualTo(FlightTracker.MaxRows));
            Assert.That(rows[0].Registration, Is.EqualTo("VH-T04"));
            Assert.That(rows[0].IsSelected, Is.True);
            Assert.That(FlightTracker.TotalTracked, Is.EqualTo(5));
        }

        [Test]
        public void StepsHaveAMatchingLabelEach()
        {
            Assert.That(FlightTracker.StepLabels.Length, Is.EqualTo(FlightTracker.StepCount));
        }

        [Test]
        public void Painter_FitsEveryWindow_ShowsEveryStepAndMakesRowsSelectable()
        {
            var (clock, ops, plane) = HudTestAirline.Create();
            ops.ScheduleDeparture(plane, HudTestAirline.Code("KGC"), clock.Now.Advance(1800));
            var rows = Rows(ops, clock.Now);
            foreach (var (width, height) in HudTestAirline.Viewports)
            {
                var layout = HudShell.Layout(width, height, false, false, false,
                    FlightTrackerPainter.HeightFor(rows.Count, false));
                Assert.That(layout.Operations.IsEmpty, Is.False, $"{width}x{height}");
                var list = new HudDrawList();
                FlightTrackerPainter.Paint(list, layout.Operations, rows, rows.Count);
                foreach (var c in list.Commands)
                {
                    Assert.That(c.Box.Right, Is.LessThanOrEqualTo(layout.Operations.Right + 0.5f), $"{width}x{height} '{c.Text}'");
                    Assert.That(c.Box.Bottom, Is.LessThanOrEqualTo(layout.Operations.Bottom + 0.5f), $"{width}x{height} '{c.Text}'");
                }

                Assert.That(list.Commands.Any(c => c.ActionId == HudAction.Select(plane.Registration)), Is.True);
                Assert.That(list.Commands.Any(c => c.Text.Contains("BOOKED")), Is.True);
            }
        }

        [Test]
        public void TrackerSlot_NeverOverlapsTheOtherCornersOrFallsOffScreen()
        {
            foreach (var (width, height) in HudTestAirline.Viewports)
            foreach (var guide in new[] { false, true })
            foreach (var map in new[] { false, true })
            {
                var layout = HudShell.Layout(width, height, guide, false, map, FlightTrackerPainter.HeightFor(3, true));
                var label = $"{width}x{height} guide={guide} map={map}";
                if (layout.Operations.IsEmpty)
                    continue;
                Assert.That(layout.Operations.Right, Is.LessThanOrEqualTo(width), label);
                Assert.That(layout.Operations.Bottom, Is.LessThanOrEqualTo(height), label);
                Assert.That(layout.Operations.Overlaps(layout.Career), Is.False, label + " career");
                Assert.That(layout.Operations.Overlaps(layout.MiniMap), Is.False, label + " map");
                Assert.That(layout.Operations.Overlaps(layout.SelectedCard), Is.False, label + " selected");
                Assert.That(layout.Operations.Overlaps(layout.Toast), Is.False, label + " toast");
                Assert.That(layout.Operations.Overlaps(layout.Capsule), Is.False, label + " capsule");
            }
        }

        [Test]
        public void HidingTheCareerCard_FreesItsCornerButTheGuideAlwaysShows()
        {
            var hidden = HudShell.Layout(1440, 900, false, false, false, 0f, showCareerCard: false);
            Assert.That(hidden.Career.IsEmpty, Is.True);
            var guided = HudShell.Layout(1440, 900, true, false, false, 0f, showCareerCard: false);
            Assert.That(guided.Career.IsEmpty, Is.False);
            var shown = HudShell.Layout(1440, 900);
            Assert.That(shown.Career.IsEmpty, Is.False);
        }

        [Test]
        public void ANarrowWindowWithNoRoomSimplyDropsTheTracker()
        {
            var layout = HudShell.Layout(800, 360, false, false, false, FlightTrackerPainter.HeightFor(3, true));
            Assert.That(layout.Operations.IsEmpty, Is.True);
        }
    }

    public sealed class HudVisibilityTests
    {
        [Test]
        public void Defaults_KeepTheCurrentHud_AndMapIsOnRequest()
        {
            var hud = HudVisibility.Default();
            foreach (HudView view in Enum.GetValues(typeof(HudView)))
            {
                Assert.That(hud.Shows(view, HudElement.AircraftLabels), Is.True);
                Assert.That(hud.Shows(view, HudElement.FlightTracker), Is.True);
                Assert.That(hud.Shows(view, HudElement.CareerCard), Is.True);
                Assert.That(hud.Shows(view, HudElement.AirportMap), Is.False);
            }
        }

        [Test]
        public void EachViewKeepsItsOwnSettings()
        {
            var hud = HudVisibility.Default();
            Assert.That(hud.Toggle(HudView.Follow, HudElement.AircraftLabels), Is.False);
            Assert.That(hud.Shows(HudView.Overview, HudElement.AircraftLabels), Is.True);
            Assert.That(hud.Shows(HudView.Follow, HudElement.AircraftLabels), Is.False);
            Assert.That(hud.Toggle(HudView.Overview, HudElement.AirportMap), Is.True);
            Assert.That(hud.Shows(HudView.Follow, HudElement.AirportMap), Is.False);
        }

        [Test]
        public void MasksRoundTrip_AndIgnoreUnknownBits()
        {
            var hud = HudVisibility.Default(labels: false, airportMap: true);
            hud.Set(HudView.Follow, HudElement.FlightTracker, false);
            var copy = new HudVisibility();
            foreach (HudView view in Enum.GetValues(typeof(HudView)))
                copy.SetMask(view, hud.Mask(view));
            foreach (HudView view in Enum.GetValues(typeof(HudView)))
            foreach (HudElement element in Enum.GetValues(typeof(HudElement)))
                Assert.That(copy.Shows(view, element), Is.EqualTo(hud.Shows(view, element)), $"{view} {element}");
            copy.SetMask(HudView.Overview, -1);
            Assert.That(copy.Mask(HudView.Overview), Is.EqualTo((1 << HudVisibility.ElementCount) - 1));
        }

        [Test]
        public void EveryElementAndViewHasWordsForTheOptionsPage()
        {
            foreach (HudView view in Enum.GetValues(typeof(HudView)))
                Assert.That(HudVisibility.Label(view), Is.Not.Empty);
            foreach (HudElement element in Enum.GetValues(typeof(HudElement)))
            {
                Assert.That(HudVisibility.Label(element), Is.Not.Empty);
                Assert.That(HudVisibility.Detail(element), Is.Not.Empty);
            }

            Assert.That(Enum.GetValues(typeof(HudElement)).Length, Is.EqualTo(HudVisibility.ElementCount));
            Assert.That(Enum.GetValues(typeof(HudView)).Length, Is.EqualTo(HudVisibility.ViewCount));
        }
    }
}
