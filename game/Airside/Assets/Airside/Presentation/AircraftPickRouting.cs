using System;
using System.Collections.Generic;
using Airside.Simulation;

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

        /// <summary>
        /// An arrival becomes a field interaction once it is within this distance of
        /// the landing threshold. Earlier than this it is still a map/contact return,
        /// not a practical object to click in the 3D airport view.
        /// </summary>
        public const float ApproachSelectableDistanceFromThresholdMetres = 1800f;

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

        /// <summary>
        /// After the on-field set is rebuilt: index of <paramref name="current"/> in
        /// <paramref name="next"/>, or -1 if that aircraft left. Follow should release at -1
        /// rather than clamping onto a different aircraft.
        /// </summary>
        public static int IndexOfSame<T>(T[] next, T current) where T : class
        {
            if (current == null || next == null || next.Length == 0)
                return -1;
            for (var i = 0; i < next.Length; i++)
                if (ReferenceEquals(next[i], current))
                    return i;
            return -1;
        }

        /// <summary>True when this registration currently has an active on-field view.</summary>
        public static bool IsOnFieldSelectable(string aircraftId, IReadOnlyDictionary<string, bool> onFieldById)
        {
            return !string.IsNullOrEmpty(aircraftId)
                   && onFieldById != null
                   && onFieldById.TryGetValue(aircraftId, out var onField)
                   && onField;
        }

        /// <summary>05-frame helper kept for tests; prefer the runway-aware overload.</summary>
        public static bool ApproachIsCloseEnough(float aircraftWorldX, float landingThresholdWorldX) =>
            aircraftWorldX >= landingThresholdWorldX - ApproachSelectableDistanceFromThresholdMetres;

        /// <summary>
        /// True when the aircraft is within selectable range of the assigned runway's
        /// landing threshold in world XZ (not raw 05-frame X).
        /// </summary>
        public static bool ApproachIsCloseEnough(float worldX, float worldZ, RunwayDirection runway)
        {
            RunwayFrame.ToWorld(runway, CircuitProfile.WestThresholdX, 0f, 0f,
                out var thresholdX, out _, out var thresholdZ);
            var dx = worldX - thresholdX;
            var dz = worldZ - thresholdZ;
            return dx * dx + dz * dz
                   <= ApproachSelectableDistanceFromThresholdMetres * ApproachSelectableDistanceFromThresholdMetres;
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
