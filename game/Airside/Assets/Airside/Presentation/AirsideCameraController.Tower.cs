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

        public void ApplyAgentTowerLook(float pitch, float yaw)
        {
            if (!AirsideBareField.HasLaunchFlag("-airsideAgentGameplay") || !IsTower)
                throw new System.InvalidOperationException("Agent tower review required");
            if (!float.IsFinite(pitch) || !float.IsFinite(yaw) || pitch < -50 || pitch > 30)
                throw new System.ArgumentException("Invalid tower look");
            _cockpitPitch = _cockpitShownPitch = pitch;
            _cockpitYaw = _cockpitShownYaw = yaw;
        }

        private float ClampTowerPitch(float pitch) => Mathf.Clamp(pitch, -50f, 30f);
    }
}
