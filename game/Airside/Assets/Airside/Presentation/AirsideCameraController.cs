using UnityEngine;
using UnityEngine.InputSystem;

namespace Airside.Presentation
{
    public sealed class AirsideCameraController : MonoBehaviour
    {
        private readonly Vector3 _overviewCenter = new(5f, 0f, 10f);
        private Transform[] _followTargets = System.Array.Empty<Transform>();
        private int _followIndex;
        private Transform _followTarget;
        private Vector3 _center = new(5f, 0f, 10f);
        private float _yaw = 138f;
        private float _pitch = 38f;
        private float _distance = 52f;
        private bool _following;

        public void SetFollowTarget(Transform target)
        {
            _followTargets = target != null ? new[] { target } : System.Array.Empty<Transform>();
            _followIndex = 0;
            _followTarget = target;
        }

        public void SetFollowTargets(Transform[] targets)
        {
            _followTargets = targets ?? System.Array.Empty<Transform>();
            if (_followTargets.Length == 0)
            {
                _followTarget = null;
                _followIndex = 0;
                return;
            }

            _followIndex = Mathf.Clamp(_followIndex, 0, _followTargets.Length - 1);
            _followTarget = _followTargets[_followIndex];
        }

        private void LateUpdate()
        {
            ReadInput();
            var desiredCenter = _following && _followTarget != null ? _followTarget.position : _center;
            if (_following)
                _center = Vector3.Lerp(_center, desiredCenter, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 3.5f));

            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.SetPositionAndRotation(_center - rotation * Vector3.forward * _distance, rotation);
        }

        private void ReadInput()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null)
            {
                if (keyboard.fKey.wasPressedThisFrame)
                    CycleOrStartFollow();
                if (keyboard.oKey.wasPressedThisFrame)
                {
                    _following = false;
                    _center = _overviewCenter;
                    _distance = 52f;
                }

                if (!_following)
                {
                    var move = Vector2.zero;
                    if (keyboard.wKey.isPressed) move.y += 1f;
                    if (keyboard.sKey.isPressed) move.y -= 1f;
                    if (keyboard.dKey.isPressed) move.x += 1f;
                    if (keyboard.aKey.isPressed) move.x -= 1f;
                    var planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                    var planarRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
                    _center += (planarForward * move.y + planarRight * move.x) * (18f * Time.unscaledDeltaTime);
                }
            }

            if (mouse == null)
                return;

            if (mouse.rightButton.isPressed)
            {
                var delta = mouse.delta.ReadValue();
                _yaw += delta.x * 0.18f;
                _pitch = Mathf.Clamp(_pitch - delta.y * 0.14f, 18f, 72f);
            }

            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                _distance = Mathf.Clamp(_distance - scroll * 0.035f, 14f, 90f);
        }

        private void CycleOrStartFollow()
        {
            if (_followTargets.Length == 0)
            {
                _following = _followTarget != null;
                return;
            }

            if (!_following)
            {
                _following = true;
                _followIndex = 0;
                _followTarget = _followTargets[0];
                return;
            }

            // Already following: cycle through commercials (and wrap).
            _followIndex = (_followIndex + 1) % _followTargets.Length;
            _followTarget = _followTargets[_followIndex];
        }
    }
}
