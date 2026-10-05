using UnityEngine;
using UnityEngine.InputSystem;

namespace Airside.Presentation
{
    public sealed partial class AirsideCameraController
    {
        private Transform _cockpitSeat;
        private bool _cockpitActive;
        private float _cockpitYaw, _cockpitPitch;
        private float _cockpitShownYaw, _cockpitShownPitch;
        private float _cockpitTargetFov = 65f;
        private float _cockpitRumble;
        public bool CockpitMotionEnabled
        {
            get => AirsideSettings.Current.CockpitMotion;
            set => AirsideSettings.Current.CockpitMotion = value;
        }
        public void SetCockpitRumble(float strength) => _cockpitRumble = Mathf.Clamp01(strength);
        private float _savedNear, _savedFar, _savedFov;
        private bool _cockpitRightDrag;
        private int _cockpitPreset = -1;
        private readonly UnityEngine.InputSystem.Controls.KeyControl[] _glanceKeys = new UnityEngine.InputSystem.Controls.KeyControl[5];
        public bool IsCockpit => _cockpitActive;
        // Head and body motion layered on the rigid seat (CockpitMotion): offset in seat space, degrees.
        private Vector3 _cockpitMotionOffset, _cockpitMotionEuler;
        public Vector3 CockpitPosition => _cockpitSeat != null
            ? _cockpitSeat.position + _cockpitSeat.rotation * (CockpitMotionEnabled ? _cockpitMotionOffset : Vector3.zero) : transform.position;
        public Quaternion CockpitRotation => _cockpitSeat != null
            ? _cockpitSeat.rotation * Quaternion.Euler(
                _cockpitShownPitch + (CockpitMotionEnabled ? _cockpitMotionEuler.x : 0f),
                _cockpitShownYaw + (CockpitMotionEnabled ? _cockpitMotionEuler.y : 0f),
                CockpitMotionEnabled ? _cockpitMotionEuler.z : 0f) : transform.rotation;

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
            _cockpitRumble = 0f;
            _camera.fieldOfView = 65f;
            ApplyCockpitPose();
            return true;
        }

        public void RecenterCockpit()
        {
            _cockpitShownYaw = _cockpitShownPitch = _cockpitYaw = _cockpitPitch = 0f;
            _cockpitTargetFov = 65f;
            _cockpitPreset = -1;
        }

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
                    if (_cockpitPreset >= 0)
                    {
                        _cockpitYaw = _cockpitShownYaw;
                        _cockpitPitch = _cockpitShownPitch;
                        _cockpitPreset = -1;
                    }
                    var delta = mouse.delta.ReadValue();
                    var pitchSign = AirsideSettings.Current.InvertOrbit ? 1f : -1f;
                    _cockpitYaw = Mathf.Clamp(_cockpitYaw + delta.x * 0.15f, -95f, 95f);
                    _cockpitPitch = Mathf.Clamp(_cockpitPitch + delta.y * 0.15f * pitchSign, -35f, 45f);
                }
                if (!overHud)
                    _cockpitTargetFov = Mathf.Clamp(_cockpitTargetFov - mouse.scroll.ReadValue().y * 0.025f, 35f, 85f);
            }
            else _cockpitRightDrag = false;
            if (_cockpitRightDrag) _cockpitPreset = -1;
            var keyboard = Keyboard.current;
            if (keyboard != null && !KeyboardCaptured)
            {
                _glanceKeys[0] = keyboard.digit1Key; _glanceKeys[1] = keyboard.digit2Key; _glanceKeys[2] = keyboard.digit3Key;
                _glanceKeys[3] = keyboard.digit4Key; _glanceKeys[4] = keyboard.digit5Key;
                var keys = _glanceKeys;
                var dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                if (keyboard.homeKey.wasPressedThisFrame) RecenterCockpit();
                if (keyboard.equalsKey.isPressed) _cockpitTargetFov -= 24f * dt;
                if (keyboard.minusKey.isPressed) _cockpitTargetFov += 24f * dt;
                _cockpitTargetFov = Mathf.Clamp(_cockpitTargetFov, 35f, 85f);
                for (var i = 0; i < keys.Length; i++)
                    if (keys[i].wasPressedThisFrame)
                    {
                        _cockpitPreset = i;
                        if (i == 0) _cockpitTargetFov = 65f;
                    }
                var yawInput = (keyboard.rightArrowKey.isPressed ? 1f : 0f) - (keyboard.leftArrowKey.isPressed ? 1f : 0f);
                var pitchInput = (keyboard.downArrowKey.isPressed ? 1f : 0f) - (keyboard.upArrowKey.isPressed ? 1f : 0f);
                if (yawInput != 0f || pitchInput != 0f)
                {
                    if (_cockpitPreset >= 0)
                    {
                        _cockpitYaw = _cockpitShownYaw;
                        _cockpitPitch = _cockpitShownPitch;
                    }
                    _cockpitPreset = -1;
                    _cockpitYaw = Mathf.Clamp(_cockpitYaw + yawInput * 90f * AirsideSettings.Current.CameraSpeed * dt,
                        -CockpitLookPresets.MaxYaw, CockpitLookPresets.MaxYaw);
                    _cockpitPitch = Mathf.Clamp(_cockpitPitch + pitchInput * 60f * AirsideSettings.Current.CameraSpeed * dt,
                        CockpitLookPresets.MinPitch, CockpitLookPresets.MaxPitch);
                }
            }
            if (_cockpitPreset >= 0)
            {
                var glance = CockpitLookPresets.All[_cockpitPreset];
                _cockpitYaw = glance.Yaw;
                _cockpitPitch = glance.Pitch;
            }
            var turnEase = 1f - Mathf.Exp(-(_cockpitPreset >= 0 ? 7f : 16f) * Time.unscaledDeltaTime);
            _cockpitShownYaw = Mathf.Lerp(_cockpitShownYaw, _cockpitYaw, turnEase);
            _cockpitShownPitch = Mathf.Lerp(_cockpitShownPitch, _cockpitPitch, turnEase);
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, _cockpitTargetFov,
                1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            ApplyCockpitPose();
        }

        private void ApplyCockpitPose()
        {
            transform.SetPositionAndRotation(CockpitPosition, CockpitRotation);
            if (CockpitMotionEnabled && _cockpitRumble > 0f)
            {
                // Small angular motion only: never move the eye through the fitted shell.
                var t = Time.unscaledTime;
                transform.rotation *= Quaternion.Euler(
                    Mathf.Sin(t * 37f) * 0.10f * _cockpitRumble,
                    Mathf.Sin(t * 29f) * 0.06f * _cockpitRumble,
                    Mathf.Sin(t * 43f) * 0.08f * _cockpitRumble);
            }
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
