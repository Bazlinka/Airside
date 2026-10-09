using UnityEngine;

namespace Airside.Presentation
{
    public sealed partial class AirsideCameraController
    {
        // The control-tower cab view: a fixed eye that turns through a full circle, like the passenger seat,
        // but looks down the field rather than out of an aircraft.
        private bool _towerView;
        public bool IsTower => _cockpitActive && _towerView;

        public bool StartTower(Transform eye)
        {
            if (!StartPassenger(eye)) return false;
            _towerView = true;
            return true;
        }

        private float ClampTowerPitch(float pitch) => Mathf.Clamp(pitch, -50f, 30f);
    }
}
