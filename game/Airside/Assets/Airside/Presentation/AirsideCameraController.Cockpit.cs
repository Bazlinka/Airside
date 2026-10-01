using UnityEngine;
using UnityEngine.InputSystem;

namespace Airside.Presentation
{
    public sealed partial class AirsideCameraController
    {
        private Transform _cockpitSeat;
        private bool _cockpitActive;
        private float _cockpitYaw, _cockpitPitch;
        private float _savedNear, _savedFar, _savedFov;
        private bool _cockpitRightDrag;
        private int _cockpitPreset = -1;
        public bool IsCockpit => _cockpitActive;
        // Head and body motion layered on the rigid seat (CockpitMotion): offset in seat space, degrees.
        private Vector3 _cockpitMotionOffset, _cockpitMotionEuler;
        public Vector3 CockpitPosition => _cockpitSeat != null
            ? _cockpitSeat.position + _cockpitSeat.rotation * _cockpitMotionOffset : transform.position;
        public Quaternion CockpitRotation => _cockpitSeat != null
            ? _cockpitSeat.rotation * Quaternion.Euler(_cockpitPitch + _cockpitMotionEuler.x,
                _cockpitYaw + _cockpitMotionEuler.y, _cockpitMotionEuler.z) : transform.rotation;

        public void SetCockpitMotion(Vector3 offset, Vector3 euler)
        {
            _cockpitMotionOffset = offset;
            _cockpitMotionEuler = euler;
        }

        public bool StartCockpit(Transform seat)
        {
            if (seat == null || !seat.gameObject.activeInHierarchy || _camera == null) return false;
            if (!_cockpitActive)
            {
                _savedNear = _camera.nearClipPlane;
                _savedFar = _camera.farClipPlane;
                _savedFov = _camera.fieldOfView;
            }
            _cockpitActive = true;
            _cockpitSeat = seat;
            _cockpitMotionOffset = _cockpitMotionEuler = Vector3.zero;
            _following = false;
            _easingOverview = false;
            _cockpitRightDrag = false;
            RecenterCockpit();
            _camera.fieldOfView = 65f;
            ApplyCockpitPose();
            return true;
        }

        public void RecenterCockpit() { _cockpitYaw = _cockpitPitch = 0f; _cockpitPreset = -1; }

        public void EndCockpit()
        {
            if (!_cockpitActive) return;
            _cockpitActive = false;
            _cockpitSeat = null;
            _cockpitMotionOffset = _cockpitMotionEuler = Vector3.zero;
            if (_camera != null)
            {
                _camera.nearClipPlane = _savedNear;
                _camera.farClipPlane = _savedFar;
                _camera.fieldOfView = _savedFov;
            }
            _fov = _savedFov;
            _cockpitRightDrag = false;
            _rightPressOnField = _leftPressOnField = _middlePressOnField = false;
        }

        private void UpdateCockpitCamera()
        {
            // The runtime resolves by registration and owns loss-of-target exit.
            if (_cockpitSeat == null || !_cockpitSeat.gameObject.activeInHierarchy) return;
            var mouse = Mouse.current;
            if (mouse != null && !KeyboardCaptured)
            {
                var overHud = PointerOverHud != null && PointerOverHud(mouse.position.ReadValue());
                if (mouse.rightButton.wasPressedThisFrame) _cockpitRightDrag = !overHud;
                if (!mouse.rightButton.isPressed) _cockpitRightDrag = false;
                if (_cockpitRightDrag)
                {
                    var delta = mouse.delta.ReadValue();
                    _cockpitYaw = Mathf.Clamp(_cockpitYaw + delta.x * 0.15f, -95f, 95f);
                    _cockpitPitch = Mathf.Clamp(_cockpitPitch - delta.y * 0.15f, -35f, 45f);
                }
                if (!overHud)
                    _camera.fieldOfView = Mathf.Clamp(_camera.fieldOfView - mouse.scroll.ReadValue().y * 0.025f, 48f, 75f);
            }
            else _cockpitRightDrag = false;
            if (_cockpitRightDrag) _cockpitPreset = -1;
            var keyboard = Keyboard.current;
            if (keyboard != null && !KeyboardCaptured)
            {
                var keys = new[] { keyboard.digit1Key, keyboard.digit2Key, keyboard.digit3Key, keyboard.digit4Key, keyboard.digit5Key };
                for (var i = 0; i < keys.Length; i++)
                    if (keys[i].wasPressedThisFrame) _cockpitPreset = i;
                var yawInput = (keyboard.rightArrowKey.isPressed ? 1f : 0f) - (keyboard.leftArrowKey.isPressed ? 1f : 0f);
                var pitchInput = (keyboard.downArrowKey.isPressed ? 1f : 0f) - (keyboard.upArrowKey.isPressed ? 1f : 0f);
                if (yawInput != 0f || pitchInput != 0f)
                {
                    _cockpitPreset = -1;
                    _cockpitYaw = Mathf.Clamp(_cockpitYaw + yawInput * 90f * Time.unscaledDeltaTime,
                        -CockpitLookPresets.MaxYaw, CockpitLookPresets.MaxYaw);
                    _cockpitPitch = Mathf.Clamp(_cockpitPitch + pitchInput * 60f * Time.unscaledDeltaTime,
                        CockpitLookPresets.MinPitch, CockpitLookPresets.MaxPitch);
                }
            }
            if (_cockpitPreset >= 0)
            {
                var glance = CockpitLookPresets.All[_cockpitPreset];
                _cockpitYaw = CockpitLookPresets.Ease(_cockpitYaw, glance.Yaw, 7f, Time.unscaledDeltaTime);
                _cockpitPitch = CockpitLookPresets.Ease(_cockpitPitch, glance.Pitch, 7f, Time.unscaledDeltaTime);
            }
            ApplyCockpitPose();
        }

        private void ApplyCockpitPose()
        {
            transform.SetPositionAndRotation(CockpitPosition, CockpitRotation);
            CurrentDistance = 0f;
            CurrentPitch = transform.eulerAngles.x;
            CurrentYaw = transform.eulerAngles.y;
            _camera.nearClipPlane = 0.035f;
            _camera.farClipPlane = Mathf.Max(_savedFar, 55000f);
            // Never use the overview distance's horizon compression from inside an aircraft.
            Shader.SetGlobalFloat(HorizonScaleId, 1f);
        }
    }
}
