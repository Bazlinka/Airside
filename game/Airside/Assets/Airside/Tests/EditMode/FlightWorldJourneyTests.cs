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

        private void Start(string code)
        {
            // The game bootstrap touches the prototype's shared materials before loading
            // its component. Do likewise: Unity disallows native material-block creation
            // from an AddComponent constructor when its static type is still cold.
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(AirsidePrototype).TypeHandle);
            _host = new GameObject("Regional journey test");
            _prototype = _host.AddComponent<AirsidePrototype>();
            _clock = new ManualSimulationClock(new SimulationTime(0));
            var random = new SeededRandomSource(5);
            var operations = new AirlineOperations(_clock, random, DestinationCatalogue.Adelaide, AirlineOperations.AdelaideRegionalBays);
            var airline = Airline.Player("Flight world test", "#335566");
            operations.AddAirline(airline);
            _aircraft = operations.AddAircraft(airline, "VH-TST", AircraftType.Saab340, AirlineOperations.AdelaideRegionalBays[0]);
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
