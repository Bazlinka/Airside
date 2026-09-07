using UnityEngine;
using UnityEngine.InputSystem;

namespace Airside.Presentation
{
    public sealed class AirsideCameraController : MonoBehaviour
    {
        private readonly Vector3 _overviewCenter = new(5f, 0f, 10f);
        private const float OverviewDistance = 52f;
        private const float FollowDistanceGround = 22f;
        private const float FollowDistanceAir = 34f;
        private Transform[] _followTargets = System.Array.Empty<Transform>();
        private int _followIndex;
        private Transform _followTarget;
        private Vector3 _center = new(5f, 0f, 10f);
        private float _yaw = 138f;
        private float _pitch = 38f;
        private float _distance = OverviewDistance;
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
            if (_following && _followTarget != null)
            {
                // Look a little ahead of the aircraft so taxi/takeoff reads forward motion.
                var ahead = _followTarget.forward;
                if (ahead.sqrMagnitude < 0.0001f)
                    ahead = Vector3.forward;
                ahead.y = 0f;
                if (ahead.sqrMagnitude > 0.0001f)
                    ahead.Normalize();
                else
                    ahead = Vector3.forward;

                var altitude = Mathf.Max(0f, _followTarget.position.y);
                var lookPoint = _followTarget.position
                    + ahead * Mathf.Lerp(4.5f, 10f, Mathf.Clamp01(altitude / 12f))
                    + Vector3.up * Mathf.Lerp(1.2f, 2.5f, Mathf.Clamp01(altitude / 12f));
                _center = Vector3.Lerp(_center, lookPoint, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 3.8f));

                var followDistance = Mathf.Lerp(FollowDistanceGround, FollowDistanceAir, Mathf.Clamp01(altitude / 10f));
                _distance = Mathf.Lerp(_distance, followDistance, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 2f));

                // Ease yaw toward the aircraft heading without fighting player orbit.
                var desiredYaw = Quaternion.LookRotation(ahead).eulerAngles.y + 28f;
                _yaw = Mathf.LerpAngle(_yaw, desiredYaw, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 0.55f));
                _pitch = Mathf.Lerp(_pitch, Mathf.Lerp(28f, 36f, Mathf.Clamp01(altitude / 10f)),
                    1f - Mathf.Exp(-Time.unscaledDeltaTime * 0.7f));
            }

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
                    _distance = OverviewDistance;
                    _pitch = 38f;
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

        /// <summary>Presentation helper for first-session: frame the lead commercial.</summary>
        public void StartFollowFirst()
        {
            if (_followTargets.Length == 0)
                return;
            _following = true;
            _followIndex = 0;
            _followTarget = _followTargets[0];
        }
    }
}
