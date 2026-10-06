using System;
using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class FlightViewInformationTests
    {
        private static FleetAircraft Aircraft(FleetState state)
        {
            var aircraft = new FleetAircraft("VH-WATCH", Airline.Player("Flight information", "#123456"),
                AircraftType.Atr42, AirlineOperations.AdelaideRegionalBays[0], new SimulationTime(0));
            aircraft.CurrentDestination = DestinationCatalogue.All.First(d => d.Code == "KGC");
            aircraft.Restore(state, new SimulationTime(0), new SimulationTime(900));
            return aircraft;
        }
        private static FlightViewHudData Fill(FleetAircraft aircraft, double x = 0, double z = 0,
            double ox = 0, double oz = 0, EnrouteProfile? profile = null, double elapsed = 0)
        {
            var data = new FlightViewHudData();
            FlightViewInformation.Fill(data, aircraft, DestinationCatalogue.Adelaide, 300,
                x, z, ox, oz, 1000, 1, 0, 224, -2, profile, elapsed);
            return data;
        }

        [TestCase(0f)] [TestCase(90f)] [TestCase(180f)] [TestCase(270f)]
        public void FieldForwardConvertsToTrueHeading(float degrees)
        {
            var yaw = YpadFrame.UnityYawFromTrue(degrees) * Math.PI / 180;
            var heading = FlightViewInformation.TrueHeading(Math.Sin(yaw), Math.Cos(yaw));
            var difference = Math.Abs(heading - degrees);
            Assert.That(Math.Min(difference, 360 - difference), Is.LessThan(.0001));
        }

        [Test]
        public void FloatingOriginCannotChangeLocationDistanceOrWatchedIdentity()
        {
            var aircraft = Aircraft(FleetState.Outbound);
            YpadFrame.ToWorld(-35.5, 137.8, out var x, out var z);
            var geographic = Fill(aircraft, x, z);
            var shifted = Fill(aircraft, x - 80000, z + 96000, 80000, -96000);
            Assert.That(shifted.Registration, Is.EqualTo("VH-WATCH"));
            Assert.That(shifted.Location, Is.EqualTo(geographic.Location));
            Assert.That(shifted.Distance, Is.EqualTo(geographic.Distance));
            Assert.That(shifted.Remaining, Is.EqualTo(geographic.Remaining));
            Assert.That(shifted.Altitude, Is.EqualTo("3281 ft"));
            Assert.That(shifted.Speed, Is.EqualTo("224 kt"));
            Assert.That(shifted.VerticalSpeed, Is.EqualTo("-394 ft/min"));
        }

        [TestCase(FleetState.Outbound, "ADL → KGC")]
        [TestCase(FleetState.Inbound, "KGC → ADL")]
        public void AirborneLegShowsSupportedAreaEstimateAndDistanceProgress(FleetState state, string route)
        {
            var aircraft = Aircraft(state);
            var leg = DestinationCatalogue.Adelaide.DistanceKmTo(aircraft.CurrentDestination.Value);
            var profile = new EnrouteProfile(leg, 900, aircraft.Type);
            var data = Fill(aircraft, profile: profile, elapsed: 300);
            Assert.That(data.Route, Is.EqualTo(route));
            Assert.That(data.Arrival, Is.EqualTo("10 min"));
            Assert.That(data.JourneyProgress, Is.EqualTo(profile.DistanceFractionAt(300)).Within(.00001));
            Assert.That(data.Remaining, Does.EndWith("km direct"));
            Assert.That(data.Journey, Does.Contain("arrival area estimate"));
            Assert.That(aircraft.StateEndsAt.Value.ElapsedSeconds, Is.EqualTo(900));
        }

        [TestCase(FleetState.TaxiOut)] [TestCase(FleetState.HoldingShort)]
        [TestCase(FleetState.TakingOff)] [TestCase(FleetState.Landing)]
        [TestCase(FleetState.GoAround)] [TestCase(FleetState.HoldingForLanding)] [TestCase(FleetState.TaxiIn)]
        public void GroundAndRunwaySequencesDoNotInventArrivalTimes(FleetState state)
        {
            var data = Fill(Aircraft(state));
            Assert.That(data.Arrival, Is.EqualTo("—"));
            Assert.That(data.Remaining, Is.EqualTo("—"));
            Assert.That(data.JourneyProgress, Is.EqualTo(-1));
            Assert.That(data.Journey, Is.Not.Empty);
            if (state is FleetState.Landing or FleetState.GoAround or FleetState.HoldingForLanding or FleetState.TaxiIn)
                Assert.That(data.Route, Is.EqualTo("KGC → ADL"));
        }

        [Test]
        public void PlannedAndCancelledStandDeparturesAndTurnaroundStayTruthful()
        {
            var aircraft = Aircraft(FleetState.AtStand);
            Assert.That(Fill(aircraft).Route, Is.EqualTo("At Adelaide"), "A retained last trip is not a scheduled departure.");
            var melbourne = DestinationCatalogue.All.First(d => d.Code == "MEL");
            aircraft.Scheduled = new ScheduledDeparture(melbourne, new SimulationTime(600));
            var data = Fill(aircraft);
            Assert.That(data.Route, Is.EqualTo("ADL → MEL"));
            Assert.That(data.Journey, Is.EqualTo("Departure in 5 min"));
            aircraft.Scheduled = new ScheduledDeparture(melbourne, new SimulationTime(600), cancelled: true);
            Assert.That(Fill(aircraft).Journey, Is.EqualTo("Departure cancelled"));
            Assert.That(Fill(aircraft).Route, Is.EqualTo("At Adelaide"));
            aircraft.Restore(FleetState.AtDestination, new SimulationTime(0), new SimulationTime(900));
            Assert.That(Fill(aircraft).Route, Is.EqualTo("At Kingscote · next leg to ADL"));
            Assert.That(Fill(aircraft).Arrival, Is.EqualTo("—"));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void EveryViewHasTheSameFlightDetailsAndControlsStayInTheirPanels(int view)
        {
            var data = Fill(Aircraft(FleetState.Inbound), profile: new EnrouteProfile(130, 900, AircraftType.Atr42), elapsed: 300);
            data.SelectedView = view;
            for (var i = 0; i < 4; i++) data.Available[i] = true;
            foreach (var viewport in new[] { (w: 320f, h: 600f), (w: 800f, h: 600f), (w: 1440f, h: 900f), (w: 1920f, h: 1080f) })
            {
                var layout = new FlightViewHudLayout(viewport.w, viewport.h);
                Assert.That(layout.Identity.Bottom, Is.LessThan(viewport.h * .5f));
                Assert.That(layout.Controls.Y, Is.GreaterThan(viewport.h * .5f));
                Assert.That(layout.Toast.Overlaps(layout.Identity), Is.False);
                Assert.That(layout.Toast.Overlaps(layout.Controls), Is.False);
                var draw = new HudDrawList();
                FlightViewHudPainter.Paint(draw, layout, data);
                foreach (var command in draw.Commands.Where(c => c.Kind is HudDrawKind.Text or HudDrawKind.Button))
                {
                    var panel = command.Box.Y < layout.Controls.Y ? layout.Identity : layout.Controls;
                    Assert.That(command.Box.X, Is.GreaterThanOrEqualTo(panel.X));
                    Assert.That(command.Box.Right, Is.LessThanOrEqualTo(panel.Right + .001f));
                    Assert.That(command.Box.Y, Is.GreaterThanOrEqualTo(panel.Y));
                    Assert.That(command.Box.Bottom, Is.LessThanOrEqualTo(panel.Bottom));
                }
                var buttons = draw.Commands.Where(c => c.Kind == HudDrawKind.Button).ToArray();
                foreach (var text in draw.Commands.Where(c => c.Kind == HudDrawKind.Text))
                    foreach (var button in buttons)
                        Assert.That(text.Box.Overlaps(button.Box), Is.False, text.Text + " / " + button.Text);
                var labels = draw.Commands.Where(c => c.Kind == HudDrawKind.Text).Select(c => c.Text).ToArray();
                Assert.That(labels.Any(s => s.StartsWith("FIELD HT")), Is.True);
                Assert.That(labels.Any(s => s.StartsWith("HDG")), Is.True);
                Assert.That(labels.Any(s => s.StartsWith("TO AREA")), Is.True);
                Assert.That(buttons.Single(b => b.ActionId == FlightViewHudPainter.ViewPrefix + view).Enabled, Is.True);
            }
        }
    }
}
