using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class FullMapAuditRegressionTests
    {
        private static Destination Destination(string code) => DestinationCatalogue.All.First(d => d.Code == code);
        private static FleetAircraft Aircraft(bool player) => new("VH-MAP", player
            ? Airline.Player("Map Air", "#123456") : Airline.Jetstar(), AircraftType.Atr42,
            AirlineOperations.AdelaideRegionalBays[0], new SimulationTime(0));

        [TestCase(true)] [TestCase(false)]
        public void FirstScheduledDepartureHasLocatedRowIdentityAndRouteBeforePushback(bool player)
        {
            var aircraft = Aircraft(player);
            aircraft.Scheduled = new ScheduledDeparture(Destination("KGC"), new SimulationTime(600));
            var rows = new List<FullMapFlightPresentation.FieldFlight>();
            if (FullMapFlightPresentation.TryFieldFlight(aircraft, DestinationCatalogue.Adelaide,
                true, -34.95, 138.53, out var row)) rows.Add(row);
            Assert.That(rows.Count, Is.EqualTo(1), "This is the runtime row feeding map counts and hit targets.");
            Assert.That(rows[0].Registration, Is.EqualTo(aircraft.Registration));
            Assert.That(rows[0].Aircraft, Is.SameAs(aircraft));
            Assert.That(rows[0].From.Code, Is.EqualTo("ADL"));
            Assert.That(rows[0].To.Code, Is.EqualTo("KGC"));
            Assert.That(rows[0].Latitude, Is.EqualTo(-34.95));
            Assert.That(rows[0].Longitude, Is.EqualTo(138.53));
            Assert.That(aircraft.CurrentDestination, Is.Null);
            Assert.That(aircraft.State, Is.EqualTo(FleetState.AtStand));
        }

        [Test]
        public void CancelledUnscheduledAndUnlocatedFirstDeparturesHaveNoRows()
        {
            var aircraft = Aircraft(true);
            Assert.That(FullMapFlightPresentation.TryFieldFlight(aircraft, DestinationCatalogue.Adelaide,
                true, -34.95, 138.53, out _), Is.False);
            aircraft.Scheduled = new ScheduledDeparture(Destination("KGC"), new SimulationTime(600), cancelled: true);
            Assert.That(FullMapFlightPresentation.TryFieldFlight(aircraft, DestinationCatalogue.Adelaide,
                true, -34.95, 138.53, out _), Is.False);
            aircraft.Scheduled = new ScheduledDeparture(Destination("KGC"), new SimulationTime(600));
            Assert.That(FullMapFlightPresentation.TryFieldFlight(aircraft, DestinationCatalogue.Adelaide,
                false, -34.95, 138.53, out _), Is.False);
            Assert.That(FullMapFlightPresentation.TryFieldFlight(aircraft, DestinationCatalogue.Adelaide,
                true, double.NaN, 138.53, out _), Is.False);
            aircraft.Restore(FleetState.Outbound, new SimulationTime(0), null);
            Assert.That(FullMapFlightPresentation.TryFieldFlight(aircraft, DestinationCatalogue.Adelaide,
                true, -34.95, 138.53, out _), Is.False, "An off-map schedule is not a current trip.");
        }

        [TestCase(FleetState.TaxiOut, "ADL", "KGC")]
        [TestCase(FleetState.Landing, "KGC", "ADL")]
        [TestCase(FleetState.TaxiIn, "KGC", "ADL")]
        public void ActiveFieldFlightsKeepTheirRouteDirection(FleetState state, string from, string to)
        {
            var aircraft = Aircraft(true);
            aircraft.Restore(state, new SimulationTime(0), null);
            aircraft.CurrentDestination = Destination("KGC");
            Assert.That(FullMapFlightPresentation.TryFieldFlight(aircraft, DestinationCatalogue.Adelaide,
                true, -34.95, 138.53, out var row), Is.True);
            Assert.That(row.From.Code, Is.EqualTo(from));
            Assert.That(row.To.Code, Is.EqualTo(to));
        }

        [Test]
        public void StandWithRetainedTripUsesNewScheduleAndHidesCancelledBooking()
        {
            var aircraft = Aircraft(true);
            aircraft.CurrentDestination = Destination("MEL");
            aircraft.Scheduled = new ScheduledDeparture(Destination("KGC"), new SimulationTime(600));
            Assert.That(FullMapFlightPresentation.TryFieldFlight(aircraft, DestinationCatalogue.Adelaide,
                true, -34.95, 138.53, out var row), Is.True);
            Assert.That(row.To.Code, Is.EqualTo("KGC"));
            aircraft.Scheduled = new ScheduledDeparture(Destination("KGC"), new SimulationTime(600), cancelled: true);
            Assert.That(FullMapFlightPresentation.TryFieldFlight(aircraft, DestinationCatalogue.Adelaide,
                true, -34.95, 138.53, out _), Is.False);
            Assert.That(aircraft.CurrentDestination.Value.Code, Is.EqualTo("MEL"), "Presentation does not mutate trips.");
        }

        [Test]
        public void InspectingAnotherFlightPreservesPlanningButExplicitPlanningClearsInspection()
        {
            var state = new FullMapFlightPresentation.Interaction("A", "A", null).Inspect("B");
            Assert.That(state.PlanningId, Is.EqualTo("A"));
            Assert.That(state.SelectedId, Is.EqualTo("B"));
            Assert.That(state.InspectorId, Is.EqualTo("B"));
            state = state.Plan("C");
            Assert.That(state.PlanningId, Is.EqualTo("C"));
            Assert.That(state.SelectedId, Is.EqualTo("C"));
            Assert.That(state.InspectorId, Is.Null);
            Assert.That(state.Plan("C").InspectorId, Is.Null, "Reopening planning cannot resurrect B.");
        }

        [TestCase(true)] [TestCase(false)]
        public void HoldLinkOrCycleSelectionCannotLeavePreviousInspectorActive(bool player)
        {
            var state = new FullMapFlightPresentation.Interaction("A", "A", "A").Select("B", player);
            Assert.That(state.SelectedId, Is.EqualTo("B"));
            Assert.That(state.InspectorId, Is.Null);
            Assert.That(state.PlanningId, Is.EqualTo(player ? "B" : "A"));
        }

        [TestCase(1, 0)] [TestCase(-1, 0)] [TestCase(0, 1)] [TestCase(0, -1)]
        public void FieldHeadingMatchesProjectedGeographicSegment(double forwardX, double forwardZ)
        {
            var lens = new AustraliaMapLens();
            lens.ShowSouthAustralia(720, 600);
            const double x = 2400, z = -1600;
            YpadFrame.ToLatLon(x, z, out var lat, out var lon);
            YpadFrame.ToLatLon(x + forwardX * 1000, z + forwardZ * 1000, out var aheadLat, out var aheadLon);
            lens.Project(0, 0, 720, 600, lon, lat, out var px, out var py);
            lens.Project(0, 0, 720, 600, aheadLon, aheadLat, out var ax, out var ay);
            var expected = Math.Atan2(ay - py, ax - px) * 180 / Math.PI + 90;
            Assert.That(FullMapFlightPresentation.FieldHeading(lens, 720, 600, x, z, forwardX, forwardZ),
                Is.EqualTo(expected).Within(.001));
        }

        [TestCase(.2f)] [TestCase(1f)] [TestCase(100f)]
        public void TrueNorthHeadingRemainsNorthWithFloatingRenderOrigin(float zoom)
        {
            var lens = new AustraliaMapLens();
            lens.SetZoom(zoom);
            lens.CenterOn(720, 600, 138.53, -34.95);
            var yaw = YpadFrame.UnityYawFromTrue(0) * Math.PI / 180;
            const double originX = 7000, originZ = -4000;
            var heading = FullMapFlightPresentation.FieldHeading(lens, 720, 600,
                (2400 - originX) + originX, (-1600 - originZ) + originZ, Math.Sin(yaw), Math.Cos(yaw));
            Assert.That(heading, Is.EqualTo(0).Within(.03));
            Assert.That(FullMapFlightPresentation.FieldHeading(lens, 720, 600, 0, 0, 1, 0),
                Is.EqualTo(52.33574).Within(.1), "World +x follows runway 05 rather than geographic east.");
        }
    }
}
