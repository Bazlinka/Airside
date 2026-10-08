using UnityEngine;
using UnityEngine.InputSystem;

namespace Airside.Presentation
{
    public sealed partial class AirsideCameraController
    {
        private Transform _cockpitSeat;
        private bool _cockpitActive;
        private bool _flightExterior, _passengerSeat;
        private Vector3 _exteriorCentre;
        private float _exteriorRadius, _exteriorBaseRadius, _exteriorTargetRadius;
        public bool IsFlightExterior => _cockpitActive && _flightExterior;
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
        private bool _cockpitRightDrag;     // a look drag is in progress, started with either mouse button
        // Gliding from the outside camera to the seat (or between seats) and back out, instead of cutting.
        // The glide's start is kept in the target's own frame, so a moving aircraft carries it along
        // instead of the camera sweeping from a fixed world point the aircraft has already left.
        private Vector3 _blendFromLocalPos;
        private Quaternion _blendFromLocalRot = Quaternion.identity;
        private float _blendFromFov = 65f;
        private float _blendSeconds = CockpitLookInput.TransitionSeconds;
        private Vector3 _exitFromPos;
        private Quaternion _exitFromRot = Quaternion.identity;
        private float _exitFromFov;
        // The aircraft the view was on, so the exit glide starts from where the camera would be had it
        // stayed with the moving aircraft, not from a fixed world point the aircraft has flown away from.
        private Transform _exitAnchor;
        private Vector3 _exitAnchorPos;
        private Quaternion _exitAnchorRot = Quaternion.identity;
        private const float ExitAnchorMaxStepMetres = 1000f;
        private float _exitSeconds = CockpitLookInput.TransitionSeconds;
        private int _exitBlendFrame = -1;
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

        /// <summary>0..1 progress through the glide into the seat or exterior orbit.</summary>
        public float SeatBlendProgress => Mathf.Clamp01(_blendSeconds / CockpitLookInput.TransitionSeconds);

        public void SetCockpitMotion(Vector3 offset, Vector3 euler)
        {
            _cockpitMotionOffset = offset;
            _cockpitMotionEuler = euler;
        }

        public bool StartCockpit(Transform seat)
        {
            if (seat == null || !seat.gameObject.activeInHierarchy || _camera == null) return false;
            _blendFromLocalPos = seat.InverseTransformPoint(transform.position);
            _blendFromLocalRot = Quaternion.Inverse(seat.rotation) * transform.rotation;
            _blendFromFov = _camera.fieldOfView;
            _blendSeconds = 0f;
            if (!_cockpitActive)
            {
                _savedNear = _camera.nearClipPlane;
                _savedFar = _camera.farClipPlane;
                _savedFov = _camera.fieldOfView;
            }
            _cockpitActive = true;
            _flightExterior = false;
            _passengerSeat = false;
            _towerView = false;
            _cockpitSeat = seat;
            _cockpitMotionOffset = _cockpitMotionEuler = Vector3.zero;
            _following = false;
            _easingOverview = false;
            _cockpitRightDrag = false;
            RecenterCockpit();
            _cockpitRumble = 0f;
            // The field of view eases to 65 in UpdateCockpitCamera rather than snapping here.
            ApplyCockpitPose();
            return true;
        }

        public void RecenterCockpit()
        {
            _cockpitYaw = _flightExterior ? -35f : 0f;
            _cockpitPitch = _flightExterior ? 18f : 0f;
            _cockpitShownYaw = _cockpitYaw;
            _cockpitShownPitch = _cockpitPitch;
            _cockpitTargetFov = _flightExterior ? 48f : 65f;
            _cockpitPreset = -1;
            if (_flightExterior) _exteriorRadius = _exteriorTargetRadius = _exteriorBaseRadius;
        }
        public bool StartPassenger(Transform seat)
        {
            if (!StartCockpit(seat)) return false;
            _passengerSeat = true;
            return true;
        }
        public bool StartFlightExterior(Transform aircraft, Vector3 centre, float span)
        {
            if (!StartCockpit(aircraft)) return false;
            _flightExterior = true;
            _exteriorCentre = centre;
            _exteriorBaseRadius = Mathf.Max(28f, span * 1.15f);
            RecenterCockpit();
            ApplyCockpitPose();
            return true;
        }

        public void EndCockpit()
        {
            if (!_cockpitActive) return;
            _exitFromPos = transform.position;
            _exitFromRot = transform.rotation;
            _exitAnchor = _cockpitSeat;
            if (_exitAnchor != null)
            {
                _exitAnchorPos = _exitAnchor.position;
                _exitAnchorRot = _exitAnchor.rotation;
            }
            _exitFromFov = _camera != null ? _camera.fieldOfView : _savedFov;
            _exitSeconds = 0f;
            _cockpitActive = false;
            _flightExterior = false;
            _passengerSeat = false;
            _towerView = false;
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
                // Either button looks: right-drag is awkward on a trackpad, and nothing else is clickable out here.
                if (mouse.rightButton.wasPressedThisFrame || mouse.leftButton.wasPressedThisFrame)
                    _cockpitRightDrag = !overHud;
                if (!mouse.rightButton.isPressed && !mouse.leftButton.isPressed) _cockpitRightDrag = false;
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
                    // Bounded per frame, and finer when zoomed in, so a trackpad flick cannot whip the view round.
                    var fov = _flightExterior ? CockpitLookInput.ReferenceFov : _cockpitTargetFov;
                    var speed = AirsideSettings.Current.CameraSpeed;
                    var yawRate = CockpitLookInput.DegreesPerPixel(_flightExterior || _passengerSeat
                        ? CockpitLookInput.OrbitDegreesPerPixel : CockpitLookInput.CockpitDegreesPerPixel, fov, speed);
                    var pitchRate = CockpitLookInput.DegreesPerPixel(CockpitLookInput.CockpitDegreesPerPixel, fov, speed);
                    _cockpitYaw = ClampFlightViewYaw(_cockpitYaw + CockpitLookInput.ClampPixels(delta.x) * yawRate);
                    _cockpitPitch = ClampFlightViewPitch(_cockpitPitch + CockpitLookInput.ClampPixels(delta.y) * pitchRate * pitchSign);
                }
                if (!overHud)
                {
                    var scroll = CockpitLookInput.ClampScroll(mouse.scroll.ReadValue().y);
                    if (_flightExterior) _exteriorTargetRadius = ClampExteriorRadius(_exteriorTargetRadius - scroll * 0.03f);
                    else _cockpitTargetFov = Mathf.Clamp(_cockpitTargetFov - scroll * 0.025f, 35f, 85f);
                }
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
                var zoomInput = (keyboard.equalsKey.isPressed ? 1f : 0f) - (keyboard.minusKey.isPressed ? 1f : 0f);
                if (_flightExterior) _exteriorTargetRadius = ClampExteriorRadius(_exteriorTargetRadius - zoomInput * _exteriorBaseRadius * dt);
                else _cockpitTargetFov -= zoomInput * 24f * dt;
                _cockpitTargetFov = Mathf.Clamp(_cockpitTargetFov, 35f, 85f);
                for (var i = 0; i < keys.Length; i++)
                    if (keys[i].wasPressedThisFrame)
                    {
                        if (_flightExterior || _passengerSeat)
                        {
                            if (i == 0) RecenterCockpit();
                        }
                        else
                        {
                            _cockpitPreset = i;
                            if (i == 0) _cockpitTargetFov = 65f;
                        }
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
                    _cockpitYaw = ClampFlightViewYaw(_cockpitYaw + yawInput * 90f * AirsideSettings.Current.CameraSpeed * dt);
                    _cockpitPitch = ClampFlightViewPitch(_cockpitPitch + pitchInput * 60f * AirsideSettings.Current.CameraSpeed * dt);
                }
            }
            if (_cockpitPreset >= 0)
            {
                var glance = CockpitLookPresets.All[_cockpitPreset];
                _cockpitYaw = glance.Yaw;
                _cockpitPitch = glance.Pitch;
            }
            var turnEase = 1f - Mathf.Exp(-(_cockpitPreset >= 0 ? 7f : 16f) * Time.unscaledDeltaTime);
            _cockpitShownYaw = Mathf.LerpAngle(_cockpitShownYaw, _cockpitYaw, turnEase);
            _cockpitShownPitch = Mathf.Lerp(_cockpitShownPitch, _cockpitPitch, turnEase);
            AdvanceFlightViewTransition(Time.unscaledDeltaTime);
            _exteriorRadius = Mathf.Lerp(_exteriorRadius, _exteriorTargetRadius, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            ApplyCockpitPose();
        }

        // One clock-driven step, also exercised by EditMode tests without depending on editor frame time.
        private void AdvanceFlightViewTransition(float deltaSeconds)
        {
            var dt = Mathf.Clamp(deltaSeconds, 0f, 0.1f);
            _blendSeconds += dt;
            // During the glide the field of view travels with it (exterior 48 to seat 65 used to snap
            // in a quarter of a second while the camera was still moving); afterwards it eases to zoom input.
            _camera.fieldOfView = _blendSeconds < CockpitLookInput.TransitionSeconds && _cockpitSeat != null
                ? Mathf.Lerp(_blendFromFov, _cockpitTargetFov, CockpitLookInput.Ease(_blendSeconds))
                : Mathf.Lerp(_camera.fieldOfView, _cockpitTargetFov, 1f - Mathf.Exp(-12f * dt));
        }

        /// <summary>Move the camera and its glide anchors into the new presentation origin.</summary>
        public void ShiftFlightOrigin(Vector3 delta)
        {
            transform.position += delta;
            _exitFromPos += delta;
        }

        private float ClampFlightViewYaw(float yaw) => _flightExterior || _passengerSeat
            ? Mathf.Repeat(yaw + 180f, 360f) - 180f : Mathf.Clamp(yaw, -CockpitLookPresets.MaxYaw, CockpitLookPresets.MaxYaw);
        private float ClampFlightViewPitch(float pitch) => _towerView ? ClampTowerPitch(pitch) : Mathf.Clamp(pitch,
            _flightExterior ? -20f : _passengerSeat ? -60f : CockpitLookPresets.MinPitch,
            _flightExterior ? 80f : _passengerSeat ? 70f : CockpitLookPresets.MaxPitch);
        private float ClampExteriorRadius(float radius) => Mathf.Clamp(radius, _exteriorBaseRadius * 0.65f, _exteriorBaseRadius * 4f);

        private void ApplyCockpitPose()
        {
            if (_flightExterior && _cockpitSeat != null)
            {
                var centre=_cockpitSeat.TransformPoint(_exteriorCentre);
                var orbit=Quaternion.Euler(_cockpitShownPitch,_cockpitSeat.eulerAngles.y+_cockpitShownYaw,0);
                var position=centre+orbit*Vector3.back*_exteriorRadius;
                position.y=Mathf.Max(AirsideFlightPath.GroundY+.5f,position.y);
                transform.SetPositionAndRotation(position,Quaternion.LookRotation(centre-position,Vector3.up));
            }
            else transform.SetPositionAndRotation(CockpitPosition, CockpitRotation);
            if (!_flightExterior && CockpitMotionEnabled && _cockpitRumble > 0f)
            {
                // Small angular motion only: never move the eye through the fitted shell.
                var t = Time.unscaledTime;
                transform.rotation *= Quaternion.Euler(
                    Mathf.Sin(t * 37f) * 0.10f * _cockpitRumble,
                    Mathf.Sin(t * 29f) * 0.06f * _cockpitRumble,
                    Mathf.Sin(t * 43f) * 0.08f * _cockpitRumble);
            }
            if (_blendSeconds < CockpitLookInput.TransitionSeconds)
            {
                var glide = CockpitLookInput.Ease(_blendSeconds);
                var fromPos = _cockpitSeat != null ? _cockpitSeat.TransformPoint(_blendFromLocalPos) : transform.position;
                var fromRot = _cockpitSeat != null ? _cockpitSeat.rotation * _blendFromLocalRot : transform.rotation;
                transform.SetPositionAndRotation(Vector3.Lerp(fromPos, transform.position, glide),
                    Quaternion.Slerp(fromRot, transform.rotation, glide));
            }
            CurrentDistance = _flightExterior ? _exteriorRadius : 0f;
            CurrentPitch = transform.eulerAngles.x;
            CurrentYaw = transform.eulerAngles.y;
            _camera.nearClipPlane = _flightExterior ? .15f : .035f;
            _camera.farClipPlane = Mathf.Max(_savedFar, transform.position.y > FlightWorldDetail.CruiseExitMetres ? 190000f : 55000f);
            // Never use the overview distance's horizon compression from inside an aircraft.
            Shader.SetGlobalFloat(HorizonScaleId, 1f);
        }

        /// <summary>Glides the free camera out from where the seat view left it. Called after each free-camera pose.</summary>
        private void ApplyExitBlend()
        {
            if (_exitSeconds >= CockpitLookInput.TransitionSeconds || _camera == null) { _exitAnchor = null; return; }
            if (_exitBlendFrame != Time.frameCount)
            {
                _exitBlendFrame = Time.frameCount;
                _exitSeconds += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            }
            var glide = CockpitLookInput.Ease(_exitSeconds);
            if (_exitAnchor != null)
            {
                // Follow the aircraft step by step. A jump of kilometres is an origin shift (already applied to
                // the glide start by ShiftFlightOrigin), not flight, so it is skipped.
                var step = _exitAnchor.position - _exitAnchorPos;
                _exitAnchorPos = _exitAnchor.position;
                if (step.sqrMagnitude < ExitAnchorMaxStepMetres * ExitAnchorMaxStepMetres)
                    _exitFromPos += step;
                // Turn with it too: swing the camera's offset and heading by the aircraft's own turn this
                // frame, so a banking aircraft does not slide sideways out of the glide's start view.
                var turn = _exitAnchor.rotation * Quaternion.Inverse(_exitAnchorRot);
                _exitAnchorRot = _exitAnchor.rotation;
                _exitFromPos = _exitAnchor.position + turn * (_exitFromPos - _exitAnchor.position);
                _exitFromRot = turn * _exitFromRot;
            }
            transform.SetPositionAndRotation(Vector3.Lerp(_exitFromPos, transform.position, glide),
                Quaternion.Slerp(_exitFromRot, transform.rotation, glide));
            _camera.fieldOfView = Mathf.Lerp(_exitFromFov, _camera.fieldOfView, glide);
        }
    }
}
