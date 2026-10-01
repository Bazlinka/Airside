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
        public bool IsCockpit => _cockpitActive;
        public Vector3 CockpitPosition => _cockpitSeat != null ? _cockpitSeat.position : transform.position;
        public Quaternion CockpitRotation => _cockpitSeat != null
            ? _cockpitSeat.rotation * Quaternion.Euler(_cockpitShownPitch, _cockpitShownYaw, 0f) : transform.rotation;

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
        }

        public void EndCockpit()
        {
            if (!_cockpitActive) return;
            _cockpitActive = false;
            _cockpitSeat = null;
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
                    var pitchSign = AirsideSettings.Current.InvertOrbit ? 1f : -1f;
                    _cockpitYaw = Mathf.Clamp(_cockpitYaw + delta.x * 0.15f, -95f, 95f);
                    _cockpitPitch = Mathf.Clamp(_cockpitPitch + delta.y * 0.15f * pitchSign, -35f, 45f);
                }
                if (!overHud)
                    _cockpitTargetFov = Mathf.Clamp(_cockpitTargetFov - mouse.scroll.ReadValue().y * 0.025f, 35f, 85f);
            }
            else _cockpitRightDrag = false;
            var keys = Keyboard.current;
            if (keys != null && !KeyboardCaptured)
            {
                var dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                var lookStep = 65f * AirsideSettings.Current.CameraSpeed * dt;
                if (keys.leftArrowKey.isPressed) _cockpitYaw -= lookStep;
                if (keys.rightArrowKey.isPressed) _cockpitYaw += lookStep;
                if (keys.upArrowKey.isPressed) _cockpitPitch -= lookStep;
                if (keys.downArrowKey.isPressed) _cockpitPitch += lookStep;
                if (keys.equalsKey.isPressed) _cockpitTargetFov -= 24f * dt;
                if (keys.minusKey.isPressed) _cockpitTargetFov += 24f * dt;
                _cockpitYaw = Mathf.Clamp(_cockpitYaw, -95f, 95f);
                _cockpitPitch = Mathf.Clamp(_cockpitPitch, -35f, 45f);
                _cockpitTargetFov = Mathf.Clamp(_cockpitTargetFov, 35f, 85f);
                if (keys.digit1Key.wasPressedThisFrame || keys.homeKey.wasPressedThisFrame) RecenterCockpit();
                if (keys.digit2Key.wasPressedThisFrame) { _cockpitYaw = -70f; _cockpitPitch = 0f; }
                if (keys.digit3Key.wasPressedThisFrame) { _cockpitYaw = 0f; _cockpitPitch = 32f; }
                if (keys.digit4Key.wasPressedThisFrame) { _cockpitYaw = 70f; _cockpitPitch = 0f; }
                if (keys.digit5Key.wasPressedThisFrame) { _cockpitYaw = 0f; _cockpitPitch = -30f; }
            }
            var turnEase = 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime);
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
