using System;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Moving map for the flight views (cockpit, windows, exterior): the watched aircraft stays centred
    /// with its heading and ground track, the route to fly, the airports around it and the rest of the
    /// fleet. On and near the field it shows the baked Adelaide airfield (runway frame, with a north
    /// mark); away from it, the South Australian coast (north up) with the great-circle route.
    /// N toggles it, the buttons or the scroll wheel change scale, A returns to automatic scale.
    /// Presentation only: it reads the simulation and never changes it.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        private bool _flightMapVisible = true;
        private int _flightMapRange = CockpitMovingMap.Auto;
        private Texture2D _flightMapField;
        private Texture2D _flightMapCoast;
        private GUIStyle _flightMapLabel;

        private const string FlightMapZoomIn = "flightmap:in";
        private const string FlightMapZoomOut = "flightmap:out";
        private const string FlightMapAuto = "flightmap:auto";
        private const string FlightMapShow = "flightmap:show";
        private const string FlightMapHide = "flightmap:hide";
        private const float FlightMapHeader = 32f, FlightMapFooter = 44f;

        private void DisposeFlightMap()
        {
            if (_flightMapField != null) Destroy(_flightMapField);
            if (_flightMapCoast != null) Destroy(_flightMapCoast);
        }

        private Texture2D FlightMapFieldTexture()
        {
            if (_flightMapField != null) return _flightMapField;
            FieldMiniMap.WorldBounds(out var minX, out var maxX, out var minZ, out var maxZ);
            const int width = 1280;
            var height = Mathf.Max(8, Mathf.RoundToInt(width * (maxZ - minZ) / (maxX - minX)));
            _flightMapField = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Flight map airfield", filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave
            };
            _flightMapField.SetPixels32(FieldMiniMap.Bake(width, height));
            _flightMapField.Apply(false, true);
            return _flightMapField;
        }

        private Texture2D FlightMapCoastTexture()
        {
            if (_flightMapCoast != null) return _flightMapCoast;
            const int size = 1536;
            _flightMapCoast = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Flight map coast", filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave
            };
            _flightMapCoast.SetPixels32(RegionalMiniMap.Bake(size, size));
            _flightMapCoast.Apply(false, true);
            return _flightMapCoast;
        }

        private void ToggleFlightMap()
        {
            _flightMapVisible = !_flightMapVisible;
            ShowToast(_flightMapVisible ? "Moving map on (N)." : "Moving map off (N).");
            PlayUiClick();
        }

        private void StepFlightMapRange(int delta, int currentAuto)
        {
            var start = _flightMapRange == CockpitMovingMap.Auto ? currentAuto : _flightMapRange;
            _flightMapRange = CockpitMovingMap.Clamp(start + delta);
        }

        private static HudBox FlightMapBox(HudLayout layout, FlightViewHudLayout placement)
        {
            var size = Mathf.Clamp(layout.Viewport.y * 0.30f, 200f, 320f);
            var width = size + 16f;
            var height = FlightMapHeader + size + FlightMapFooter + 8f;
            var x = layout.Viewport.x - width - 20f;
            var y = layout.Viewport.y - height - 20f;
            // Never sit on top of the viewing dock on a narrow window; go above it instead.
            var dock = placement.Controls;
            if (x < dock.Right + 8f && y + height > dock.Y)
                y = Mathf.Max(placement.Identity.Bottom + 8f, dock.Y - height - 12f);
            return new HudBox(x, y, width, height);
        }

        private void DrawFlightMap(HudLayout layout, FlightViewHudLayout placement, FleetAircraft aircraft)
        {
            var box = FlightMapBox(layout, placement);
            if (!_flightMapVisible)
            {
                var tab = new HudBox(layout.Viewport.x - 128f, layout.Viewport.y - 56f, 108f, 32f);
                _hudPanels.Add(HudPainter.ToRect(tab));
                _flightViewDrawList.Clear();
                _flightViewDrawList.Button(tab, "Map · N", FlightMapShow, HudButtonStyle.Secondary);
                if (_hudPainter.Draw(_flightViewDrawList) == FlightMapShow) { _flightMapVisible = true; PlayUiClick(); }
                return;
            }

            _hudPanels.Add(HudPainter.ToRect(box));
            var view = _cockpitView;
            YpadFrame.ToLatLon(view.position.x + _flightOriginX, view.position.z + _flightOriginZ, out var lat, out var lon);
            var heading = FlightViewInformation.TrueHeading(view.forward.x, view.forward.z);

            // Route: home to destination outbound, destination to home on the way back (as the flight card).
            var home = _operations.Home;
            var target = aircraft.CurrentDestination;
            if (aircraft.State == FleetState.AtStand)
                target = aircraft.Scheduled.HasValue && !aircraft.Scheduled.Value.Cancelled
                    ? aircraft.Scheduled.Value.Destination : null;
            var returning = aircraft.State is FleetState.Inbound or FleetState.HoldingForLanding
                or FleetState.GoAround or FleetState.Landing or FleetState.AwaitingStand or FleetState.TaxiIn;
            var legFrom = returning && target.HasValue ? target.Value : home;
            var legTo = returning || !target.HasValue ? home : target.Value;
            var position = new Destination("POS", "Aircraft", "", lat, lon);
            var kmHome = position.DistanceKmTo(home);
            var kmTarget = position.DistanceKmTo(legTo);
            var legKm = legFrom.DistanceKmTo(legTo);
            var onGround = _cockpitGearHeight < 60f;
            var autoRange = CockpitMovingMap.AutoRange(kmHome, kmTarget, legKm, onGround);
            var rangeIndex = _flightMapRange == CockpitMovingMap.Auto ? autoRange : _flightMapRange;
            var rangeKm = CockpitMovingMap.RangesKm[rangeIndex];

            var size = box.Width - 16f;
            var mapRect = new Rect(box.X + 8f, box.Y + FlightMapHeader, size, size);
            var panel = new HudBox(box.X, box.Y, box.Width, box.Height);
            _flightViewDrawList.Clear();
            _flightViewDrawList.Surface(panel);
            _flightViewDrawList.Text(new HudBox(box.X + 12f, box.Y + 8f, 110f, 18f), "MOVING MAP", 11f, HudTone.Accent, style: HudTextStyle.Bold);
            _flightViewDrawList.Button(new HudBox(box.Right - 118f, box.Y + 4f, 26f, 24f), "−", FlightMapZoomOut, HudButtonStyle.Secondary);
            _flightViewDrawList.Button(new HudBox(box.Right - 90f, box.Y + 4f, 26f, 24f), "+", FlightMapZoomIn, HudButtonStyle.Secondary);
            _flightViewDrawList.Button(new HudBox(box.Right - 62f, box.Y + 4f, 26f, 24f), "A", FlightMapAuto, HudButtonStyle.Secondary);
            _flightViewDrawList.Button(new HudBox(box.Right - 34f, box.Y + 4f, 26f, 24f), "×", FlightMapHide, HudButtonStyle.Secondary);
            var footerY = mapRect.yMax + 4f;
            var routeText = legTo.Code == home.Code && legFrom.Code == home.Code ? "At " + home.Code : legFrom.Code + " → " + legTo.Code;
            _flightViewDrawList.Text(new HudBox(box.X + 12f, footerY, box.Width - 24f, 18f),
                routeText + "   " + CockpitMovingMap.DistanceText("TO " + legTo.Code, kmTarget)
                + (_flightViewHud.Arrival != "—" ? "   ETE " + _flightViewHud.Arrival : ""), 11f, HudTone.Default);
            _flightViewDrawList.Text(new HudBox(box.X + 12f, footerY + 18f, box.Width - 24f, 18f),
                "HDG " + ((int)Mathf.Round(heading) % 360).ToString("000") + "° T   GS " + _flightViewHud.Speed
                + "   " + Math.Abs(lat).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + (lat < 0 ? "°S " : "°N ")
                + Math.Abs(lon).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + (lon < 0 ? "°W" : "°E"), 10f, HudTone.Muted);
            var action = _hudPainter.Draw(_flightViewDrawList);
            if (action == FlightMapZoomIn) { StepFlightMapRange(-1, autoRange); PlayUiClick(); }
            else if (action == FlightMapZoomOut) { StepFlightMapRange(1, autoRange); PlayUiClick(); }
            else if (action == FlightMapAuto) { _flightMapRange = CockpitMovingMap.Auto; PlayUiClick(); }
            else if (action == FlightMapHide) { _flightMapVisible = false; PlayUiClick(); }

            var ev = Event.current;
            if (ev != null && ev.type == EventType.ScrollWheel && mapRect.Contains(ev.mousePosition))
            {
                StepFlightMapRange(ev.delta.y > 0f ? 1 : -1, autoRange);
                ev.Use();
            }

            DrawFlightMapContent(mapRect, aircraft, lat, lon, heading, view, rangeKm, legFrom, legTo, legKm);
        }

        private void DrawFlightMapContent(Rect rect, FleetAircraft aircraft, double lat, double lon, float heading,
            Transform view, float rangeKm, Destination legFrom, Destination legTo, double legKm)
        {
            if (Event.current != null && Event.current.type != EventType.Repaint)
                return;
            _flightMapLabel ??= new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Overflow };
            var half = rect.width * 0.5f;
            var centre = new Vector2(half, half);
            var worldX = (float)(view.position.x + _flightOriginX);
            var worldZ = (float)(view.position.z + _flightOriginZ);
            FieldMiniMap.WorldBounds(out var minX, out var maxX, out var minZ, out var maxZ);
            var fieldMode = rangeKm <= CockpitMovingMap.FieldDetailMaxKm
                && worldX > minX - 2000f && worldX < maxX + 2000f && worldZ > minZ - 2000f && worldZ < maxZ + 2000f;
            var pxPerDegLat = CockpitMovingMap.PixelsPerDegreeLatitude(half, rangeKm);

            GUI.BeginGroup(rect);
            AirsideTheme.DrawRounded(new Rect(0f, 0f, rect.width, rect.height),
                fieldMode ? (Color)FieldMiniMap.Grass : (Color)FieldMiniMap.Water, 8f);
            Vector2 ProjectLatLon(double la, double lo)
            {
                CockpitMovingMap.Offset(lat, lon, la, lo, pxPerDegLat, out var dx, out var dy);
                return centre + new Vector2((float)dx, (float)dy);
            }

            Vector2 northScreen = new Vector2(0f, -1f);
            Vector2 headingScreen;
            Rect fieldRect = default;
            if (fieldMode)
            {
                var pxPerMetre = half / (rangeKm * 1000f);
                var size = new Vector2((maxX - minX) * pxPerMetre, (maxZ - minZ) * pxPerMetre);
                var own = FieldMiniMap.WorldToMap(new Rect(0f, 0f, size.x, size.y), worldX, worldZ);
                fieldRect = new Rect(centre.x - own.x, centre.y - own.y, size.x, size.y);
                GUI.DrawTexture(fieldRect, FlightMapFieldTexture(), ScaleMode.StretchToFill, true);
                YpadFrame.FromEastNorth(0, 1, out var nx, out var nz);
                northScreen = new Vector2((float)nx, (float)-nz).normalized;
                var flat = Vector3.ProjectOnPlane(view.forward, Vector3.up);
                headingScreen = new Vector2(flat.x, -flat.z).normalized;
            }
            else
            {
                var texture = FlightMapCoastTexture();
                var topLeft = ProjectLatLon(RegionalMiniMap.North, RegionalMiniMap.West);
                var bottomRight = ProjectLatLon(RegionalMiniMap.South, RegionalMiniMap.East);
                GUI.DrawTexture(new Rect(topLeft.x, topLeft.y, bottomRight.x - topLeft.x, bottomRight.y - topLeft.y),
                    texture, ScaleMode.StretchToFill, true);
                CockpitMovingMap.HeadingDirection(heading, out var hx, out var hy);
                headingScreen = new Vector2((float)hx, (float)hy);
            }

            // Range rings at half and full range.
            var ringColour = AirsideTheme.WithAlpha(AirsideTheme.InstrumentText, 0.16f);
            DrawMapCircle(centre, half * 0.5f, ringColour);
            DrawMapCircle(centre, half * 0.98f, ringColour);
            _flightMapLabel.normal.textColor = AirsideTheme.WithAlpha(AirsideTheme.InstrumentText, 0.55f);
            GUI.Label(new Rect(centre.x + 4f, centre.y - half * 0.5f - 7f, 60f, 14f), CockpitMovingMap.RangeLabel(rangeKm * 0.5f), _flightMapLabel);

            // Route and airports (coast map; the airfield view is too close for either to mean much).
            if (!fieldMode)
            {
                if (legKm > 5.0)
                {
                    var progress = _flightViewHud.JourneyProgress;
                    var previous = ProjectLatLon(legFrom.Latitude, legFrom.Longitude);
                    const int steps = 64;
                    for (var i = 1; i <= steps; i++)
                    {
                        RouteMap.GreatCirclePoint(legFrom.Latitude, legFrom.Longitude, legTo.Latitude, legTo.Longitude, i / (double)steps, out var la, out var lo);
                        var next = ProjectLatLon(la, lo);
                        var flown = progress >= 0f && i / (float)steps <= progress;
                        DrawLine(previous, next, flown ? AirsideTheme.WithAlpha(AirsideTheme.Aqua, 0.35f)
                            : AirsideTheme.WithAlpha(AirsideTheme.Amber, 0.9f), flown ? 1.2f : 1.8f);
                        previous = next;
                    }
                }

                foreach (var airport in DestinationCatalogue.All)
                {
                    var p = ProjectLatLon(airport.Latitude, airport.Longitude);
                    if (p.x < -4f || p.y < -4f || p.x > rect.width + 4f || p.y > rect.height + 4f) continue;
                    var key = airport.Code == legFrom.Code || airport.Code == legTo.Code;
                    var colour = key ? AirsideTheme.Amber : AirsideTheme.WithAlpha(AirsideTheme.InstrumentText, 0.7f);
                    AirsideTheme.DrawRounded(new Rect(p.x - 2.5f, p.y - 2.5f, 5f, 5f), colour, 3f);
                    if (key || rangeKm <= 250f)
                    {
                        _flightMapLabel.normal.textColor = colour;
                        GUI.Label(new Rect(p.x + 5f, p.y - 7f, 40f, 14f), airport.Code, _flightMapLabel);
                    }
                }
            }

            // The rest of the fleet.
            foreach (var pair in _fleetAircraftById)
            {
                if (pair.Key == aircraft.Registration) continue;
                var other = pair.Value;
                Vector2 point;
                if (fieldMode)
                {
                    if (!_fleetViewById.TryGetValue(pair.Key, out var otherView) || otherView == null || !otherView.gameObject.activeInHierarchy)
                        continue;
                    point = fieldRect.position + FieldMiniMap.WorldToMap(new Rect(0f, 0f, fieldRect.width, fieldRect.height),
                        otherView.position.x + (float)_flightOriginX, otherView.position.z + (float)_flightOriginZ);
                }
                else
                {
                    if (!TryMiniMapLocation(other, out var la, out var lo)) continue;
                    point = ProjectLatLon(la, lo);
                }

                if (point.x < 0f || point.y < 0f || point.x > rect.width || point.y > rect.height) continue;
                var livery = AirsideTheme.FromHex(other.Airline.LiveryHex);
                var s = other.Airline.IsPlayer ? 6f : 4f;
                AirsideTheme.DrawRounded(new Rect(point.x - s * 0.5f - 1f, point.y - s * 0.5f - 1f, s + 2f, s + 2f),
                    AirsideTheme.WithAlpha(AirsideTheme.Glass, 0.9f), s);
                AirsideTheme.DrawRounded(new Rect(point.x - s * 0.5f, point.y - s * 0.5f, s, s), livery, s);
            }

            // Own aircraft: ground track ahead, then the chevron.
            DrawLine(centre, centre + headingScreen * half * 0.9f, AirsideTheme.WithAlpha(AirsideTheme.Aqua, 0.45f), 1.2f);
            var perp = new Vector2(-headingScreen.y, headingScreen.x);
            var tip = centre + headingScreen * 10f;
            var left = centre - headingScreen * 7f + perp * 6f;
            var right = centre - headingScreen * 7f - perp * 6f;
            var notch = centre - headingScreen * 3f;
            var ship = AirsideTheme.Aqua;
            DrawLine(tip, left, ship, 2.2f); DrawLine(left, notch, ship, 2.2f);
            DrawLine(notch, right, ship, 2.2f); DrawLine(right, tip, ship, 2.2f);

            _flightMapLabel.normal.textColor = AirsideTheme.WithAlpha(AirsideTheme.InstrumentText, 0.7f);
            GUI.Label(new Rect(8f, rect.height - 20f, 120f, 14f),
                (_flightMapRange == CockpitMovingMap.Auto ? "AUTO  " : "") + CockpitMovingMap.RangeLabel(rangeKm)
                + (fieldMode ? "  ·  RWY-UP" : "  ·  N-UP"), _flightMapLabel);

            // North mark.
            var np = new Vector2(rect.width - 18f, 18f);
            DrawLine(np, np + northScreen * 9f, AirsideTheme.WithAlpha(AirsideTheme.InstrumentText, 0.8f), 1.5f);
            _flightMapLabel.normal.textColor = AirsideTheme.WithAlpha(AirsideTheme.InstrumentText, 0.8f);
            GUI.Label(new Rect(np.x + northScreen.x * 16f - 4f, np.y + northScreen.y * 16f - 7f, 12f, 14f), "N", _flightMapLabel);
            GUI.EndGroup();
        }

        private static void DrawMapCircle(Vector2 centre, float radius, Color colour)
        {
            const int segments = 48;
            var previous = centre + new Vector2(radius, 0f);
            for (var i = 1; i <= segments; i++)
            {
                var a = i * Mathf.PI * 2f / segments;
                var next = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                DrawLine(previous, next, colour, 1f);
                previous = next;
            }
        }
    }
}
