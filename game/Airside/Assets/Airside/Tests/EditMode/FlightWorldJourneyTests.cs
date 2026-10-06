using System;
using System.Collections.Generic;
using System.Reflection;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Airside.Tests
{
    public sealed class FlightWorldJourneyTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject _host;
        private AirsidePrototype _prototype;
        private ManualSimulationClock _clock;
        private FleetAircraft _aircraft;
        private CommercialFlight _flight;
        private long _seconds;

        private void Set(string name, object value) => typeof(AirsidePrototype).GetField(name, Hidden).SetValue(_prototype, value);

        private void Start(string code, AircraftType type = null)
        {
            // The game bootstrap touches the prototype's shared materials before loading
            // its component. Do likewise: Unity disallows native material-block creation
            // from an AddComponent constructor when its static type is still cold.
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(AirsidePrototype).TypeHandle);
            _host = new GameObject("Regional journey test");
            _prototype = _host.AddComponent<AirsidePrototype>();
            _clock = new ManualSimulationClock(new SimulationTime(0));
            var random = new SeededRandomSource(5);
            var operations = new AirlineOperations(_clock, random, DestinationCatalogue.Adelaide,
                type != null && type.IsRotorcraft ? AirlineOperations.AdelaideHelipadStands : AirlineOperations.AdelaideRegionalBays);
            var airline = Airline.Player("Flight world test", "#335566");
            operations.AddAirline(airline);
            _aircraft = operations.AddAircraft(airline, "VH-TST", type ?? AircraftType.Saab340,
                type != null && type.IsRotorcraft ? AirlineOperations.AdelaideHelipadStands[0] : AirlineOperations.AdelaideRegionalBays[0]);
            DestinationCatalogue.TryFind(code, out var destination);
            _aircraft.CurrentDestination = destination;
            _seconds = LegTiming.AirborneSeconds(DestinationCatalogue.Adelaide.DistanceKmTo(destination), _aircraft.Type);
            var circuit = new AirportSimulation(_clock, random, new ReservationTable());
            _flight = new CommercialFlight(_aircraft.Registration, _clock.Now, AirportSimulation.StandOne,
                circuit.TaxiNetwork.RoutesTo(AirportSimulation.StandOne));
            Set("_clock", _clock); Set("_operations", operations); Set("_simulation", circuit);
            ((Dictionary<string, FleetAircraft>)typeof(AirsidePrototype).GetField("_fleetAircraftById", Hidden).GetValue(_prototype))
                .Add(_aircraft.Registration, _aircraft);
            ((Dictionary<string, CommercialFlight>)typeof(AirsidePrototype).GetField("_fleetFlightById", Hidden).GetValue(_prototype))
                .Add(_aircraft.Registration, _flight);
        }

        private Vector3 Pose(double elapsed)
        {
            _clock.Set(new SimulationTime((long)elapsed));
            Set("_preciseTime", elapsed);
            var args = new object[] { _flight, 0f, 0d, 0d, 0d };
            typeof(AirsidePrototype).GetMethod("JourneyWorld", Hidden).Invoke(_prototype, args);
            return new Vector3((float)(double)args[2], (float)(double)args[3], (float)(double)args[4]);
        }

        [TearDown] public void Cleanup() { if (_host != null) Object.DestroyImmediate(_host); }

        [TestCase(FleetState.Landing)]
        [TestCase(FleetState.GoAround)]
        [TestCase(FleetState.TaxiOut)]
        public void FullMapIncludesLocalFlightsUsingTheirActualRenderPosition(FleetState state)
        {
            Start("CPD"); _aircraft.Restore(state, new SimulationTime(0), new SimulationTime(200));
            var actor = new GameObject("Visible local aircraft"); actor.transform.SetParent(_host.transform);
            actor.transform.position = new Vector3(123, 100, 456);
            Set("_flightOriginX", 1000d); Set("_flightOriginZ", 2000d);
            ((Dictionary<string, Transform>)typeof(AirsidePrototype).GetField("_fleetViewById", Hidden).GetValue(_prototype))
                .Add(_aircraft.Registration, actor.transform);
            typeof(AirsidePrototype).GetMethod("LocateMapFlights", Hidden).Invoke(_prototype, null);
            var flights = (System.Collections.IList)typeof(AirsidePrototype).GetField("_mapFlights", Hidden).GetValue(_prototype);
            Assert.That(flights.Count, Is.EqualTo(1));
            var row = flights[0]; YpadFrame.ToLatLon(1123, 2456, out var latitude, out var longitude);
            Assert.That(row.GetType().GetField("Latitude").GetValue(row), Is.EqualTo(latitude).Within(.000001));
            Assert.That(row.GetType().GetField("Longitude").GetValue(row), Is.EqualTo(longitude).Within(.000001));
        }

        [Test] public void FullMapFlightSelectionKeepsTheMapOpenAndExposesViewingActions()
        {
            Start("CPD");
            _aircraft.Restore(FleetState.Outbound, new SimulationTime(0), new SimulationTime(_seconds));
            Pose(_seconds * .5);
            typeof(AirsidePrototype).GetMethod("SelectMapFlight", Hidden).Invoke(_prototype, new object[] { _aircraft });
            Assert.That(typeof(AirsidePrototype).GetField("_activeWorkspace", Hidden).GetValue(_prototype).ToString(), Is.EqualTo("Map"));
            Assert.That(typeof(AirsidePrototype).GetField("_mapFlightInspectorId", Hidden).GetValue(_prototype), Is.EqualTo(_aircraft.Registration));
            Assert.That(typeof(AirsidePrototype).GetField("_mapAircraft", Hidden).GetValue(_prototype), Is.Null,
                "viewing an airborne flight must not replace the aircraft being planned");
            typeof(AirsidePrototype).GetMethod("FillSelectionCard", Hidden).Invoke(_prototype, new object[] { _aircraft });
            var card = (SelectionCardData)typeof(AirsidePrototype).GetField("_selectionCard", Hidden).GetValue(_prototype);
            var commands = new HudDrawList();
            SelectionCardPainter.Paint(commands, new HudBox(0, 0, 320, 500), card);
            Assert.That(System.Linq.Enumerable.Any(commands.Commands, c => c.ActionId == "camera-exterior" && c.Enabled), Is.True);
            Assert.That(card.JourneyLeft, Does.StartWith("Lands"));
        }

        [Test] public void HelicopterMapAndExteriorUseTheHelicopterTrackInsteadOfARunwayPath()
        {
            Start("CPD", AircraftType.Bell412);
            _aircraft.Restore(FleetState.Outbound, new SimulationTime(0), new SimulationTime(_seconds));
            var world = Pose(_seconds * .5);
            var rotor = HelicopterTrack.For(_aircraft, _seconds * .5);
            Assert.That(world.x, Is.EqualTo(rotor.X).Within(.01));
            Assert.That(world.z, Is.EqualTo(rotor.Z).Within(.01));
            Assert.That(world.y, Is.EqualTo(AirsideAdelaideEmergencyAviation.PadGroundY + .18f + rotor.HeightMetres).Within(.01));
            var mode = typeof(AirsidePrototype).GetNestedType("AircraftViewMode", BindingFlags.NonPublic);
            var reason = typeof(AirsidePrototype).GetMethod("FlightViewReason", Hidden);
            Assert.That(reason.Invoke(_prototype, new[] { (object)_aircraft, Enum.Parse(mode, "Exterior") }), Is.EqualTo(""));
            Assert.That(reason.Invoke(_prototype, new[] { (object)_aircraft, Enum.Parse(mode, "LeftWindow") }), Is.EqualTo("Passenger view unavailable"));
        }

        [Test] public void InterstateFlightCanBeViewedDuringItsSouthAustralianSegment()
        {
            Start("MEL");
            _aircraft.Restore(FleetState.Outbound, new SimulationTime(0), new SimulationTime(_seconds));
            Pose(60);
            var canWatch = typeof(AirsidePrototype).GetMethod("CanWatchJourney", Hidden);
            Assert.That(canWatch.Invoke(_prototype, new object[] { _aircraft }), Is.True);
            Pose(_seconds - 1);
            Assert.That(canWatch.Invoke(_prototype, new object[] { _aircraft }), Is.False);
        }

        [Test] public void HiddenRegionalAircraftStillHasGeographicPositionAndStatusReadout()
        {
            Start("CPD");
            _aircraft.Restore(FleetState.Outbound, new SimulationTime(0), new SimulationTime(_seconds));
            var position = Pose(_seconds * .5);
            Set("_flightOriginX", 240000d); Set("_flightOriginZ", -80000d);
            var args = new object[] { _aircraft, 0d, 0d };
            Assert.That(typeof(AirsidePrototype).GetMethod("TryMiniMapLocation", Hidden).Invoke(_prototype, args), Is.True);
            YpadFrame.ToLatLon(position.x, position.z, out var latitude, out var longitude);
            Assert.That((double)args[1], Is.EqualTo(latitude).Within(.00001));
            Assert.That((double)args[2], Is.EqualTo(longitude).Within(.00001));
            var text = (string)typeof(AirsidePrototype).GetMethod("SelectionLiveStats", Hidden).Invoke(_prototype, new object[] { _aircraft });
            SelectionCardText.SplitReadout(text, out var speed, out var altitude, out _);
            Assert.That(speed, Does.Contain("kt")); Assert.That(altitude, Does.Contain("ft"));
        }

        [Test] public void MiniMapSelectionKeepsStatusAndCameraActionsOutsideThePlanner()
        {
            Start("CPD");
            _aircraft.Restore(FleetState.Outbound, new SimulationTime(0), new SimulationTime(_seconds));
            Pose(_seconds * .5);
            typeof(AirsidePrototype).GetMethod("SelectMiniMapAircraft", Hidden).Invoke(_prototype, new object[] { _aircraft });
            Assert.That(typeof(AirsidePrototype).GetField("_selectedAircraftId", Hidden).GetValue(_prototype), Is.EqualTo(_aircraft.Registration));
            Assert.That(typeof(AirsidePrototype).GetField("_activeWorkspace", Hidden).GetValue(_prototype).ToString(), Is.EqualTo("None"));
            typeof(AirsidePrototype).GetMethod("FillSelectionCard", Hidden).Invoke(_prototype, new object[] { _aircraft });
            var card = (SelectionCardData)typeof(AirsidePrototype).GetField("_selectionCard", Hidden).GetValue(_prototype);
            Assert.That(card.ShowCameraActions, Is.True);
            Assert.That(card.CanExterior, Is.True); Assert.That(card.CanPassenger, Is.True);
            Assert.That(card.JourneyLeft, Does.StartWith("Lands"));
        }

        [TestCase("KGC", FleetState.Outbound)] [TestCase("CPD", FleetState.Outbound)]
        [TestCase("KGC", FleetState.Inbound)] [TestCase("CPD", FleetState.Inbound)]
        public void RegionalTrackStaysContinuousAcrossCruiseAndTerminalHandoffs(string code, FleetState state)
        {
            Start(code);
            _aircraft.Restore(state, new SimulationTime(0), new SimulationTime(_seconds));
            var previous = Pose(0);
            // Real presentation path, not a copy of the formula. Sampling both directions
            // catches ownership/height discontinuities that simple runway endpoint tests miss.
            for (var t = 0.25; t <= _seconds; t += 0.25)
            {
                var current = Pose(t);
                Assert.That(Vector3.Distance(previous, current), Is.LessThan(80f), $"{code} {state} jumped at {t:0.00}s");
                Assert.That(float.IsNaN(current.y) || float.IsInfinity(current.y), Is.False);
                previous = current;
            }
        }

        [Test] public void DistantTerrainAndPoseOwnershipRemainActiveAcrossAllFlightViews()
        {
            Start("CPD");
            _aircraft.Restore(FleetState.Outbound,new SimulationTime(0),new SimulationTime(_seconds));
            Pose(_seconds*.5); Set("_cockpitAircraftId",_aircraft.Registration);
            var viewMode=typeof(AirsidePrototype).GetNestedType("AircraftViewMode",BindingFlags.NonPublic);
            var update=typeof(AirsidePrototype).GetMethod("UpdateFlightWorld",Hidden);
            var originX=typeof(AirsidePrototype).GetField("_flightOriginX",Hidden);
            var originZ=typeof(AirsidePrototype).GetField("_flightOriginZ",Hidden);
            var terrainField=typeof(AirsidePrototype).GetField("_flightTerrain",Hidden);
            object x=null,z=null;
            try
            {
                foreach(var mode in Enum.GetValues(viewMode))
                {
                    Set("_aircraftViewMode",mode);update.Invoke(_prototype,null);
                    if(x == null){x=originX.GetValue(_prototype);z=originZ.GetValue(_prototype);}
                    Assert.That(originX.GetValue(_prototype),Is.EqualTo(x));Assert.That(originZ.GetValue(_prototype),Is.EqualTo(z));
                    Assert.That((double)x == 0 && (double)z == 0,Is.False,"Expected a distant streaming origin");
                    Assert.That(((AirsideFlightWorldTerrain)terrainField.GetValue(_prototype)).gameObject.activeSelf,Is.True);
                    Assert.That(typeof(AirsidePrototype).GetMethod("WatchingJourney",Hidden).Invoke(_prototype,new object[]{_aircraft.Registration}),Is.True);
                }
                typeof(AirsidePrototype).GetMethod("ResetFlightWorld",Hidden).Invoke(_prototype,null);
                Assert.That(originX.GetValue(_prototype),Is.EqualTo(0d));Assert.That(originZ.GetValue(_prototype),Is.EqualTo(0d));
            }
            finally
            {
                var terrain=(AirsideFlightWorldTerrain)terrainField.GetValue(_prototype);
                if(terrain != null) Object.DestroyImmediate(terrain.gameObject);
            }
        }

        [Test] public void DistantActorCullingRestoresPreviouslyHiddenActorsAndAcceptsDestroyedObjects()
        {
            var active = new GameObject("Airport vehicle");
            var inactive = new GameObject("Parked hidden vehicle");
            var removed = new GameObject("Recycled vehicle");
            try
            {
                inactive.SetActive(false);
                var visibility = new FlightWorldActorVisibility();
                visibility.Hide(active.transform); visibility.Hide(inactive.transform); visibility.Hide(removed.transform);
                visibility.Hide(active.transform);
                Assert.That(active.activeSelf, Is.False);
                Object.DestroyImmediate(removed);
                visibility.Restore();
                Assert.That(active.activeSelf, Is.True);
                Assert.That(inactive.activeSelf, Is.False);
            }
            finally { Object.DestroyImmediate(active); Object.DestroyImmediate(inactive); if(removed != null) Object.DestroyImmediate(removed); }
        }
    }
}
