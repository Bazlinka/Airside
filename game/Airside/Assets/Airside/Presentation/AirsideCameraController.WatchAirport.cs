using UnityEngine;

namespace Airside.Presentation
{
    public sealed partial class AirsideCameraController
    {
        private Vector3? _watchCentre;
        public bool WatchingIndependentAirport => _watchCentre.HasValue;
        /// <summary>Frame an independent airport; local panning remains anchored there.</summary>
        public void WatchAirport(Vector3 centre,float distance,float pitch,float yaw)
        {
            ReleaseFollow();
            _watchCentre=centre;
            _center=centre;
            _distance=distance;
            _pitch=pitch;
            _yaw=yaw;
            _fov=OverviewFov;
            ApplyTransform();
        }

        private float OverviewNearClip()
        {
            var normal=AirsideCameraFeel.NearClip(_distance);
            var independent=_watchCentre.HasValue || ReviewView.Id=="parafield";
            // At a remote field the centimetre pavement/paint layers need more
            // depth precision than the 30 km world clip and standard near plane give.
            // Orbiting keeps the nearest scenery well beyond two percent of distance.
            return independent && !_following ? Mathf.Max(normal,Mathf.Min(300,_distance*.02f)) : normal;
        }
    }
}
