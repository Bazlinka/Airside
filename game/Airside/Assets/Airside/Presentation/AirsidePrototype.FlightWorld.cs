using System;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    public sealed partial class AirsidePrototype
    {
        private double _flightOriginX, _flightOriginZ;
        private AirsideFlightWorldTerrain _flightTerrain;
        private readonly FlightWorldActorVisibility _flightAirportActors = new();
        private bool AirportPresentationVisible => _flightOriginX == 0 && _flightOriginZ == 0;

        private void UpdateFlightWorldAirportActors()
        {
            if (AirportPresentationVisible) { _flightAirportActors.Restore(); return; }
            _flightAirportActors.Hide(_boardingRoot);
            _flightAirportActors.Hide(_aerobridgeRoot);
            _flightAirportActors.Hide(_apronLifeRoot);
            _flightAirportActors.Hide(_birdFlockRoot);
            _flightAirportActors.Hide(_cloudUmbraRoot);
            _flightAirportActors.Hide(_fuelTruck);
            _flightAirportActors.Hide(_cateringTruck);
            _flightAirportActors.Hide(_baggageCart);
            _flightAirportActors.Hide(_passengerBus);
            _flightAirportActors.Hide(_stairs);
            _flightAirportActors.Hide(_chocks);
            _flightAirportActors.Hide(_gpuCart);
            foreach (var set in _ambientSets)
            { _flightAirportActors.Hide(set.Fuel); _flightAirportActors.Hide(set.Bags); }
            foreach (var truck in _stairTruckPool) _flightAirportActors.Hide(truck.Root);
            foreach (var bus in _remoteBusPool) _flightAirportActors.Hide(bus.Root);
            foreach (var tug in _tugPool) _flightAirportActors.Hide(tug.Root);
            foreach (var boat in _coastBoats) _flightAirportActors.Hide(boat.Boat);
        }
        private bool CanWatchJourney(FleetAircraft aircraft)
        {
            if (aircraft == null || !aircraft.CurrentDestination.HasValue
                || aircraft.State is not (FleetState.Outbound or FleetState.Inbound or FleetState.AtDestination)) return false;
            var destination = aircraft.CurrentDestination.Value;
            if (FlightWorldGrid.Covered(destination.Latitude, destination.Longitude)) return true;
            // An interstate service is watchable during its South Australian segment.
            return TryMiniMapLocation(aircraft, out var latitude, out var longitude)
                && RegionalMiniMap.Contains(latitude, longitude);
        }
        private bool WatchingJourney(string id) => InCockpit && id == _cockpitAircraftId
            && _fleetAircraftById.TryGetValue(id, out var aircraft) && CanWatchJourney(aircraft);
        private Vector3 FlightOrigin => new Vector3((float)_flightOriginX,0,(float)_flightOriginZ);

        // Runs before aircraft poses. Only presentation coordinates move; schedules/saves do not.
        private void UpdateFlightWorld()
        {
            var x=0.0; var z=0.0;
            FleetAircraft aircraft = null;
            var active=InCockpit && _fleetAircraftById.TryGetValue(_cockpitAircraftId,out aircraft);
            if(active && CanWatchJourney(aircraft) && _fleetFlightById.TryGetValue(_cockpitAircraftId,out var flight))
                JourneyWorld(flight,0,out x,out _,out z);
            else if(active && _cockpitView != null)
            { x=_cockpitView.position.x+_flightOriginX;z=_cockpitView.position.z+_flightOriginZ; }
            if (WatchingOutstation && OutstationJourney.TryFor(WatchedOutstation(), _preciseTime, out var network))
            {
                YpadFrame.ToWorld(network.Latitude, network.Longitude, out x, out z);
                active = true;
            }
            var distant=active && Math.Max(Math.Abs(x),Math.Abs(z))>80000;
            var ox=distant ? FlightWorldGrid.Origin(x) : 0;
            var oz=distant ? FlightWorldGrid.Origin(z) : 0;
            // Correct the previous position too: origin steps must never read as a speed spike.
            var originDelta=new Vector3((float)(_flightOriginX-ox),0,(float)(_flightOriginZ-oz));
            _cockpitPreviousPosition+=originDelta;
            if(_cameraController!=null) _cameraController.ShiftFlightOrigin(originDelta);
            _flightOriginX=ox;_flightOriginZ=oz;
            if(_airfieldRoot!=null) _airfieldRoot.position=-FlightOrigin;
            Shader.SetGlobalVector("_AirsideFlightOrigin",new Vector4((float)ox,0,(float)oz,0));
            UpdateFlightWorldAirportActors();
            if(active)
            {
                if(_flightTerrain==null) CreateFlightTerrainTimed();
                _flightTerrain.gameObject.SetActive(true);
                _flightTerrain.Tick(x,z,ox,oz);
            }
            else if(!InCockpit && _cameraController!=null && FlightWorldGrid.WideMap(AirsideCameraController.CurrentDistance))
            {
                // ADR 0251: zoomed past the classic limit, the overview camera streams the state under its focus.
                // No floating origin here: outside the cockpit the render origin is the world origin.
                if(_flightTerrain==null) CreateFlightTerrainTimed();
                _flightTerrain.gameObject.SetActive(true);
                var focus=_cameraController.FocusPoint;
                var distance=AirsideCameraController.CurrentDistance;
                FlightWorldGrid.WideHorizonFade(distance,AirsideCameraFeel.HorizonScale(distance,AirsideBareField.CameraFarClip),
                    out var fadeStart,out var fadeEnd);
                _flightTerrain.SetHorizonFade(fadeStart,fadeEnd);
                _flightTerrain.Tick(focus.x,focus.z,0,0,true,FlightWorldGrid.CoarseRadiusFor(distance));
            }
            else if(_flightTerrain!=null) _flightTerrain.gameObject.SetActive(false);
        }
        private void CreateFlightTerrainTimed()
        {
            var clock=System.Diagnostics.Stopwatch.StartNew();
            _flightTerrain=AirsideFlightWorldTerrain.Create();
            Debug.Log($"[Airside terrain] flight terrain created in {clock.Elapsed.TotalMilliseconds:F0} ms");
        }
        private void ResetFlightWorld()
        {
            _flightOriginX=_flightOriginZ=0;
            Shader.SetGlobalVector("_AirsideFlightOrigin",Vector4.zero);
            _flightAirportActors.Restore();
            if(_airfieldRoot!=null) _airfieldRoot.position=Vector3.zero;
            if(_flightTerrain!=null) _flightTerrain.gameObject.SetActive(false);
        }
        private Vector3? FleetJourneyPosition(CommercialFlight flight,float ahead)
        {
            if(!WatchingJourney(flight.AircraftId) || IsArrivingOnFinal(_fleetAircraftById[flight.AircraftId])) return null;
            JourneyWorld(flight,ahead,out var x,out var y,out var z);
            return new Vector3((float)(x-_flightOriginX),(float)y,(float)(z-_flightOriginZ));
        }
        private void JourneyWorld(CommercialFlight flight,float ahead,out double x,out double y,out double z)
        {
            var aircraft=_fleetAircraftById[flight.AircraftId];
            if (aircraft.Type.IsRotorcraft)
            {
                var rotor = HelicopterTrack.For(aircraft, _preciseTime + ahead);
                x = rotor.X; z = rotor.Z;
                y = AirsideAdelaideEmergencyAviation.PadGroundY + .18f + rotor.HeightMetres;
                return;
            }
            if (aircraft.State == FleetState.AtDestination
                && RegionalRunways.TryGet(aircraft.CurrentDestination.Value.Code,out var parkedRunway))
            {
                RegionalFlightPath.Landing(parkedRunway,0,0,0,out x,out y,out z);
                return;
            }
            TryEnroute(aircraft,out var profile,out var elapsed);
            elapsed=Math.Clamp(elapsed+ahead,0,profile.LegSeconds);
            var destination=aircraft.CurrentDestination.Value;
            double lat,lon;
            if(aircraft.State==FleetState.Inbound)
            {
                var metres=ArrivalMapTrack.DistanceOutMetres(profile.LegMetres/1000,profile.LegSeconds,
                    profile.LegSeconds-elapsed,aircraft.Type);
                var runway=_operations.RunwayFor(aircraft);
                ArrivalMapTrack.LatLon(destination,_operations.Home,aircraft.Registration,profile.LegMetres,metres,
                    runway,aircraft.Type,ArrivalApproach.LateralFactor(aircraft,runway),out lat,out lon);
                YpadFrame.ToWorld(lat,lon,out x,out z);
                if (RegionalRunways.TryGet(destination.Code,out var departureRunway) && elapsed<240)
                {
                    var exit=AirsideFlightPath.GroundY+profile.AltitudeFeetAt(RegionalFlightPath.DepartureSeconds)/EnrouteProfile.FeetPerMetre;
                    if(elapsed<=RegionalFlightPath.DepartureSeconds)
                    {
                        RegionalFlightPath.Departure(departureRunway,elapsed,exit,out x,out y,out z);
                        return;
                    }
                    RegionalFlightPath.Departure(departureRunway,RegionalFlightPath.DepartureSeconds,exit,out var sx,out _,out var sz);
                    var atExit=ArrivalMapTrack.DistanceOutMetres(profile.LegMetres/1000,profile.LegSeconds,
                        profile.LegSeconds-RegionalFlightPath.DepartureSeconds,aircraft.Type);
                    ArrivalMapTrack.LatLon(destination,_operations.Home,aircraft.Registration,profile.LegMetres,atExit,
                        runway,aircraft.Type,ArrivalApproach.LateralFactor(aircraft,runway),out var exitLat,out var exitLon);
                    YpadFrame.ToWorld(exitLat,exitLon,out var tx,out var tz);
                    var blend=Math.Clamp((elapsed-120)/120,0,1);var keepDeparture=1-blend*blend*(3-2*blend);
                    x+=(sx-tx)*keepDeparture;z+=(sz-tz)*keepDeparture;
                }
                y=AirsideFlightPath.GroundY+profile.AltitudeFeetAt(elapsed)/EnrouteProfile.FeetPerMetre;
                // Ease the cruise height onto the same capped slope used by the local final.
                // Changing ownership at ShowMetres must not change the cockpit's altitude.
                if(metres<ArrivalApproach.ShowMetres+ArrivalMapTrack.BlendMetres)
                {
                    var hold=AirsideFlightPath.Approach((float)ApproachHold.HoldingFinalProgress(0),0,aircraft.Type);
                    var finalHeight=AirsideFlightPath.GroundY+ArrivalApproach.Height(
                        CircuitProfile.GlideslopeHeight(hold.x-(float)metres));
                    var blend=Math.Clamp((metres-ArrivalApproach.ShowMetres)/ArrivalMapTrack.BlendMetres,0,1);
                    var finalWeight=1-blend*blend*(3-2*blend);
                    y+=(finalHeight-y)*finalWeight;
                }
                return;
            }
            var remaining=profile.LegSeconds-elapsed;
            if (RegionalRunways.TryGet(destination.Code,out var regionalRunway) && remaining<=RegionalFlightPath.TerminalSeconds)
            {
                RegionalFlightPath.Landing(regionalRunway,0,0,remaining,out x,out y,out z);
                return;
            }
            var performance = AircraftPerformance.For(aircraft.Type);
            var exitSeconds = performance.DepartedSeconds;
            // Outbound begins at the END of Takeoff, not the END of Departed. Use the
            // same local climb-out in every view before handing the pose to the route.
            if (DepartureFlightTransition.UsesLocalClimbout(performance, elapsed))
            {
                var local = DepartureWorldPosition(flight, aircraft, (float)(elapsed / exitSeconds));
                x = local.x; y = local.y; z = local.z;
                return;
            }
            OutboundRouteWorld(aircraft, profile, elapsed, out x, out y, out z);
            var end = DepartureWorldPosition(flight, aircraft, 1);
            const float derivativeSeconds = .1f;
            var beforeEnd = DepartureWorldPosition(flight, aircraft, 1 - derivativeSeconds / exitSeconds);
            OutboundRouteWorld(aircraft, profile, exitSeconds, out var rx, out _, out var rz);
            OutboundRouteWorld(aircraft, profile, exitSeconds + derivativeSeconds, out var nx, out _, out var nz);
            // Keep this join before the regional approach blend; its timer and ETA stay intact.
            var joinSeconds = DepartureFlightTransition.JoinDuration(performance, profile);
            var sinceExit = elapsed - exitSeconds;
            x += DepartureFlightTransition.RouteOffset(sinceExit, joinSeconds, end.x-rx,
                (end.x-beforeEnd.x)/derivativeSeconds - (nx-rx)/derivativeSeconds);
            y = AirsideFlightPath.GroundY + DepartureFlightTransition.OutboundHeight(performance, profile, elapsed);
            z += DepartureFlightTransition.RouteOffset(sinceExit, joinSeconds, end.z-rz,
                (end.z-beforeEnd.z)/derivativeSeconds - (nz-rz)/derivativeSeconds);
            if (RegionalRunways.TryGet(destination.Code,out regionalRunway) && remaining<360)
            {
                // Bend the cruise track onto the mapped runway's terminal approach.
                var t=profile.DistanceFractionAt(profile.LegSeconds-RegionalFlightPath.TerminalSeconds);
                RouteMap.FlightPoint(_operations.Home.Latitude,_operations.Home.Longitude,destination.Latitude,
                    destination.Longitude,t,aircraft.Registration,out lat,out lon);
                YpadFrame.ToWorld(lat,lon,out var tx,out var tz);
                RegionalFlightPath.Landing(regionalRunway,0,0,RegionalFlightPath.TerminalSeconds,out var sx,out var sy,out var sz);
                var blend=Math.Clamp((360-remaining)/180,0,1);blend=blend*blend*(3-2*blend);
                x+=(sx-tx)*blend;z+=(sz-tz)*blend;
                y+=(sy-(AirsideFlightPath.GroundY+profile.AltitudeFeetAt(profile.LegSeconds-180)/EnrouteProfile.FeetPerMetre))*blend;
            }
        }

        private Vector3 DepartureWorldPosition(CommercialFlight flight, FleetAircraft aircraft, float progress) =>
            RunwayPosition(flight, ApplyDepartureTurn(flight, AircraftPhase.Departed, progress,
                AirsideFlightPath.Departed(progress, TakeoffOffsetX, aircraft.Type, aircraft.AssignedRunway)));

        private void OutboundRouteWorld(FleetAircraft aircraft, EnrouteProfile profile, double elapsed,
            out double x, out double y, out double z)
        {
            var destination = aircraft.CurrentDestination.Value;
            RouteMap.FlightPoint(_operations.Home.Latitude, _operations.Home.Longitude,
                destination.Latitude, destination.Longitude, profile.DistanceFractionAt(elapsed),
                aircraft.Registration, out var lat, out var lon);
            YpadFrame.ToWorld(lat, lon, out x, out z);
            y = AirsideFlightPath.GroundY + profile.AltitudeFeetAt(elapsed) / EnrouteProfile.FeetPerMetre;
        }
    }
}
