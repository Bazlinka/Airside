using UnityEngine;
using UnityEngine.InputSystem;

namespace Airside.Presentation
{
    public sealed class AirsideCameraController : MonoBehaviour
    {
        private const float S = Airside.Simulation.AirportTaxiNetwork.WorldScale;
        private readonly Vector3 _overviewCenter = new Vector3(8f, 0f, 16f) * S;
        // Framed against the aircraft's real size (15 span, 15.6 long) and the airfield
        // that now surrounds it, so the overview reads as a place rather than a close-up
        // of one corner of it.
        // The airfield now runs from the runway at z -3.5 to the terminal at z 33 in
        // layout units — about 120 world units deep — so the overview has to stand well
        // back to hold all of it.
        private const float OverviewDistance = 70f * S;
        private const float FollowDistanceGround = 9.5f * S;
        private const float FollowDistanceAir = 15f * S;
        private Transform[] _followTargets = System.Array.Empty<Transform>();
        private int _followIndex;
        private Transform _followTarget;
        private Vector3 _center = new(5f, 0f, 10f);
        private float _yaw = 138f;
        private float _pitch = 38f;
        private float _distance = OverviewDistance;
        private bool _following;
        private float _touchdownShake;
        // Scroll while following used to be overwritten by the auto follow distance on
        // the very next frame, so zoom silently did nothing. The player's scroll now
        // biases the follow distance and holds until they return to overview.
        private float _followZoom = 1f;
        private const float MinFollowZoom = 0.45f;
        private const float MaxFollowZoom = 2.4f;

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
                    + ahead * Mathf.Lerp(4.5f * S, 10f * S, Mathf.Clamp01(altitude / (12f * S)))
                    + Vector3.up * Mathf.Lerp(1.2f * S, 2.5f * S, Mathf.Clamp01(altitude / (12f * S)));
                _center = Vector3.Lerp(_center, lookPoint, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 3.8f));

                var followDistance = Mathf.Lerp(FollowDistanceGround, FollowDistanceAir, Mathf.Clamp01(altitude / (10f * S)))
                    * _followZoom;
                _distance = Mathf.Lerp(_distance, followDistance, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 2f));

                // Ease yaw toward the aircraft heading without fighting player orbit.
                var desiredYaw = Quaternion.LookRotation(ahead).eulerAngles.y + 28f;
                _yaw = Mathf.LerpAngle(_yaw, desiredYaw, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 0.55f));
                _pitch = Mathf.Lerp(_pitch, Mathf.Lerp(28f, 36f, Mathf.Clamp01(altitude / (10f * S))),
                    1f - Mathf.Exp(-Time.unscaledDeltaTime * 0.7f));
            }

            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            var shakeOffset = Vector3.zero;
            if (_touchdownShake > 0f)
            {
                var strength = _touchdownShake * 0.55f * S;
                shakeOffset = new Vector3(
                    Mathf.Sin(Time.unscaledTime * 48f) * strength,
                    Mathf.Sin(Time.unscaledTime * 61f) * strength * 0.6f,
                    Mathf.Cos(Time.unscaledTime * 53f) * strength * 0.4f);
                _touchdownShake = Mathf.MoveTowards(_touchdownShake, 0f, Time.unscaledDeltaTime * 2.8f);
            }

            transform.SetPositionAndRotation(_center - rotation * Vector3.forward * _distance + shakeOffset, rotation);
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
                    _followZoom = 1f;
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
                    _center += (planarForward * move.y + planarRight * move.x) * (18f * S * Time.unscaledDeltaTime);
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
            {
                if (_following)
                    _followZoom = Mathf.Clamp(_followZoom - scroll * 0.0016f, MinFollowZoom, MaxFollowZoom);
                else
                    _distance = Mathf.Clamp(_distance - scroll * 0.035f * S, 14f * S, 90f * S);
            }
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

        /// <summary>Brief camera shake when a commercial touches down (presentation only).</summary>
        public void PulseTouchdown()
        {
            _touchdownShake = 1f;
        }
    }
}
