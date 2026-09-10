using UnityEngine;
using UnityEngine.InputSystem;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// Overview orbit + follow camera. Decision 0025 first-playable: phase-aware
    /// follow framing so taxi, approach, landing and takeoff each read differently.
    /// </summary>
    public sealed class AirsideCameraController : MonoBehaviour
    {
        // Bare field: look at the runway centre from high enough to read 3100 m of asphalt.
        private readonly Vector3 _overviewCenter = new(0f, 0f, 0f);
        private static float OverviewDistance => AirsideBareField.OverviewDistance;
        private static float OverviewFov => AirsideBareField.OverviewFov;
        private static float OverviewPitch => AirsideBareField.OverviewPitch;
        private static float OverviewYaw => AirsideBareField.OverviewYaw;
        private Transform[] _followTargets = System.Array.Empty<Transform>();
        private int _followIndex;
        private Transform _followTarget;
        private Vector3 _center = new(0f, 0f, 0f);
        private float _yaw = AirsideBareField.OverviewYaw;
        private float _pitch = AirsideBareField.OverviewPitch;
        private float _distance = AirsideBareField.OverviewDistance;
        private bool _following;
        private bool _easingOverview;
        private float _orbitSuppressUntil;
        private float _touchdownShake;
        private AircraftPhase _followPhase = AircraftPhase.AtStand;
        private float _followProgress;
        private float _fov = OverviewFov;
        private Camera _camera;
        private Vector3 _lastTargetPosition;
        private bool _hasLastTargetPosition;

        /// <summary>
        /// When true (sim pause), follow easing and touchdown shake freeze. Player orbit
        /// and overview pan still work so the field can be inspected while paused.
        /// </summary>
        public bool FreezePresentation { get; set; }

        /// <summary>
        /// No aircraft covers this much ground in one frame — the fastest phase at 4x
        /// time and 30 fps moves about 4 m. A jump this large means the slot was
        /// recycled: the departure that just flew out has been replaced by a new
        /// arrival joining final at the other end of the field.
        /// </summary>
        private const float RespawnJumpMetres = 20f;

        public void SetFollowTarget(Transform target)
        {
            _followTargets = target != null ? new[] { target } : System.Array.Empty<Transform>();
            _followIndex = 0;
            _followTarget = target;
            _hasLastTargetPosition = false;
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
            _hasLastTargetPosition = false;
        }

        /// <summary>
        /// Presentation-only: tell the follow camera which phase the tracked aircraft
        /// is in so framing / FOV can lean into the beat.
        /// </summary>
        public void SetFollowPhase(AircraftPhase phase, float progress01)
        {
            _followPhase = phase;
            _followProgress = Mathf.Clamp01(progress01);
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera != null)
                _fov = _camera.fieldOfView;
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
                var lookAhead = LookAheadMetres(_followPhase, _followProgress, altitude);
                var lookHeight = LookHeightMetres(_followPhase, altitude);
                var lookPoint = _followTarget.position + ahead * lookAhead + Vector3.up * lookHeight;

                // A recycled slot puts the new arrival hundreds of metres away in one
                // frame. Easing to it dragged the camera the length of the field, so cut
                // straight there instead.
                var recycled = _hasLastTargetPosition
                    && Vector3.Distance(_lastTargetPosition, _followTarget.position) > RespawnJumpMetres;
                _lastTargetPosition = _followTarget.position;
                _hasLastTargetPosition = true;

                var followDistance = FollowDistance(_followPhase, altitude, _followProgress);
                if (recycled)
                {
                    _center = lookPoint;
                    _distance = followDistance;
                    _fov = FollowFov(_followPhase, _followProgress);
                    if (Time.unscaledTime >= _orbitSuppressUntil)
                        _yaw = Quaternion.LookRotation(ahead).eulerAngles.y + YawBiasDegrees(_followPhase);
                    _pitch = FollowPitch(_followPhase, altitude, _followProgress);
                    ApplyTransform();
                    return;
                }

                // Track harder on the fast phases. At one fixed rate the camera trails a
                // departure by speed/rate metres, which at 4x let the aircraft run off
                // the edge of frame during climb-out.
                var dt = FreezePresentation ? 0f : Time.unscaledDeltaTime;
                var centreRate = _followPhase switch
                {
                    AircraftPhase.Takeoff or AircraftPhase.Departed => 8f,
                    AircraftPhase.Approach or AircraftPhase.Landing => 6f,
                    _ => 4.2f
                };
                if (dt > 0f)
                {
                    _center = Vector3.Lerp(_center, lookPoint, 1f - Mathf.Exp(-dt * centreRate));
                    _distance = Mathf.Lerp(_distance, followDistance, 1f - Mathf.Exp(-dt * 2.4f));

                    // Ease yaw toward the aircraft heading without fighting player orbit.
                    if (Time.unscaledTime >= _orbitSuppressUntil)
                    {
                        var yawBias = YawBiasDegrees(_followPhase);
                        var desiredYaw = Quaternion.LookRotation(ahead).eulerAngles.y + yawBias;
                        _yaw = Mathf.LerpAngle(_yaw, desiredYaw, 1f - Mathf.Exp(-dt * 0.7f));
                    }
                    var desiredPitch = FollowPitch(_followPhase, altitude, _followProgress);
                    _pitch = Mathf.Lerp(_pitch, desiredPitch, 1f - Mathf.Exp(-dt * 0.85f));

                    var targetFov = FollowFov(_followPhase, _followProgress);
                    _fov = Mathf.Lerp(_fov, targetFov, 1f - Mathf.Exp(-dt * 1.6f));
                }
            }
            else
            {
                if (_easingOverview)
                {
                    var k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 3.2f);
                    _center = Vector3.Lerp(_center, _overviewCenter, k);
                    _distance = Mathf.Lerp(_distance, OverviewDistance, k);
                    _pitch = Mathf.Lerp(_pitch, OverviewPitch, k);
                    _yaw = Mathf.LerpAngle(_yaw, OverviewYaw, k);
                    _fov = Mathf.Lerp(_fov, OverviewFov, k);
                    if (Vector3.Distance(_center, _overviewCenter) < 0.08f
                        && Mathf.Abs(_distance - OverviewDistance) < 0.08f
                        && Mathf.Abs(Mathf.DeltaAngle(_yaw, OverviewYaw)) < 0.4f)
                    {
                        _center = _overviewCenter;
                        _distance = OverviewDistance;
                        _pitch = OverviewPitch;
                        _yaw = OverviewYaw;
                        _fov = OverviewFov;
                        _easingOverview = false;
                    }
                }
                else
                {
                    _fov = Mathf.Lerp(_fov, OverviewFov, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 1.2f));
                }
            }

            ApplyTransform();
        }

        private void ApplyTransform()
        {
            if (_camera != null)
                _camera.fieldOfView = _fov;

            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            var shakeOffset = Vector3.zero;
            if (_touchdownShake > 0f)
            {
                // Restrained ATR-scale nudge — readable in follow, not a crash cutscene.
                var strength = _touchdownShake * 0.28f;
                shakeOffset = new Vector3(
                    Mathf.Sin(Time.unscaledTime * 36f) * strength,
                    Mathf.Sin(Time.unscaledTime * 44f) * strength * 0.55f,
                    Mathf.Cos(Time.unscaledTime * 40f) * strength * 0.35f);
                if (!FreezePresentation)
                    _touchdownShake = Mathf.MoveTowards(_touchdownShake, 0f, Time.unscaledDeltaTime * 2.2f);
            }

            transform.SetPositionAndRotation(_center - rotation * Vector3.forward * _distance + shakeOffset, rotation);
        }

        private static float LookAheadMetres(AircraftPhase phase, float progress, float altitude)
        {
            var air = Mathf.Lerp(4.5f, 10f, Mathf.Clamp01(altitude / 12f));
            return phase switch
            {
                AircraftPhase.TaxiIn or AircraftPhase.TaxiOut or AircraftPhase.Pushback => 8f,
                AircraftPhase.AtStand => 4f,
                AircraftPhase.Takeoff => Mathf.Lerp(18f, 40f, progress),
                AircraftPhase.Approach => Mathf.Lerp(28f, 40f, progress),
                AircraftPhase.Landing => Mathf.Lerp(32f, 12f, progress),
                AircraftPhase.Departed => 48f,
                _ => air
            };
        }

        private static float LookHeightMetres(AircraftPhase phase, float altitude)
        {
            var air = Mathf.Lerp(1.2f, 2.5f, Mathf.Clamp01(altitude / 12f));
            return phase switch
            {
                AircraftPhase.AtStand => 1.6f,
                AircraftPhase.TaxiIn or AircraftPhase.TaxiOut or AircraftPhase.Pushback => 1.35f,
                AircraftPhase.Landing => 1.1f,
                AircraftPhase.Approach => 1.8f,
                _ => air
            };
        }

        private static float FollowDistance(AircraftPhase phase, float altitude, float progress)
        {
            // ATR 42-class is ~22.7 m long / 24.6 m span — keep a little more room than the v06 kit.
            var air = Mathf.Lerp(34f, 48f, Mathf.Clamp01(altitude / 10f));
            return phase switch
            {
                AircraftPhase.AtStand => 36f,
                AircraftPhase.TaxiIn or AircraftPhase.TaxiOut or AircraftPhase.Pushback => 36f,
                AircraftPhase.Takeoff => Mathf.Lerp(46f, 95f, progress),
                AircraftPhase.Approach => Mathf.Lerp(68f, 52f, progress),
                AircraftPhase.Landing => Mathf.Lerp(54f, 36f, progress),
                AircraftPhase.Departed => Mathf.Lerp(80f, 240f, progress),
                _ => air
            };
        }

        private static float FollowPitch(AircraftPhase phase, float altitude, float progress)
        {
            var air = Mathf.Lerp(26f, 34f, Mathf.Clamp01(altitude / 10f));
            return phase switch
            {
                AircraftPhase.AtStand => 22f,
                AircraftPhase.TaxiIn or AircraftPhase.TaxiOut or AircraftPhase.Pushback => 24f,
                AircraftPhase.Takeoff => Mathf.Lerp(28f, 36f, progress),
                AircraftPhase.Approach => Mathf.Lerp(34f, 26f, progress),
                AircraftPhase.Landing => Mathf.Lerp(32f, 20f, progress),
                _ => air
            };
        }

        private static float YawBiasDegrees(AircraftPhase phase) => phase switch
        {
            AircraftPhase.Takeoff => 48f,   // stronger three-quarter chase
            AircraftPhase.Approach => 26f,  // rear-quarter approach
            AircraftPhase.Landing => 20f,
            AircraftPhase.AtStand => 58f,   // apron side angle
            _ => 30f
        };

        private static float FollowFov(AircraftPhase phase, float progress) => phase switch
        {
            AircraftPhase.Approach => Mathf.Lerp(50f, 46f, progress),
            AircraftPhase.Landing => Mathf.Lerp(48f, 52f, progress),
            AircraftPhase.Takeoff => Mathf.Lerp(52f, 48f, progress),
            AircraftPhase.AtStand => 50f,
            _ => 52f
        };

        private void ReadInput()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null)
            {
                if (keyboard.fKey.wasPressedThisFrame)
                    CycleOrStartFollow();
                if (keyboard.oKey.wasPressedThisFrame)
                    ReturnToOverview();

                if (!_following)
                {
                    var move = Vector2.zero;
                    if (keyboard.wKey.isPressed) move.y += 1f;
                    if (keyboard.sKey.isPressed) move.y -= 1f;
                    if (keyboard.dKey.isPressed) move.x += 1f;
                    if (keyboard.aKey.isPressed) move.x -= 1f;
                    var planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                    var planarRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
                    _center += (planarForward * move.y + planarRight * move.x)
                        * (AirsideBareField.OverviewPanMetresPerSecond * Time.unscaledDeltaTime);
                }
            }

            if (mouse == null)
                return;

            if (mouse.rightButton.isPressed)
            {
                var delta = mouse.delta.ReadValue();
                _yaw += delta.x * 0.18f;
                _pitch = Mathf.Clamp(_pitch - delta.y * 0.14f, 18f, 72f);
                // Suppress follow yaw bias briefly so orbit is not fought every frame.
                _orbitSuppressUntil = Time.unscaledTime + 0.9f;
                _easingOverview = false;
            }

            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                var step = scroll * 0.0025f * Mathf.Max(80f, _distance);
                _distance = Mathf.Clamp(
                    _distance - step,
                    AirsideBareField.MinOrbitDistance,
                    AirsideBareField.MaxOrbitDistance);
            }
        }

        /// <summary>HUD / hotkey: start follow or cycle commercials.</summary>
        public void CycleOrStartFollow()
        {
            if (_followTargets.Length == 0)
            {
                _following = _followTarget != null;
                return;
            }

            if (!_following)
            {
                // Resume the last followed commercial instead of always restarting at 0.
                _following = true;
                _followIndex = Mathf.Clamp(_followIndex, 0, _followTargets.Length - 1);
                _followTarget = _followTargets[_followIndex];
                _hasLastTargetPosition = false;
                return;
            }

            // Already following: cycle through commercials (and wrap).
            _followIndex = (_followIndex + 1) % _followTargets.Length;
            _followTarget = _followTargets[_followIndex];
            _hasLastTargetPosition = false;
        }

        /// <summary>HUD / hotkey: return to the default overview framing.</summary>
        public void ReturnToOverview()
        {
            _following = false;
            _easingOverview = true;
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
            _touchdownShake = 0.7f;
        }

        public bool IsFollowing => _following;
        public Transform FollowTarget => _followTarget;
    }
}
