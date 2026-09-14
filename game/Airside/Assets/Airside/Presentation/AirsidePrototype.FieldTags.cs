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
                if (view == null || !_fleetAircraftById.TryGetValue(pair.Key, out var aircraft))
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
                var selected = aircraft.Registration == _selectedAircraftId;
                var mine = aircraft.Airline.IsPlayer;
                var livery = AirsideTheme.FromHex(aircraft.Airline.LiveryHex);
                var ink = AirsideTheme.RunwayInk;

                var text = mine ? $"{aircraft.Registration} · {FieldTagPhase(aircraft)}" : aircraft.Registration;
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
                _placedTags.Add(pill);

                // Stem from the pill down to the aircraft.
                DrawSolid(new Rect(gui.x - 1f, pill.yMax, 2f, Mathf.Max(2f, gui.y - pill.yMax)), new Color(livery.r, livery.g, livery.b, fade));
                DrawSolid(pill, new Color(ink.r, ink.g, ink.b, 0.82f * fade));
                DrawSolid(new Rect(pill.x, pill.y, 5f, pill.height), new Color(livery.r, livery.g, livery.b, fade));
                if (selected)
                    AirsideTheme.DrawPanelFrame(pill, new Color(AirsideTheme.SafetyYellow.r, AirsideTheme.SafetyYellow.g, AirsideTheme.SafetyYellow.b, fade));
                var previous = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, fade);
                GUI.Label(new Rect(pill.x + 4f, pill.y, pill.width - 4f, pill.height), text, tagStyle);
                GUI.color = previous;

                if (fade > 0.3f && GUI.Button(pill, GUIContent.none, GUIStyle.none))
                    SelectAircraft(aircraft);
            }
        }

        private static string FieldTagPhase(FleetAircraft aircraft) => aircraft.State switch
        {
            FleetState.AtStand => aircraft.Scheduled.HasValue ? "planned" : "free",
            FleetState.TaxiOut => "taxiing",
            FleetState.HoldingShort => "holding",
            FleetState.TakingOff => "takeoff",
            FleetState.HoldingForLanding => "circuit",
            FleetState.Landing => "landing",
            FleetState.AwaitingStand => "needs stand",
            FleetState.TaxiIn => "taxiing in",
            _ => "away"
        };

        private bool IsInsideHudPanel(Vector2 gui)
        {
            foreach (var rect in _hudPanels)
                if (rect.Contains(gui))
                    return true;
            return false;
        }
    }
}
