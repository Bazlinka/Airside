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
        private float _exteriorRadius, _exteriorBaseRadius;
        public bool IsFlightExterior => _cockpitActive && _flightExterior;
        private float _cockpitYaw, _cockpitPitch;
        private float _savedNear, _savedFar, _savedFov;
        private bool _cockpitRightDrag;
        public bool IsCockpit => _cockpitActive;
        public Vector3 CockpitPosition => _cockpitSeat != null ? _cockpitSeat.position : transform.position;
        public Quaternion CockpitRotation => _cockpitSeat != null
            ? _cockpitSeat.rotation * Quaternion.Euler(_cockpitPitch, _cockpitYaw, 0f) : transform.rotation;

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
            _flightExterior = false;
            _passengerSeat = false;
            _cockpitSeat = seat;
            _following = false;
            _easingOverview = false;
            _cockpitRightDrag = false;
            RecenterCockpit();
            _camera.fieldOfView = 65f;
            ApplyCockpitPose();
            return true;
        }

        public void RecenterCockpit()
        {
            _cockpitYaw=_flightExterior ? -35f : 0f; _cockpitPitch=_flightExterior ? 18f : 0f;
            if(_flightExterior) _exteriorRadius=_exteriorBaseRadius;
        }
        public bool StartPassenger(Transform seat)
        {
            if (!StartCockpit(seat)) return false;
            _passengerSeat=true;
            return true;
        }
        public bool StartFlightExterior(Transform aircraft, Vector3 centre, float span)
        {
            if(!StartCockpit(aircraft)) return false;
            _flightExterior=true; _exteriorCentre=centre;
            _exteriorBaseRadius=Mathf.Max(28f,span*1.15f); RecenterCockpit();
            _camera.fieldOfView=48f; ApplyCockpitPose(); return true;
        }

        public void EndCockpit()
        {
            if (!_cockpitActive) return;
            _cockpitActive = false;
            _flightExterior = false;
            _passengerSeat = false;
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
                    _cockpitYaw = (_flightExterior || _passengerSeat) ? Mathf.Repeat(_cockpitYaw + delta.x*.2f + 180f,360f)-180f
                        : Mathf.Clamp(_cockpitYaw + delta.x*.15f,-95f,95f);
                    _cockpitPitch = Mathf.Clamp(_cockpitPitch - delta.y*.15f, _flightExterior ? -20f : _passengerSeat ? -60f : -35f, _flightExterior ? 80f : _passengerSeat ? 70f : 45f);
                }
                if (!overHud)
                {
                    if (_flightExterior) _exteriorRadius=Mathf.Clamp(_exteriorRadius-mouse.scroll.ReadValue().y*.03f,
                        _exteriorBaseRadius*.65f,_exteriorBaseRadius*4f);
                    else _camera.fieldOfView=Mathf.Clamp(_camera.fieldOfView-mouse.scroll.ReadValue().y*.025f,48f,75f);
                }
            }
            else _cockpitRightDrag = false;
            ApplyCockpitPose();
        }

        private void ApplyCockpitPose()
        {
            if (_flightExterior && _cockpitSeat != null)
            {
                var centre=_cockpitSeat.TransformPoint(_exteriorCentre);
                var orbit=Quaternion.Euler(_cockpitPitch,_cockpitSeat.eulerAngles.y+_cockpitYaw,0);
                var position=centre+orbit*Vector3.back*_exteriorRadius;
                position.y=Mathf.Max(AirsideFlightPath.GroundY+.5f,position.y);
                transform.SetPositionAndRotation(position,Quaternion.LookRotation(centre-position,Vector3.up));
            }
            else transform.SetPositionAndRotation(CockpitPosition, CockpitRotation);
            CurrentDistance = _flightExterior ? _exteriorRadius : 0f;
            CurrentPitch = transform.eulerAngles.x;
            CurrentYaw = transform.eulerAngles.y;
            _camera.nearClipPlane = _flightExterior ? .15f : .035f;
            _camera.farClipPlane = Mathf.Max(_savedFar, 55000f);
            // Never use the overview distance's horizon compression from inside an aircraft.
            Shader.SetGlobalFloat(HorizonScaleId, 1f);
        }
    }
}
