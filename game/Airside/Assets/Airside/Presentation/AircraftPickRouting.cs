using System;
using System.Collections.Generic;

namespace Airside.Presentation
{
    /// <summary>
    /// Click-vs-drag and on-field pick rules for direct aircraft selection.
    /// Presentation-only; never decides reservations, schedules or persistence.
    /// </summary>
    public static class AircraftPickRouting
    {
        /// <summary>Must match the camera left-drag threshold so a pan never selects.</summary>
        public const float DragThresholdPixels = 4f;

        // ATR 42-class pick volume (metres), slightly larger than the mesh for forgiving clicks.
        public const float ProxyLengthMetres = 28f;
        public const float ProxyWidthMetres = 30f;
        public const float ProxyHeightMetres = 10f;
        public const float ProxyCentreYMetres = 3.5f;

        public const string ViewNamePrefix = "Commercial ";
        public const string ProxyChildName = "AircraftPickProxy";
        public const string MarkerChildName = "SelectionMarker";
        public const string PickLayerName = "AircraftPick";

        public static bool CountsAsClick(float pressX, float pressY, float releaseX, float releaseY,
            float thresholdPixels = DragThresholdPixels)
        {
            var dx = releaseX - pressX;
            var dy = releaseY - pressY;
            return dx * dx + dy * dy <= thresholdPixels * thresholdPixels;
        }

        public static bool TryRegistrationFromViewName(string viewName, out string registration)
        {
            registration = null;
            if (string.IsNullOrEmpty(viewName))
                return false;
            if (!viewName.StartsWith(ViewNamePrefix, StringComparison.Ordinal))
                return false;
            registration = viewName.Substring(ViewNamePrefix.Length);
            return registration.Length > 0;
        }

        /// <summary>
        /// Among candidate hits, return the nearest selectable aircraft id.
        /// Non-selectable hits (off-field / stale) are ignored so they cannot steal the pick.
        /// </summary>
        public static string ResolveNearest(IReadOnlyList<AircraftPickHit> hits)
        {
            string best = null;
            var bestDistance = float.PositiveInfinity;
            for (var i = 0; i < hits.Count; i++)
            {
                var hit = hits[i];
                if (!hit.Selectable || string.IsNullOrEmpty(hit.AircraftId))
                    continue;
                if (hit.DistanceMetres >= bestDistance)
                    continue;
                bestDistance = hit.DistanceMetres;
                best = hit.AircraftId;
            }

            return best;
        }

        /// <summary>True when this registration currently has an active on-field view.</summary>
        public static bool IsOnFieldSelectable(string aircraftId, IReadOnlyDictionary<string, bool> onFieldById)
        {
            return !string.IsNullOrEmpty(aircraftId)
                   && onFieldById != null
                   && onFieldById.TryGetValue(aircraftId, out var onField)
                   && onField;
        }
    }

    /// <summary>One raycast candidate for <see cref="AircraftPickRouting.ResolveNearest"/>.</summary>
    public readonly struct AircraftPickHit
    {
        public AircraftPickHit(string aircraftId, float distanceMetres, bool selectable)
        {
            AircraftId = aircraftId;
            DistanceMetres = distanceMetres;
            Selectable = selectable;
        }

        public string AircraftId { get; }
        public float DistanceMetres { get; }
        public bool Selectable { get; }
    }
}
