using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Registration tags floating over every aircraft on the field. From the overview an
    /// ATR is a few pixels wide; the tag says which one it is and clicks select it. Tags
    /// fade out as the camera closes in and hide once the aircraft itself is plain to see.
    /// L toggles them.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        private const float FieldTagLiftMetres = 9f;
        private bool _fieldTagsVisible = true;
        private readonly System.Collections.Generic.List<Rect> _placedTags = new();

        private void ToggleFieldTags()
        {
            _fieldTagsVisible = !_fieldTagsVisible;
            ShowToast(_fieldTagsVisible ? "Aircraft tags on (L)." : "Aircraft tags off (L).");
            PlayUiClick();
        }

        private void DrawFieldTags(GUIStyle small)
        {
            if (!_fieldTagsVisible || _mainCamera == null || _fleetViewById.Count == 0)
                return;

            var scale = HudLayout.ScaleFor(Screen.width, Screen.height);
            _placedTags.Clear();
            var tagStyle = Styled(small, "field-tag", s => new GUIStyle(s) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false });
            var cameraPosition = _mainCamera.transform.position;

            foreach (var pair in _fleetViewById)
            {
                var view = pair.Value;
                // An aircraft away on a leg keeps its view at a stale pose with the object
                // hidden. Its tag was still drawn, floating over empty tarmac and taking
                // clicks that then selected an aircraft nowhere near the airport.
                if (view == null || !view.gameObject.activeInHierarchy
                    || !_fleetAircraftById.TryGetValue(pair.Key, out var aircraft))
                    continue;

                var world = view.position + Vector3.up * FieldTagLiftMetres;
                var distance = Vector3.Distance(cameraPosition, world);
                if (distance < RouteMap.FieldTagHideDistanceMetres)
                    continue;
                var screen = _mainCamera.WorldToScreenPoint(world);
                if (screen.z <= 0f)
                    continue;

                var gui = new Vector2(screen.x / scale, (Screen.height - screen.y) / scale);
                if (IsInsideHudPanel(gui))
                    continue;

                var fade = Mathf.InverseLerp(RouteMap.FieldTagHideDistanceMetres, RouteMap.FieldTagHideDistanceMetres * 2f, distance);
                // Other operators' tags are quieter than yours (Ownership).
                if (!aircraft.Airline.IsPlayer && aircraft.Registration != _selectedAircraftId)
                    fade *= Ownership.OtherAlpha;
                var selected = aircraft.Registration == _selectedAircraftId;
                var mine = aircraft.Airline.IsPlayer;
                var livery = AirsideTheme.FromHex(aircraft.Airline.LiveryHex);
                var ink = AirsideTheme.RunwayInk;

                var text = mine ? $"{Ownership.PlayerBadge} · {aircraft.Registration} · {AircraftStatus.TagPhase(aircraft)}" : aircraft.Registration;
                var width = tagStyle.CalcSize(new GUIContent(text)).x + 18f;
                var pill = new Rect(gui.x - width * 0.5f, gui.y - 30f, width, 20f);
                // Parked side by side, tags would print over each other: lift each one above
                // any tag already placed where it would land.
                for (var guard = 0; guard < 8; guard++)
                {
                    var bumped = false;
                    foreach (var placed in _placedTags)
                    {
                        if (!placed.Overlaps(pill))
                            continue;
                        pill.y = placed.y - pill.height - 2f;
                        bumped = true;
                    }
                    if (!bumped)
                        break;
                }
                // IMGUI hands an event to controls in the order they are drawn, and tags are
                // drawn before the panels. A pill reaching under a panel therefore took clicks
                // meant for that panel's buttons, selecting an aircraft instead.
                if (OverlapsHudPanel(pill))
                    continue;
                _placedTags.Add(pill);

                // Stem from the pill down to the aircraft.
                DrawSolid(new Rect(gui.x - 1f, pill.yMax, 2f, Mathf.Max(2f, gui.y - pill.yMax)), new Color(livery.r, livery.g, livery.b, fade));
                DrawSolid(pill, new Color(ink.r, ink.g, ink.b, 0.82f * fade));
                DrawSolid(new Rect(pill.x, pill.y, 5f, pill.height), new Color(livery.r, livery.g, livery.b, fade));
                var severity = mine ? AircraftStatus.Severity(aircraft, _clock.Now) : StatusSeverity.Normal;
                if (selected)
                    AirsideTheme.DrawPanelFrame(pill, new Color(AirsideTheme.SafetyYellow.r, AirsideTheme.SafetyYellow.g, AirsideTheme.SafetyYellow.b, fade));
                else if (severity != StatusSeverity.Normal)
                {
                    var alert = SeverityColour(severity, livery);
                    AirsideTheme.DrawPanelFrame(pill, new Color(alert.r, alert.g, alert.b, fade));
                }
                else if (mine)
                    AirsideTheme.DrawPanelFrame(pill, new Color(livery.r, livery.g, livery.b, fade));
                var previous = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, fade);
                GUI.Label(new Rect(pill.x + 4f, pill.y, pill.width - 4f, pill.height), text, tagStyle);
                GUI.color = previous;

                if (fade > 0.3f)
                {
                    // Owned by the HUD: a press here must not also pan or 3D-pick behind it.
                    _hudOverlays.Add(pill);
                    if (GUI.Button(pill, GUIContent.none, GUIStyle.none))
                        SelectAircraft(aircraft);
                }
            }
        }

        private bool IsInsideHudPanel(Vector2 gui)
        {
            return HudHitTest.Contains(gui, _hudPanels);
        }

        private bool OverlapsHudPanel(Rect rect)
        {
            for (var i = 0; i < _hudPanels.Count; i++)
                if (_hudPanels[i].Overlaps(rect))
                    return true;
            return false;
        }
    }
}
