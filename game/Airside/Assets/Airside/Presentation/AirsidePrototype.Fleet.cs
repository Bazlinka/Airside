using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// The unified Fleet workspace's runtime (ADR 0239): what the roster filters and sort ask for, selecting
    /// inside the sheet, selling with a confirm, moving a base, sending outstation aircraft on routes, buying
    /// for a chosen base, and the camera buttons. The pure model, layout and painter are in
    /// <see cref="FleetWorkspaceModel"/>; every command still goes through <see cref="AirlineOperations"/>.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        private readonly FleetBoardState _fleetBoard = new();
        private float _sellArmedUntil;
        private string _sellArmedRegistration;

        /// <summary>
        /// Clicks on the Fleet sheet. Selecting a row stays inside the sheet (it used to close it, so the
        /// profile beside the roster could only ever show the first aircraft); everything else that is not the
        /// Fleet's own falls through to the shared dispatcher.
        /// </summary>
        private void DispatchFleetAction(string action)
        {
            if (string.IsNullOrEmpty(action))
                return;

            var registration = HudAction.Payload(action, HudAction.SelectPrefix);
            if (registration.Length > 0)
            {
                _selectedAircraftId = registration;
                _fleetBoard.RoutePage = 0;
                _sellArmedRegistration = null;
                PlayUiClick();
                return;
            }

            var baseCode = HudAction.Payload(action, FleetActions.BasePrefix);
            if (baseCode.Length > 0)
            {
                _fleetBoard.ToggleBase(baseCode);
                _rosterScrollRow = 0;
                _fleetMarketStart = 0;
                PlayUiClick();
                return;
            }

            var openCode = HudAction.Payload(action, FleetActions.OpenBasePrefix);
            if (openCode.Length > 0)
            {
                OpenBaseFromHud(openCode);
                return;
            }

            var routeCode = HudAction.Payload(action, FleetActions.RoutePrefix);
            if (routeCode.Length > 0)
            {
                SendOutstationAircraft(routeCode);
                return;
            }

            var repeatCode = HudAction.Payload(action, FleetActions.RepeatPrefix);
            if (repeatCode.Length > 0)
            {
                ToggleOutstationRepeat(repeatCode);
                return;
            }

            switch (action)
            {
                case FleetActions.CycleStatus:
                    _fleetBoard.CycleStatus();
                    _rosterScrollRow = 0;
                    PlayUiClick();
                    return;
                case FleetActions.CycleSort:
                    _fleetBoard.CycleSort();
                    _rosterScrollRow = 0;
                    PlayUiClick();
                    return;
                case FleetActions.RoutesPrevious:
                    _fleetBoard.RoutePage = Math.Max(0, _fleetBoard.RoutePage - 1);
                    PlayUiClick();
                    return;
                case FleetActions.RoutesNext:
                    _fleetBoard.RoutePage = Math.Min(Math.Max(0, _fleetWorkspace.RoutePageCount - 1),
                        _fleetBoard.RoutePage + 1);
                    PlayUiClick();
                    return;
                case FleetActions.Sell:
                    SellSelectedAircraft();
                    return;
                case FleetActions.MoveBase:
                    MoveSelectedToAdelaide();
                    return;
                case FleetActions.OutstationCheck:
                {
                    var result = _operations.StartOutstationCheck(_selectedAircraftId);
                    ShowToast(result.Accepted ? "Check started." : result.Reason);
                    if (result.Accepted) SaveAirline();
                    PlayUiClick();
                    return;
                }
                case FleetActions.RemoveRepeat:
                {
                    var result = _operations.RemoveRepeatSchedule(_selectedAircraftId);
                    ShowToast(result.Accepted ? "Repeat schedule removed." : result.Reason);
                    if (result.Accepted) SaveAirline();
                    PlayUiClick();
                    return;
                }
                case HudAction.CameraCockpit:
                case HudAction.CameraPassenger:
                case HudAction.CameraExterior:
                    EnterViewFromFleet(action);
                    return;
            }

            DispatchWorkspaceAction(action);
        }

        /// <summary>Which of the three views the Fleet profile may offer for the selected aircraft, asked of the live scene.</summary>
        private void FillFleetCameras()
        {
            var camera = _fleetWorkspace.Camera;
            camera.Clear();
            if (_fleetWorkspace.SelectedIsOutstation || !_fleetWorkspace.SelectedIsPlayer
                || !TryFindFleetAircraft(_fleetWorkspace.SelectedRegistration, out var aircraft))
                return;
            camera.Visible = true;
            camera.Hint = CockpitReason(aircraft);
            camera.Cockpit = camera.Hint.Length == 0;
            camera.Window = FlightViewReason(aircraft, AircraftViewMode.LeftWindow).Length == 0;
            camera.Exterior = FlightViewReason(aircraft, AircraftViewMode.Exterior).Length == 0;
        }

        private void EnterViewFromFleet(string action)
        {
            if (!TryFindFleetAircraft(_selectedAircraftId, out var aircraft))
                return;
            var mode = action == HudAction.CameraCockpit ? AircraftViewMode.Cockpit
                : action == HudAction.CameraPassenger ? AircraftViewMode.LeftWindow
                : AircraftViewMode.Exterior;
            var reason = FlightViewReason(aircraft, mode);
            if (reason.Length > 0)
            {
                ShowToast(reason, HudTone.Caution);
                PlayUiClick();
                return;
            }

            // The sheet covers the field; leave it as the card's own buttons do by being in the overview.
            _activeWorkspace = HudWorkspace.None;
            EnterFlightView(aircraft, mode);
        }

        private void OpenBaseFromHud(string code)
        {
            var result = _operations.OpenOutstationBase(code);
            ShowToast(result.Accepted ? "Your " + FleetStatusText.NameOf(code) + " base is open." : result.Reason);
            if (result.Accepted)
            {
                _fleetBoard.BaseFilter = code;
                _rosterScrollRow = 0;
                _fleetMarketStart = 0;
                SaveAirline();
            }

            PlayUiClick();
        }

        /// <summary>Two clicks within five seconds, so a stray click never sells an aircraft.</summary>
        private void SellSelectedAircraft()
        {
            var registration = _selectedAircraftId;
            if (string.IsNullOrEmpty(registration))
                return;
            var entry = Airside.Simulation.PlayerFleet.Find(_operations, _clock.Now, registration);
            if (entry == null)
                return;

            if (_sellArmedRegistration != registration || Time.unscaledTime > _sellArmedUntil)
            {
                _sellArmedRegistration = registration;
                _sellArmedUntil = Time.unscaledTime + 5f;
                ShowToast($"Click SELL again to sell {registration} for ${entry.ResaleValue:N0}.", HudTone.Caution);
                PlayUiClick();
                return;
            }

            _sellArmedRegistration = null;
            var result = entry.IsOutstation
                ? _operations.SellOutstationAircraft(registration)
                : _operations.SellAircraft(entry.Live);
            if (result.Accepted)
            {
                // Anything still pointing at the aircraft would be left holding a registration that is gone.
                if (_mapAircraft != null && _mapAircraft.Registration == registration)
                    _mapAircraft = null;
                if (_mapFlightInspectorId == registration)
                    _mapFlightInspectorId = null;
                _selectedAircraftId = null;
                ShowToast($"Sold {registration} for ${entry.ResaleValue:N0}.", HudTone.Accent);
                SaveAirline();
            }
            else
                ShowToast(result.Reason, HudTone.Negative);

            PlayUiClick();
        }

        private void MoveSelectedToAdelaide()
        {
            var registration = _selectedAircraftId;
            var cost = _operations.OutstationFleet.Where(a => a.Registration == registration)
                .Select(a => _operations.RelocationCost(a)).FirstOrDefault();
            var result = _operations.RelocateToAdelaide(registration);
            if (result.Accepted)
            {
                ShowToast($"{registration} is flying to Adelaide. The move cost ${cost:N0}. "
                          + "It joins your Adelaide fleet when it parks.", HudTone.Accent);
                SaveAirline();
            }
            else
                ShowToast(result.Reason, HudTone.Negative);

            PlayUiClick();
        }

        private void SendOutstationAircraft(string destinationCode)
        {
            var result = _operations.ScheduleOutstationService(_selectedAircraftId, destinationCode,
                _clock.Now.Advance(30 * 60));
            ShowToast(result.Accepted
                ? $"{_selectedAircraftId} will leave for {FleetStatusText.NameOf(destinationCode)} in about 30 minutes."
                : result.Reason);
            if (result.Accepted) SaveAirline();
            PlayUiClick();
        }

        private void ToggleOutstationRepeat(string destinationCode)
        {
            // Pause or resume only the plan for this route; another route replaces it.
            var existing = _operations.RepeatSchedules.FirstOrDefault(p =>
                p.Registration == _selectedAircraftId && p.DestinationCode == destinationCode);
            var result = existing == null
                ? _operations.SetRepeatSchedule(_selectedAircraftId, destinationCode, 12)
                : _operations.PauseRepeatSchedule(_selectedAircraftId, !existing.Paused);
            ShowToast(result.Accepted ? "Repeat schedule updated." : result.Reason);
            if (result.Accepted) SaveAirline();
            PlayUiClick();
        }

        // Aircraft based away from Adelaide have no 3D view, so the map is where they are seen (ADR 0239): parked at
        // their base city, or along the great circle while on a service. Separate hit lists, because the live
        // aircraft's lists hold FleetAircraft and these are not.
        private readonly List<Vector2> _mapOutstationPoints = new();
        private readonly List<string> _mapOutstationIds = new();

        private void DrawOutstationMarkers(Rect mapRect, GUIStyle small, Color ink)
        {
            _mapOutstationPoints.Clear();
            _mapOutstationIds.Clear();
            if (_operations == null)
                return;

            var mouse = Event.current.mousePosition;
            foreach (var aircraft in _operations.OutstationFleet)
            {
                if (!Airside.Simulation.PlayerFleet.TryPosition(aircraft, _clock.Now, out var lat, out var lon,
                        out var phase, out _))
                    continue;
                var point = Project(mapRect, lon, lat);
                if (!mapRect.Contains(point))
                    continue;
                // Aircraft parked in the same city sit side by side instead of on top of each other.
                while (MarkerNear(point))
                    point.x += 12f;

                var heading = 0f;
                if (phase == OutstationPhase.Outbound && DestinationCatalogue.TryFind(aircraft.DestinationCode, out var far))
                    heading = (float)FlightRoute.HeadingDegrees(lat, lon, far.Latitude, far.Longitude);
                else if (phase == OutstationPhase.Inbound && DestinationCatalogue.TryFind(aircraft.BaseCode, out var home))
                    heading = (float)FlightRoute.HeadingDegrees(lat, lon, home.Latitude, home.Longitude);

                var selected = aircraft.Registration == _selectedAircraftId;
                var size = Mathf.Lerp(18f, 30f, Mathf.InverseLerp(1f, 12f, _mapLens.Zoom)) * 1.15f;
                if (selected)
                    AirsideTheme.DrawRounded(new Rect(point.x - size * .7f, point.y - size * .7f, size * 1.4f, size * 1.4f),
                        AirsideTheme.WithAlpha(AirsideTheme.Aqua, .25f), size);
                DrawPlaneIcon(point, size + 3f, heading, new Color(0f, 0f, 0f, 0.75f));
                var colour = Color.Lerp(AirsideTheme.FromHex(_operations.PlayerAirline.LiveryHex),
                    AirsideTheme.InstrumentText, .35f);
                DrawPlaneIcon(point, size, heading, colour);
                _mapOutstationPoints.Add(point);
                _mapOutstationIds.Add(aircraft.Registration);

                if (!selected && (mouse - point).sqrMagnitude > 18f * 18f)
                    continue;
                var labelRect = new Rect(Mathf.Min(point.x + size * 0.6f, mapRect.xMax - 244f),
                    Mathf.Min(point.y - 8f, mapRect.yMax - 40f), 240f, 36f);
                AirsideTheme.DrawRounded(labelRect, new Color(ink.r, ink.g, ink.b, 0.8f), 5f);
                GUI.Label(new Rect(labelRect.x + 4f, labelRect.y + 1f, labelRect.width - 8f, 18f),
                    $"{aircraft.Registration} · {aircraft.BaseCode}", small);
                GUI.Label(new Rect(labelRect.x + 4f, labelRect.y + 17f, labelRect.width - 8f, 18f),
                    $"{aircraft.Type.Name} · {FleetStatusText.ForOutstation(Airside.Simulation.PlayerFleet.Find(_operations, _clock.Now, aircraft.Registration), _clock.Now, _operations.Clock)}",
                    small);
            }
        }

        private bool MarkerNear(Vector2 point)
        {
            for (var i = 0; i < _mapOutstationPoints.Count; i++)
                if ((_mapOutstationPoints[i] - point).sqrMagnitude < 6f * 6f)
                    return true;
            return false;
        }

        /// <summary>A click on an outstation aircraft's marker opens its profile in Fleet.</summary>
        private bool TryPickOutstationMarker(Vector2 mouse)
        {
            var best = -1;
            var bestDistance = 18f * 18f;
            for (var i = 0; i < _mapOutstationPoints.Count; i++)
            {
                var distance = (_mapOutstationPoints[i] - mouse).sqrMagnitude;
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            if (best < 0)
                return false;
            _selectedAircraftId = _mapOutstationIds[best];
            _fleetBoard.RoutePage = 0;
            _mapFlightInspectorId = null;
            _activeWorkspace = HudWorkspace.Fleet;
            PlayUiClick();
            return true;
        }

        /// <summary>BUY for a base other than Adelaide: the aircraft is delivered there and flies from there.</summary>
        private void BuyAtOutstationFromHud(AircraftType type, string baseCode)
        {
            var result = _operations.BuyAircraftAtOutstation(type, baseCode);
            if (result.Accepted)
            {
                var bought = _operations.OutstationFleet[_operations.OutstationFleet.Count - 1];
                _selectedAircraftId = bought.Registration;
                var priceBit = AircraftAcquisition.TryFor(type, out var offer) ? $" for ${offer.Price:N0}" : string.Empty;
                ShowToast($"Bought {Article.A(type.Name)}{priceBit}, based at {FleetStatusText.NameOf(baseCode)}. "
                          + "Pick its first route in the profile.");
                SaveAirline();
            }
            else
                ShowToast(result.Reason);

            PlayUiClick();
        }
    }
}
