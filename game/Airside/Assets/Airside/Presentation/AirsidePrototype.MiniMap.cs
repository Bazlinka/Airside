using System.Collections.Generic;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Corner map of the Adelaide airfield: the layout baked once into a texture, every
    /// aircraft on the field as a dot and the camera's heading marked. Click a dot to select,
    /// click or drag elsewhere to move the camera there. N toggles it.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        private bool _miniMapVisible = true;
        private Texture2D _miniMapTexture;
        private bool _miniMapDragging;
        private bool _miniMapPressed;
        private Vector2 _miniMapPressAt;
        private readonly List<Vector2> _miniMapDots = new();
        private readonly List<string> _miniMapDotIds = new();

        private void ToggleMiniMap()
        {
            _miniMapVisible = !_miniMapVisible;
            ShowToast(_miniMapVisible ? "Airfield map on (N)." : "Airfield map off (N).");
            PlayUiClick();
        }

        private bool MiniMapShows =>
            _miniMapVisible && !(_activeWorkspace != HudWorkspace.None || _devToolsOpen || _controlsHelpOpen);

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
            _ = panel;
            if (!MiniMapShows || panelRect.width <= 0f || panelRect.height <= 0f)
            {
                // A press held while the map hid (N, a dialog) would otherwise come back
                // armed and turn the next field drag into a mini-map recentre.
                _miniMapPressed = _miniMapDragging = false;
                return;
            }

            GUI.Label(new Rect(panelRect.x + 10f, panelRect.y + 2f, panelRect.width - 20f, FieldMiniMap.HeaderHeight),
                "ADELAIDE AIRFIELD", small);
            var area = new Rect(panelRect.x + 6f, panelRect.y + FieldMiniMap.HeaderHeight, panelRect.width - 12f,
                panelRect.height - FieldMiniMap.HeaderHeight - 6f);
            var map = FieldMiniMap.FitMap(area);
            GUI.DrawTexture(map, MiniMapTexture(), ScaleMode.StretchToFill, true);
            DrawMiniMapRunwayNames(map, small);

            DrawMiniMapView(map);
            DrawMiniMapAircraft(map);
            HandleMiniMapPointer(map);
        }

        private static void DrawMiniMapRunwayNames(Rect map, GUIStyle small)
        {
            var colour = GUI.color;
            GUI.color = new Color(0.12f, 0.13f, 0.14f, 0.9f);
            foreach (var (label, x, z) in FieldMiniMap.RunwayLabels())
            {
                var point = FieldMiniMap.WorldToMap(map, x, z);
                GUI.Label(new Rect(point.x - 10f, point.y - 8f, 20f, 16f), label, small);
            }

            GUI.color = colour;
        }

        private void DrawMiniMapView(Rect map)
        {
            if (_mainCamera == null || _cameraController == null)
                return;

            var focus = _cameraController.FocusPoint;
            var centre = ClampToRect(FieldMiniMap.WorldToMap(map, focus.x, focus.z), map);
            var flatForward = Vector3.ProjectOnPlane(_mainCamera.transform.forward, Vector3.up);
            // World +z is up on the map, while GUI +y points down.
            var mapForward = new Vector2(flatForward.x, -flatForward.z);
            FieldMiniMap.ViewChevron(map, centre, mapForward, out var tip, out var left, out var right);

            var heading = new Color(AirsideTheme.Cloud.r, AirsideTheme.Cloud.g, AirsideTheme.Cloud.b, 0.72f);
            DrawLine(left, tip, heading, 1.4f);
            DrawLine(tip, right, heading, 1.4f);
            DrawSolid(new Rect(centre.x - 2f, centre.y - 2f, 4f, 4f), AirsideTheme.SafetyYellow);
        }

        private void DrawMiniMapAircraft(Rect map)
        {
            _miniMapDots.Clear();
            _miniMapDotIds.Clear();
            // Three passes so your own aircraft draw over other operators and the
            // selection draws over everything; dictionary order used to bury them.
            for (var pass = 0; pass < 3; pass++)
            foreach (var pair in _fleetViewById)
            {
                var view = pair.Value;
                if (view == null || !view.gameObject.activeInHierarchy || !_fleetAircraftById.TryGetValue(pair.Key, out var aircraft))
                    continue;

                var mine = aircraft.Airline.IsPlayer;
                var selected = pair.Key == _selectedAircraftId;
                if (MiniMapDotPass(mine, selected) != pass)
                    continue;

                var point = FieldMiniMap.WorldToMap(map, view.position.x, view.position.z);
                if (!map.Contains(point))
                    continue;
                _miniMapDots.Add(point);
                _miniMapDotIds.Add(pair.Key);

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

        /// <summary>Draw order: other operators, then yours, then the selection on top.</summary>
        public static int MiniMapDotPass(bool mine, bool selected) => selected ? 2 : mine ? 1 : 0;

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
