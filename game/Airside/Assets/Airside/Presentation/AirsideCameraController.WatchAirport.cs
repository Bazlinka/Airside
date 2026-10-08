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
        }
    }
}
