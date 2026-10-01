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
        private bool CanWatchJourney(FleetAircraft aircraft) => aircraft != null
            && aircraft.CurrentDestination.HasValue
            && aircraft.State is FleetState.Outbound or FleetState.Inbound or FleetState.AtDestination
            && FlightWorldGrid.Covered(aircraft.CurrentDestination.Value.Latitude, aircraft.CurrentDestination.Value.Longitude);
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
            var distant=active && Math.Max(Math.Abs(x),Math.Abs(z))>80000;
            var ox=distant ? FlightWorldGrid.Origin(x) : 0;
            var oz=distant ? FlightWorldGrid.Origin(z) : 0;
            // Correct the previous position too: origin steps must never read as a speed spike.
            var originDelta=new Vector3((float)(_flightOriginX-ox),0,(float)(_flightOriginZ-oz));
            _cockpitPreviousPosition+=originDelta;
            if(InCockpit && _cameraController!=null) _cameraController.transform.position+=originDelta;
            _flightOriginX=ox;_flightOriginZ=oz;
            if(_airfieldRoot!=null) _airfieldRoot.position=-FlightOrigin;
            Shader.SetGlobalVector("_AirsideFlightOrigin",new Vector4((float)ox,0,(float)oz,0));
            if(active)
            {
                if(_flightTerrain==null) _flightTerrain=AirsideFlightWorldTerrain.Create();
                _flightTerrain.gameObject.SetActive(true);
                _flightTerrain.Tick(x,z,ox,oz);
            }
            else if(_flightTerrain!=null) _flightTerrain.gameObject.SetActive(false);
        }
        private void ResetFlightWorld()
        {
            _flightOriginX=_flightOriginZ=0;
            Shader.SetGlobalVector("_AirsideFlightOrigin",Vector4.zero);
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
                // Extended final is a 3 degree slope; match the local arrival before its ownership handoff.
                if(metres<=ArrivalApproach.ShowMetres)
                {
                    ArrivalMapTrack.FinalWorldXZ(runway,aircraft.Type,(float)metres,
                        ArrivalApproach.LateralFactor(aircraft,runway),0,out var fx,out var fz);
                    var hold=AirsideFlightPath.Approach((float)ApproachHold.HoldingFinalProgress(0),0,aircraft.Type);
                    y=hold.y+metres*CircuitProfile.GlideslopeTangent;
                }
                return;
            }
            var remaining=profile.LegSeconds-elapsed;
            if (RegionalRunways.TryGet(destination.Code,out var regionalRunway) && remaining<=RegionalFlightPath.TerminalSeconds)
            {
                RegionalFlightPath.Landing(regionalRunway,0,0,remaining,out x,out y,out z);
                return;
            }
            var progress=profile.DistanceFractionAt(elapsed);
            RouteMap.FlightPoint(_operations.Home.Latitude,_operations.Home.Longitude,destination.Latitude,
                destination.Longitude,progress,aircraft.Registration,out lat,out lon);
            YpadFrame.ToWorld(lat,lon,out x,out z);
            // The map starts at the airport centre, the 3D climb-out ends kilometres beyond it.
            // Fade their positional difference over 40 km rather than teleporting to the centre.
            var end=RunwayPosition(flight,ApplyDepartureTurn(flight,AircraftPhase.Departed,1,
                AirsideFlightPath.Departed(1,TakeoffOffsetX,aircraft.Type,aircraft.AssignedRunway)));
            YpadFrame.ToWorld(_operations.Home.Latitude,_operations.Home.Longitude,out var hx,out var hz);
            var u=Math.Clamp(profile.LegMetres*progress/40000,0,1);
            var keep=1-u*u*(3-2*u);
            x+=(end.x-hx)*keep;z+=(end.z-hz)*keep;
            y=AirsideFlightPath.GroundY+profile.AltitudeFeetAt(elapsed)/EnrouteProfile.FeetPerMetre;
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
    }
}
