using System;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    public sealed partial class AirsidePrototype
    {
        private string _outstationWatchId;
        private Transform _outstationWatchView;
        private CockpitInterior _outstationInterior;
        private bool WatchingOutstation => !string.IsNullOrEmpty(_outstationWatchId);
        private OutstationAircraft WatchedOutstation()
        {
            if (_operations == null) return null;
            foreach (var aircraft in _operations.OutstationFleet)
                if (aircraft.Registration == _outstationWatchId) return aircraft;
            return null;
        }

        private void EnterOutstationView(OutstationAircraft aircraft, AircraftViewMode mode)
        {
            if (_cameraController == null || !OutstationJourney.TryFor(aircraft, _preciseTime, out var journey)
                || !journey.Airborne) { ShowToast("Flight views open when this aircraft is airborne."); return; }
            if (mode != AircraftViewMode.Exterior && mode != AircraftViewMode.Cockpit
                && !PassengerCabinProfile.TryFor(aircraft.Type.Id, out _)) return;
            if (InCockpit) ExitCockpit(true);
            ExitOutstationView(false);
            _outstationWatchId = aircraft.Registration;
            _selectedAircraftId = aircraft.Registration;
            _activeWorkspace = HudWorkspace.None;
            var accent = AirsideTheme.FromHex(_operations.PlayerAirline.LiveryHex);
            _outstationWatchView = BuildAircraftForType("Network " + aircraft.Registration, aircraft.Type, accent, null);
            PaintFleetLivery(_outstationWatchView, _operations.PlayerAirline, aircraft.Type, aircraft.Registration, false, accent);
            UpdateFlightWorld();
            UpdateOutstationView();
            if (mode == AircraftViewMode.Exterior)
            {
                var profile = AircraftVisualProfiles.For(aircraft.Type);
                _cameraController.StartFlightExterior(_outstationWatchView,
                    profile.VisualCentreOffsetMetres + Vector3.up * profile.PickCentreYMetres,
                    Mathf.Max(profile.PickSizeMetres.x, profile.PickSizeMetres.z));
            }
            else
            {
                _outstationInterior = mode == AircraftViewMode.Cockpit
                    ? aircraft.Type.Id == AircraftType.Saab340.Id ? SaabCockpitInterior.Build(_outstationWatchView)
                    : JetCockpitProfile.TryFor(aircraft.Type.Id, out _) ? JetCockpitInterior.Build(_outstationWatchView, aircraft.Type)
                    : TurbopropCockpitInterior.Create(_outstationWatchView, aircraft.Type)
                    : PassengerCabinInterior.Build(_outstationWatchView, aircraft.Type, false);
                if (_outstationInterior == null) { ExitOutstationView(true); return; }
                _outstationInterior.Enter();
                if (mode == AircraftViewMode.Cockpit) _cameraController.StartCockpit(_outstationInterior.Seat);
                else _cameraController.StartPassenger(_outstationInterior.Seat);
            }
            ShowToast("Network flight view · outside Adelaide's detailed airport scene. Esc returns to Fleet.");
            PlayUiClick();
        }

        private void UpdateOutstationView()
        {
            if (!WatchingOutstation) return;
            var aircraft = WatchedOutstation();
            if (_outstationWatchView == null || !OutstationJourney.TryFor(aircraft, _preciseTime, out var journey)
                || !journey.Airborne)
            {
                ExitOutstationView(true);
                ShowToast("Aircraft reached the ground. Its service continues in Fleet.");
                return;
            }
            YpadFrame.ToWorld(journey.Latitude, journey.Longitude, out var x, out var z);
            _outstationWatchView.position = new Vector3((float)(x - _flightOriginX),
                AirsideFlightPath.GroundY + (float)(journey.AltitudeFeet / EnrouteProfile.FeetPerMetre),
                (float)(z - _flightOriginZ));
            _outstationWatchView.rotation = Quaternion.Euler(0, (float)journey.WorldYaw, 0);
            var parts = PartsFor(_outstationWatchView);
            var engines = new EngineState(1, 1, true, false);
            SpinPropellers(_outstationWatchView, parts.Propellers, AircraftPhase.Departed, engines, (float)journey.Progress);
            SpinJetFans(_outstationWatchView, parts.FanLeft, parts.FanRight, AircraftPhase.Departed, engines, (float)journey.Progress);
            UpdateAircraftLightsAndGear(parts.LightsAndGear, AircraftPhase.Departed, PresentationDaylight,
                (float)journey.Progress, PresentationDeltaTime, PresentationClock, engines, null, aircraft.Type);
            UpdateCabinDoor(parts.CabinDoors, AircraftPhase.Departed, engines);
            if (_outstationInterior != null)
            {
                _outstationInterior.SetAttitude(0, 0);
                _outstationInterior.SetEnvironment(PresentationDaylight, 0, Time.unscaledTime);
                _outstationInterior.SetReadout($"GS {journey.SpeedKnots:0} kt\nHEIGHT {journey.AltitudeFeet:0} ft\nHDG {journey.Heading:000}° T");
                if (_outstationInterior is JetCockpitInterior jet) jet.SetFlightState(_outstationWatchView, engines, "Cruise");
            }
        }

        private void ExitOutstationView(bool fleet)
        {
            if (!WatchingOutstation) return;
            _outstationWatchId = null;
            _cameraController?.EndCockpit();
            if (_outstationInterior != null) { _outstationInterior.Leave(); Destroy(_outstationInterior.gameObject); }
            _outstationInterior = null;
            if (_outstationWatchView != null)
            {
                AirsideNamedChildren.Forget(_outstationWatchView);
                _aircraftViewParts.Remove(_outstationWatchView.GetInstanceID());
                Destroy(_outstationWatchView.gameObject);
            }
            _outstationWatchView = null;
            ResetFlightWorld();
            _cameraController?.ReturnToOverview();
            if (fleet) _activeWorkspace = HudWorkspace.Fleet;
        }

        private static Rect OutstationHudRect()
        {
            var scale = HudLayout.ScaleFor(Screen.width, Screen.height);
            var viewportWidth = Screen.width / scale;
            var width = Mathf.Min(720f, viewportWidth - 32f);
            return new Rect((viewportWidth - width) / 2f, 16f, width, 116f);
        }

        /// <summary>" · lands MEL RWY 16 · Gate T2-07" from the destination's airport template; empty when it has none.</summary>
        private string OutstationLandingText(OutstationAircraft aircraft, OutstationJourney journey)
        {
            var code = journey.Phase == OutstationPhase.Inbound ? aircraft.BaseCode : aircraft.DestinationCode;
            var wind = RunwayWeather.At(_operations.Clock, new SimulationTime((long)_preciseTime));
            return AirportArrivalPlanner.TryPlan(code, aircraft.Type, RouteAccess.IsInternational(code), wind,
                aircraft.Registration + "@" + aircraft.ReturnAtSeconds, null, out var arrival)
                ? " · lands " + code + " " + arrival.Text : string.Empty;
        }

        private void DrawOutstationViewHud(GUIStyle panel, GUIStyle title, GUIStyle button)
        {
            var aircraft = WatchedOutstation();
            if (aircraft == null || !OutstationJourney.TryFor(aircraft, _preciseTime, out var journey)) return;
            var box = OutstationHudRect();
            var width = box.width;
            GUI.Box(box, GUIContent.none, panel);
            GUI.Label(new Rect(box.x + 16f, box.y + 10f, width - 32f, 26f),
                aircraft.Registration + " · " + FleetStatusText.NameOf(aircraft.BaseCode) + " ↔ "
                + FleetStatusText.NameOf(aircraft.DestinationCode), title);
            GUI.Label(new Rect(box.x + 16f, box.y + 42f, width - 32f, 22f),
                $"{journey.Phase} · {journey.SpeedKnots:0} kt · {journey.AltitudeFeet:N0} ft · back { _operations.Clock.TimeText(new SimulationTime(aircraft.ReturnAtSeconds))}{OutstationLandingText(aircraft, journey)}");
            if (GUI.Button(new Rect(box.x + 16f, box.y + 76f, 116f, 28f), "FLEET / BACK", button)) ExitOutstationView(true);
            var third = Mathf.Min(130f, (width - 170f) / 3f);
            if (GUI.Button(new Rect(box.x + 146f, box.y + 76f, third, 28f), "EXTERIOR", button)) EnterOutstationView(aircraft, AircraftViewMode.Exterior);
            if (GUI.Button(new Rect(box.x + 154f + third, box.y + 76f, third, 28f), "COCKPIT", button)) EnterOutstationView(aircraft, AircraftViewMode.Cockpit);
            GUI.enabled = PassengerCabinProfile.TryFor(aircraft.Type.Id, out _);
            if (GUI.Button(new Rect(box.x + 162f + third * 2, box.y + 76f, third, 28f), "WINDOW", button)) EnterOutstationView(aircraft, AircraftViewMode.LeftWindow);
            GUI.enabled = true;
        }
    }
}
