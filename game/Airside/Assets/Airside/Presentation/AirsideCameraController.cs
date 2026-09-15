using System;
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
        // The release circuit is authored at real YPAD metres. The explicit full-airport
        // QA route retains the older architectural-miniature scene, so it needs its own
        // framing instead of inheriting a 3.1 km overview and appearing almost empty.
        private readonly Vector3 _overviewCenter = AirsideBareField.Enabled
            ? new Vector3(150f, 0f, 350f)
            : new Vector3(12f, 0f, 16f);
        // Review shots: -airsideOverviewYaw / -airsideOverviewPitch / -airsideOverviewDistance
        // re-aim the overview from the command line, so packaged-build screenshots of the
        // surroundings can be repeated from the same place.
        private static float OverviewDistance => CommandLineFloat(
            "-airsideOverviewDistance", AirsideBareField.Enabled ? AirsideBareField.OverviewDistance : 155f);
        private static float OverviewFov => AirsideBareField.Enabled ? AirsideBareField.OverviewFov : 50f;
        private static float OverviewPitch => CommandLineFloat(
            "-airsideOverviewPitch", AirsideBareField.Enabled ? AirsideBareField.OverviewPitch : 38f);
        private static float OverviewYaw => CommandLineFloat(
            "-airsideOverviewYaw", AirsideBareField.Enabled ? AirsideBareField.OverviewYaw : 138f);
        private static string[] _commandLine;

        private static float CommandLineFloat(string flag, float fallback)
        {
            _commandLine ??= System.Environment.GetCommandLineArgs();
            var i = System.Array.IndexOf(_commandLine, flag);
            return i >= 0 && i + 1 < _commandLine.Length
                   && float.TryParse(_commandLine[i + 1], System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out var value)
                ? value
                : fallback;
        }
        private Transform[] _followTargets = System.Array.Empty<Transform>();
        private int _followIndex;
        private Transform _followTarget;
        private Vector3 _center = new(0f, 0f, 0f);
        private float _yaw = OverviewYaw;
        private float _pitch = OverviewPitch;
        private float _distance = OverviewDistance;
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
        /// Set while a HUD text field or panel owns the keyboard, so typing an airline
        /// name does not pan, orbit or lift the camera on A/D/W/S/Q/E/Z/X.
        /// </summary>
        public bool KeyboardCaptured { get; set; }

        /// <summary>
        /// True when a screen point (Input System coordinates, origin bottom-left) is over
        /// a HUD panel, so a click there is left to the HUD instead of starting a drag.
        /// </summary>
        public Func<Vector2, bool> PointerOverHud { get; set; }

        /// <summary>
        /// Fired on a left-button release that never became a drag and did not start over
        /// the HUD. Argument is Input System screen position (origin bottom-left). Used
        /// for direct on-field aircraft selection.
        /// </summary>
        public Action<Vector2> FieldClick { get; set; }

        /// <summary>Pixels a left press must travel before it counts as a drag rather than a click.</summary>
        private static float DragThresholdPixels => AircraftPickRouting.DragThresholdPixels;

        private bool _leftDragArmed;
        private bool _leftDragging;
        private bool _leftPressOnField;
        private Vector2 _leftPressAt;

        /// <summary>
        /// No aircraft covers this much ground in one frame — the fastest phase at 4x
        /// time and 30 fps moves about 4 m. A jump this large means the slot was
        /// recycled: the departure that just flew out has been replaced by a new
        /// arrival joining final at the other end of the field.
        /// </summary>
        private const float RespawnJumpMetres = 20f;

        /// <summary>
        /// Faster than anything drawn on the field moves (a 737 climbing out is ~90 m/s).
        /// A long frame — a GC pause, a save, the Mac waking — legitimately moves an
        /// aircraft this far per second of hitch, so it widens the respawn threshold rather
        /// than cutting the follow camera as if the slot had been recycled.
        /// </summary>
        private const float MaxAircraftSpeedMetresPerSecond = 150f;

        private const float MinPitchDegrees = 4f;
        private const float MaxPitchDegrees = 85f;
        private const float KeyboardOrbitDegreesPerSecond = 70f;

        /// <summary>Never let the camera sink into the airfield at low orbit angles.</summary>
        private const float MinGroundClearanceMetres = 2.5f;

        /// <summary>
        /// Player zoom while following, as a multiplier on the phase framing distance.
        /// Held separately because the follow lerp rewrites <c>_distance</c> every
        /// frame — a raw scroll would be erased before the next one was drawn.
        /// Sticky across follow sessions so a preferred framing survives a toggle.
        /// </summary>
        private float _followZoom = 1f;
        private const float MinFollowZoom = 0.35f;
        private const float MaxFollowZoom = 3.5f;

        /// <summary>Hold off the follow yaw bias briefly so orbit is not fought every frame.</summary>
        private void SuppressFollowOrbit() => _orbitSuppressUntil = Time.unscaledTime + 0.9f;

        public void SetFollowTargets(Transform[] targets)
        {
            var previous = _followTarget;
            var wasFollowing = _following;
            _followTargets = targets ?? System.Array.Empty<Transform>();
            var keep = AircraftPickRouting.IndexOfSame(_followTargets, previous);
            if (keep >= 0)
            {
                _followIndex = keep;
                _followTarget = previous;
                return;
            }

            _followIndex = 0;
            _followTarget = null;
            _hasLastTargetPosition = false;
            if (wasFollowing)
                ReleaseFollow();
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

        // Launch intro: a single eased glide from a low, wide establishing shot into the
        // overview. Input is ignored while it plays; SkipIntro jumps to the end.
        private const float IntroStartDistance = 5600f;
        private const float IntroStartPitch = 16f;
        private const float IntroStartYawOffset = -120f;
        private const float IntroStartFov = 38f;
        private float _introDuration;
        private float _introElapsed;

        public bool IsPlayingIntro => _introElapsed < _introDuration;

        /// <summary>Seconds into the intro, on the same capped clock the glide uses.</summary>
        public float IntroElapsed => _introElapsed;

        public void PlayIntro(float seconds)
        {
            _following = false;
            _easingOverview = false;
            _introDuration = Mathf.Max(0.01f, seconds);
            _introElapsed = 0f;
            ApplyIntroPose(0f);
        }

        public void SkipIntro()
        {
            if (!IsPlayingIntro)
                return;
            _introElapsed = _introDuration;
            ApplyIntroPose(1f);
        }

        private void ApplyIntroPose(float t)
        {
            // Smootherstep: no jolt as the glide begins or settles.
            var e = t * t * t * (t * (t * 6f - 15f) + 10f);
            _center = _overviewCenter;
            _distance = Mathf.Lerp(IntroStartDistance, OverviewDistance, e);
            _pitch = Mathf.Lerp(IntroStartPitch, OverviewPitch, e);
            _yaw = OverviewYaw + Mathf.Lerp(IntroStartYawOffset, 0f, e);
            _fov = Mathf.Lerp(IntroStartFov, OverviewFov, e);
            ApplyTransform();
        }

        private Transform _profileOwner;
        private AircraftVisualProfileComponent _followProfile;

        private void LateUpdate()
        {
            if (IsPlayingIntro)
            {
                // Capped step: the first frames after launch take seconds while the world
                // builds, and an uncapped clock spent the whole glide behind the splash.
                _introElapsed = Mathf.Min(_introDuration, _introElapsed + Mathf.Min(Time.unscaledDeltaTime, 1f / 30f));
                ApplyIntroPose(_introElapsed / _introDuration);
                return;
            }

            ReadInput();
            // A destroyed view (aircraft removed from the schedule) used to leave _following
            // set with no target: WASD panning stayed disabled and the HUD still read
            // "Follow on" while the camera sat still. Hand the camera back instead.
            if (_following && _followTarget == null)
                ReleaseFollow();

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

                if (_profileOwner != _followTarget)
                {
                    // Cached per target: a GetComponent every frame of every follow is waste.
                    _profileOwner = _followTarget;
                    _followProfile = _followTarget.GetComponent<AircraftVisualProfileComponent>();
                }

                var visualProfile = _followProfile;
                var visualCentre = visualProfile != null
                    ? _followTarget.TransformPoint(visualProfile.VisualCentreOffsetMetres)
                    : _followTarget.position;
                var altitude = Mathf.Max(0f, _followTarget.position.y);
                var lookAhead = LookAheadMetres(_followPhase, _followProgress, altitude);
                var lookHeight = LookHeightMetres(_followPhase, altitude);
                var lookPoint = visualCentre + ahead * lookAhead + Vector3.up * lookHeight;

                // A recycled slot puts the new arrival hundreds of metres away in one
                // frame. Easing to it dragged the camera the length of the field, so cut
                // straight there instead.
                var recycled = _hasLastTargetPosition
                    && Vector3.Distance(_lastTargetPosition, visualCentre)
                    > RespawnJumpMetres + MaxAircraftSpeedMetresPerSecond * Time.unscaledDeltaTime;
                _lastTargetPosition = visualCentre;
                _hasLastTargetPosition = true;

                // Follow curves are calibrated to AIR-001's ATR footprint. A type-aware
                // profile keeps a true-size narrowbody in frame without altering the
                // deterministic motion or the player's independent follow zoom.
                var aircraftScale = visualProfile != null
                    ? visualProfile.FollowDistanceMultiplier
                    : 1f;
                var followDistance = FollowDistance(_followPhase, altitude, _followProgress)
                                     * aircraftScale * _followZoom;
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

            var position = _center - rotation * Vector3.forward * _distance + shakeOffset;
            // The orbit now opens up to a near-level pitch, which at close range can
            // put the camera under the airfield. Keep it a readable height above the
            // ground beneath it rather than clamping the angle the player asked for.
            var groundY = AirsideAdelaideGround.WorldHeight(position.x, position.z);
            position.y = Mathf.Max(position.y, groundY + MinGroundClearanceMetres);
            transform.SetPositionAndRotation(position, rotation);
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
                AircraftPhase.Landing => Mathf.Lerp(32f, 20f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, progress))),
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
                AircraftPhase.Landing => Mathf.Lerp(54f, 42f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, progress))),
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
            // Follow and reset-view are owned by AirsidePrototype, which drives them
            // from the HUD bar and its hotkeys. Handling them here as well meant one
            // F press toggled follow off in Update and back on in LateUpdate.
            if (keyboard != null && !KeyboardCaptured)
            {
                var dt = Time.unscaledDeltaTime;

                // Keyboard orbit, for trackpads and anyone not holding a mouse button.
                var keyYaw = 0f;
                if (keyboard.qKey.isPressed) keyYaw -= 1f;
                if (keyboard.eKey.isPressed) keyYaw += 1f;
                if (keyYaw != 0f)
                {
                    _yaw += keyYaw * KeyboardOrbitDegreesPerSecond * dt;
                    SuppressFollowOrbit();
                }

                // Raise / lower the orbit centre. Z and X rather than R or F, which
                // are the reset-view and follow keys.
                var keyLift = 0f;
                if (keyboard.xKey.isPressed) keyLift += 1f;
                if (keyboard.zKey.isPressed) keyLift -= 1f;
                if (keyLift != 0f)
                {
                    _center += Vector3.up * (keyLift * AirsideBareField.OverviewPanMetresPerSecond * 0.5f * dt);
                    _easingOverview = false;
                }

                if (!_following)
                {
                    var move = Vector2.zero;
                    if (keyboard.wKey.isPressed) move.y += 1f;
                    if (keyboard.sKey.isPressed) move.y -= 1f;
                    if (keyboard.dKey.isPressed) move.x += 1f;
                    if (keyboard.aKey.isPressed) move.x -= 1f;
                    if (move != Vector2.zero)
                    {
                        var planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                        var planarRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
                        _center += (planarForward * move.y + planarRight * move.x)
                            * (AirsideBareField.OverviewPanMetresPerSecond * dt);
                        _easingOverview = false;
                    }
                }
            }

            if (mouse == null)
                return;

            if (mouse.rightButton.isPressed)
            {
                var delta = mouse.delta.ReadValue();
                _yaw += delta.x * 0.18f;
                _pitch = Mathf.Clamp(_pitch - delta.y * 0.14f, MinPitchDegrees, MaxPitchDegrees);
                SuppressFollowOrbit();
                _easingOverview = false;
            }

            // Left-drag (Bailey 2026-09-14) and middle-drag slide the view across the
            // field. A left press on a HUD panel stays a click, and a left press only
            // becomes a drag once it has moved a few pixels, so plain clicks still work.
            // A field press that never crosses the drag threshold becomes a FieldClick
            // on release — that is how aircraft are selected in 3D.
            if (mouse.leftButton.wasPressedThisFrame)
            {
                _leftPressAt = mouse.position.ReadValue();
                _leftPressOnField = PointerOverHud == null || !PointerOverHud(_leftPressAt);
                _leftDragArmed = _leftPressOnField;
                _leftDragging = false;
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                if (_leftPressOnField && !_leftDragging
                    && AircraftPickRouting.CountsAsClick(
                        _leftPressAt.x, _leftPressAt.y,
                        mouse.position.ReadValue().x, mouse.position.ReadValue().y))
                    FieldClick?.Invoke(_leftPressAt);
                _leftDragArmed = _leftDragging = _leftPressOnField = false;
            }
            else if (!mouse.leftButton.isPressed)
            {
                _leftDragArmed = _leftDragging = _leftPressOnField = false;
            }
            else if (_leftDragArmed && !_leftDragging
                     && (mouse.position.ReadValue() - _leftPressAt).sqrMagnitude > DragThresholdPixels * DragThresholdPixels)
            {
                _leftDragging = true;
            }

            if (mouse.middleButton.isPressed || _leftDragging)
                PanByPixels(mouse.delta.ReadValue());

            var scroll = mouse.scroll.ReadValue().y;
            // Scrolling a HUD panel (the route map, a list) belongs to that panel, not the camera.
            if (Mathf.Abs(scroll) > 0.01f && (PointerOverHud == null || !PointerOverHud(mouse.position.ReadValue())))
            {
                _easingOverview = false;
                // Queue zoom in log space so every notch is the same proportion whether you
                // are 30 m or 3 km out, cap how much one frame can ask for (trackpads report
                // big pixel deltas), then ease it in below instead of jumping.
                var clamped = Mathf.Clamp(scroll, -MaxScrollPerFrame, MaxScrollPerFrame);
                _zoomPendingLog = Mathf.Clamp(_zoomPendingLog - clamped * ZoomLogPerScrollUnit, -MaxZoomPendingLog, MaxZoomPendingLog);
            }

            ApplyZoomEasing();
        }

        // One mouse-wheel notch (~120 units on macOS) is ~11 % closer or further.
        private const float ZoomLogPerScrollUnit = 0.001f;
        private const float MaxScrollPerFrame = 240f;
        private const float MaxZoomPendingLog = 0.9f;
        private const float ZoomEaseRate = 10f;
        private float _zoomPendingLog;

        private void ApplyZoomEasing()
        {
            if (Mathf.Abs(_zoomPendingLog) < 0.0002f)
            {
                _zoomPendingLog = 0f;
                return;
            }

            var dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            var step = _zoomPendingLog * (1f - Mathf.Exp(-dt * ZoomEaseRate));
            _zoomPendingLog -= step;
            var factor = Mathf.Exp(step);
            if (_following)
            {
                // Following: bias the phase framing instead of setting an absolute
                // distance, which the follow lerp would erase on the next frame.
                _followZoom = Mathf.Clamp(_followZoom * factor, MinFollowZoom, MaxFollowZoom);
            }
            else
            {
                _distance = Mathf.Clamp(_distance * factor,
                    AirsideBareField.MinOrbitDistance,
                    AirsideBareField.MaxOrbitDistance);
            }
        }

        /// <summary>
        /// Slide the view across the field in the plane you are looking along. Panning a
        /// followed aircraft would only fight the follow, so it drops follow and hands the
        /// camera back to you.
        /// </summary>
        private void PanByPixels(Vector2 delta)
        {
            if (delta.sqrMagnitude <= 0.0001f)
                return;
            if (_following)
                ReleaseFollow();
            var planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            var planarRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            // Scale with distance so the drag tracks the ground under the cursor.
            var metresPerPixel = _distance * 0.0016f;
            _center -= (planarRight * delta.x + planarForward * delta.y) * metresPerPixel;
            _easingOverview = false;
        }

        /// <summary>The ground point the camera orbits, for the mini-map's view marker.</summary>
        public Vector3 FocusPoint => _center;

        /// <summary>
        /// Mini-map: slide the orbit centre to a ground position, keeping angle, zoom and
        /// height. Drops follow, like panning does.
        /// </summary>
        public void CentreOn(float x, float z)
        {
            if (_following)
                ReleaseFollow();
            _easingOverview = false;
            _center = new Vector3(x, _center.y, z);
        }

        /// <summary>
        /// HUD / hotkey: stop following and hand the camera back where it is. The
        /// player keeps the current position, angle and zoom and is free to orbit,
        /// pan and zoom from there — turning follow off is not a request to be
        /// dragged back across the field.
        /// </summary>
        public void ReleaseFollow()
        {
            _following = false;
            _easingOverview = false;
            _hasLastTargetPosition = false;
        }

        /// <summary>
        /// HUD / hotkey: explicitly reset to the default overview framing. Separate
        /// from <see cref="ReleaseFollow"/> so ending a follow never forces a view
        /// change the player did not ask for.
        /// </summary>
        public void ReturnToOverview()
        {
            _following = false;
            _easingOverview = true;
        }

        /// <summary>Presentation helper for first-session: frame the lead commercial.</summary>
        public void StartFollowFirst()
        {
            // Target arrays can hold null slots (flights past the visible limit) or views
            // hidden while away; following one of those framed nothing.
            for (var i = 0; i < _followTargets.Length; i++)
            {
                var target = _followTargets[i];
                if (target == null || !target.gameObject.activeInHierarchy)
                    continue;
                _following = true;
                _followIndex = i;
                _followTarget = target;
                _hasLastTargetPosition = false;
                return;
            }
        }

        /// <summary>HUD selection: follow one exact aircraft already registered as a target.</summary>
        public bool StartFollow(Transform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
                return false;

            var index = System.Array.IndexOf(_followTargets, target);
            if (index < 0)
                return false;

            _following = true;
            _followIndex = index;
            _followTarget = target;
            _hasLastTargetPosition = false;
            return true;
        }

        /// <summary>Brief camera shake when a commercial touches down (presentation only).</summary>
        public void PulseTouchdown()
        {
            _touchdownShake = 0.7f;
        }

        // EditMode helpers — same curves as the private follow framing.
        public static float TestLookAheadMetres(AircraftPhase phase, float progress, float altitude) =>
            LookAheadMetres(phase, progress, altitude);

        public static float TestFollowDistance(AircraftPhase phase, float altitude, float progress) =>
            FollowDistance(phase, altitude, progress);

                public bool IsFollowing => _following;
        public Transform FollowTarget => _followTarget;
    }
}
