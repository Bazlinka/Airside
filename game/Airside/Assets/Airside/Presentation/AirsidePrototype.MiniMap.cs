using System.Collections.Generic;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Corner map of the Adelaide airfield: the layout baked once into a texture, every
    /// aircraft on the field as a dot and the camera's view outlined. Click a dot to select,
    /// click or drag elsewhere to move the camera there. N toggles it.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        private const float MiniMapFarMetres = 2600f;

        private bool _miniMapVisible = true;
        private Texture2D _miniMapTexture;
        private bool _miniMapDragging;
        private bool _miniMapPressed;
        private Vector2 _miniMapPressAt;
        private readonly List<Vector2> _miniMapDots = new();
        private readonly List<string> _miniMapDotIds = new();
        private readonly Vector2[] _miniMapView = new Vector2[4];

        private static readonly Vector2[] ViewportCorners =
        {
            new(0f, 0f), new(1f, 0f), new(1f, 1f), new(0f, 1f)
        };

        private void ToggleMiniMap()
        {
            _miniMapVisible = !_miniMapVisible;
            ShowToast(_miniMapVisible ? "Airfield map on (N)." : "Airfield map off (N).");
            PlayUiClick();
        }

        private bool MiniMapShows =>
            _miniMapVisible && !(_mapOpen || _hangarOpen || _flightsOpen || _devToolsOpen || _controlsHelpOpen);

        private Texture2D MiniMapTexture()
        {
            if (_miniMapTexture != null)
                return _miniMapTexture;

            var width = Mathf.RoundToInt(FieldMiniMap.PanelWidth * FieldMiniMap.TextureScale);
            var mapArea = FieldMiniMap.FitMap(new Rect(0f, 0f, width, (FieldMiniMap.PanelHeight - FieldMiniMap.HeaderHeight) * FieldMiniMap.TextureScale));
            var w = Mathf.Max(8, Mathf.RoundToInt(mapArea.width));
            var h = Mathf.Max(8, Mathf.RoundToInt(mapArea.height));
            _miniMapTexture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "Airfield mini-map",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            _miniMapTexture.SetPixels32(FieldMiniMap.Bake(w, h));
            _miniMapTexture.Apply(false, true);
            return _miniMapTexture;
        }

        private void DrawMiniMap(Rect panelRect, GUIStyle panel, GUIStyle small)
        {
            if (!MiniMapShows || panelRect.width <= 0f || panelRect.height <= 0f)
            {
                // A press held while the map hid (N, a dialog) would otherwise come back
                // armed and turn the next field drag into a mini-map recentre.
                _miniMapPressed = _miniMapDragging = false;
                return;
            }

            GUI.Box(panelRect, GUIContent.none, panel);
            GUI.Label(new Rect(panelRect.x + 10f, panelRect.y + 2f, panelRect.width - 20f, FieldMiniMap.HeaderHeight),
                "ADELAIDE AIRFIELD  ·  N hides", small);
            var area = new Rect(panelRect.x + 6f, panelRect.y + FieldMiniMap.HeaderHeight, panelRect.width - 12f,
                panelRect.height - FieldMiniMap.HeaderHeight - 6f);
            var map = FieldMiniMap.FitMap(area);
            GUI.DrawTexture(map, MiniMapTexture(), ScaleMode.StretchToFill, true);

            DrawMiniMapView(map);
            DrawMiniMapAircraft(map);
            HandleMiniMapPointer(map);
        }

        private void DrawMiniMapView(Rect map)
        {
            if (_mainCamera == null)
                return;

            for (var i = 0; i < 4; i++)
            {
                var ray = _mainCamera.ViewportPointToRay(new Vector3(ViewportCorners[i].x, ViewportCorners[i].y, 0f));
                var ground = FieldMiniMap.GroundPoint(ray.origin, ray.direction, MiniMapFarMetres);
                _miniMapView[i] = ClampToRect(FieldMiniMap.WorldToMap(map, ground.x, ground.y), map);
            }

            var outline = new Color(AirsideTheme.Cloud.r, AirsideTheme.Cloud.g, AirsideTheme.Cloud.b, 0.8f);
            for (var i = 0; i < 4; i++)
                DrawLine(_miniMapView[i], _miniMapView[(i + 1) % 4], outline, 1.2f);

            if (_cameraController != null)
            {
                var focus = _cameraController.FocusPoint;
                var centre = ClampToRect(FieldMiniMap.WorldToMap(map, focus.x, focus.z), map);
                DrawSolid(new Rect(centre.x - 2f, centre.y - 2f, 4f, 4f), AirsideTheme.SafetyYellow);
            }
        }

        private void DrawMiniMapAircraft(Rect map)
        {
            _miniMapDots.Clear();
            _miniMapDotIds.Clear();
            foreach (var pair in _fleetViewById)
            {
                var view = pair.Value;
                if (view == null || !view.gameObject.activeInHierarchy || !_fleetAircraftById.TryGetValue(pair.Key, out var aircraft))
                    continue;

                var point = FieldMiniMap.WorldToMap(map, view.position.x, view.position.z);
                if (!map.Contains(point))
                    continue;
                _miniMapDots.Add(point);
                _miniMapDotIds.Add(pair.Key);

                var mine = aircraft.Airline.IsPlayer;
                var selected = pair.Key == _selectedAircraftId;
                var livery = AirsideTheme.FromHex(aircraft.Airline.LiveryHex);
                var size = mine || selected ? 8f : 6f;
                var severity = AircraftStatus.Severity(aircraft, _clock.Now);
                var ring = selected
                    ? AirsideTheme.SafetyYellow
                    : mine ? SeverityColour(severity, AirsideTheme.Cloud) : AirsideTheme.RunwayInk;
                DrawSolid(new Rect(point.x - size * 0.5f - 1.5f, point.y - size * 0.5f - 1.5f, size + 3f, size + 3f), ring);
                var fill = mine || selected ? livery : new Color(livery.r, livery.g, livery.b, Ownership.OtherAlpha + 0.2f);
                DrawSolid(new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size), fill);
            }
        }

        private void HandleMiniMapPointer(Rect map)
        {
            var ev = Event.current;
            if (ev == null)
                return;

            switch (ev.type)
            {
                // A release outside the window never reaches OnGUI. Without this, a later
                // left-drag on the field kept recentring the camera through the map.
                case EventType.MouseDown when !map.Contains(ev.mousePosition):
                    _miniMapPressed = _miniMapDragging = false;
                    break;

                case EventType.MouseDown when ev.button == 0 && map.Contains(ev.mousePosition):
                    _miniMapPressed = true;
                    _miniMapDragging = false;
                    _miniMapPressAt = ev.mousePosition;
                    ev.Use();
                    break;

                case EventType.MouseDrag when _miniMapPressed:
                    if (!_miniMapDragging && (ev.mousePosition - _miniMapPressAt).sqrMagnitude > AircraftPickRouting.DragThresholdPixels * AircraftPickRouting.DragThresholdPixels)
                        _miniMapDragging = true;
                    if (_miniMapDragging)
                        CentreCameraOnMiniMap(map, ev.mousePosition);
                    ev.Use();
                    break;

                case EventType.MouseUp when _miniMapPressed:
                    if (!_miniMapDragging)
                    {
                        var hit = FieldMiniMap.NearestDot(_miniMapDots, ev.mousePosition);
                        if (hit >= 0 && _fleetAircraftById.TryGetValue(_miniMapDotIds[hit], out var aircraft))
                            SelectAircraft(aircraft);
                        else
                            CentreCameraOnMiniMap(map, ev.mousePosition);
                        PlayUiClick();
                    }

                    _miniMapPressed = _miniMapDragging = false;
                    ev.Use();
                    break;
            }
        }

        private void CentreCameraOnMiniMap(Rect map, Vector2 point)
        {
            if (_cameraController == null)
                return;
            var ground = FieldMiniMap.MapToWorld(map, point);
            _cameraController.CentreOn(ground.x, ground.y);
        }

        private static Vector2 ClampToRect(Vector2 point, Rect rect) =>
            new(Mathf.Clamp(point.x, rect.xMin, rect.xMax), Mathf.Clamp(point.y, rect.yMin, rect.yMax));
    }
}
