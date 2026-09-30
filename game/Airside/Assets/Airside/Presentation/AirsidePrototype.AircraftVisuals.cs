using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Airside.Presentation
{
    public sealed partial class AirsidePrototype
    {
        private void UpdateAircraftVisual()
        {
            SyncCommercialAircraftViews();
            for (var index = 0; index < VisualFlights.Count; index++)
            {
                if (index >= _commercialAircraft.Length)
                    break;
                var flight = VisualFlights[index];
                var view = _commercialAircraft[index];
                if (view == null)
                    continue;
                if (FleetMode)
                {
                    var assignedId = index < _commercialAircraftIds.Length
                        ? _commercialAircraftIds[index]
                        : null;
                    if (!PrepareFleetViewForPose(view, assignedId, flight.AircraftId,
                            IsFleetFlightVisible(flight.AircraftId)))
                        continue;
                }
                else if (!view.gameObject.activeSelf)
                {
                    continue;
                }
                var phase = flight.Operation.Phase;
                var progress = VisualPhaseProgress(flight, 0f);
                var aircraftType = AircraftType.Atr42;
                var runway = RunwayDirection.Runway05;
                if (FleetMode && _fleetAircraftById.TryGetValue(flight.AircraftId, out var fleetAircraft))
                {
                    aircraftType = fleetAircraft.Type;
                    runway = fleetAircraft.AssignedRunway;
                }
                var lane = ApproachLaneOffset(flight);
                var route = TaxiRouteFor(flight, phase);
                var position = FleetGroundPosition(flight, 0f)
                    ?? FleetGoAroundWorldPosition(flight, 0f)
                    ?? FleetGoAroundRejoinWorldPosition(flight, route, lane, aircraftType, 0f)
                    ?? FleetArrivalFinalPosition(flight, 0f)
                    ?? RunwayPosition(flight,
                    ApplyDepartureTurn(flight, phase, progress,
                        PositionFor(phase, progress, route, lane, aircraftType, runway)));
                // Keep look-ahead inside the current taxi segment so yaw does not cut corners.
                var lookAhead = phase == AircraftPhase.Takeoff
                        && progress < AirsideFlightPath.LineupProgress ? 0.04f
                    : phase is AircraftPhase.TaxiOut or AircraftPhase.TaxiIn or AircraftPhase.Pushback ? 0.03f
                    : 0.15f;
                var lookAheadProgress = VisualPhaseProgress(flight, lookAhead);
                var next = FleetGroundPosition(flight, lookAhead)
                           ?? FleetGoAroundWorldPosition(flight, lookAhead)
                           ?? FleetGoAroundRejoinWorldPosition(flight, route, lane, aircraftType, lookAhead)
                           ?? FleetArrivalFinalPosition(flight, lookAhead)
                           ?? RunwayPosition(flight,
                               ApplyDepartureTurn(flight, phase, lookAheadProgress,
                                   PositionFor(phase, lookAheadProgress, route, lane, aircraftType, runway)));
                // An arrival cleared earlier than expected eases onto the landing path.
                var handoff = ArrivalHandoffOffset(flight, position);
                position += handoff;
                next += handoff;
                // Fractional phase progress is exact — catch-up lag made some phases slide
                // while airborne phases snapped, which read as inconsistent smoothness.
                view.position = position;

                // Position look-ahead supplies yaw only. AirsideFlightPath.PitchDegrees is the
                // sole pitch owner; including the path's climb here applied pitch twice and
                // made the airframe/follow camera visibly twitch around rotation.
                var direction = AirsideAircraftMotion.HorizontalHeading(
                    FleetGroundFacing(flight, next - position));
                var heading = direction.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(direction)
                    : view.rotation;
                heading = DepartureLookRotation(flight, phase, progress, heading);
                var pitch = PhasePitchDegrees(phase, progress);
                var bank = SmoothedBankDegrees(flight.AircraftId, view, heading, phase,
                    DepartureBankDegrees(flight, phase, progress));
                var targetRotation = heading * Quaternion.Euler(pitch, 0f, bank);
                // Exponential damping keeps the turn rate identical at 30 and 144 fps, and
                // freezes attitude while paused instead of drifting on unscaled time.
                var turningOff = TryDepartureArc(flight, phase, progress, out _, out var turnAlong, out _, out _)
                                 && turnAlong > 0f;
                // Ground heading is already the trailed-gear direction. A slow follow left the
                // fuselage pointing down the taxiway while the nose had entered the turn, so
                // the tail swung out. Follow it closely; the airborne rates stay softer.
                var turnRate = phase is AircraftPhase.TaxiOut or AircraftPhase.TaxiIn or AircraftPhase.Pushback
                    ? 28f
                    : turningOff ? 2.8f
                    : phase == AircraftPhase.Takeoff && progress < AirsideFlightPath.RotateProgress ? 14f : 5f;
                view.rotation = Quaternion.Slerp(
                    view.rotation,
                    targetRotation,
                    AirsideFlightPath.DampFactor(turnRate, PresentationDeltaTime));

                // The pre-pose guard above excludes off-map and temporarily misassigned views.
                // Keep this defensive check in case another presentation pass deactivates the
                // root while its pose is being updated.
                if (!view.gameObject.activeSelf)
                    continue;

                var engines = FleetEngines(flight);
                var viewParts = PartsFor(view);
                if (viewParts.Profile != null)
                    view.position += Vector3.up * AircraftGearPivot.LiftMetres(view.rotation,
                        new Vector3(0f, viewParts.Profile.ModelGroundOffsetMetres, viewParts.MainGearZMetres));
                SpinPropellers(view, viewParts.Propellers, phase, engines, progress);
                SpinJetFans(view, viewParts.FanLeft, viewParts.FanRight, phase, engines, progress);
                UpdateNoseWheelSteering(viewParts.GearNose,
                    FleetNoseWheelSteering(flight, viewParts.WheelbaseMetres), PresentationDeltaTime);
                RollLandingGearTires(view, FleetTireRollSpeed(flight, phase, progress, aircraftType));
                ApplyOleoSettling(view, phase, progress);
                UpdateControlSurfaces(viewParts.ControlSurfaces, phase, progress, bank, PresentationDeltaTime,
                    engines.HasValue);
                UpdateGroundShadow(view);
                UpdateSelectionMarker(view, flight.AircraftId);
                GroundPose? groundPose = TryFleetGround(flight, out var groundAircraft, out var groundVisual)
                    ? FleetGroundPose(groundAircraft, groundVisual, 0f)
                    : null;
                UpdateAircraftLightsAndGear(viewParts.LightsAndGear, phase, PresentationDaylight, progress,
                    PresentationDeltaTime, PresentationClock, engines, groundPose, aircraftType);
                UpdateDistantLight(view, AirsideReusableMotion.LandingLightsOn(phase, progress, engines.HasValue));
                UpdateCabinDoor(viewParts.CabinDoors, phase, engines);
                var glowState = CabinWindowGlowState(phase, PresentationDaylight);
                if (viewParts.CabinWindowGlowState != glowState)
                {
                    UpdateCabinWindowGlow(viewParts.CabinWindowGlass, phase, PresentationDaylight);
                    viewParts.CabinWindowGlowState = glowState;
                    _aircraftViewParts[view.GetInstanceID()] = viewParts;
                }
                UpdateEngineHeat(viewParts.EngineHeatVents, phase, engines,
                    EnginePower(view), PresentationClock);

                if (_cameraController != null
                    && _cameraController.IsFollowing
                    && _cameraController.FollowTarget == view)
                    _cameraController.SetFollowPhase(phase, progress);
            }
        }

        /// <summary>
        /// ADR 0151 — the exhaust behind a running engine, driven by shaft power rather than by a
        /// phase flag. It grows and brightens with power, blooms once at light-off (the puff every
        /// turbine makes when it starts), and cools toward a dull haze at idle. Presentation only.
        /// </summary>
        private static void UpdateEngineHeat(
            (Transform Transform, Renderer Renderer)[] vents, AircraftPhase phase, EngineState? engines,
            float powerFraction, float seconds)
        {
            var anyRunning = engines?.AnyRunning
                ?? (phase != AircraftPhase.AtStand && phase != AircraftPhase.Departed);
            var power = Mathf.Clamp01(powerFraction);
            for (var i = 0; i < vents.Length; i++)
            {
                var child = vents[i].Transform;
                if (child == null)
                    continue;

                // "EngineHeat L" / "EngineHeat R" — each stack follows its own engine.
                var isLeft = child.name.EndsWith("L", StringComparison.Ordinal);
                var running = engines.HasValue
                    ? (isLeft ? engines.Value.Left : engines.Value.Right)
                    : anyRunning ? 1f : 0f;
                var lit = running > AirsidePropellerDynamics.LightOffFraction * 0.92f;
                child.gameObject.SetActive(lit);
                if (!lit)
                    continue;

                // A start throws a short puff of grey-white smoke before it settles to idle; running,
                // the exhaust is a faint haze that thickens a little with power (ADR 0170).
                var lightOff = 1f - Mathf.Clamp01(
                    Mathf.Abs(running - AirsidePropellerDynamics.LightOffFraction) / 0.14f);
                var strength = power * Mathf.Clamp01(running);
                var shimmer = 0.88f + 0.12f * Mathf.Sin(
                    seconds * AirsideReusableMotion.HeatPulseHz * Mathf.PI * 2f + child.GetInstanceID() * 0.01f);
                var size = AirsidePropellerDynamics.ExhaustScale(strength, lightOff);
                child.localScale = new Vector3(size.x * shimmer, size.x * shimmer, size.y);
                var renderer = vents[i].Renderer;
                if (renderer == null)
                    continue;
                var tint = AirsidePropellerDynamics.ExhaustTint(strength, lightOff);
                tint.a *= shimmer;
                SetRendererColor(renderer, tint);
            }
        }

        /// <summary>Shaft power 0..1 last written for this aircraft by the propeller or fan pass.</summary>
        private float EnginePower(Transform aircraft) =>
            _propPower.TryGetValue(aircraft.GetInstanceID(), out var power) ? Mathf.Clamp01(power) : 0f;

        /// <summary>
        /// ADR 0151 — a constant-speed propeller. The governor holds the shaft speed and the
        /// power goes into blade pitch, so the difference between taxi and takeoff is the angle
        /// of the blades and the density of the disc rather than how fast it turns. A shut-down
        /// engine is feathered; a start unfeathers, motors on the starter, lights and is caught
        /// by the governor; a landing rollout goes into reverse.
        /// </summary>
        private void SpinPropellers(Transform aircraft, PropellerPart[] propellers, AircraftPhase phase,
            EngineState? engines = null, float progress01 = 1f)
        {
            if (propellers.Length == 0)
                return;

            var id = aircraft.GetInstanceID();
            var np = AirsidePropellerDynamics.NpFractionForPhase(phase);
            var power = AirsidePropellerDynamics.PowerFractionForPhase(phase, progress01);
            var reverse = phase == AircraftPhase.Landing
                ? AirsidePropellerDynamics.ReverseBlend(progress01)
                : 0f;
            // Fleet aircraft carry a modelled start sequence per engine. Everything else (sky and
            // live traffic) is simply running whenever its phase says the propellers turn.
            var idling = AirsideReusableMotion.PropellersSpinning(phase) ? 1f : 0f;
            var leftRunning = engines?.Left ?? idling;
            var rightRunning = engines?.Right ?? idling;

            var left = SpooledPropRpm(_enginePropRpm, id * 2 + 1, leftRunning, np);
            var right = SpooledPropRpm(_enginePropRpm, id * 2 + 2, rightRunning, np);
            var advance = AirsidePropellerDynamics.Advance01ForPhase(phase, progress01);
            var leftPitch = AirsidePropellerDynamics.BladePitchOffsetDegrees(leftRunning, power, advance, reverse);
            var rightPitch = AirsidePropellerDynamics.BladePitchOffsetDegrees(rightRunning, power, advance, reverse);
            // Audio, exhaust and heat follow power, not shaft speed: under a governor the speed
            // barely moves between taxi and takeoff, so reading power off it would be silent.
            _propRpm[id] = Mathf.Max(left, right);
            _propPower[id] = power * Mathf.Clamp01(Mathf.Max(leftRunning, rightRunning));

            for (var i = 0; i < propellers.Length; i++)
            {
                var child = propellers[i].Transform;
                if (child == null)
                    continue;
                var isLeft = propellers[i].IsLeft;
                SpinOnePropeller(child, isLeft ? left : right, isLeft ? leftPitch : rightPitch);
            }
        }

        /// <summary>
        /// The narrowbody has separate turbofan hub/blade assemblies rather than props.
        /// Keep their presentation parallel to the turboprops: each engine spools on its
        /// own, blades become a restrained intake blur at high power, and the stronger
        /// spool also drives the existing generic engine audio response.
        /// </summary>
        private void SpinJetFans(Transform aircraft, Transform fanLeft, Transform fanRight, AircraftPhase phase,
            EngineState? engines = null, float progress01 = 1f)
        {
            if (fanLeft == null && fanRight == null)
                return;

            // ADR 0151: N1, not an invented fan rpm. A jet idles far lower than people expect and
            // approach idle is lower still, which is why a go-around takes so long to spool up —
            // the lag below is a function of where the spool is starting from.
            var target = AirsidePropellerDynamics.JetN1ForPhase(phase, progress01);
            var id = aircraft.GetInstanceID();
            var leftN1 = SpooledJetN1(id * 2 + 1, target * (engines?.Left ?? 1f));
            var rightN1 = SpooledJetN1(id * 2 + 2, target * (engines?.Right ?? 1f));
            _propRpm[id] = Mathf.Lerp(AirsideReusableMotion.PropRpmTaxi,
                AirsideReusableMotion.PropRpmTakeoff, Mathf.Max(leftN1, rightN1));
            _propPower[id] = Mathf.Max(leftN1, rightN1);

            SpinOneJetFan(fanLeft, leftN1);
            SpinOneJetFan(fanRight, rightN1);
        }

        /// <summary>One turbofan. A shut-down fan stands still.</summary>
        private void SpinOneJetFan(Transform fan, float n1)
        {
            if (fan == null)
                return;

            var rpm = n1 > 0.01f
                ? Mathf.Lerp(0f, AirsideReusableMotion.JetFanRpmTakeoff, n1)
                : AirsidePropellerDynamics.ParkedRpm;
            var dt = PropDeltaTime;
            var step = rpm * 6f * dt;
            // Fan blades wagon-wheel at a far lower speed than propeller blades because there are
            // so many of them; judge the blur the same way, by what one frame can draw.
            var blades = JetFanBladeCount(fan);
            var blur = AirsideReusableMotion.PropBlurForStep(AirsidePropellerDynamics.BlurStepDegrees(rpm, dt), blades);
            ApplyJetFanBlurToHub(fan, blur, DiscViewFade(fan));
            if (step <= 0f)
                return;
            fan.Rotate(Vector3.forward, step, Space.Self);
        }

        private int JetFanBladeCount(Transform fan)
        {
            var id = fan.GetInstanceID();
            if (JetFanBladeCounts.TryGetValue(id, out var cached))
                return cached;
            var count = 0;
            for (var i = 0; i < fan.childCount; i++)
                if (fan.GetChild(i).name.StartsWith("Fan blade", StringComparison.Ordinal))
                    count++;
            cached = Mathf.Clamp(count, 8, 26);
            JetFanBladeCounts[id] = cached;
            return cached;
        }

        /// <summary>
        /// Spool one fan toward its commanded N1 with the lag that spool actually has: slow out of
        /// the idle range, quick at high power, and slower still coming back.
        /// </summary>
        private float SpooledJetN1(int key, float targetN1)
        {
            targetN1 = Mathf.Clamp01(targetN1);
            if (!_jetFanRpm.TryGetValue(key, out var current))
                current = targetN1;
            var seconds = Mathf.Max(0.2f, AirsidePropellerDynamics.JetSpoolSeconds(current, targetN1));
            current = Mathf.Lerp(current, targetN1, AirsideFlightPath.DampFactor(1f / seconds * 2.4f, PropDeltaTime));
            _jetFanRpm[key] = current;
            return current;
        }

        /// <summary>
        /// ADR 0151: shaft speed for one engine, rate limited by the stage it is in. A starter
        /// turns the propeller slowly; light-off accelerates it hard; the governor then holds it
        /// and only trims; a run-down coasts, and feathering the blades brakes it to a stop.
        /// </summary>
        private float SpooledPropRpm(Dictionary<int, float> spools, int key, float engineFraction,
            float operatingNpFraction)
        {
            var target = AirsidePropellerDynamics.EngineRpm(engineFraction, operatingNpFraction);
            if (target > 0f)
                target *= AirsidePropellerDynamics.GovernorHunt(PresentationClock, key * 0.37f);
            else if (engineFraction <= 0.001f)
                // Stopped and feathered: the blades run down and stay still.
                target = AirsidePropellerDynamics.ParkedRpm;

            if (!spools.TryGetValue(key, out var current))
                current = target;
            var dt = PropDeltaTime;
            float upPerSecond, downPerSecond;
            if (AirsidePropellerDynamics.Motoring(engineFraction))
            {
                upPerSecond = AirsideReusableMotion.PropMotoringRpmPerSecond;
                downPerSecond = AirsideReusableMotion.PropMotoringRpmPerSecond;
            }
            else if (engineFraction <= 0.001f)
            {
                upPerSecond = AirsideReusableMotion.PropMotoringRpmPerSecond;
                // A feathered propeller stops far sooner than a windmilling one: the blades brake it.
                downPerSecond = AirsideReusableMotion.PropSpoolDownRpmPerSecond
                    * (current < AirsideReusableMotion.PropRpmGroundIdle * 0.4f ? 3.2f : 1f);
            }
            else if (engineFraction < AirsidePropellerDynamics.GovernorCaptureFraction)
            {
                upPerSecond = AirsideReusableMotion.PropLightOffRpmPerSecond;
                downPerSecond = AirsideReusableMotion.PropSpoolDownRpmPerSecond;
            }
            else
            {
                upPerSecond = AirsideReusableMotion.PropSpoolUpRpmPerSecond;
                downPerSecond = AirsideReusableMotion.PropSpoolDownRpmPerSecond;
            }

            var eased = Mathf.Lerp(current, target, AirsideFlightPath.DampFactor(1.6f, dt));
            current = Mathf.Clamp(eased, current - downPerSecond * dt, current + upPerSecond * dt);
            // The easing only approaches zero; snap the last crawl so a parked propeller really stops.
            if (target <= 0f && current < 0.5f)
                current = 0f;
            spools[key] = current;
            return current;
        }

        /// <summary>
        /// ADR 0148: blades show only while the frame can draw them turning. Past a third of the gap
        /// between blades per frame they wagon-wheel, so they fade into the blur disc. ADR 0168: the
        /// disc is the blades' real time-averaged coverage, so at full power the propeller all but
        /// disappears, leaving the spinner and a faint haze with a tip ring.
        /// </summary>
        private void SpinOnePropeller(Transform propeller, float rpm, float bladePitchOffsetDegrees)
        {
            var dt = PropDeltaTime;
            var id = propeller.GetInstanceID();
            var step = rpm * 6f * dt;
            if (!PropBladeCounts.TryGetValue(id, out var blades))
                PropBladeCounts[id] = blades = CountBlades(propeller);
            ApplyBladePitch(propeller, bladePitchOffsetDegrees);
            var blur = rpm < 1f ? 0f : AirsideReusableMotion.PropBlurForStep(
                AirsidePropellerDynamics.BlurStepDegrees(rpm, dt), blades);
            // A coarse blade puts more of itself in the line of sight than a fine one, and a disc
            // seen edge-on all but disappears. Both are what makes takeoff power read differently
            // from taxi, and the edge-on case costs nothing to draw.
            var density = AirsidePropellerDynamics.DiscPitchDensity(
                bladePitchOffsetDegrees + AirsidePropellerDynamics.AuthoredPitchDegrees);
            ApplyPropBlurToHub(propeller, blur, DiscViewFade(propeller) * density);
            if (step <= 0f)
                return;
            // The disc turns with the propeller: its texture is the same all the way round (ADR 0168).
            propeller.Rotate(Vector3.forward, step, Space.Self);
        }

        /// <summary>URP Unlit, alpha blended, double-sided, over a procedural propeller-blur texture.</summary>
        private static Material PropBlurMaterial(int blades) =>
            BlurDiscMaterial(PropBlurMaterials, blades, $"airside_prop_blur_{blades}", "mat_prop_blur",
                new Color(0.72f, 0.74f, 0.78f, 0.3f), r => AirsidePropellerDynamics.PropDiscAlpha(r, blades),
                _ => Color.white);

        /// <summary>
        /// ADR 0168: a turbofan face at speed, a near-solid dark disc with a faint lighter band where
        /// the blades' twist catches the light, clear over the spinner.
        /// </summary>
        private static Material JetFanBlurMaterial() =>
            BlurDiscMaterial(JetFanBlurMaterials, 0, "airside_fan_blur", "mat_fan_blur",
                new Color(0.2f, 0.23f, 0.26f, 0.9f), AirsidePropellerDynamics.JetFanDiscAlpha,
                r => Color.Lerp(new Color(0.55f, 0.58f, 0.62f), Color.white,
                    Mathf.Exp(-Mathf.Pow((r - 0.62f) / 0.16f, 2f))));

        private static void ApplyPropBlurToHub(Transform propeller, float blend, float discDensity = 1f)
        {
            // Graphics test (ADR 0155): with blur off the blades carry the rotation on their own.
            if (!AirsideSettings.Current.PropellerBlur)
                blend = 0f;
            blend = Mathf.Clamp01(blend);
            var showBlades = blend < 0.92f;
            var selfRenderer = propeller.GetComponent<Renderer>();
            if (selfRenderer != null)
                selfRenderer.enabled = showBlades;

            var alpha = AirsideReusableMotion.PropDiscPeakAlpha * blend * Mathf.Max(0f, discDensity);
            var light = AirsidePropellerDynamics.DiscLightLevel(CurrentDaylight);
            for (var i = 0; i < propeller.childCount; i++)
            {
                var child = propeller.GetChild(i);
                if (child.name == "PropDisc")
                {
                    // Below a visible alpha the disc is a transparent draw for nothing: drop it.
                    child.gameObject.SetActive(alpha > 0.006f);
                    var discRenderer = child.GetComponent<Renderer>();
                    if (discRenderer != null)
                        SetRendererColor(discRenderer, new Color(0.72f * light, 0.74f * light, 0.78f * light, alpha));
                    continue;
                }

                // ADR 0168: the spinner, hub and stripe are solid and stay; only blades blur away.
                var renderer = child.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.enabled = showBlades || !AirsidePropellerDynamics.BlursAtSpeed(child.name);
            }
        }

        private static void ApplyJetFanBlurToHub(Transform fan, float blend, float discDensity = 1f)
        {
            if (!AirsideSettings.Current.PropellerBlur)
                blend = 0f;
            blend = Mathf.Clamp01(blend);
            var showBlades = blend < 0.92f;
            var alpha = AirsideReusableMotion.JetFanDiscPeakAlpha * blend * Mathf.Max(0f, discDensity);
            var light = AirsidePropellerDynamics.DiscLightLevel(CurrentDaylight);
            for (var i = 0; i < fan.childCount; i++)
            {
                var child = fan.GetChild(i);
                if (child.name == "FanDisc")
                {
                    child.gameObject.SetActive(alpha > 0.006f);
                    var discRenderer = child.GetComponent<Renderer>();
                    if (discRenderer != null)
                        SetRendererColor(discRenderer, new Color(0.2f * light, 0.23f * light, 0.26f * light, alpha));
                    continue;
                }

                if (child.name.StartsWith("Fan blade", StringComparison.Ordinal))
                {
                    var renderer = child.GetComponent<Renderer>();
                    if (renderer != null)
                        renderer.enabled = showBlades;
                }
            }
        }

        private void RollLandingGearTires(Transform aircraft, float rollSpeedMetresPerSecond)
        {
            // Distance travelled / radius — stops naturally when ground speed is zero, and
            // turns the other way on the tail-first pushback.
            if (PresentationDeltaTime <= 0f || Mathf.Abs(rollSpeedMetresPerSecond) <= 0.001f)
                return;
            var direction = rollSpeedMetresPerSecond < 0f ? -1f : 1f;
            var groundSpeed = Mathf.Abs(rollSpeedMetresPerSecond);

            var profile = PartsFor(aircraft).Profile;
            var namedChildren8 = AirsideNamedChildren.Get(aircraft);
            var childNames8 = AirsideNamedChildren.Names(aircraft);
            for (var childIndex8 = 0; childIndex8 < namedChildren8.Length; childIndex8++)
            {
                var child = namedChildren8[childIndex8];
                var childName = childNames8[childIndex8];
                if (child == aircraft || !AirsideAircraftParts.RollsInPlace(childName))
                    continue;
                var radius = childName.IndexOf("nose", StringComparison.OrdinalIgnoreCase) >= 0
                    ? profile?.NoseTireRadiusMetres ?? AirsideReusableMotion.NoseTireRadiusMetres
                    : profile?.MainTireRadiusMetres ?? AirsideReusableMotion.MainTireRadiusMetres;
                var degrees = PresentationDeltaTime
                    * AirsideFlightPath.TireAngularDegreesPerSecond(groundSpeed, radius);
                if (degrees > 0f)
                    child.Rotate(Vector3.right, degrees * direction, Space.Self);
            }
        }

        private void SyncCommercialAircraftViews()
        {
            var flights = VisualFlights;
            var needed = flights.Count;

            // Keep each visual glued to its AircraftId across respawn reordering.
            // Count-only rebuild left transforms at stale list indices after Sort.
            // Runs every frame, so the working collections are fields and the id of each
            // view is remembered alongside it rather than parsed back out of its name.
            var byId = _syncViewsById;
            byId.Clear();
            if (_commercialAircraft != null)
            {
                for (var i = 0; i < _commercialAircraft.Length; i++)
                {
                    var existing = _commercialAircraft[i];
                    if (existing != null && i < _commercialAircraftIds.Length && _commercialAircraftIds[i] != null)
                        byId[_commercialAircraftIds[i]] = existing;
                }
            }

            // Drop slot reservations for aircraft that have left the schedule.
            var liveIds = _syncLiveIds;
            liveIds.Clear();
            foreach (var flight in flights)
                liveIds.Add(flight.AircraftId);
            var staleSlots = _syncStaleSlots;
            staleSlots.Clear();
            foreach (var pair in _commercialLiverySlot)
            {
                if (!liveIds.Contains(pair.Key))
                    staleSlots.Add(pair.Key);
            }
            foreach (var id in staleSlots)
                _commercialLiverySlot.Remove(id);

            var usedSlots = _syncUsedSlots;
            usedSlots.Clear();
            foreach (var pair in _commercialLiverySlot)
                usedSlots.Add(pair.Value);

            if (_syncNextViews.Length != needed)
            {
                _syncNextViews = new Transform[needed];
                _syncNextIds = new string[needed];
            }

            var next = _syncNextViews;
            var nextIds = _syncNextIds;
            var kept = _syncKept;
            kept.Clear();
            var visibleLimit = AirsideFocusMode.VisibleCommercialFlights;
            for (var index = 0; index < needed; index++)
            {
                var flight = flights[index];
                var visible = FleetMode ? IsFleetFlightVisible(flight.AircraftId) : index < visibleLimit;

                // Assign a stable livery slot: reuse this aircraft's slot, else take
                // the lowest slot no other current aircraft holds.
                if (!_commercialLiverySlot.TryGetValue(flight.AircraftId, out var slot))
                {
                    slot = 0;
                    while (usedSlots.Contains(slot))
                        slot++;
                    usedSlots.Add(slot);
                    _commercialLiverySlot[flight.AircraftId] = slot;
                }

                nextIds[index] = flight.AircraftId;
                if (byId.TryGetValue(flight.AircraftId, out var existing))
                {
                    if (FleetMode)
                        RefreshFreighterLivery(existing, flight.AircraftId);
                    next[index] = existing;
                    kept.Add(existing);
                    existing.gameObject.SetActive(visible);
                    continue;
                }

                if (FleetMode)
                {
                    // Fleet aircraft are built once and hidden while away, so a return
                    // from a two-hour leg does not reload the model mid-approach.
                    next[index] = BuildFleetAircraft(flight.AircraftId);
                    next[index].gameObject.SetActive(visible);
                    continue;
                }

                if (!visible)
                {
                    next[index] = null;
                    continue;
                }

                // Coastline Regional v06 turboprop — the smooth reference airframe.
                var color = new Color(0.12f, 0.43f, 0.76f);
                var livery = "Textures/Decals/dc_livery_coastline_regional_v01.png";
                next[index] = BuildAircraft($"Commercial {flight.AircraftId}", color, livery);
            }

            foreach (var pair in byId)
            {
                if (!kept.Contains(pair.Value) && pair.Value != null)
                {
                    AirsideNamedChildren.Forget(pair.Value);
                    ForgetAircraftViewParts(pair.Value);
                    Destroy(pair.Value.gameObject);
                }
            }

            var changed = _commercialAircraft == null || _commercialAircraft.Length != next.Length;
            if (!changed)
            {
                for (var i = 0; i < next.Length; i++)
                {
                    if (_commercialAircraft[i] != next[i])
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (changed)
            {
                // Swap buffers: the old arrays become next frame's scratch when sizes match.
                var previousViews = _commercialAircraft;
                var previousIds = _commercialAircraftIds;
                _commercialAircraft = next;
                _commercialAircraftIds = nextIds;
                _syncNextViews = previousViews != null && previousViews.Length == needed ? previousViews : new Transform[needed];
                _syncNextIds = previousIds.Length == needed ? previousIds : new string[needed];
            }
            else
            {
                Array.Copy(nextIds, _commercialAircraftIds, needed);
            }

            next = _commercialAircraft;
            if (FleetMode)
            {
                RefreshFleetFollowTargets(next);
                return;
            }

            if (changed && needed > 0 && _cameraController != null)
            {
                var follow = next.Where(t => t != null && t.gameObject.activeSelf).ToArray();
                if (follow.Length > 0)
                    _cameraController.SetFollowTargets(follow);
            }
        }

        private void UpdateTouchdownSmoke()
        {
            if (_touchdownSmoke == null)
                return;

            for (var index = 0; index < VisualFlights.Count; index++)
            {
                var flight = VisualFlights[index];
                var phase = flight.Operation.Phase;
                var id = flight.AircraftId;
                // Flights past the visible limit have no view; a hidden fleet aircraft has
                // nothing on screen to smoke or sound. Either used to dereference null.
                var view = index < _commercialAircraft.Length ? _commercialAircraft[index] : null;
                var hasView = view != null && view.gameObject.activeInHierarchy;

                // Fire once when the visual path actually meets the runway — not at the
                // Approach→Landing phase change (that is still ~1.5 m AGL after the path fix).
                if (phase == AircraftPhase.Landing
                    && hasView
                    && !_touchdownFired.Contains(id)
                    && VisualPhaseProgress(flight, 0f) >= AirsideFlightPath.TouchdownProgress)
                {
                    _touchdownFired.Add(id);
                    TryGetMainGearContacts(_commercialAircraft[index], out var smokeLeft, out var smokeRight);
                    var smokeAt = (smokeLeft + smokeRight) * 0.5f;
                    smokeAt.y = AirsideFlightPath.GroundY;
                    _touchdownSmoke.position = smokeAt + Vector3.up * 0.15f;
                    _touchdownSmoke.rotation = _commercialAircraft[index].rotation;
                    _touchdownSmoke.localScale = Vector3.one * 1.35f;
                    for (var p = 0; p < _touchdownSmoke.childCount; p++)
                    {
                        var puff = _touchdownSmoke.GetChild(p);
                        var side = p % 2 == 0
                            ? -AirsideReusableMotion.MainGearHalfTrackMetres
                            : AirsideReusableMotion.MainGearHalfTrackMetres;
                        var aft = -0.15f * (p / 2);
                        puff.localPosition = new Vector3(side, 0.12f, aft);
                    }

                    _touchdownSmoke.gameObject.SetActive(true);
                    _touchdownSmokeRemaining = 1.35f;
                    SpawnSkidMarks(_commercialAircraft[index]);
                    EmitTouchdownWheelSmoke(_commercialAircraft[index], flight, phase);

                    if (_cameraController != null)
                        _cameraController.PulseTouchdown();
                }
                else if (phase != AircraftPhase.Landing)
                {
                    _touchdownFired.Remove(id);
                }

                // Rolling trail: after the wheels are down the tread keeps smoking
                // until the rollout has scrubbed most of the speed off.
                if (phase == AircraftPhase.Landing
                    && hasView
                    && _touchdownFired.Contains(id))
                {
                    UpdateRollingWheelSmoke(_commercialAircraft[index], flight, phase);
                }

                // Soft rotate cue once the visual path lifts — presentation only.
                if (phase == AircraftPhase.Takeoff
                    && hasView
                    && !_rotateFired.Contains(id)
                    && VisualPhaseProgress(flight, 0f) >= AirsideFlightPath.RotateProgress)
                {
                    _rotateFired.Add(id);
                }
                else if (phase != AircraftPhase.Takeoff)
                {
                    _rotateFired.Remove(id);
                }

                _previousPhases[id] = phase;
            }

            if (_touchdownSmokeRemaining <= 0f)
            {
                _touchdownSmoke.gameObject.SetActive(false);
            }
            else
            {
                _touchdownSmokeRemaining -= Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(_touchdownSmokeRemaining / 1.35f);
                var n = _touchdownSmoke.childCount;
                if (_touchdownSmokeRenderers == null || _touchdownSmokeRenderers.Length != n)
                {
                    _touchdownSmokeRenderers = new Renderer[n];
                    for (var i = 0; i < n; i++)
                        _touchdownSmokeRenderers[i] = _touchdownSmoke.GetChild(i).GetComponent<Renderer>();
                }

                for (var i = 0; i < n; i++)
                {
                    var puff = _touchdownSmoke.GetChild(i);
                    puff.localScale = Vector3.Lerp(new Vector3(2.8f, 0.25f, 2.8f), new Vector3(1.0f, 0.35f, 1.0f), t);
                    puff.localPosition += Vector3.up * (Time.unscaledDeltaTime * 0.35f);
                    var renderer = _touchdownSmokeRenderers[i];
                    if (renderer != null)
                    {
                        var color = GetRendererColor(renderer);
                        color.a = t * 0.5f;
                        SetRendererColor(renderer, color);
                    }
                }
            }

            UpdateSkidMarks();
        }

        private void SpawnSkidMarks(Transform aircraft)
        {
            if (_skidMarkRoot == null || aircraft == null)
                return;

            // Two dark rubber streaks under main gear — fade over ~22s (presentation only).
            TryGetMainGearContacts(aircraft, out var leftContact, out var rightContact);
            for (var i = 0; i < 2; i++)
            {
                var mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mark.name = "Skid mark";
                DestroyPresentationObject(mark.GetComponent<Collider>());
                mark.transform.SetParent(_skidMarkRoot, false);
                // Under the real tyres (the root is the nose datum on the jets), on the tarmac.
                var contact = i == 0 ? leftContact : rightContact;
                contact.y = AirsideFlightPath.GroundY;
                mark.transform.position = contact
                    + aircraft.forward * -0.4f
                    + Vector3.up * 0.04f;
                var fwd = Vector3.ProjectOnPlane(aircraft.forward, Vector3.up);
                if (fwd.sqrMagnitude < 0.0001f)
                    fwd = Vector3.forward;
                mark.transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
                mark.transform.localScale = new Vector3(0.22f, 0.02f, 3.6f);
                var markRenderer = mark.GetComponent<Renderer>();
                markRenderer.sharedMaterial = CreateMaterial(new Color(0.12f, 0.11f, 0.1f, 0.7f));
                SetRendererColor(markRenderer, new Color(0.12f, 0.11f, 0.1f, 0.7f));
            }
        }

        private void UpdateSkidMarks()
        {
            if (_skidMarkRoot == null)
                return;

            for (var i = _skidMarkRoot.childCount - 1; i >= 0; i--)
            {
                var mark = _skidMarkRoot.GetChild(i);
                var renderer = mark.GetComponent<Renderer>();
                if (renderer == null)
                {
                    DestroyPresentationObject(mark.gameObject);
                    continue;
                }

                var color = GetRendererColor(renderer);
                color.a -= Time.unscaledDeltaTime / 22f;
                if (color.a <= 0.02f)
                {
                    DestroyPresentationObject(mark.gameObject);
                    continue;
                }

                SetRendererColor(renderer, color);
                // Stretch slightly as the mark ages so it reads as a rollout streak.
                var scale = mark.localScale;
                scale.z = Mathf.MoveTowards(scale.z, 5.2f, Time.unscaledDeltaTime * 0.08f);
                mark.localScale = scale;
            }
        }

        private static Transform BuildTouchdownSmoke()
        {
            if (ArtPresentationLoader.TryInstantiatePrefab("vfx_touchdown_smoke_v01", out var kit))
            {
                kit.name = "Touchdown smoke";
                kit.gameObject.SetActive(false);
                return kit;
            }

            var root = new GameObject("Touchdown smoke").transform;
            for (var i = 0; i < 4; i++)
            {
                var smoke = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                smoke.name = i % 2 == 0 ? "Smoke L" : "Smoke R";
                smoke.transform.SetParent(root, false);
                var side = i % 2 == 0 ? -0.75f : 0.75f;
                smoke.transform.localPosition = new Vector3(side, 0.12f, -0.15f * (i / 2));
                smoke.transform.localScale = new Vector3(1.1f, 0.35f, 1.1f);
                var smokeRenderer = smoke.GetComponent<Renderer>();
                smokeRenderer.sharedMaterial = CreateMaterial(new Color(0.85f, 0.85f, 0.88f, 0.4f));
                SetRendererColor(smokeRenderer, new Color(0.85f, 0.85f, 0.88f, 0.4f));
                var collider = smoke.GetComponent<Collider>();
                if (collider != null)
                    DestroyPresentationObject(collider);
            }

            root.gameObject.SetActive(false);
            return root;
        }

        /// <summary>
        /// Pool of tyre-smoke puffs, all inactive until the wheels touch. Pooled
        /// rather than spawned so a long rollout never allocates per frame.
        /// </summary>
        private void BuildWheelSmoke()
        {
            const int poolSize = 24;
            var root = new GameObject("Tyre smoke").transform;
            root.SetParent(transform, false);
            var smokeMaterial = CreateMaterial(WheelSmokeColor);
            _wheelPuffs = new WheelPuff[poolSize];

            for (var i = 0; i < poolSize; i++)
            {
                var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                puff.name = $"Tyre puff {i + 1}";
                puff.transform.SetParent(root, false);
                var collider = puff.GetComponent<Collider>();
                if (collider != null)
                    DestroyPresentationObject(collider);

                var renderer = puff.GetComponent<Renderer>();
                // One shared material for the pool: per-puff alpha rides on a
                // MaterialPropertyBlock via SetRendererColor, so no instancing.
                renderer.sharedMaterial = smokeMaterial;
                puff.SetActive(false);

                _wheelPuffs[i] = new WheelPuff
                {
                    Transform = puff.transform,
                    Renderer = renderer
                };
            }
        }

        /// <summary>
        /// The hard puff as stationary tyres are slammed up to ground speed. Both
        /// mains light at once; the nose is still in the air at this point.
        /// </summary>
        private void EmitTouchdownWheelSmoke(Transform aircraft, CommercialFlight flight, AircraftPhase phase)
        {
            if (_wheelPuffs == null || aircraft == null)
                return;

            TryGetMainGearContacts(aircraft, out var left, out var right);
            var aft = -aircraft.forward;
            var type = FleetMode && _fleetAircraftById.TryGetValue(flight.AircraftId, out var fleetAircraft)
                ? fleetAircraft.Type : AircraftType.Atr42;
            var speed = AirsideFlightPath.GroundSpeedMetresPerSecond(phase, VisualPhaseProgress(flight, 0f), type);
            // Touchdown speed is the top of the range, so this is near full strength.
            var strength = Mathf.Clamp01(speed / TouchdownSmokeReferenceSpeed);

            for (var i = 0; i < 5; i++)
            {
                EmitWheelSmoke(left, aft, strength);
                EmitWheelSmoke(right, aft, strength);
            }

            _wheelSmokeEmitCooldown = 0f;
        }

        /// <summary>
        /// The thinning trail behind the mains during the rollout. Emission rate and
        /// puff strength both fall with ground speed, so the smoke dies away as the
        /// aircraft brakes rather than stopping abruptly.
        /// </summary>
        private void UpdateRollingWheelSmoke(Transform aircraft, CommercialFlight flight, AircraftPhase phase)
        {
            if (_wheelPuffs == null || aircraft == null)
                return;

            var type = FleetMode && _fleetAircraftById.TryGetValue(flight.AircraftId, out var fleetAircraft)
                ? fleetAircraft.Type : AircraftType.Atr42;
            var speed = AirsideFlightPath.GroundSpeedMetresPerSecond(phase, VisualPhaseProgress(flight, 0f), type);
            var strength = Mathf.Clamp01(speed / TouchdownSmokeReferenceSpeed);
            if (strength <= 0.18f)
                return;

            _wheelSmokeEmitCooldown -= Time.unscaledDeltaTime;
            if (_wheelSmokeEmitCooldown > 0f)
                return;

            // Fast tread smokes more often; the gap stretches out as speed bleeds off.
            _wheelSmokeEmitCooldown = Mathf.Lerp(0.22f, 0.04f, strength);

            TryGetMainGearContacts(aircraft, out var left, out var right);
            var aft = -aircraft.forward;
            // Trail puffs are softer than the touchdown burst.
            var trail = strength * 0.55f;
            EmitWheelSmoke(left, aft, trail);
            EmitWheelSmoke(right, aft, trail);
        }

        /// <summary>
        /// Ground contact patches of the main gear, taken from the real tyre
        /// transforms now that they sit on their axles. Falls back to the authored
        /// half-track when the kit did not load.
        /// </summary>
        private static bool TryGetMainGearContacts(Transform aircraft, out Vector3 left, out Vector3 right)
        {
            var leftSum = Vector3.zero;
            var rightSum = Vector3.zero;
            var leftCount = 0;
            var rightCount = 0;

            // Drop from axle to tread by this type's own main-tyre radius: the 737's 0.62 m
            // mains sat puffs a quarter-metre inside the tyre on the shared ATR default.
            var profile = aircraft.GetComponent<AircraftVisualProfileComponent>();
            var tyreRadius = profile != null
                ? profile.MainTireRadiusMetres
                : AirsideReusableMotion.MainTireRadiusMetres;
            var namedChildren15 = AirsideNamedChildren.Get(aircraft);
            var childNames15 = AirsideNamedChildren.Names(aircraft);
            for (var childIndex15 = 0; childIndex15 < namedChildren15.Length; childIndex15++)
            {
                var child = namedChildren15[childIndex15];
                var childName = childNames15[childIndex15];
                if (child == aircraft)
                    continue;
                if (!childName.StartsWith("Tire", StringComparison.Ordinal))
                    continue;
                if (childName.IndexOf("nose", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                // The axle is the transform origin after the rebake; drop to the tread.
                var contact = child.position - Vector3.up * tyreRadius;
                if (childName.IndexOf(" L", StringComparison.Ordinal) >= 0)
                {
                    leftSum += contact;
                    leftCount++;
                }
                else if (childName.IndexOf(" R", StringComparison.Ordinal) >= 0)
                {
                    rightSum += contact;
                    rightCount++;
                }
            }

            if (leftCount > 0 && rightCount > 0)
            {
                left = leftSum / leftCount;
                right = rightSum / rightCount;
                return true;
            }

            // Primitive fallback silhouette: no named tyres, so use the authored track.
            var half = aircraft.right * AirsideReusableMotion.MainGearHalfTrackMetres;
            var ground = aircraft.position;
            ground.y = AirsideFlightPath.GroundY;
            left = ground - half;
            right = ground + half;
            return false;
        }

        /// <summary>
        /// Light one pooled puff at <paramref name="position"/>. <paramref name="strength"/>
        /// runs 0..1 and drives size, opacity and how far the puff climbs.
        /// </summary>
        private void EmitWheelSmoke(Vector3 position, Vector3 aftDrift, float strength)
        {
            if (_wheelPuffs == null)
                return;

            for (var i = 0; i < _wheelPuffs.Length; i++)
            {
                if (_wheelPuffs[i].Transform == null || _wheelPuffs[i].Transform.gameObject.activeSelf)
                    continue;

                var puff = _wheelPuffs[i];
                var spread = 0.35f * strength;
                puff.Transform.position = position + new Vector3(
                    UnityEngine.Random.Range(-spread, spread),
                    UnityEngine.Random.Range(0.02f, 0.14f),
                    UnityEngine.Random.Range(-spread, spread));
                puff.Age = 0f;
                puff.Life = Mathf.Lerp(0.45f, 1.5f, strength);
                // Kicked backwards off the tread, rising as it expands.
                puff.Drift = aftDrift * Mathf.Lerp(1.5f, 6.0f, strength)
                             + Vector3.up * Mathf.Lerp(0.25f, 0.9f, strength);
                puff.StartRadius = Mathf.Lerp(0.18f, 0.42f, strength);
                puff.EndRadius = Mathf.Lerp(0.9f, 2.6f, strength);
                puff.StartAlpha = Mathf.Lerp(0.18f, 0.5f, strength);
                puff.Transform.localScale = Vector3.one * puff.StartRadius;
                puff.Transform.gameObject.SetActive(true);

                if (puff.Renderer != null)
                {
                    var color = WheelSmokeColor;
                    color.a = puff.StartAlpha;
                    SetRendererColor(puff.Renderer, color);
                }

                _wheelPuffs[i] = puff;
                return;
            }
        }

        /// <summary>
        /// Age every live puff: expand, drift, fade, then return it to the pool.
        /// Runs on unscaled time so a puff stays a puff at 4x rather than stretching
        /// across the whole rollout, and freezes with the rest of the presentation.
        /// </summary>
        /// <summary>Kill every live puff — used when the circuit is restarted.</summary>
        private void ClearWheelSmoke()
        {
            if (_wheelPuffs == null)
                return;

            for (var i = 0; i < _wheelPuffs.Length; i++)
            {
                if (_wheelPuffs[i].Transform != null)
                    _wheelPuffs[i].Transform.gameObject.SetActive(false);
            }

            _wheelSmokeEmitCooldown = 0f;
        }

        private void UpdateWheelSmoke()
        {
            if (_wheelPuffs == null)
                return;

            var dt = Time.unscaledDeltaTime;
            for (var i = 0; i < _wheelPuffs.Length; i++)
            {
                var puff = _wheelPuffs[i];
                if (puff.Transform == null || !puff.Transform.gameObject.activeSelf)
                    continue;

                puff.Age += dt;
                var t = Mathf.Clamp01(puff.Age / puff.Life);
                if (t >= 1f)
                {
                    puff.Transform.gameObject.SetActive(false);
                    _wheelPuffs[i] = puff;
                    continue;
                }

                puff.Transform.position += puff.Drift * dt;
                // Slow the drift as the puff loses its kick.
                puff.Drift = Vector3.Lerp(puff.Drift, Vector3.up * 0.2f, dt * 1.6f);
                puff.Transform.localScale = Vector3.one * Mathf.Lerp(puff.StartRadius, puff.EndRadius, t);

                if (puff.Renderer != null)
                {
                    var color = WheelSmokeColor;
                    // Hold briefly, then fade out — smoke thins rather than blinking off.
                    color.a = puff.StartAlpha * (1f - t * t);
                    SetRendererColor(puff.Renderer, color);
                }

                _wheelPuffs[i] = puff;
            }
        }

        private static void PlaceBirdWing(Transform bird, string name, Vector3 localPos, bool left)
        {
            var wing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wing.name = name;
            DestroyPresentationObject(wing.GetComponent<Collider>());
            wing.transform.SetParent(bird, false);
            wing.transform.localPosition = localPos;
            wing.transform.localScale = new Vector3(0.55f, 0.02f, 0.14f);
            var wingRenderer = wing.GetComponent<Renderer>();
            wingRenderer.sharedMaterial = AirsideMaterialLibrary.CreateShared(
                new Color(0.18f, 0.18f, 0.2f),
                AirsideMaterialLibrary.SurfaceKind.Plastic);
            wingRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Pivot hint stored as unused local euler y sign for flap direction.
            wing.transform.localEulerAngles = new Vector3(0f, left ? -8f : 8f, 0f);
        }

        /// <summary>
        /// Type-aware visual dispatch. Domain/simulation remains the owner of what an
        /// aircraft is; presentation only selects the matching silhouette and metrics.
        /// </summary>
        private static Transform BuildAircraftForType(
            string name,
            AircraftType type,
            Color accent,
            string liveryDecalRelativePath = null)
        {
            if (AircraftVisualProfiles.IsBoeing7378(type))
                return BuildNarrowbody7378(name, accent, liveryDecalRelativePath);
            if (AircraftVisualProfiles.IsBoeing737800(type))
                return BuildNarrowbody7378(name, accent, liveryDecalRelativePath, AircraftVisualProfiles.Boeing737800);
            if (AircraftVisualProfiles.IsAirbusA320200(type))
                return BuildNarrowbody7378(name, accent, liveryDecalRelativePath, AircraftVisualProfiles.AirbusA320200);
            if (AircraftVisualProfiles.IsEmbraerE190(type))
                return BuildNarrowbody7378(name, accent, liveryDecalRelativePath, AircraftVisualProfiles.EmbraerE190);
            if (AircraftVisualProfiles.IsAirbusA220300(type))
                return BuildNarrowbody7378(name, accent, liveryDecalRelativePath, AircraftVisualProfiles.AirbusA220300);
            if (AircraftVisualProfiles.IsAirbusA321Neo(type))
                return BuildNarrowbody7378(name, accent, liveryDecalRelativePath, AircraftVisualProfiles.AirbusA321Neo);
            if (AircraftVisualProfiles.IsAirbusA350900(type))
                return BuildNarrowbody7378(name, accent, liveryDecalRelativePath, AircraftVisualProfiles.AirbusA350900);
            if (AircraftVisualProfiles.IsBoeing78710(type))
                return BuildNarrowbody7378(name, accent, liveryDecalRelativePath, AircraftVisualProfiles.Boeing78710);
            if (AircraftVisualProfiles.IsAirbusA330900(type))
                return BuildNarrowbody7378(name, accent, liveryDecalRelativePath, AircraftVisualProfiles.AirbusA330900);
            if (AircraftVisualProfiles.IsBoeing7879(type))
                return BuildNarrowbody7378(name, accent, liveryDecalRelativePath, AircraftVisualProfiles.Boeing7879);
            if (AircraftVisualProfiles.IsDash8Q400(type))
                return BuildDash8Q400(name, accent, liveryDecalRelativePath);
            if (AircraftVisualProfiles.IsSaab340(type))
                return BuildSaab340(name, accent, liveryDecalRelativePath);

            var regional = BuildAircraft(name, accent, liveryDecalRelativePath);
            AircraftVisualProfileComponent.Ensure(regional, AircraftVisualProfiles.RegionalTurboprop);
            return regional;
        }

        private static Transform BuildAircraft(string name, Color accent, string liveryDecalRelativePath = null)
        {
            var root = new GameObject(name).transform;
            // Production AIR-001: true-size ATR 42-class starter aircraft. Motion roots
            // use y=0.7f, so offset the metre-authored kit to put its tires on the ground.
            // v06 remains a safe fallback for branches/builds that have not imported it yet.
            var aircraftArt = PreferArtKit(
                "Models/Aircraft/mdl_atr42_starter_v03.gltf",
                PreferArtKit("Models/Aircraft/mdl_atr42_starter_v02.gltf",
                    PreferArtKit("Models/Aircraft/mdl_atr42_starter_v01.gltf",
                        "Models/Aircraft/mdl_regional_turboprop_01_v06.gltf")));
            var finalAtr42 = aircraftArt.EndsWith("mdl_atr42_starter_v03.gltf", StringComparison.Ordinal)
                || aircraftArt.EndsWith("mdl_atr42_starter_v02.gltf", StringComparison.Ordinal)
                || aircraftArt.EndsWith("mdl_atr42_starter_v01.gltf", StringComparison.Ordinal);
            var usedArt = ArtPresentationLoader.TryInstantiate(
                aircraftArt,
                root,
                out _,
                RenameAircraftPart,
                kitName => Atr42PartColor(kitName, accent),
                localPosition: new Vector3(0f, -0.7f, 0f));

            if (usedArt)
            {
                NestCrossPropellerBlades(root);
                // glTF kits author prop verts at nacelle world positions while the
                // Propeller transform sits at the kit origin — rebake so spin stays on-hub.
                RebakePropellerPivots(root);
                RebakeAircraftArticulatedPivots(root);
                NestLandingGearParts(root);
                // Tyre / wheel / rim meshes are baked at aircraft-space position with
                // the node at the kit origin, so a naive spin sweeps them around the
                // fuselage centreline. Rebake each to its axle so the ground roll turns
                // them in place — the landing-gear mirror of RebakePropellerPivots.
                RebakeWheelPivots(root);
                if (finalAtr42)
                    RelocateAtrDoors(root);
                NestCabinDoorParts(root);
                ConvertToAirstairDoor(root);
                NestFlapParts(root);
                NestWingMountedParts(root);
                AirsideAircraftRenderBatcher.CombineStaticGlazing(root);
                if (finalAtr42)
                {
                    EnsureAircraftLod(root);
                }
            }

            if (!usedArt)
            {
                // Batch C turboprop silhouette with separated props/engines/gear (primitive fallback).
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Fuselage";
                body.transform.SetParent(root, false);
                body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                body.transform.localScale = new Vector3(0.72f, 2.8f, 0.72f);
                body.GetComponent<Renderer>().sharedMaterial = CreateMaterial(new Color(0.93f, 0.95f, 0.97f));
                ParentBlock(root, "Livery stripe", new Vector3(0f, 0.12f, 0.05f), new Vector3(0.76f, 0.08f, 2.2f), accent);
                ParentBlock(root, "Wing L", new Vector3(-2.1f, 0.05f, 0.35f), new Vector3(3.6f, 0.12f, 1.5f), accent);
                ParentBlock(root, "Wing R", new Vector3(2.1f, 0.05f, 0.35f), new Vector3(3.6f, 0.12f, 1.5f), accent);
                ParentBlock(root, "Engine L", new Vector3(-1.35f, -0.05f, 0.85f), new Vector3(0.45f, 0.45f, 1.1f), accent * 0.85f);
                ParentBlock(root, "Engine R", new Vector3(1.35f, -0.05f, 0.85f), new Vector3(0.45f, 0.45f, 1.1f), accent * 0.85f);
                ParentBlock(root, "Propeller L", new Vector3(-1.35f, -0.05f, 1.45f), new Vector3(0.08f, 1.35f, 0.18f), new Color(0.2f, 0.2f, 0.22f));
                ParentBlock(root, "Propeller R", new Vector3(1.35f, -0.05f, 1.45f), new Vector3(0.08f, 1.35f, 0.18f), new Color(0.2f, 0.2f, 0.22f));
                ParentBlock(root, "Tail", new Vector3(0f, 0.85f, -2.15f), new Vector3(0.14f, 1.5f, 1.0f), accent);
                ParentBlock(root, "Tailplane", new Vector3(0f, 0.55f, -2.2f), new Vector3(2.2f, 0.1f, 0.7f), accent);
                ParentBlock(root, "Gear nose", new Vector3(0f, -0.55f, 1.5f), new Vector3(0.12f, 0.45f, 0.28f), new Color(0.25f, 0.25f, 0.28f));
                ParentBlock(root, "Gear L", new Vector3(-0.7f, -0.55f, -0.2f), new Vector3(0.12f, 0.45f, 0.32f), new Color(0.25f, 0.25f, 0.28f));
                ParentBlock(root, "Gear R", new Vector3(0.7f, -0.55f, -0.2f), new Vector3(0.12f, 0.45f, 0.32f), new Color(0.25f, 0.25f, 0.28f));
                ParentBlock(root, "CabinDoor", new Vector3(0.55f, 0.05f, 0.35f), new Vector3(0.08f, 0.85f, 0.55f), new Color(0.78f, 0.8f, 0.83f));
            }

            ApplyLiveryDecal(root, liveryDecalRelativePath);
            EnsurePropDiscs(root);
            EnsureGroundShadow(root);
            // Only inject lamp / heat proxies when the authored kit did not already ship them.
            if (!HasNamedChild(root, "NavLight L"))
                ParentBlock(root, "NavLight L", new Vector3(-3.7f, 0.08f, 0.2f), new Vector3(0.12f, 0.12f, 0.12f), new Color(0.1f, 0.9f, 0.2f));
            if (!HasNamedChild(root, "NavLight R"))
                ParentBlock(root, "NavLight R", new Vector3(3.7f, 0.08f, 0.2f), new Vector3(0.12f, 0.12f, 0.12f), new Color(0.9f, 0.12f, 0.12f));
            if (!HasNamedChild(root, "Beacon"))
                ParentBlock(root, "Beacon", new Vector3(0f, 0.85f, 0.2f), new Vector3(0.14f, 0.14f, 0.14f), new Color(0.95f, 0.2f, 0.15f));
            if (!HasNamedChild(root, "LandingLight") && !HasNamedChild(root, "LandingLight L"))
                ParentBlock(root, "LandingLight", new Vector3(0f, -0.15f, 2.5f), new Vector3(0.18f, 0.12f, 0.2f), new Color(0.95f, 0.95f, 0.85f));
            if (!HasNamedChild(root, "TaxiLight"))
                ParentBlock(root, "TaxiLight", new Vector3(0f, -0.2f, 2.2f), new Vector3(0.14f, 0.1f, 0.16f), new Color(0.95f, 0.92f, 0.7f));
            // The final starter keeps the silhouette clean; the legacy heat cubes
            // read as opaque blobs at its larger scale. Older kits retain their cue.
            if (!finalAtr42 && !HasNamedChild(root, "EngineHeat L") && !HasNamedChild(root, "EngineHeat R"))
            {
                // Batch F4 VFX-002 — prefer reusable heat kit; fall back to translucent quads.
                if (ArtPresentationLoader.TryInstantiatePrefab("vfx_engine_heat_v01", out var heatKit))
                {
                    while (heatKit.childCount > 0)
                    {
                        var child = heatKit.GetChild(0);
                        child.SetParent(root, false);
                    }

                    DestroyPresentationObject(heatKit.gameObject);
                }
                else
                {
                    ParentBlock(root, "EngineHeat L", new Vector3(-1.35f, -0.05f, 0.15f), new Vector3(0.35f, 0.35f, 0.7f), new Color(0.78f, 0.76f, 0.72f, 0.1f));
                    ParentBlock(root, "EngineHeat R", new Vector3(1.35f, -0.05f, 0.15f), new Vector3(0.35f, 0.35f, 0.7f), new Color(0.78f, 0.76f, 0.72f, 0.1f));
                }
            }

            AttachEngineAudio(root, AircraftType.Atr42, 12f, 220f, 0.11f);
            return root;
        }

        private static string RenameAircraftPart(string kitName)
        {
            if (kitName.StartsWith("livery_cowl_", StringComparison.Ordinal))
                return kitName.EndsWith("left", StringComparison.Ordinal) ? "Engine livery L" : "Engine livery R";
            if (kitName is "livery_secondary" or "livery_emblem")
                return "Livery " + kitName.Substring("livery_".Length);
            if (kitName.StartsWith("cabin_window_r", StringComparison.Ordinal))
                return "Cabin window R" + kitName.Substring("cabin_window_r".Length);
            if (kitName.StartsWith("cabin_window_", StringComparison.Ordinal))
                return "Cabin window " + kitName.Substring("cabin_window_".Length);
            if (kitName.StartsWith("tire_nose_", StringComparison.Ordinal))
                return "Tire nose " + FriendlyPartSuffix(kitName.Substring("tire_nose_".Length));
            if (kitName.StartsWith("wheel_nose_", StringComparison.Ordinal))
                return "Wheel nose " + FriendlyPartSuffix(kitName.Substring("wheel_nose_".Length));
            if (kitName.StartsWith("rim_nose_", StringComparison.Ordinal))
                return "Rim nose " + FriendlyPartSuffix(kitName.Substring("rim_nose_".Length));
            if (kitName.StartsWith("tire_left_", StringComparison.Ordinal))
                return "Tire L " + FriendlyPartSuffix(kitName.Substring("tire_left_".Length));
            if (kitName.StartsWith("wheel_left_", StringComparison.Ordinal))
                return "Wheel L " + FriendlyPartSuffix(kitName.Substring("wheel_left_".Length));
            if (kitName.StartsWith("rim_left_", StringComparison.Ordinal))
                return "Rim L " + FriendlyPartSuffix(kitName.Substring("rim_left_".Length));
            if (kitName.StartsWith("tire_right_", StringComparison.Ordinal))
                return "Tire R " + FriendlyPartSuffix(kitName.Substring("tire_right_".Length));
            if (kitName.StartsWith("wheel_right_", StringComparison.Ordinal))
                return "Wheel R " + FriendlyPartSuffix(kitName.Substring("wheel_right_".Length));
            if (kitName.StartsWith("fan_blade_l", StringComparison.Ordinal))
                return "Fan blade L" + FriendlyPartSuffix(kitName.Substring("fan_blade_l".Length));
            if (kitName.StartsWith("fan_blade_r", StringComparison.Ordinal))
                return "Fan blade R" + FriendlyPartSuffix(kitName.Substring("fan_blade_r".Length));
            if (kitName.StartsWith("rim_right_", StringComparison.Ordinal))
                return "Rim R " + FriendlyPartSuffix(kitName.Substring("rim_right_".Length));

            return kitName switch
            {
            "fuselage" => "Fuselage",
            "fuselage_mid" => "Fuselage mid",
            "fuselage_aft" => "Fuselage aft",
            "cabin_ring_fwd" => "Fuselage",
            "cabin_ring_mid" => "Fuselage mid",
            "cabin_ring_aft" => "Fuselage aft",
            "cabin_ring_tail" => "Fuselage aft",
            "tail_cone" => "Fuselage aft",
            "belly_fairing" => "Belly fairing",
            "nose" => "Nose",
            "nose_tip" => "Nose",
            "nose_ring_a" => "Nose",
            "nose_ring_b" => "Nose",
            "radome" => "Radome",
            "cockpit" => "Cockpit",
            "cockpit_loft" => "Cockpit",
            "cockpit_frame" => "Cockpit frame",
            "cabin_windows" => "Cabin windows",
            "cabin_window_band" => "Cabin window band",
            "cabin_window_1" => "Cabin window 1",
            "cabin_window_2" => "Cabin window 2",
            "cabin_window_3" => "Cabin window 3",
            "cabin_window_4" => "Cabin window 4",
            "cabin_window_5" => "Cabin window 5",
            "cabin_window_6" => "Cabin window 6",
            "cabin_window_7" => "Cabin window 7",
            "cabin_window_r1" => "Cabin window R1",
            "cabin_window_r2" => "Cabin window R2",
            "cabin_window_r3" => "Cabin window R3",
            "cabin_window_r4" => "Cabin window R4",
            "cabin_window_r5" => "Cabin window R5",
            "cabin_window_r6" => "Cabin window R6",
            "cabin_window_r7" => "Cabin window R7",
            "cabin_window_frame_1" => "Cabin window frame 1",
            "cabin_window_frame_3" => "Cabin window frame 3",
            "cabin_window_frame_5" => "Cabin window frame 5",
            "cabin_window_frame_7" => "Cabin window frame 7",
            "cabin_window_frame_r1" => "Cabin window frame R1",
            "cabin_window_frame_r2" => "Cabin window frame R2",
            "cabin_window_frame_r3" => "Cabin window frame R3",
            "cabin_window_frame_r4" => "Cabin window frame R4",
            "cabin_window_frame_r5" => "Cabin window frame R5",
            "cabin_window_frame_r7" => "Cabin window frame R7",
            "cockpit_glare" => "Cockpit glare",
            "windscreen_c" => "Windscreen C",
            "cockpit_side_l" => "Cockpit side L",
            "cockpit_side_r" => "Cockpit side R",
            "windscreen_l" => "Windscreen L",
            "windscreen_r" => "Windscreen R",
            "windscreen_pillar_l" => "Windscreen pillar L",
            "windscreen_pillar_r" => "Windscreen pillar R",
            "windscreen_pillar_c" => "Windscreen pillar C",
            "livery_stripe" => "Livery stripe",
            "livery_stripe_lower" => "Livery stripe lower",
            "livery_tail_sweep" => "Livery tail sweep",
            "door_frame_fwd" => "Door frame",
            "door_handle_fwd" => "Door handle",
            "inspection_panel_fwd" => "Inspection panel fwd",
            "inspection_panel_aft" => "Inspection panel aft",
            "cargo_sill" => "Cargo sill",
            "wing_fence_left" => "Wing fence L",
            "wing_fence_right" => "Wing fence R",
            "wing_fence_mid_l" => "Wing fence mid L",
            "wing_fence_mid_r" => "Wing fence mid R",
            "static_wick_left" => "Static wick L",
            "static_wick_right" => "Static wick R",
            "prop_hub_left" => "Prop hub L",
            "prop_hub_right" => "Prop hub R",
            "hub_cap_left" => "Hub cap L",
            "hub_cap_right" => "Hub cap R",
            "tailplane_tip_l" => "Tailplane tip L",
            "tailplane_tip_r" => "Tailplane tip R",
            "vor_antenna" => "VOR antenna",
            "wing_left" => "Wing L",
            "wing_right" => "Wing R",
            "wing_root_left" => "Wing root L",
            "wing_root_right" => "Wing root R",
            "wing_fairing_left" => "Wing fairing L",
            "wing_fairing_right" => "Wing fairing R",
            "flap_left" => "Flap L",
            "flap_right" => "Flap R",
            "flap_track_l1" => "Flap track L1",
            "flap_track_l2" => "Flap track L2",
            "flap_track_r1" => "Flap track R1",
            "flap_track_r2" => "Flap track R2",
            "flap_fairing_l" => "Flap fairing L",
            "flap_fairing_r" => "Flap fairing R",
            "spoiler_left" => "Spoiler L",
            "spoiler_right" => "Spoiler R",
            "aileron_left" => "Aileron L",
            "aileron_right" => "Aileron R",
            "wingtip_left" => "Wingtip L",
            "wingtip_right" => "Wingtip R",
            "winglet_left" => "Winglet L",
            "winglet_right" => "Winglet R",
            "engine_left" => "Engine L",
            "engine_right" => "Engine R",
            "fan_left" => "Fan L",
            "fan_right" => "Fan R",
            "pylon_left" => "Pylon L",
            "pylon_right" => "Pylon R",
            "nacelle_left" => "Nacelle L",
            "nacelle_right" => "Nacelle R",
            "intake_left" => "Intake L",
            "intake_right" => "Intake R",
            "exhaust_left" => "Exhaust L",
            "exhaust_right" => "Exhaust R",
            "exhaust_stack_l" => "Exhaust stack L",
            "exhaust_stack_r" => "Exhaust stack R",
            "oil_cooler_l" => "Oil cooler L",
            "oil_cooler_r" => "Oil cooler R",
            "cowl_flap_l" => "Cowl flap L",
            "cowl_flap_r" => "Cowl flap R",
            "propeller_left" => "Propeller L",
            "propeller_right" => "Propeller R",
            "propeller_left_b" => "PropBlade L",
            "propeller_right_b" => "PropBlade R",
            "propeller_left_c" => "PropBlade L2",
            "propeller_right_c" => "PropBlade R2",
            "propeller_left_d" => "PropBlade L3",
            "propeller_right_d" => "PropBlade R3",
            "propeller_left_e" => "PropBlade L4",
            "propeller_right_e" => "PropBlade R4",
            "propeller_left_f" => "PropBlade L5",
            "propeller_right_f" => "PropBlade R5",
            "propeller_left_tip" => "PropTip L",
            "propeller_right_tip" => "PropTip R",
            "propeller_left_tip_b" => "PropTip L2",
            "propeller_right_tip_b" => "PropTip R2",
            "propeller_left_tip_c" => "PropTip L3",
            "propeller_right_tip_c" => "PropTip R3",
            "propeller_left_tip_d" => "PropTip L4",
            "propeller_right_tip_d" => "PropTip R4",
            "propeller_left_tip_e" => "PropTip L5",
            "propeller_right_tip_e" => "PropTip R5",
            "propeller_left_tip_f" => "PropTip L6",
            "propeller_right_tip_f" => "PropTip R6",
            "spinner_left" => "Spinner L",
            "spinner_right" => "Spinner R",
            "spinner_stripe_l" => "Spinner stripe L",
            "spinner_stripe_r" => "Spinner stripe R",
            "tail_fin" => "Tail",
            "tail_root_fairing" => "Tail root fairing",
            "tailplane_saddle" => "Tailplane saddle",
            "tail_fin_tip" => "Tail tip",
            "tailplane" => "Tailplane",
            "dorsal_fin" => "Dorsal fin",
            "hf_antenna" => "HF antenna",
            "tail_nav_light" => "Tail nav light",
            "elevator_left" => "Elevator L",
            "elevator_right" => "Elevator R",
            "rudder" => "Rudder",
            "gear_nose" => "Gear nose",
            "gear_left" => "Gear L",
            "gear_right" => "Gear R",
            "gear_oleo_nose" => "Gear oleo nose",
            "gear_oleo_left" => "Gear oleo L",
            "gear_oleo_right" => "Gear oleo R",
            "gear_scissors_nose" => "Gear scissors nose",
            "gear_scissors_left" => "Gear scissors L",
            "gear_scissors_right" => "Gear scissors R",
            "gear_door_nose" => "Gear door nose",
            "gear_door_left" => "Gear door L",
            "gear_door_right" => "Gear door R",
            "gear_fairing_left" => "Gear fairing L",
            "gear_fairing_right" => "Gear fairing R",
            "tire_nose" => "Tire nose",
            "tire_left" => "Tire L",
            "tire_right" => "Tire R",
            "wheel_nose" => "Wheel nose",
            "wheel_left" => "Wheel L",
            "wheel_right" => "Wheel R",
            "rim_nose" => "Rim nose",
            "rim_left" => "Rim L",
            "rim_right" => "Rim R",
            "door_fwd" => "CabinDoor",
            "door_left_1" => "CabinDoor",
            "door_outline_fwd" => "Cabin door frame",
            "cargo_door_outline" => "Cargo door frame",
            "cargo_door" => "Cargo door",
            "cargo_door_latch" => "Cargo door latch",
            "antenna" => "Antenna",
            "antenna_aft" => "Antenna aft",
            "pitot" => "Pitot",
            "pitot_b" => "Pitot B",
            "nav_light_left" => "NavLight L",
            "nav_light_right" => "NavLight R",
            "beacon_top" => "Beacon",
            // Widebody belly beacon (787, A330neo, A350): it was left under its kit name, so it
            // never matched the "Beacon" prefix and never flashed (ADR 0124).
            "beacon_bottom" => "Beacon bottom",
            "landing_light_l" => "LandingLight L",
            "landing_light_r" => "LandingLight R",
            "taxi_light" => "TaxiLight",
            "engine_heat_left" => "EngineHeat L",
            "engine_heat_right" => "EngineHeat R",
            _ => kitName
            };
        }

        private static bool IsAircraftGlass(string kitName) =>
            (kitName.StartsWith("cabin_window_", StringComparison.Ordinal)
             && !kitName.StartsWith("cabin_window_frame", StringComparison.Ordinal))
            || kitName.StartsWith("cockpit_side_", StringComparison.Ordinal)
            || (kitName.StartsWith("windscreen_", StringComparison.Ordinal)
                && !kitName.StartsWith("windscreen_pillar", StringComparison.Ordinal));

        private static Color? AircraftPartColor(string kitName, Color accent)
        {
            if (kitName.StartsWith("livery_", StringComparison.Ordinal))
                return AircraftLiveryPaint.Colour(kitName, accent);
            if (kitName.StartsWith("glazing_", StringComparison.Ordinal))
            {
                if (kitName.EndsWith("_gasket", StringComparison.Ordinal)
                    || kitName.EndsWith("_mask", StringComparison.Ordinal))
                    return AircraftGlazingSurround;
                if (kitName.EndsWith("_interior", StringComparison.Ordinal))
                    return new Color(0.02f, 0.022f, 0.025f);
            }
            if (IsAircraftGlass(kitName))
                return AircraftGlass;
            // Real windscreen posts and frames are part of the dark flight-deck band, as is the
            // Q400's centre sill; its centre glare panel glows with the windscreen at night, so
            // it is glass. The ATR's paired _l/_r sill and glare pieces are skin fairings below
            // the band and stay fuselage white.
            if (kitName.StartsWith("windscreen_pillar", StringComparison.Ordinal)
                || kitName.StartsWith("cockpit_frame", StringComparison.Ordinal)
                || kitName.StartsWith("cockpit_mask_", StringComparison.Ordinal)
                || kitName == "cockpit_sill")
                return AircraftGlazingSurround;
            if (kitName == "cockpit_glare")
                return AircraftGlass;
            if (kitName.StartsWith("cockpit_sill_", StringComparison.Ordinal)
                || kitName.StartsWith("cockpit_glare_", StringComparison.Ordinal))
                return new Color(0.93f, 0.95f, 0.97f);
            if (kitName.StartsWith("pilot_", StringComparison.Ordinal))
                return kitName.EndsWith("_head", StringComparison.Ordinal)
                    ? new Color(0.53f, 0.40f, 0.33f)
                    : new Color(0.075f, 0.105f, 0.15f);
            if (kitName.StartsWith("fan_", StringComparison.Ordinal))
                return new Color(0.16f, 0.18f, 0.21f);
            if (kitName.StartsWith("tire_", StringComparison.Ordinal))
                return new Color(0.12f, 0.12f, 0.13f);
            if (kitName.StartsWith("wheel_", StringComparison.Ordinal)
                || kitName.StartsWith("rim_", StringComparison.Ordinal))
                return new Color(0.55f, 0.56f, 0.58f);
            if (kitName.StartsWith("engine_heat_", StringComparison.Ordinal))
                return new Color(0.78f, 0.76f, 0.72f, 0.10f); // neutral haze, not a glow (ADR 0170)

            return kitName switch
            {
            "fuselage" or "fuselage_mid" or "fuselage_aft" or "fuselage_port"
                or "cabin_ring_fwd" or "cabin_ring_mid" or "cabin_ring_aft" or "cabin_ring_tail" or "tail_cone"
                or "nose" or "nose_tip" or "nose_ring_a" or "nose_ring_b" or "radome"
                or "belly_fairing" or "cargo_door" or "door_frame_fwd"
                or "gear_fairing_left" or "gear_fairing_right" => new Color(0.93f, 0.95f, 0.97f),
            "cockpit" or "cockpit_loft" or "cabin_windows" => AircraftGlass,
            "cabin_window_frame_1" or "cabin_window_frame_3" or "cabin_window_frame_5" or "cabin_window_frame_7"
                or "cabin_window_frame_r1" or "cabin_window_frame_r2" or "cabin_window_frame_r3"
                or "cabin_window_frame_r4" or "cabin_window_frame_r5" or "cabin_window_frame_r7"
                => new Color(0.75f, 0.78f, 0.82f),
            "livery_stripe" or "livery_stripe_lower" or "livery_tail_sweep" => accent,
            "door_handle_fwd" or "cargo_door_latch" or "cargo_sill"
                or "door_outline_fwd" or "cargo_door_outline" => new Color(0.48f, 0.52f, 0.55f),
            "inspection_panel_fwd" or "inspection_panel_aft" => new Color(0.86f, 0.88f, 0.90f),
            "wing_left" or "wing_right" or "wing_root_left" or "wing_root_right"
                or "wing_fairing_left" or "wing_fairing_right" or "wing_centre_saddle"
                or "wingtip_left" or "wingtip_right"
                or "wing_fence_left" or "wing_fence_right" or "wing_fence_mid_l" or "wing_fence_mid_r"
                or "flap_left" or "flap_right" or "flap_fairing_l" or "flap_fairing_r"
                or "spoiler_left" or "spoiler_right"
                or "aileron_left" or "aileron_right"
                or "tailplane"
                or "tail_root_fairing" or "tailplane_saddle"
                or "tailplane_tip_l" or "tailplane_tip_r"
                or "elevator_left" or "elevator_right" => new Color(0.86f, 0.89f, 0.91f),
            "tail_fin" or "tail_fin_tip" or "dorsal_fin" or "rudder" or "winglet_left" or "winglet_right" => accent,
            "flap_track_l1" or "flap_track_l2" or "flap_track_r1" or "flap_track_r2"
                => new Color(0.32f, 0.34f, 0.38f),
            "engine_left" or "engine_right" or "pylon_left" or "pylon_right"
                or "nacelle_left" or "nacelle_right"
                or "nacelle_fillet_left" or "nacelle_fillet_right"
                or "intake_left" or "intake_right"
                or "oil_cooler_l" or "oil_cooler_r" or "cowl_flap_l" or "cowl_flap_r"
                => new Color(0.86f, 0.89f, 0.91f),
            "exhaust_left" or "exhaust_right" or "exhaust_stack_l" or "exhaust_stack_r"
                => new Color(0.35f, 0.36f, 0.38f),
            "propeller_left" or "propeller_right" or "propeller_left_b" or "propeller_right_b"
                or "propeller_left_c" or "propeller_right_c"
                or "propeller_left_d" or "propeller_right_d"
                or "propeller_left_e" or "propeller_right_e"
                or "propeller_left_f" or "propeller_right_f"
                or "spinner_left" or "spinner_right" or "prop_hub_left" or "prop_hub_right"
                or "hub_cap_left" or "hub_cap_right"
                => new Color(0.2f, 0.2f, 0.22f),
            "propeller_left_tip" or "propeller_right_tip"
                or "propeller_left_tip_b" or "propeller_right_tip_b"
                or "propeller_left_tip_c" or "propeller_right_tip_c"
                or "propeller_left_tip_d" or "propeller_right_tip_d"
                or "propeller_left_tip_e" or "propeller_right_tip_e"
                or "propeller_left_tip_f" or "propeller_right_tip_f"
                => new Color(0.92f, 0.78f, 0.18f),
            "spinner_stripe_l" or "spinner_stripe_r" => new Color(0.92f, 0.55f, 0.12f),
            "gear_nose" or "gear_left" or "gear_right"
                or "gear_oleo_nose" or "gear_oleo_left" or "gear_oleo_right"
                or "gear_scissors_nose" or "gear_scissors_left" or "gear_scissors_right"
                or "gear_door_nose" or "gear_door_left" or "gear_door_right" => new Color(0.25f, 0.25f, 0.28f),
            "tire_nose" or "tire_left" or "tire_right" => new Color(0.12f, 0.12f, 0.13f),
            "rim_nose" or "rim_left" or "rim_right"
                or "wheel_nose" or "wheel_left" or "wheel_right" => new Color(0.55f, 0.56f, 0.58f),
            "door_fwd" or "cargo_door" => new Color(0.91f, 0.93f, 0.95f),
            "antenna" or "antenna_aft" or "pitot" or "pitot_b" or "vor_antenna"
                or "hf_antenna" or "static_wick_left" or "static_wick_right" => new Color(0.35f, 0.35f, 0.38f),
            "nav_light_left" => new Color(0.2f, 0.9f, 0.3f),
            "nav_light_right" => new Color(0.9f, 0.2f, 0.2f),
            "beacon_top" or "beacon_bottom" => new Color(0.95f, 0.35f, 0.12f),
            "tail_nav_light" => new Color(0.95f, 0.95f, 0.9f),
            "landing_light_l" or "landing_light_r" or "taxi_light" => new Color(0.95f, 0.95f, 0.85f),
            _ => null
            };
        }

        /// <summary>
        /// Engine paint (ADR 0112): cowlings are painted metal, not airline colour. The shared
        /// fallback painted every narrowbody and turboprop nacelle one steel blue, which no
        /// Adelaide operator flies. Jets get pale grey cowls with a bare-metal intake lip;
        /// turboprop nacelles match the white fuselage.
        /// </summary>
        private static Color? EnginePaint(string kitName, bool turboprop)
        {
            switch (kitName)
            {
                case "intake_left":
                case "intake_right":
                    return new Color(0.64f, 0.66f, 0.69f);
                case "engine_left":
                case "engine_right":
                case "nacelle_left":
                case "nacelle_right":
                case "nacelle_fillet_left":
                case "nacelle_fillet_right":
                case "cowl_flap_l":
                case "cowl_flap_r":
                case "oil_cooler_l":
                case "oil_cooler_r":
                    return turboprop ? new Color(0.91f, 0.93f, 0.95f) : new Color(0.84f, 0.86f, 0.89f);
                case "pylon_left":
                case "pylon_right":
                    return new Color(0.80f, 0.82f, 0.85f);
            }

            return null;
        }

        /// <summary>
        /// Parent blades, hubs and spinners under each propeller so SpinPropellers
        /// rotates the whole assembly (0025 item 7).
        /// </summary>
        private static void NestCrossPropellerBlades(Transform aircraft)
        {
            Transform propL = null, propR = null;
            Transform hubL = null, hubR = null, spinnerL = null, spinnerR = null;
            Transform capL = null, capR = null;
            Transform stripeL = null, stripeR = null;
            var bladesL = new Transform[5];
            var bladesR = new Transform[5];
            var tipsL = new Transform[6];
            var tipsR = new Transform[6];
            var namedChildren17 = AirsideNamedChildren.Get(aircraft);
            var childNames17 = AirsideNamedChildren.Names(aircraft);
            for (var childIndex17 = 0; childIndex17 < namedChildren17.Length; childIndex17++)
            {
                var child = namedChildren17[childIndex17];
                var childName = childNames17[childIndex17];
                if (childName == "Propeller L") propL = child;
                else if (childName == "Propeller R") propR = child;
                else if (childName == "PropBlade L") bladesL[0] = child;
                else if (childName == "PropBlade R") bladesR[0] = child;
                else if (childName == "PropBlade L2") bladesL[1] = child;
                else if (childName == "PropBlade R2") bladesR[1] = child;
                else if (childName == "PropBlade L3") bladesL[2] = child;
                else if (childName == "PropBlade R3") bladesR[2] = child;
                else if (childName == "PropBlade L4") bladesL[3] = child;
                else if (childName == "PropBlade R4") bladesR[3] = child;
                else if (childName == "PropBlade L5") bladesL[4] = child;
                else if (childName == "PropBlade R5") bladesR[4] = child;
                else if (childName == "PropTip L") tipsL[0] = child;
                else if (childName == "PropTip R") tipsR[0] = child;
                else if (childName == "PropTip L2") tipsL[1] = child;
                else if (childName == "PropTip R2") tipsR[1] = child;
                else if (childName == "PropTip L3") tipsL[2] = child;
                else if (childName == "PropTip R3") tipsR[2] = child;
                else if (childName == "PropTip L4") tipsL[3] = child;
                else if (childName == "PropTip R4") tipsR[3] = child;
                else if (childName == "PropTip L5") tipsL[4] = child;
                else if (childName == "PropTip R5") tipsR[4] = child;
                else if (childName == "PropTip L6") tipsL[5] = child;
                else if (childName == "PropTip R6") tipsR[5] = child;
                else if (childName == "Prop hub L") hubL = child;
                else if (childName == "Prop hub R") hubR = child;
                else if (childName == "Spinner L") spinnerL = child;
                else if (childName == "Spinner R") spinnerR = child;
                else if (childName == "Hub cap L") capL = child;
                else if (childName == "Hub cap R") capR = child;
                else if (childName == "Spinner stripe L") stripeL = child;
                else if (childName == "Spinner stripe R") stripeR = child;
            }

            for (var i = 0; i < bladesL.Length; i++)
                NestUnderProp(propL, bladesL[i], i == 0 ? "Blade" : $"Blade {i + 1}");
            for (var i = 0; i < bladesR.Length; i++)
                NestUnderProp(propR, bladesR[i], i == 0 ? "Blade" : $"Blade {i + 1}");
            for (var i = 0; i < tipsL.Length; i++)
                NestUnderProp(propL, tipsL[i], i == 0 ? "Tip" : $"Tip {i + 1}");
            for (var i = 0; i < tipsR.Length; i++)
                NestUnderProp(propR, tipsR[i], i == 0 ? "Tip" : $"Tip {i + 1}");
            NestUnderProp(propL, hubL, "Hub");
            NestUnderProp(propR, hubR, "Hub");
            NestUnderProp(propL, spinnerL, "Spinner");
            NestUnderProp(propR, spinnerR, "Spinner");
            NestUnderProp(propL, capL, "Hub cap");
            NestUnderProp(propR, capR, "Hub cap");
            NestUnderProp(propL, stripeL, "Stripe");
            NestUnderProp(propR, stripeR, "Stripe");
            // Parts were renamed above; the per-frame passes read cached names.
            AirsideNamedChildren.Forget(aircraft);
        }

        /// <summary>
        /// The AIR-005 kit keeps each turbofan blade as a separately named mesh for
        /// authored readability. Parent those blades to the matching fan hub before
        /// rebaking, exactly as the turboprop blades are parented to their hubs.
        /// </summary>
        private static void NestJetFanBlades(Transform aircraft)
        {
            Transform leftFan = null, rightFan = null;
            var leftBlades = new List<Transform>();
            var rightBlades = new List<Transform>();
            var children = AirsideNamedChildren.Get(aircraft);
            var names = AirsideNamedChildren.Names(aircraft);
            for (var i = 0; i < children.Length; i++)
            {
                var child = children[i];
                var childName = names[i];
                if (childName == "Fan L") leftFan = child;
                else if (childName == "Fan R") rightFan = child;
                else if (childName.StartsWith("Fan blade L", StringComparison.Ordinal)) leftBlades.Add(child);
                else if (childName.StartsWith("Fan blade R", StringComparison.Ordinal)) rightBlades.Add(child);
            }

            foreach (var blade in leftBlades)
                NestUnderProp(leftFan, blade, blade.name);
            foreach (var blade in rightBlades)
                NestUnderProp(rightFan, blade, blade.name);
            AirsideNamedChildren.Forget(aircraft);
        }

        /// <summary>
        /// Move each Propeller transform to its hub centre and rebake mesh verts so
        /// <see cref="SpinPropellers"/> rotates about the nacelle, not the airframe origin.
        /// No-ops when the prop node is already at the hub (Resources/prefab path).
        /// </summary>
        private static void RebakePropellerPivots(Transform aircraft)
        {
            var namedChildren18 = AirsideNamedChildren.Get(aircraft);
            var childNames18 = AirsideNamedChildren.Names(aircraft);
            for (var childIndex18 = 0; childIndex18 < namedChildren18.Length; childIndex18++)
            {
                var child = namedChildren18[childIndex18];
                var childName = childNames18[childIndex18];
                if (child == aircraft || !childName.StartsWith("Propeller", StringComparison.Ordinal))
                    continue;
                RebakePropellerPivot(child);
            }
        }

        /// <summary>Move each jet fan root to its hub so it rotates inside its nacelle.</summary>
        private static void RebakeJetFanPivots(Transform aircraft)
        {
            var children = AirsideNamedChildren.Get(aircraft);
            var names = AirsideNamedChildren.Names(aircraft);
            for (var i = 0; i < children.Length; i++)
            {
                if (children[i] == aircraft || !(names[i] is "Fan L" or "Fan R"))
                    continue;
                RebakePropellerPivot(children[i]);
            }
            AirsideNamedChildren.Forget(aircraft);
        }

        private static void RebakePropellerPivot(Transform prop)
        {
            if (!TryEstimatePropHubWorld(prop, out var hubWorld))
                return;

            // Already at the hub (Resources/prefab path with local blade verts).
            if ((prop.position - hubWorld).sqrMagnitude < 0.0025f)
                return;

            var filters = prop.GetComponentsInChildren<MeshFilter>(true);
            var worldVerts = new Vector3[filters.Length][];
            for (var i = 0; i < filters.Length; i++)
            {
                var filter = filters[i];
                if (filter == null || filter.sharedMesh == null)
                {
                    worldVerts[i] = null;
                    continue;
                }

                var source = filter.sharedMesh;
                var local = source.vertices;
                var world = new Vector3[local.Length];
                var xf = filter.transform;
                for (var v = 0; v < local.Length; v++)
                    world[v] = xf.TransformPoint(local[v]);
                worldVerts[i] = world;
            }

            prop.position = hubWorld;

            for (var i = 0; i < filters.Length; i++)
            {
                if (worldVerts[i] == null)
                    continue;
                var filter = filters[i];
                var mesh = Object.Instantiate(filter.sharedMesh);
                mesh.name = filter.sharedMesh.name + " hub-pivot";
                var local = new Vector3[worldVerts[i].Length];
                var xf = filter.transform;
                for (var v = 0; v < local.Length; v++)
                    local[v] = xf.InverseTransformPoint(worldVerts[i][v]);
                mesh.vertices = local;
                mesh.RecalculateBounds();
                mesh.RecalculateNormals();
                filter.sharedMesh = mesh;
            }
        }

        private static bool TryEstimatePropHubWorld(Transform prop, out Vector3 hubWorld)
        {
            hubWorld = default;
            var hub = prop.Find("Hub");
            if (hub != null)
            {
                var hubRenderer = hub.GetComponent<Renderer>();
                if (hubRenderer != null)
                {
                    hubWorld = hubRenderer.bounds.center;
                    return true;
                }
            }

            var spinner = prop.Find("Spinner");
            if (spinner != null)
            {
                var spinnerRenderer = spinner.GetComponent<Renderer>();
                if (spinnerRenderer != null)
                {
                    hubWorld = spinnerRenderer.bounds.center;
                    return true;
                }
            }

            var selfRenderer = prop.GetComponent<Renderer>();
            if (selfRenderer != null)
            {
                hubWorld = selfRenderer.bounds.center;
                return true;
            }

            var sum = Vector3.zero;
            var count = 0;
            foreach (var renderer in prop.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer.name == "PropDisc")
                    continue;
                sum += renderer.bounds.center;
                count++;
            }

            if (count == 0)
                return false;
            hubWorld = sum / count;
            return true;
        }

        private static void NestUnderProp(Transform prop, Transform part, string rename)
        {
            if (prop == null || part == null || part.parent == prop)
                return;
            part.SetParent(prop, true);
            part.name = rename;
        }

        /// <summary>
        /// FBX/glTF fallback meshes arrive with aircraft-space vertices and zeroed
        /// transforms. Move gameplay parts to their actual hinges and rebake the
        /// vertices so their runtime rotations do not orbit around the fuselage.
        /// </summary>
        private static void RebakeAircraftArticulatedPivots(Transform aircraft)
        {
            var namedChildren20 = AirsideNamedChildren.Get(aircraft);
            var childNames20 = AirsideNamedChildren.Names(aircraft);
            for (var childIndex20 = 0; childIndex20 < namedChildren20.Length; childIndex20++)
            {
                var child = namedChildren20[childIndex20];
                var childName = childNames20[childIndex20];
                if (child == aircraft)
                    continue;
                var renderer = child.GetComponent<Renderer>();
                if (renderer == null)
                    continue;

                var bounds = renderer.bounds;
                var pivot = bounds.center;
                var articulated = true;
                if (childName is "Gear nose" or "Gear L" or "Gear R")
                {
                    pivot.y = bounds.max.y;
                }
                else if (childName.StartsWith("Gear door", StringComparison.Ordinal))
                {
                    pivot.y = bounds.max.y;
                }
                else if (childName is "Flap L" or "Flap R"
                         || childName.StartsWith("Aileron", StringComparison.Ordinal)
                         || childName.StartsWith("Elevator", StringComparison.Ordinal)
                         || childName.StartsWith("Spoiler", StringComparison.Ordinal))
                {
                    pivot.z = bounds.max.z;
                }
                else if (childName.StartsWith("Rudder", StringComparison.Ordinal)
                         || childName.StartsWith("CabinDoor", StringComparison.Ordinal)
                         || childName.StartsWith("Cargo door", StringComparison.OrdinalIgnoreCase))
                {
                    pivot.z = bounds.max.z;
                }
                else
                {
                    articulated = false;
                }

                if (articulated)
                    RebakePartPivot(child, pivot);
            }
        }

        /// <summary>
        /// Parent scissors / tires under matching gear struts so retract takes the
        /// whole assembly (0025 item 7) — mirrors NestCrossPropellerBlades.
        /// </summary>
        private static void NestLandingGearParts(Transform aircraft)
        {
            Transform gearNose = null, gearL = null, gearR = null;
            var movingParts = new List<Transform>();
            var namedChildren21 = AirsideNamedChildren.Get(aircraft);
            var childNames21 = AirsideNamedChildren.Names(aircraft);
            for (var childIndex21 = 0; childIndex21 < namedChildren21.Length; childIndex21++)
            {
                var child = namedChildren21[childIndex21];
                var childName = childNames21[childIndex21];
                if (childName == "Gear nose") gearNose = child;
                else if (childName == "Gear L") gearL = child;
                else if (childName == "Gear R") gearR = child;
                else if (childName.StartsWith("Gear scissors", StringComparison.Ordinal)
                         || childName.StartsWith("Gear oleo", StringComparison.Ordinal)
                         || childName.StartsWith("Tire", StringComparison.Ordinal)
                         || childName.StartsWith("Wheel", StringComparison.Ordinal)
                         || childName.StartsWith("Rim", StringComparison.Ordinal))
                    movingParts.Add(child);
            }

            foreach (var part in movingParts)
            {
                var lower = part.name.ToLowerInvariant();
                var gear = lower.Contains("nose") ? gearNose
                    : part.name.IndexOf(" L", StringComparison.Ordinal) >= 0 ? gearL
                    : part.name.IndexOf(" R", StringComparison.Ordinal) >= 0 ? gearR
                    : null;
                NestUnderProp(gear, part, part.name);
            }
            // Gear doors stay siblings so UpdateAircraftLightsAndGear can animate them independently.
        }

        /// <summary>
        /// Make wing flex a proper rig rather than rotating only the wing skin. Authored
        /// glTF mesh nodes arrive as siblings, so flaps, engines, props/fans, tip devices,
        /// lights and wing-mounted main gear otherwise remain behind and visibly separate.
        /// Articulated roots keep their own pivots and animations after reparenting.
        /// </summary>
        private static void NestWingMountedParts(Transform aircraft)
        {
            Transform wingL = null, wingR = null;
            var attached = new List<(Transform Part, int Side)>();
            var children = AirsideNamedChildren.Get(aircraft);
            var names = AirsideNamedChildren.Names(aircraft);
            for (var i = 0; i < children.Length; i++)
            {
                var child = children[i];
                var childName = names[i];
                if (childName == "Wing L")
                {
                    wingL = child;
                    continue;
                }
                if (childName == "Wing R")
                {
                    wingR = child;
                    continue;
                }

                var side = WingMountedSide(childName);
                if (side != 0)
                    attached.Add((child, side));
            }

            foreach (var item in attached)
            {
                var wing = item.Side < 0 ? wingL : wingR;
                if (wing == null || item.Part.IsChildOf(wing))
                    continue;
                NestUnderProp(wing, item.Part, item.Part.name);
            }

            AirsideNamedChildren.Forget(aircraft);
        }

        private static int WingMountedSide(string partName)
        {
            if (string.IsNullOrEmpty(partName))
                return 0;
            var lower = partName.ToLowerInvariant();
            var attached = lower.StartsWith("wing root")
                           || lower.StartsWith("wing fairing")
                           || lower.StartsWith("wingtip")
                           || lower.StartsWith("winglet")
                           || lower.StartsWith("wing fence")
                           || lower.StartsWith("static wick")
                           || lower.StartsWith("flap ")
                           || lower.StartsWith("aileron")
                           || lower.StartsWith("spoiler")
                           || lower.StartsWith("engine ")
                           || lower.StartsWith("engineheat")
                           || lower.StartsWith("pylon")
                           || lower.StartsWith("nacelle")
                           || lower.StartsWith("intake")
                           || lower.StartsWith("exhaust")
                           || lower.StartsWith("oil cooler")
                           || lower.StartsWith("oil_cooler")
                           || lower.StartsWith("cowl flap")
                           || lower.StartsWith("propeller")
                           || lower.StartsWith("fan ")
                           || lower.StartsWith("gear fairing")
                           || lower is "gear l" or "gear r"
                           || lower.StartsWith("gear door")
                           || lower.StartsWith("gear_door_inner")
                           || lower.StartsWith("navlight")
                           || lower.StartsWith("landinglight");
            if (!attached)
                return 0;

            if (lower.EndsWith(" l") || lower.EndsWith(" left")
                                          || lower.EndsWith("_l") || lower.EndsWith("_left"))
                return -1;
            if (lower.EndsWith(" r") || lower.EndsWith(" right")
                                          || lower.EndsWith("_r") || lower.EndsWith("_right"))
                return 1;
            return 0;
        }

        /// <summary>
        /// Overview LOD: keep the full ATR close-up, drop small static detail far out.
        /// Moving parts stay in every LOD so gear/props never pop off.
        /// </summary>
        private static void EnsureAircraftLod(Transform aircraft)
        {
            if (aircraft.GetComponent<LODGroup>() != null)
                return;

            var all = new List<Renderer>(64);
            var nearOnly = new List<Renderer>(32);
            foreach (var renderer in aircraft.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer.name is "GroundShadow" or "PropDisc")
                    continue;
                all.Add(renderer);
                var n = renderer.name;
                var moving = n.StartsWith("Propeller", StringComparison.Ordinal)
                    || n.StartsWith("Tire", StringComparison.Ordinal)
                    || n.StartsWith("Gear", StringComparison.Ordinal)
                    || n.StartsWith("Flap", StringComparison.Ordinal)
                    || n.StartsWith("Aileron", StringComparison.Ordinal)
                    || n.StartsWith("Elevator", StringComparison.Ordinal)
                    || n.StartsWith("Rudder", StringComparison.Ordinal)
                    || n.StartsWith("Spoiler", StringComparison.Ordinal)
                    || n.StartsWith("CabinDoor", StringComparison.Ordinal)
                    || n.StartsWith("LandingLight", StringComparison.Ordinal)
                    || n.StartsWith("NavLight", StringComparison.Ordinal)
                    || n.StartsWith("Beacon", StringComparison.Ordinal);
                var fine = n.IndexOf("rim", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("scissors", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("rivet", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("antenna", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("fairing", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!moving && fine)
                    nearOnly.Add(renderer);
            }

            if (all.Count == 0)
                return;

            var far = new List<Renderer>(all.Count);
            foreach (var renderer in all)
            {
                if (!nearOnly.Contains(renderer))
                    far.Add(renderer);
            }

            // Medium drops fine detail immediately; High keeps it to ~12% screen height.
            var detailHeight = AirsideRuntimeQuality.Current == AirsideRuntimeQuality.Ladder.High
                ? 0.12f
                : 0.35f;
            var group = aircraft.gameObject.AddComponent<LODGroup>();
            group.SetLODs(new[]
            {
                new LOD(detailHeight, all.ToArray()),
                // ADR 0142: 0.4 % (was 2 %), so a jet on a 10 km final is still drawn; beyond
                // that its distant light (AirsidePrototype.DistantLights) carries it.
                new LOD(0.004f, far.ToArray())
            });
            group.RecalculateBounds();
        }

        /// <summary>
        /// Translucent prop disc under each propeller hub — shown only at high RPM.
        /// </summary>
        private static void EnsurePropDiscs(Transform aircraft)
        {
            var namedChildren27 = AirsideNamedChildren.Get(aircraft);
            var childNames27 = AirsideNamedChildren.Names(aircraft);
            for (var childIndex27 = 0; childIndex27 < namedChildren27.Length; childIndex27++)
            {
                var child = namedChildren27[childIndex27];
                var childName = childNames27[childIndex27];
                if (child == aircraft || !childName.StartsWith("Propeller", StringComparison.Ordinal))
                    continue;
                if (child.Find("PropDisc") != null)
                    continue;

                // Size the blur disc from blade/tip bounds (v06 radial ~1.27f m — fixed 1.2f
                // diameter read as a hub pancake after pivot rebake).
                var radius = 0.6f;
                foreach (var renderer in child.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null)
                        continue;
                    var n = renderer.name;
                    if (n.IndexOf("blade", StringComparison.OrdinalIgnoreCase) < 0
                        && n.IndexOf("tip", StringComparison.OrdinalIgnoreCase) < 0
                        && !n.StartsWith("Propeller", StringComparison.Ordinal))
                        continue;
                    var extents = renderer.bounds.extents;
                    var planar = Mathf.Max(extents.x, extents.y, extents.z);
                    radius = Mathf.Max(radius, planar);
                }

                var diameter = Mathf.Clamp(radius * 2.05f, 1.2f, 4.05f);
                // ADR 0148: a double-sided quad with a blur texture (soft hub-to-tip density, faint
                // blade ghosts, a brighter tip ring) instead of a flat glass cylinder that read as a
                // grey pancake. It sits in the propeller's own disc plane (local XY, spin axis Z).
                var blades = CountBlades(child);
                var disc = GameObject.CreatePrimitive(PrimitiveType.Quad);
                disc.name = "PropDisc";
                DestroyPresentationObject(disc.GetComponent<Collider>());
                disc.transform.SetParent(child, false);
                disc.transform.localPosition = Vector3.zero;
                disc.transform.localRotation = Quaternion.identity;
                disc.transform.localScale = new Vector3(diameter * 1.02f, diameter * 1.02f, 1f);
                var discColor = new Color(0.72f, 0.74f, 0.78f, AirsideReusableMotion.PropDiscPeakAlpha);
                var discRenderer = disc.GetComponent<Renderer>();
                discRenderer.sharedMaterial = PropBlurMaterial(blades);
                discRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                discRenderer.receiveShadows = false;
                SetRendererColor(discRenderer, discColor);
                PropBladeCounts[child.GetInstanceID()] = blades;
                disc.SetActive(false);
            }
        }

        /// <summary>
        /// A low-alpha intake disc preserves the 737's fan read when individual blades
        /// would strobe at operating RPM. It is only enabled by <see cref="SpinJetFans"/>.
        /// </summary>
        private static void EnsureJetFanDiscs(Transform aircraft)
        {
            var children = AirsideNamedChildren.Get(aircraft);
            var names = AirsideNamedChildren.Names(aircraft);
            for (var i = 0; i < children.Length; i++)
            {
                var fan = children[i];
                if (fan == aircraft || !(names[i] is "Fan L" or "Fan R") || fan.Find("FanDisc") != null)
                    continue;

                var radius = 0.45f;
                foreach (var blade in fan.GetComponentsInChildren<Renderer>(true))
                {
                    if (blade == null || !blade.name.StartsWith("Fan blade", StringComparison.Ordinal))
                        continue;
                    radius = Mathf.Max(radius, Mathf.Max(blade.bounds.extents.x, blade.bounds.extents.y));
                }

                // ADR 0168: a double-sided quad in the fan plane (local XY, spin axis Z) with a near-solid
                // dark fan-face texture, clear over the spinner. It was a 26 % glass cylinder, so the
                // intake went see-through once the blades hid.
                var disc = GameObject.CreatePrimitive(PrimitiveType.Quad);
                disc.name = "FanDisc";
                DestroyPresentationObject(disc.GetComponent<Collider>());
                disc.transform.SetParent(fan, false);
                disc.transform.localPosition = new Vector3(0f, 0f, 0.035f);
                disc.transform.localRotation = Quaternion.identity;
                var diameter = Mathf.Clamp(radius * 2.05f, 0.8f, 2.7f);
                disc.transform.localScale = new Vector3(diameter, diameter, 1f);
                var colour = new Color(0.2f, 0.23f, 0.26f, AirsideReusableMotion.JetFanDiscPeakAlpha);
                var renderer = disc.GetComponent<Renderer>();
                renderer.sharedMaterial = JetFanBlurMaterial();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                SetRendererColor(renderer, colour);
                disc.SetActive(false);
            }
        }

        /// <summary>Re-resolve a view's parts after children were added to it (selection marker).</summary>
        private void ForgetAircraftViewParts(Transform aircraft)
        {
            if (aircraft != null)
                _aircraftViewParts.Remove(aircraft.GetInstanceID());
        }

        private static void ApplyLiveryDecal(Transform aircraft, string artRelativePath)
        {
            if (string.IsNullOrEmpty(artRelativePath))
                return;
            var texture = TryLoadArtTexture(artRelativePath);
            if (texture == null)
                return;

            ApplyLiveryTexture(aircraft, texture);
        }

        private static void ApplyLiveryTexture(Transform aircraft, Texture2D texture)
        {
            var namedChildren28 = AirsideNamedChildren.Get(aircraft);
            var childNames28 = AirsideNamedChildren.Names(aircraft);
            for (var childIndex28 = 0; childIndex28 < namedChildren28.Length; childIndex28++)
            {
                var child = namedChildren28[childIndex28];
                var childName = childNames28[childIndex28];
                // Covers segmented turboprop fuselage parts (v04 + lofted cabin rings / nose
                // rings) while keeping the nose gear's own tyres, wheels and rims unpainted.
                if (!AirsideAircraftParts.TakesFuselageLivery(childName))
                    continue;
                var renderer = child.GetComponent<Renderer>();
                if (renderer == null)
                    continue;
                ApplyRendererTexture(renderer, texture, Vector2.one);
            }
        }

        private static void PlaceWorldProps()
        {
            var kit = PreferArtKit(
                "Models/Props/mdl_airfield_props_kit_authored_v01.gltf",
                "Models/Props/mdl_airfield_props_kit_v02.gltf",
                "Models/Props/mdl_airfield_props_kit_v01.gltf");
            var hasPropsKit = !string.IsNullOrEmpty(kit) && ArtGltfLoader.HasKit(kit);

            // Stand lead-in cones — hero corners when props kit stamps multi-mesh cones.
            CreateCone(new Vector3(12.5f, 0.25f, 12.2f));
            CreateCone(new Vector3(12.5f, 0.25f, 21.8f));
            CreateCone(new Vector3(23.5f, 0.25f, 12.2f));
            CreateCone(new Vector3(23.5f, 0.25f, 21.8f));
            if (!hasPropsKit)
            {
                CreateCone(new Vector3(12.5f, 0.25f, 15.8f));
                CreateCone(new Vector3(12.5f, 0.25f, 18.2f));
                CreateCone(new Vector3(23.5f, 0.25f, 15.8f));
                CreateCone(new Vector3(23.5f, 0.25f, 18.2f));
                CreateCone(new Vector3(-6f, 0.25f, 11f));
                CreateCone(new Vector3(-10f, 0.25f, 11f));
                CreateCone(new Vector3(4f, 0.25f, 7.2f));
                CreateCone(new Vector3(4f, 0.25f, 10.8f));
                CreateCone(new Vector3(18f, 0.25f, 11.2f));
                CreateCone(new Vector3(28f, 0.25f, 11.2f));
                CreateCone(new Vector3(8f, 0.25f, 23.5f));
                CreateCone(new Vector3(30f, 0.25f, 23.5f));
            }
            else
            {
                // Taxi lead-in pair so the A1 entry still reads marked.
                CreateCone(new Vector3(4f, 0.25f, 7.2f));
                CreateCone(new Vector3(4f, 0.25f, 10.8f));
            }

            // Worksite / hangar barriers — thin when kit barriers are heavy silhouettes.
            CreateBarrier(new Vector3(-14f, 0.45f, 14f), 0f);
            CreateBarrier(new Vector3(-22f, 0.45f, 25.5f), 90f);
            if (!hasPropsKit)
            {
                if (!ArtPresentationLoader.HasPrefab("mdl_fuel_farm_v01"))
                    CreateBarrier(new Vector3(-28f, 0.45f, 18f), 0f);
                CreateBarrier(new Vector3(36f, 0.45f, 18f), 90f);
                CreateBarrier(new Vector3(6f, 0.45f, 24.5f), 0f);
                CreateBarrier(new Vector3(34f, 0.45f, 24.5f), 0f);
            }

            PlaceSignBoard(kit, new Vector3(10f, 0f, 22f), 90f);
            PlaceSignBoard(kit, new Vector3(-4f, 0f, 12f), 0f);
            if (!hasPropsKit)
            {
                PlaceSignBoard(kit, new Vector3(18f, 0f, 11.5f), 0f);
                PlaceSignBoard(kit, new Vector3(28f, 0f, 12f), 0f);
                PlaceSignBoard(kit, new Vector3(-18f, 0f, 16f), 90f);
            }

            // Hero dolly pair when props kit is dense; greybox keeps the fuller apron stack.
            PlaceBaggageDolly(kit, new Vector3(30f, 0f, 22f));
            PlaceBaggageDolly(kit, new Vector3(32.2f, 0f, 22f));
            if (!hasPropsKit)
            {
                PlaceBaggageDolly(kit, new Vector3(28f, 0f, 19.5f));
                PlaceBaggageDolly(kit, new Vector3(34f, 0f, 19.5f));
                PlaceBaggageDolly(kit, new Vector3(31f, 0f, 17.2f));
                PlaceBaggageDolly(kit, new Vector3(33.5f, 0f, 17.2f));
            }

            // Belt loaders live in the service kit, not the props kit (0025 wiring bug).
            // One authored hero loader when the kit is present; second only on greybox.
            var serviceKit = PreferArtKit(
                "Models/Props/mdl_service_equipment_kit_v03.gltf",
                "Models/Props/mdl_service_equipment_kit_authored_v01.gltf",
                "Models/Props/mdl_service_equipment_kit_v02.gltf",
                "Models/Props/mdl_service_equipment_kit_v01.gltf");
            var hasServiceKit = !string.IsNullOrEmpty(serviceKit) && ArtGltfLoader.HasKit(serviceKit);
            PlaceBeltLoader(serviceKit, new Vector3(12.5f, 0f, 21.5f), 200f, silhouetteOnly: hasServiceKit);
            if (!hasServiceKit)
                PlaceBeltLoader(serviceKit, new Vector3(29.5f, 0f, 15.5f), 110f, silhouetteOnly: false);

            BuildApronSafetyProps();
            BuildFuelFarm();
            BuildParkedGaAircraft();
        }

        /// <summary>
        /// Decision 0025 items 1+3 — fire hydrants, extinguisher cabinets and FOD bins
        /// so the apron edge reads as a working safety-equipped airfield.
        /// </summary>
        private static void BuildApronSafetyProps()
        {
            var hasHydrant = ArtPresentationLoader.HasPrefab("mdl_fire_hydrant_v01");
            var hasCabinet = ArtPresentationLoader.HasPrefab("mdl_extinguisher_cabinet_v01");
            var serviceKit = PreferArtKit(
                "Models/Props/mdl_service_equipment_kit_v03.gltf",
                "Models/Props/mdl_service_equipment_kit_authored_v01.gltf",
                "Models/Props/mdl_service_equipment_kit_v02.gltf",
                "Models/Props/mdl_service_equipment_kit_v01.gltf");
            var hasBinKit = !string.IsNullOrEmpty(serviceKit) && ArtGltfLoader.HasKit(serviceKit);

            PlaceFireHydrant("Hydrant apron NE", new Vector3(34f, 0f, 23.5f), 0f);
            PlaceFireHydrant("Hydrant apron NW", new Vector3(8.5f, 0f, 23.5f), 0f);
            if (!hasHydrant)
            {
                PlaceFireHydrant("Hydrant taxi", new Vector3(-2f, 0f, 11.5f), 90f);
                PlaceFireHydrant("Hydrant hangar", new Vector3(-14f, 0f, 16f), 0f);
            }

            PlaceExtinguisherCabinet("Extinguisher terminal", new Vector3(20f, 0f, 24.2f), 180f);
            PlaceExtinguisherCabinet("Extinguisher hangar", new Vector3(-15.5f, 0f, 24.2f), 180f);
            if (!hasCabinet)
                PlaceExtinguisherCabinet("Extinguisher ops", new Vector3(-5f, 0f, 24.2f), 180f);

            PlaceFodBin("FOD bin A", new Vector3(36f, 0f, 20f), 270f);
            PlaceFodBin("FOD bin B", new Vector3(10f, 0f, 11.2f), 0f);
            if (!hasBinKit)
                PlaceFodBin("FOD bin C", new Vector3(-24f, 0f, 16.5f), 90f);

            // Stand lead-in / box paint — skip when markings kit already placed stand stops
            // (avoid double-painted bays next to authored threshold/TDZ).
            if (FindBuilt("stand_stop_a") == null
                && FindBuilt("stand_stop_b") == null
                && FindBuilt("stand_stop_c") == null)
            {
                foreach (var z in new[] { 14f, 20f, 26f })
                {
                    CreateBlock($"Stand box front {z}", new Vector3(20f, 0.04f, z - 2.6f),
                        new Vector3(11.2f, 0.02f, 0.12f), Color.white);
                    CreateBlock($"Stand box back {z}", new Vector3(20f, 0.04f, z + 2.6f),
                        new Vector3(11.2f, 0.02f, 0.12f), Color.white);
                    CreateBlock($"Stand box L {z}", new Vector3(14.8f, 0.04f, z),
                        new Vector3(0.12f, 0.02f, 5.2f), Color.white);
                    CreateBlock($"Stand box R {z}", new Vector3(25.2f, 0.04f, z),
                        new Vector3(0.12f, 0.02f, 5.2f), Color.white);
                }
            }
        }

        /// <summary>
        /// Static GA aircraft west of the hangar so the GA apron reads occupied.
        /// Presentation-only; not in the simulation fleet.
        /// </summary>
        private static void BuildParkedGaAircraft()
        {
            var spots = new[]
            {
                (x: -30f, z: 14f, yaw: 90f),
                (x: -37f, z: 14f, yaw: 98f),
                (x: -33.5f, z: 10.5f, yaw: 105f),
                (x: -40.5f, z: 11.5f, yaw: 85f),
                (x: -27f, z: 11f, yaw: 110f)
            };
            // Fill all five authored tie-downs. These are background GA visitors, not
            // airline stands, so they add airport life without consuming a fleet bay.
            var count = spots.Length;
            for (var i = 0; i < count; i++)
            {
                var spot = spots[i];
                Transform root;
                if (ArtPresentationLoader.TryInstantiatePrefab("mdl_parked_ga_v01", out var prefabRoot))
                {
                    prefabRoot.name = $"Parked GA {i}";
                    root = prefabRoot;
                }
                else
                {
                    root = new GameObject($"Parked GA {i}").transform;
                    ParentBlock(root, "GA fuselage", Vector3.zero, new Vector3(0.55f, 0.55f, 2.4f), new Color(0.9f, 0.91f, 0.93f));
                    ParentBlock(root, "GA nose", new Vector3(0f, 0f, 1.15f), new Vector3(0.42f, 0.42f, 0.55f), new Color(0.9f, 0.91f, 0.93f));
                    ParentBlock(root, "GA wing", new Vector3(0f, 0.08f, 0.15f), new Vector3(3.2f, 0.08f, 0.7f), new Color(0.85f, 0.55f, 0.2f));
                    ParentBlock(root, "GA wing strut L", new Vector3(-0.9f, -0.12f, 0.15f), new Vector3(0.06f, 0.42f, 0.06f), new Color(0.4f, 0.4f, 0.42f));
                    ParentBlock(root, "GA wing strut R", new Vector3(0.9f, -0.12f, 0.15f), new Vector3(0.06f, 0.42f, 0.06f), new Color(0.4f, 0.4f, 0.42f));
                    ParentBlock(root, "GA tail", new Vector3(0f, 0.55f, -1.0f), new Vector3(0.1f, 0.9f, 0.55f), new Color(0.85f, 0.55f, 0.2f));
                    ParentBlock(root, "GA tailplane", new Vector3(0f, 0.4f, -1.05f), new Vector3(1.4f, 0.06f, 0.4f), new Color(0.85f, 0.55f, 0.2f));
                    ParentBlock(root, "GA canopy glass", new Vector3(0f, 0.32f, 0.45f), new Vector3(0.42f, 0.22f, 0.7f), new Color(0.2f, 0.35f, 0.45f, 0.42f));
                    ParentBlock(root, "GA spinner", new Vector3(0f, 0f, 1.55f), new Vector3(0.22f, 0.22f, 0.28f), new Color(0.25f, 0.25f, 0.28f));
                    ParentBlock(root, "GA prop blade A", new Vector3(0f, 0f, 1.48f), new Vector3(0.06f, 0.95f, 0.1f), new Color(0.2f, 0.2f, 0.22f));
                    ParentBlock(root, "GA gear nose", new Vector3(0f, -0.35f, 0.85f), new Vector3(0.08f, 0.35f, 0.08f), new Color(0.3f, 0.3f, 0.32f));
                    ParentBlock(root, "GA gear L", new Vector3(-0.5f, -0.35f, -0.15f), new Vector3(0.08f, 0.35f, 0.08f), new Color(0.3f, 0.3f, 0.32f));
                    ParentBlock(root, "GA gear R", new Vector3(0.5f, -0.35f, -0.15f), new Vector3(0.08f, 0.35f, 0.08f), new Color(0.3f, 0.3f, 0.32f));
                    ParentBlock(root, "GA stripe", new Vector3(0f, 0.05f, 0.1f), new Vector3(0.58f, 0.08f, 1.6f), new Color(0.85f, 0.55f, 0.2f));
                }

                root.position = new Vector3(spot.x, 0.55f, spot.z);
                root.rotation = Quaternion.Euler(0f, spot.yaw, 0f);
                CreateBlock($"Tie rope {i}a", new Vector3(spot.x - 1.4f, 0.08f, spot.z), new Vector3(0.2f, 0.06f, 0.2f), new Color(0.55f, 0.55f, 0.5f));
                CreateBlock($"Tie rope {i}b", new Vector3(spot.x + 1.4f, 0.08f, spot.z), new Vector3(0.2f, 0.06f, 0.2f), new Color(0.55f, 0.55f, 0.5f));
                PlaceContactShadow($"GA contact {i}", new Vector3(spot.x, 0.04f, spot.z), new Vector3(3.4f, 0.02f, 2.6f), 0.14f);
            }
        }

        private static AudioClip LoadEngineClip(AircraftType type)
        {
            var name = AircraftEngineAudio.ResourceName(type);
            if (_engineClipByResource.TryGetValue(name, out var cached) && cached != null)
                return cached;
            var clip = Resources.Load<AudioClip>(name) ?? CreateEngineClip();
            _engineClipByResource[name] = clip;
            return clip;
        }

        private static void AttachEngineAudio(Transform root, AircraftType type,
            float minDistance, float maxDistance, float volume)
        {
            var emitter = root.gameObject.GetComponent<AircraftSoundEmitter>()
                          ?? root.gameObject.AddComponent<AircraftSoundEmitter>();
            emitter.Configure(type, LoadEngineClip(type), CreateTouchdownClip());
            _ = minDistance;
            _ = maxDistance;
            _ = volume;
        }

        /// <summary>
        /// The procedural engine note. Identical for every aircraft, so it is synthesised once
        /// and shared; each fleet aircraft used to build and upload its own one-second clip.
        /// </summary>
        private static AudioClip CreateEngineClip()
        {
            if (_engineClip != null)
                return _engineClip;

            const int sampleRate = 22050;
            var samples = new float[sampleRate];
            for (var i = 0; i < samples.Length; i++)
            {
                var time = i / (float)sampleRate;
                samples[i] = (Mathf.Sin(time * 2f * Mathf.PI * 82f) * 0.12f) +
                             (Mathf.Sin(time * 2f * Mathf.PI * 164f) * 0.035f);
            }

            var clip = AudioClip.Create("Prototype engine", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            _engineClip = clip;
            return clip;
        }

        private static AudioClip CreateTouchdownClip()
        {
            if (_fallbackTouchdownClip != null)
                return _fallbackTouchdownClip;
            const int sampleRate = 22050;
            var samples = new float[sampleRate / 4];
            for (var i = 0; i < samples.Length; i++)
            {
                var time = i / (float)sampleRate;
                var envelope = Mathf.Exp(-time * 18f);
                var chirp = Mathf.Sin(time * 2f * Mathf.PI * (420f + time * 900f));
                var rumble = Mathf.Sin(time * 2f * Mathf.PI * 90f) * 0.35f;
                samples[i] = (chirp * 0.22f + rumble) * envelope;
            }

            var clip = AudioClip.Create("Touchdown chirp", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            _fallbackTouchdownClip = clip;
            return clip;
        }
    }
}
