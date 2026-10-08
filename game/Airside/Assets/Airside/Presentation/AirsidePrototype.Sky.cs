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
        private static WeatherKind? ReviewWeatherOverride(string[] args)
        {
            var index = Array.IndexOf(args, "-airsideReviewWeather");
            return index >= 0 && index + 1 < args.Length
                && Enum.TryParse(args[index + 1], true, out WeatherKind weather)
                    ? weather
                    : null;
        }

        /// <summary>
        /// Unity rejects <see cref="AudioSource.Play"/> for a disabled component or an
        /// inactive hierarchy.  Fleet presentation intentionally uses both states while it
        /// culls aircraft that are away, so callers must guard playback rather than retrying
        /// each frame.
        /// </summary>
        public static bool CanStartAudio(AudioSource source)
        {
            return source != null && source.isActiveAndEnabled && source.clip != null;
        }

        private void UpdateWindsock()
        {
            if (_windsockSock == null)
                return;

            // Aim the sock with the sim surface wind; keep a light sway so it does not look frozen.
            var wind = PresentationWind;
            var heading = RunwayWeather.UnityYawFromTrue(wind.DirectionDegrees);
            var sway = Mathf.Sin(Time.unscaledTime * AirsideReusableMotion.WindsockSwayHz * Mathf.PI * 2f) * 6f;
            var limp = Mathf.Lerp(18f, 4f, Mathf.Clamp01(wind.Knots / 18f));
            _windsockSock.localRotation = Quaternion.Euler(limp, heading + sway, 0f);
            // Keep parent scale stable; ripple fabric segments so authored children keep shape.
            _windsockSock.localScale = Vector3.one;
            // Each segment's ripple is an offset from the rotation it was built with. Writing
            // the ripple absolutely stood the fallback sock cylinder (built rolled 90 degrees
            // to lie along the wind) up on its end as a vertical tube.
            if (_windsockSegmentRest == null || _windsockSegmentRest.Length != _windsockSock.childCount)
            {
                _windsockSegmentRest = new Quaternion[_windsockSock.childCount];
                for (var i = 0; i < _windsockSegmentRest.Length; i++)
                    _windsockSegmentRest[i] = _windsockSock.GetChild(i).localRotation;
            }

            var rippleAmp = Mathf.Lerp(2f, 7f, Mathf.Clamp01(wind.Knots / 16f));
            for (var i = 0; i < _windsockSock.childCount; i++)
            {
                var seg = _windsockSock.GetChild(i);
                var ripple = Mathf.Sin(
                    Time.unscaledTime * AirsideReusableMotion.WindsockRippleHz * Mathf.PI * 2f * 1.4f
                    + i * 1.35f) * rippleAmp;
                seg.localRotation = _windsockSegmentRest[i] * Quaternion.Euler(ripple * 0.25f, 0f, ripple);
            }
        }

        private void UpdateWeatherPresentation()
        {
            var weather = CurrentWeather;
            var look = CurrentWeatherLook;
            var raining = look.IsRaining;
            var wet = Weather.IsAdverse(weather) || look.Wetness > 0.05f;
            var storm = weather == WeatherKind.Storm;

            if (_rainRoot != null)
                _rainRoot.gameObject.SetActive(raining);

            if (raining && _rainRoot != null)
                UpdateRainMesh(look.Precipitation, storm);

            // Fog colour and density are set once, in ApplyDayCycle, from AtmosphereLook (ADR 0143);
            // this used to set a second, competing fog here for wet or gloomy weather.


            // Darken + gloss paved surfaces when wet (VFX-004 / material wet variants).
            // Fog alone thickens atmosphere — it does not soak the apron.
            // Clear weather keeps a soft residual damp on paved slabs (REF day apron).
            var rainWetness = look.Wetness;
            // Wetness changes when a live sample arrives or the authored weather changes.
            // ApplyWetness toggles shader keywords — which invalidates the SRP Batcher
            // batch for that material. Re-applying every frame tore the batcher down
            // continuously, so only walk the surfaces when the target actually moves.
            if (!Mathf.Approximately(rainWetness, _lastAppliedWetness))
            {
                _lastAppliedWetness = rainWetness;
                for (var i = 0; i < _wetSurfaces.Count; i++)
                {
                    var (material, dry, drySmooth, dryMetallic, dryBump, paved, dryAlbedo) = _wetSurfaces[i];
                    if (material == null)
                        continue;
                    // Clear residual damp reads on overview like the turnaround dusk board.
                    var apply = rainWetness > 0.05f ? rainWetness : (paved ? 0.14f : 0f);
                    AirsideMaterialLibrary.ApplyWetness(
                        material, apply, dry, drySmooth, dryMetallic, dryBump,
                        preferWetConcreteAlbedo: paved && AirsideMaterialLibrary.AcceptsWetConcreteAlbedo(dryAlbedo),
                        dryAlbedo: dryAlbedo);
                }
            }

            SetLensWetness(rainWetness, CurrentDaylight);
            UpdateWetPuddles(rainWetness, storm);
            UpdateTaxiSpray(rainWetness, raining || storm);

            // Refresh apron probe when wetness or dusk shifts so Lit pavement picks up floods.
            if (_apronProbe != null && Time.unscaledTime >= _apronProbeRefreshAt)
            {
                _apronProbe.intensity = Mathf.Lerp(0.75f, 1.15f, rainWetness);
                MaybeRefreshApronProbe(PresentationDaylight, rainWetness);
                _apronProbeRefreshAt = Time.unscaledTime + 30f;
            }
        }

        private void UpdateTaxiSpray(float wetness, bool raining)
        {
            if (_taxiSprayRoot == null)
                return;

            Transform lead = null;
            for (var i = 0; i < VisualFlights.Count; i++)
            {
                var flight = VisualFlights[i];
                if (IsRotorcraftFlight(flight))
                    continue;
                var phase = flight.Operation.Phase;
                var progress = VisualPhaseProgress(flight, 0f);
                // Ground spray only — not climbing takeoff or airborne approach.
                var onGround = phase is AircraftPhase.TaxiIn or AircraftPhase.TaxiOut or AircraftPhase.Pushback
                    || phase == AircraftPhase.Landing
                    || (phase == AircraftPhase.Takeoff && progress < AirsideFlightPath.RotateProgress);
                if (!onGround)
                    continue;
                if (_commercialAircraft == null || i >= _commercialAircraft.Length)
                    continue;
                lead = _commercialAircraft[i];
                // A hidden fleet aircraft would leave spray hanging over empty tarmac.
                if (lead != null && lead.gameObject.activeInHierarchy)
                    break;
                lead = null;
            }

            var show = wetness > 0.12f && lead != null;
            _taxiSprayRoot.gameObject.SetActive(show);
            if (!show)
                return;

            _taxiSprayRoot.position = lead.position + Vector3.up * 0.2f;
            _taxiSprayRoot.rotation = lead.rotation;
            var n = _taxiSprayRoot.childCount;
            if (_taxiSprayRenderers == null || _taxiSprayRenderers.Length != n)
            {
                _taxiSprayRenderers = new Renderer[n];
                for (var i = 0; i < n; i++)
                    _taxiSprayRenderers[i] = _taxiSprayRoot.GetChild(i).GetComponent<Renderer>();
            }

            for (var i = 0; i < n; i++)
            {
                var puff = _taxiSprayRoot.GetChild(i);
                var pulse = 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * (6f + i) + i));
                var side = i % 2 == 0 ? -0.65f : 0.65f;
                puff.localPosition = new Vector3(side, 0.08f + pulse * 0.12f, -0.4f - i * 0.15f);
                puff.localScale = new Vector3(0.55f, 0.25f, 0.55f) * pulse * (raining ? 1.25f : 1f);
                var renderer = _taxiSprayRenderers[i];
                if (renderer != null)
                {
                    var color = GetRendererColor(renderer);
                    color.a = (0.18f + wetness * 0.28f) * pulse;
                    SetRendererColor(renderer, color);
                }
            }
        }

        private void UpdateWetPuddles(float wetness, bool storm)
        {
            if (_wetPuddleRoot == null)
                return;
            var show = wetness > 0.05f;
            _wetPuddleRoot.gameObject.SetActive(show);
            if (!show)
                return;
            var alpha = Mathf.Lerp(0.12f, storm ? 0.42f : 0.32f, wetness);
            if (_puddleRenderers == null)
            {
                var list = new List<Renderer>(16);
                for (var i = 0; i < _wetPuddleRoot.childCount; i++)
                {
                    var cluster = _wetPuddleRoot.GetChild(i);
                    for (var b = 0; b < cluster.childCount; b++)
                    {
                        var renderer = cluster.GetChild(b).GetComponent<Renderer>();
                        if (renderer != null)
                            list.Add(renderer);
                    }
                }

                _puddleRenderers = list.ToArray();
            }

            for (var i = 0; i < _puddleRenderers.Length; i++)
            {
                var renderer = _puddleRenderers[i];
                if (renderer == null)
                    continue;
                var color = GetRendererColor(renderer);
                color.a = alpha * (0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 0.7f + i * 0.4f));
                SetRendererColor(renderer, color);
            }
        }

        private void CollectWetSurfaces(Renderer[] renderers = null)
        {
            _wetSurfaces.Clear();
            // New surfaces have never been wetted — force the next weather pass to apply.
            _lastAppliedWetness = float.NaN;
            var seenMaterials = new HashSet<int>();
            renderers ??= AirsideSceneIndex.Renderers;
            foreach (var renderer in renderers)
            {
                if (renderer == null)
                    continue;
                var n = renderer.gameObject.name;
                if (!(n is "Hangar taxi link"
                        or "Service lane link W" or "Service lane link E" or "Fuel pad link"
                        or "ARFF apron link"
                        or "Access road stub" or "Access road elbow")
                    && !n.StartsWith("Service lane", StringComparison.Ordinal)
                    && !n.StartsWith("Fuel pad", StringComparison.Ordinal)
                    && !n.StartsWith("Access road", StringComparison.Ordinal)
                    && !n.StartsWith("Car park aisle", StringComparison.Ordinal)
                    && !n.StartsWith("Stand 3 apron", StringComparison.Ordinal)
                    && !n.StartsWith("Car park bay", StringComparison.Ordinal)
                    && !n.StartsWith("Runway 05", StringComparison.Ordinal) && !n.StartsWith("Runway 12", StringComparison.Ordinal) && !n.StartsWith("Taxiway ", StringComparison.Ordinal)
                    && !n.StartsWith("Pavement fillet", StringComparison.Ordinal)
                    && !n.StartsWith("Runway E", StringComparison.Ordinal)
                    && !n.StartsWith("Runway mid", StringComparison.Ordinal)
                    && !n.StartsWith("Runway blast", StringComparison.Ordinal)
                    && !n.StartsWith("Apron ", StringComparison.Ordinal)
                    && !n.StartsWith("Grass", StringComparison.Ordinal)
                    && !n.StartsWith("Infield grass", StringComparison.Ordinal)
                    && !n.StartsWith("Relief berm", StringComparison.Ordinal)
                    && !n.StartsWith("Coast ", StringComparison.Ordinal)
                    && !n.StartsWith("Outer paddock", StringComparison.Ordinal)
                    && !n.StartsWith("Outer horizon", StringComparison.Ordinal)
                    && !n.StartsWith("Coast dune", StringComparison.Ordinal)
                    && !n.StartsWith("Hill far", StringComparison.Ordinal)
                    && !n.StartsWith("Car park kerb", StringComparison.Ordinal)
                    && !n.StartsWith("Coast scrub", StringComparison.Ordinal)
                    && !n.StartsWith("Apron joint", StringComparison.Ordinal)
                    && !n.StartsWith("Apron fringe", StringComparison.Ordinal)
                    && !n.StartsWith("Apron slab", StringComparison.Ordinal)
                    && !n.StartsWith("Taxi lead", StringComparison.Ordinal)
                    && !n.StartsWith("Taxiway A", StringComparison.Ordinal)
                    && !n.StartsWith("Hangar apron", StringComparison.Ordinal)
                    && !n.StartsWith("Runway marking", StringComparison.Ordinal)
                    && !n.StartsWith("Runway edge", StringComparison.Ordinal)
                    && !n.StartsWith("Threshold", StringComparison.Ordinal)
                    && !n.StartsWith("Hold short", StringComparison.Ordinal)
                    && !n.StartsWith("Taxi edge", StringComparison.Ordinal)
                    && !n.StartsWith("Stand stop", StringComparison.Ordinal)
                    && !n.StartsWith("Stand number", StringComparison.Ordinal)
                    && !n.StartsWith("Access turn", StringComparison.Ordinal)
                    && !n.StartsWith("Access centreline", StringComparison.Ordinal)
                    && !n.StartsWith("Access edge", StringComparison.Ordinal)
                    && !n.StartsWith("Taxi exit centre", StringComparison.Ordinal)
                    && !n.StartsWith("Drop-off zebra", StringComparison.Ordinal)
                    && !n.StartsWith("Overflow bay", StringComparison.Ordinal)
                    && !n.StartsWith("Bay line", StringComparison.Ordinal)
                    && !n.StartsWith("Stall line", StringComparison.Ordinal)
                    && !n.StartsWith("Taxi arrow", StringComparison.Ordinal)
                    && !n.StartsWith("Alpha edge", StringComparison.Ordinal)
                    && !n.StartsWith("Alpha centre", StringComparison.Ordinal)
                    && !n.StartsWith("Runway digit", StringComparison.Ordinal)
                    && !n.StartsWith("Stand lead", StringComparison.Ordinal)
                    && !n.StartsWith("Apron chevron", StringComparison.Ordinal)
                    && !n.StartsWith("Hold short", StringComparison.Ordinal)
                    && !n.StartsWith("Taxi centre", StringComparison.Ordinal)
                    && !n.StartsWith("Aiming point", StringComparison.Ordinal)
                    && !n.StartsWith("TDZ ", StringComparison.Ordinal)
                    && !n.StartsWith("Threshold stripe", StringComparison.Ordinal)
                    && !n.StartsWith("Jetty ", StringComparison.Ordinal)
                    && !n.StartsWith("ARFF apron", StringComparison.Ordinal)
                    && !n.StartsWith("Fuel ", StringComparison.Ordinal)
                    && !n.StartsWith("Terminal canopy", StringComparison.Ordinal)
                    && !n.StartsWith("Stand box", StringComparison.Ordinal)
                    && !n.StartsWith("Access road shoulder", StringComparison.Ordinal)
                    && !n.StartsWith("Runway shoulder", StringComparison.Ordinal)
                    && !n.StartsWith("Access turn shoulder", StringComparison.Ordinal)
                    // Authored markings kit mesh names (glTF nodes), not CreateBlock titles.
                    // Keep prefixes tight so lighting kit taxi_/edge_/runway_edge_light stay dry.
                    && !n.StartsWith("runway_centre", StringComparison.Ordinal)
                    && !n.StartsWith("runway_edge_left", StringComparison.Ordinal)
                    && !n.StartsWith("runway_edge_right", StringComparison.Ordinal)
                    && !n.StartsWith("runway_threshold", StringComparison.Ordinal)
                    && !n.StartsWith("taxi_centreline", StringComparison.Ordinal)
                    && !n.StartsWith("taxi_edge_", StringComparison.Ordinal)
                    && !n.StartsWith("taxi_arrow_", StringComparison.Ordinal)
                    && !n.StartsWith("hold_short_", StringComparison.Ordinal)
                    && !n.StartsWith("threshold_", StringComparison.Ordinal)
                    && !n.StartsWith("stand_stop_", StringComparison.Ordinal)
                    && !n.StartsWith("aiming_", StringComparison.Ordinal)
                    && !n.StartsWith("tdz_", StringComparison.Ordinal)
                    && !n.StartsWith("chevron_", StringComparison.Ordinal)
                    && !n.StartsWith("digit_", StringComparison.Ordinal)
                    && !n.StartsWith("apron_arrow_", StringComparison.Ordinal)
                    && !n.StartsWith("Relief mound", StringComparison.Ordinal)
                    && !n.StartsWith("Grass ribbon", StringComparison.Ordinal)
                    && !n.StartsWith("Coast dune", StringComparison.Ordinal))
                    continue;

                var mat = renderer.sharedMaterial;
                if (mat == null)
                    continue;
                var materialId = mat.GetInstanceID();
                if (!seenMaterials.Add(materialId))
                    continue;
                var drySmooth = 0.28f;
                if (mat.HasProperty("_Smoothness"))
                    drySmooth = mat.GetFloat("_Smoothness");
                else if (mat.HasProperty("_Glossiness"))
                    drySmooth = mat.GetFloat("_Glossiness");
                var dryMetallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0.02f;
                var dryBump = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 0.5f;
                _wetSurfaces.Add((mat, mat.color, drySmooth, dryMetallic, dryBump,
                    IsPavedSurfaceName(n), mat.mainTexture));
            }
        }

        /// <summary>
        /// Soft reflective puddle discs on the apron / taxi — visible wet response beyond
        /// material darken (0025 items 4+7). Presentation only.
        /// </summary>
        private void BuildWetPuddles()
        {
            var root = new GameObject("Wet puddles").transform;
            _wetPuddleRoot = root;
            var spots = AirsideBareField.Enabled
                ? AdelaideWetPuddleSpots()
                : new[]
            {
                new Vector3(18f, 0.07f, 15f),
                new Vector3(24f, 0.07f, 19f),
                new Vector3(14f, 0.07f, 20.5f),
                new Vector3(28f, 0.07f, 14.5f),
                new Vector3(8f, 0.07f, 10f),
                new Vector3(4f, 0.07f, 9f),
                new Vector3(20f, 0.07f, 12f),
                new Vector3(-18f, 0.07f, 16f),
                new Vector3(32f, 0.07f, 18f),
                new Vector3(22f, 0.07f, 22f),
                new Vector3(16f, 0.07f, 17.5f),
                new Vector3(26f, 0.07f, 16f),
                new Vector3(10f, 0.07f, 14f),
                new Vector3(30f, 0.07f, 21f),
                new Vector3(-14f, 0.07f, 18f),
                new Vector3(12f, 0.07f, 18.5f),
                new Vector3(34f, 0.07f, 15f),
                new Vector3(6f, 0.07f, 12.5f),
                new Vector3(48f, 0.07f, 46f),
                new Vector3(44f, 0.07f, 44f),
                new Vector3(26f, 0.07f, 38f),
                new Vector3(-20f, 0.07f, 28.5f),
                new Vector3(-34f, 0.07f, 22f),
                new Vector3(0f, 0.07f, 2f),
                new Vector3(-6f, 0.07f, 0.5f),
                new Vector3(6f, 0.07f, -0.5f),
                new Vector3(18f, 0.07f, 9f),
                new Vector3(2f, 0.07f, 9f),
                new Vector3(40f, 0.07f, 46f),
                new Vector3(52f, 0.07f, 48f),
                new Vector3(-24f, 0.07f, 28f),
                new Vector3(22f, 0.07f, 14f)
            };

            // Batch F4 VFX-004 — reusable wet accent kit first (presentation only).
            Transform wetKit = null;
            var hasWetKit = !AirsideBareField.Enabled
                && ArtPresentationLoader.TryInstantiatePrefab("vfx_wet_surface_response_v01", out wetKit);
            if (hasWetKit)
            {
                wetKit.SetParent(root, false);
                wetKit.localPosition = new Vector3(20f, 0f, 16f);
                wetKit.name = "Wet surface kit";
            }

            // Kit owns the wet read — keep a short hero apron/taxi set; full carpet is fallback.
            var spotCount = hasWetKit ? 10 : spots.Length;
            for (var i = 0; i < spotCount; i++)
            {
                // Irregular multi-blob puddles (REF soft damp patches, not toy discs).
                var cluster = new GameObject($"Puddle {i}").transform;
                cluster.SetParent(root, false);
                cluster.position = spots[i];
                // One irregular accent per real Adelaide stand keeps the large field cheap;
                // the legacy miniature keeps its denser clustered treatment.
                var blobs = AirsideBareField.Enabled ? 1 : hasWetKit ? 1 + (i % 2) : 2 + (i % 3);
                for (var b = 0; b < blobs; b++)
                {
                    var puddle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    puddle.name = $"Puddle {i} blob {b}";
                    DestroyPresentationObject(puddle.GetComponent<Collider>());
                    puddle.transform.SetParent(cluster, false);
                    var ox = ((b * 37 + i * 13) % 17) * 0.06f - 0.4f;
                    var oz = ((b * 29 + i * 11) % 15) * 0.07f - 0.35f;
                    puddle.transform.localPosition = new Vector3(ox, 0f, oz);
                    var rx = 0.7f + (i % 3) * 0.35f + b * 0.15f;
                    var rz = rx * (0.45f + (b % 3) * 0.22f);
                    puddle.transform.localScale = new Vector3(rx, 0.012f, rz);
                    puddle.transform.localRotation = Quaternion.Euler(0f, (i * 23 + b * 41) % 360, 0f);
                    var puddleRenderer = puddle.GetComponent<Renderer>();
                    puddleRenderer.sharedMaterial = AirsideMaterialLibrary.CreateShared(
                        new Color(0.2f, 0.28f, 0.34f, 0.28f),
                        AirsideMaterialLibrary.SurfaceKind.Water);
                    SetRendererColor(puddleRenderer, new Color(0.2f, 0.28f, 0.34f, 0.28f));
                    puddleRenderer.GetPropertyBlock(RendererTintBlock);
                    RendererTintBlock.SetFloat("_Smoothness", 0.96f);
                    RendererTintBlock.SetFloat("_Metallic", 0.35f);
                    puddleRenderer.SetPropertyBlock(RendererTintBlock);
                    puddleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }

            root.gameObject.SetActive(false);
        }

        private static Vector3[] AdelaideWetPuddleSpots()
        {
            var spots = new List<Vector3>(AdelaideLayout.Bays.Length + AdelaideGateAlignment.Gates.Length);

            void Add(float x, float z, float headingDegrees, int index)
            {
                // Keep pooled water out from under parked wheels and break up the otherwise
                // immaculate apron. Alternating sides prevents a visibly repeated stripe.
                var radians = headingDegrees * Mathf.Deg2Rad;
                var side = index % 2 == 0 ? 1f : -1f;
                var rightX = Mathf.Cos(radians) * side;
                var rightZ = -Mathf.Sin(radians) * side;
                var forwardX = Mathf.Sin(radians);
                var forwardZ = Mathf.Cos(radians);
                var px = x + rightX * (5f + index % 3) - forwardX * (3f + index % 2);
                var pz = z + rightZ * (5f + index % 3) - forwardZ * (3f + index % 2);
                spots.Add(new Vector3(px, AirsideAdelaideGround.WorldHeight(px, pz) + 0.07f, pz));
            }

            for (var i = 0; i < AdelaideLayout.Bays.Length; i++)
            {
                var bay = AdelaideLayout.Bays[i];
                Add(bay.StopX, bay.StopZ, bay.HeadingDegrees, i);
            }

            for (var i = 0; i < AdelaideGateAlignment.Gates.Length; i++)
            {
                var gate = AdelaideGateAlignment.Gates[i];
                Add(gate.NoseX, gate.NoseZ, gate.HeadingDegrees, AdelaideLayout.Bays.Length + i);
            }

            return spots.ToArray();
        }

        /// <summary>Drops span local z −10…40, so the box is centred on the camera focus at ground level.</summary>
        public static Vector3 RainRootPosition(Vector3 focus) =>
            new(focus.x, AirsideAdelaideGround.WorldHeight(focus.x, focus.z), focus.z - 15f);

        private static Transform BuildTaxiSprayRoot()
        {
            var root = new GameObject("Taxi spray").transform;
            for (var i = 0; i < 4; i++)
            {
                var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                puff.name = $"Spray {i}";
                DestroyPresentationObject(puff.GetComponent<Collider>());
                puff.transform.SetParent(root, false);
                puff.transform.localScale = new Vector3(0.5f, 0.22f, 0.5f);
                var puffRenderer = puff.GetComponent<Renderer>();
                puffRenderer.sharedMaterial = CreateMaterial(new Color(0.75f, 0.8f, 0.85f, 0.25f));
                SetRendererColor(puffRenderer, new Color(0.75f, 0.8f, 0.85f, 0.25f));
            }

            root.gameObject.SetActive(false);
            return root;
        }

        public static float WeatherGloomTarget(WeatherKind weather) =>
            WeatherLook.For(weather).Gloom;

        /// <summary>
        /// Weather is a discrete forecast, so a change of kind snapped sun intensity, trilight
        /// and the whole post grade in one frame. Gloom now drifts at 0.05/s (clear to storm
        /// in about 11 s). The first frame lands on the target.
        /// </summary>
        public static float EaseWeatherGloom(float current, float target, float deltaSeconds) =>
            Mathf.MoveTowards(current, target, deltaSeconds * 0.05f);

        private void ApplyDayCycle()
        {
            var celestial = PresentationCelestial;
            var daylight = PresentationDaylight;
            CurrentDaylight = daylight;

            var sunElevation = PinDaylightPresentation ? 48.0 : celestial.Sun.ElevationDegrees;
            var sunAzimuth = PinDaylightPresentation ? 0.0 : celestial.Sun.AzimuthDegrees;
            SkyDirection.ToWorld(sunAzimuth, sunElevation, out var sunX, out var sunY, out var sunZ);
            var sunDir = new Vector3((float)sunX, (float)sunY, (float)sunZ);
            if (sunDir.sqrMagnitude < 1e-6f)
                sunDir = Vector3.up;
            sunDir.Normalize();
            // Clamp the key just below the horizon so night still has a directional shade
            // without flipping the light through the ground.
            var lightDir = sunDir;
            if (lightDir.y < -0.10f)
            {
                lightDir.y = -0.10f;
                lightDir.Normalize();
            }

            _sun.transform.rotation = Quaternion.LookRotation(-lightDir);

            var day = new Color(1f, 0.96f, 0.88f);
            var goldenHour = new Color(1f, 0.68f, 0.42f);
            var night = new Color(0.32f, 0.38f, 0.55f);
            var warm = PinDaylightPresentation
                ? 0f
                : (float)CelestialSky.GoldenHour(celestial.Sun.ElevationDegrees);
            _sun.color = Color.Lerp(Color.Lerp(night, day, daylight), goldenHour, warm * Mathf.Max(daylight, 0.12f));
            // Noon punch; night key stays dim so flood pools (not a blue wash) light the apron.
            // Night floor raised 0.18 -> 0.30 alongside ADR 0063's ambient/exposure floor: the
            // ambient trilight is flat (no shading), so even with that floor raised the field
            // read as a uniform grey wash with no sense of form. A moonlight-strength key still
            // well under the floods' own intensity (52 apron / 1.55-2.1 runway, ADR 0063) adds
            // real directional shading — aircraft, hangars and terrain read as shapes, not silhouettes
            // dissolved into flat ambient.
            _sun.intensity = Mathf.Lerp(0.30f, 1.25f, Mathf.SmoothStep(0f, 1f, daylight));
            _sun.shadowStrength = Mathf.Lerp(0.28f, 0.78f, daylight);

            // Weather gloom cools the post stack (rain/fog/storm) without fighting day fog.
            var weatherGloom = EaseWeatherGloom(_weatherGloom, CurrentWeatherLook.Gloom * (1f - CockpitAboveDeck),
                _weatherGloomReady ? Time.unscaledDeltaTime : float.PositiveInfinity);
            _weatherGloom = weatherGloom;
            _weatherGloomReady = true;
            if (weatherGloom > 0f)
                _sun.intensity *= Mathf.Lerp(1f, 0.72f, weatherGloom);
            var nightLevel = AirsideSettings.Current.NightBrightness;
            _dayVolume?.Apply(daylight, warm, weatherGloom, NightVisibility.ExposureLift(nightLevel, daylight));

            if (_fillLight != null)
            {
                SkyDirection.ToWorld(sunAzimuth + 180.0, 28.0, out var fillX, out var fillY, out var fillZ);
                var fillDir = new Vector3((float)fillX, (float)fillY, (float)fillZ);
                if (fillDir.sqrMagnitude < 1e-6f)
                    fillDir = Vector3.up;
                _fillLight.transform.rotation = Quaternion.LookRotation(-fillDir.normalized);
                // Cool day fill opens shadows; night fill is soft blue-grey form light only.
                _fillLight.color = Color.Lerp(
                    new Color(0.28f, 0.34f, 0.52f),
                    Color.Lerp(new Color(0.62f, 0.72f, 0.9f), new Color(1f, 0.82f, 0.68f), warm * 0.45f),
                    daylight);
                _fillLight.intensity = Mathf.Lerp(0.52f, 0.22f, daylight) + warm * 0.05f;
            }

            // Trilight: day = bright cool sky / warm ground separation; night = deep blue-grey
            // that still lets hangar/terminal silhouettes read outside flood pools.
            //
            // The night floor here used to be materially darker (ambientNight (0.20,0.23,0.32),
            // ambientIntensity 0.88): fine directly under a flood or runway light (9-115 m
            // range), but the default Fleet/career overview camera sits ~2400 m out over a
            // ~3900x2800 m field (AirsideBareField.OverviewDistance) — from there almost the
            // entire frame is outside every light's range and lit by this ambient alone, which
            // a real player reported as "can't see anything" at night. Lifted the night floor
            // enough that the ambient-only majority of the field reads as a dim, navigable dark
            // blue-grey instead of crushing toward black once ACES tonemapping and the night
            // exposure dip (AirsideDayVolume) are applied on top — floods/runway lights are
            // still 3-150x brighter in absolute terms, so they keep reading as the brightest
            // pools rather than the only visible things.
            var ambientDay = new Color(0.42f, 0.43f, 0.44f);
            var ambientDusk = new Color(0.52f, 0.36f, 0.3f);
            var ambientNight = new Color(0.28f, 0.32f, 0.42f);
            var ambientSky = Color.Lerp(Color.Lerp(ambientNight, ambientDay, daylight), ambientDusk, warm * 0.55f);
            var ambientEquator = Color.Lerp(
                new Color(0.30f, 0.32f, 0.40f),
                Color.Lerp(new Color(0.32f, 0.35f, 0.38f), new Color(0.5f, 0.38f, 0.32f), warm),
                daylight);
            var ambientGround = Color.Lerp(
                new Color(0.19f, 0.20f, 0.24f),
                Color.Lerp(new Color(0.26f, 0.28f, 0.22f), new Color(0.3f, 0.2f, 0.15f), warm),
                daylight);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.ambientIntensity = (Mathf.Lerp(1.05f, 1.0f, daylight) + warm * 0.06f)
                * NightVisibility.AmbientGain(nightLevel, daylight);
            if (weatherGloom > 0f)
            {
                // Dim trilight under fog/rain/storm — ambientLight is ignored in Trilight mode.
                ambientSky = Color.Lerp(ambientSky, ambientSky * 0.72f, weatherGloom);
                ambientEquator = Color.Lerp(ambientEquator, ambientEquator * 0.7f, weatherGloom);
                ambientGround = Color.Lerp(ambientGround, ambientGround * 0.65f, weatherGloom);
                RenderSettings.ambientSkyColor = ambientSky;
                RenderSettings.ambientEquatorColor = ambientEquator;
                RenderSettings.ambientGroundColor = ambientGround;
                RenderSettings.ambientIntensity *= Mathf.Lerp(1f, 0.78f, weatherGloom);
            }
            RenderSettings.subtractiveShadowColor = Color.Lerp(
                new Color(0.18f, 0.24f, 0.36f),
                new Color(0.38f, 0.28f, 0.26f),
                warm);

            // ADR 0143: one sky for the frame. The background, horizon dome and fog all come from
            // AtmosphereLook, which carries the weather (REF-001 clear blue on a clear day, grey-blue
            // overcast, slate storm, pale fog) and the camera's height for the fog.
            var cameraHeight = _mainCamera != null ? Mathf.Max(0f, _mainCamera.transform.position.y) : 0f;
            _atmosphere = AtmosphereLook.For(CurrentWeatherLook, daylight, warm, sunAzimuth < 180.0, cameraHeight);
            var sky = ToColor(_atmosphere.Sky);
            var aboveDeck = CockpitAboveDeck;
            var clearSky = AtmosphereLook.For(WeatherLook.For(WeatherKind.Clear), daylight,
                warm, sunAzimuth < 180.0, cameraHeight);
            if (AirsideSettings.Current.WeatherLayers && _mainCamera != null)
            {
                sky = Color.Lerp(sky, ToColor(clearSky.Sky), aboveDeck);
            }
            if (_mainCamera != null)
                _mainCamera.backgroundColor = sky;
            if (_horizonDome != null)
            {
                if (_horizonDomeRenderer == null)
                    _horizonDomeRenderer = _horizonDome.GetComponent<Renderer>();
                if (_horizonDomeRenderer != null)
                    SetRendererColor(_horizonDomeRenderer, sky, sky * Mathf.Lerp(0.35f, 1f, daylight));
            }

            UpdateSunAndMoonDiscs(daylight, warm, celestial);

            // ADR 0143: the only fog path. Colour matches the sky; density comes from visibility
            // (thinner for a high camera). The compact QA scene keeps its own small-scale density.
            {
                var look = CurrentWeatherLook;
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = ToColor(_atmosphere.Fog);
                RenderSettings.fogDensity = AirsideBareField.Enabled
                    ? _atmosphere.FogDensity * AirsideCameraFeel.FogScale(AirsideCameraController.CurrentDistance,
                        AirsideBareField.ClassicMaxOrbitDistance)
                    : _atmosphere.FogDensity;
            }

            if (AirsideSettings.Current.WeatherLayers && _mainCamera != null)
            {
                var cloud = ObserverInCloud;
                RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, 0.018f, cloud);
                RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, ToColor(_atmosphere.Sky), cloud);
                RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, clearSky.FogDensity, aboveDeck);
                RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, ToColor(clearSky.Fog), aboveDeck);
            }

            // ADR 0059: a storm strike briefly overrides the sky/ambient/sun with a white
            // flash that decays over ~0.5 s of real time, independent of the steady weather
            // gloom set above — that is the storm's baseline dimness, this is one instant.
            var flash = CurrentWeather == WeatherKind.Storm && AirsideSettings.Current.WeatherLayers
                ? LightningFlashEnvelope(Time.unscaledTime - _lightningFlashAt) : 0f;
            if (flash > 0f)
            {
                var punch = flash * Mathf.Lerp(0.08f, 1f, 1f - CockpitAboveDeck) * Mathf.Lerp(0.15f, 0.55f, 1f - _lightningDistance01);
                _sun.intensity += punch * 2.4f;
                RenderSettings.ambientIntensity += punch * 1.1f;
                RenderSettings.ambientSkyColor = Color.Lerp(RenderSettings.ambientSkyColor, Color.white, punch * 0.6f);
                RenderSettings.ambientEquatorColor = Color.Lerp(RenderSettings.ambientEquatorColor, Color.white, punch * 0.5f);
                RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, Color.white, punch * 0.5f);
                if (_mainCamera != null)
                    _mainCamera.backgroundColor = Color.Lerp(_mainCamera.backgroundColor, Color.white, punch * 0.7f);
                if (_horizonDomeRenderer != null)
                    SetRendererColor(_horizonDomeRenderer, Color.Lerp(sky, Color.white, punch * 0.7f), Color.white);
            }

            // Apron floods come up as daylight falls (presentation only).
            if (_apronLights != null)
            {
                // Warm night pools so REF-002 apron reads; day floods stay off.
                // The Adelaide roof floods throw roughly 50 m onto the stands; inverse-square
                // attenuation needs materially more intensity than the 16 m legacy diorama.
                var flood = Mathf.Lerp(AirsideBareField.Enabled ? 52f : 3.6f, 0.04f,
                    Mathf.SmoothStep(0f, 1f, daylight));
                for (var i = 0; i < _apronLights.Length; i++)
                {
                    var light = _apronLights[i];
                    if (light == null)
                        continue;
                    // Tiny phase offset flicker so floods don't feel static at night.
                    var flicker = daylight < 0.4f
                        ? 1f + 0.04f * Mathf.Sin(
                            Time.unscaledTime * AirsideReusableMotion.FloodFlickerHz * Mathf.PI * 2f + i * 1.7f)
                        : 1f;
                    // Corner masts (0–3) get a bit more punch than fill floods.
                    var boost = i < 4 ? 1.15f : 1f;
                    light.intensity = flood * flicker * boost;
                    light.enabled = flood > 0.06f;
                }
            }

            // Landside streetlights along the access road / car park.
            if (_landsideLights != null)
            {
                var street = Mathf.Lerp(1.15f, 0.02f, daylight);
                for (var i = 0; i < _landsideLights.Length; i++)
                {
                    var light = _landsideLights[i];
                    if (light == null)
                        continue;
                    var flicker = daylight < 0.4f
                        ? 1f + 0.035f * Mathf.Sin(
                            Time.unscaledTime * AirsideReusableMotion.FloodFlickerHz * Mathf.PI * 2f + i * 2.1f + 0.8f)
                        : 1f;
                    light.intensity = street * flicker;
                    // A 0.02 lamp is invisible but still costs a per-object light slot.
                    light.enabled = street > 0.05f;
                }
            }

            // Threshold / approach point lights punch up at dusk for runway ends.
            if (_thresholdLights != null)
            {
                var approach = Mathf.Lerp(1.85f, 0.04f, daylight);
                var sequenced = daylight < 0.42f;
                if (_hialStation == null || _hialStation.Length != _thresholdLights.Length)
                    _hialStation = HialStations(_thresholdLights, out _hialLast);
                for (var i = 0; i < _thresholdLights.Length; i++)
                {
                    var light = _thresholdLights[i];
                    if (light == null)
                        continue;
                    var level = approach;
                    // Runway 23's HIAL carries sequenced flashers: a bright flash that runs from the far end
                    // of the approach in toward the threshold, twice a second, over a steady base.
                    if (sequenced && _hialStation[i] >= 0 && _hialLast > 0)
                    {
                        var behind = (_hialLast - _hialStation[i]) / (float)_hialLast;
                        var phase = Mathf.Repeat(Time.unscaledTime * AirsideReusableMotion.AlsChaseHz * 0.8f - behind * 0.85f, 1f);
                        level = approach * (0.45f + 2.3f * (phase < 0.14f ? Mathf.Sin(phase / 0.14f * Mathf.PI) : 0f));
                    }
                    light.intensity = level;
                    light.enabled = level > 0.05f;
                }
            }

            // ALS centreline / bar lamps — steady dusk base, sequential chase at night.
            if (_alsLights != null)
            {
                var alsBase = Mathf.Lerp(2.1f, 0.03f, daylight);
                var nightChase = daylight < 0.42f;
                var chase = Time.unscaledTime * AirsideReusableMotion.AlsChaseHz;
                _alsReilSide ??= ReilSides(_alsLights);
                for (var i = 0; i < _alsLights.Length; i++)
                {
                    var light = _alsLights[i];
                    if (light == null)
                        continue;

                    // Far ALS REIL spots — sharp night flash, not centreline chase.
                    if (_alsReilSide[i] != 0)
                    {
                        var reilFlash = daylight < 0.42f
                            && Mathf.Repeat(
                                Time.unscaledTime * AirsideReusableMotion.ReilFlashHz
                                + (_alsReilSide[i] == 2 ? 0.5f : 0f), 1f) < 0.18f;
                        light.intensity = reilFlash ? 4.2f : alsBase * 0.25f;
                        light.enabled = daylight < 0.55f;
                        continue;
                    }

                    if (!nightChase)
                    {
                        light.intensity = alsBase;
                        light.enabled = alsBase > 0.05f;
                        continue;
                    }

                    // Chase from far approach (high index) toward the threshold (index 0).
                    var step = (Mathf.Min(_alsLights.Length, 8) - 1 - i) * 0.42f;
                    var wave = Mathf.Repeat(chase - step, 2.4f);
                    var pulse = wave < 0.4f
                        ? Mathf.SmoothStep(0f, 1f, 1f - Mathf.Abs(wave / 0.2f - 1f))
                        : 0f;
                    light.intensity = alsBase * (0.4f + 1.8f * pulse);
                    light.enabled = true;
                }
            }

            UpdateArffLightbar(daylight);

            // Sparse runway-edge point lights so the strip reads as a lit ribbon at night.
            if (_runwayEdgeLights != null)
            {
                var edge = Mathf.Lerp(1.55f, 0.02f, daylight);
                var reilPulse = daylight < 0.42f
                    ? (Mathf.Repeat(Time.unscaledTime * 1.8f, 1f) < 0.22f ? 2.6f : 0.15f)
                    : 0f;
                _runwayEdgeReilSide ??= ReilSides(_runwayEdgeLights);
                for (var i = 0; i < _runwayEdgeLights.Length; i++)
                {
                    var light = _runwayEdgeLights[i];
                    if (light == null)
                        continue;
                    if (_runwayEdgeReilSide[i] != 0)
                    {
                        light.intensity = edge * 0.35f + reilPulse;
                        light.enabled = daylight < 0.55f;
                        continue;
                    }

                    light.intensity = edge;
                    light.enabled = edge > 0.05f;
                }
            }

            // Per-stand marker + lead-in lights: a gentler night flicker than the runway/
            // apron lighting so individual stands read as marked without competing with it.
            if (_standLights != null)
            {
                var stand = Mathf.Lerp(1.1f, 0.03f, daylight);
                for (var i = 0; i < _standLights.Length; i++)
                {
                    var light = _standLights[i];
                    if (light == null)
                        continue;
                    var flicker = daylight < 0.4f
                        ? 1f + 0.03f * Mathf.Sin(
                            Time.unscaledTime * AirsideReusableMotion.FloodFlickerHz * Mathf.PI * 2f + i * 1.7f)
                        : 1f;
                    light.intensity = stand * flicker;
                    light.enabled = stand > 0.05f;
                }
            }

            if (_apronProbe != null)
            {
                _apronProbe.intensity = Mathf.Lerp(1.15f, 0.85f, daylight);
                MaybeRefreshApronProbe(daylight, 0f);
            }

            if (_terminalProbe != null)
                _terminalProbe.intensity = Mathf.Lerp(1.05f, 0.8f, daylight);

            UpdateAirfieldNavLights(daylight);
            UpdateNightGlow(daylight);
            UpdateAerodromeBeacon(daylight);
        }

        private static void BuildStarField()
        {
            // One mesh of inward quads: a denser sky still costs a single draw call.
            var root = new GameObject("Star field").transform;
            var rng = new System.Random(31415);
            const int count = 720;
            const int points = 8;
            const int verticesPerStar = points + 1;
            var vertices = new Vector3[count * verticesPerStar];
            var triangles = new int[count * points * 3];
            var colors = new Color[count * verticesPerStar];
            for (var i = 0; i < count; i++)
            {
                var yaw = (float)rng.NextDouble() * 360f;
                // Uniform by sky area rather than pitch, avoiding an artificial bright
                // ring near the zenith. The real-airport overview looks only a few degrees
                // above the horizon, so begin at 2° rather than hiding the whole field at 8°.
                var y = Mathf.Lerp(Mathf.Sin(2f * Mathf.Deg2Rad), 0.985f, (float)rng.NextDouble());
                var horizontal = Mathf.Sqrt(1f - y * y);
                var dir = new Vector3(
                    Mathf.Sin(yaw * Mathf.Deg2Rad) * horizontal,
                    y,
                    Mathf.Cos(yaw * Mathf.Deg2Rad) * horizontal);
                // Radius controls angular size only. The dedicated celestial shader
                // projects stars to background depth and never writes scene depth.
                const float radius = 128f * StarDistanceScale;
                var pos = dir * radius;
                var hero = i % 31 == 0 ? 1.65f : i % 11 == 0 ? 1.22f : 1f;
                var s = (0.10f + (float)rng.NextDouble() * 0.18f) * StarDistanceScale * hero;
                var bright = 0.65f + (float)rng.NextDouble() * 0.35f;
                var tint = (float)rng.NextDouble();
                var color = ToColor(CelestialStarColour.For(bright, tint));
                var right = Vector3.Cross(dir, Vector3.up);
                if (right.sqrMagnitude < 0.001f)
                    right = Vector3.right;
                right.Normalize();
                var up = Vector3.Cross(right, dir).normalized;
                var v = i * verticesPerStar;
                vertices[v] = pos;
                colors[v] = color;
                for (var p = 0; p < points; p++)
                {
                    var radians = p * Mathf.PI * 2f / points;
                    var radiusScale = p % 2 == 0 ? 1f : 0.28f;
                    vertices[v + 1 + p] = pos
                        + (right * Mathf.Cos(radians) + up * Mathf.Sin(radians)) * (s * radiusScale);
                    colors[v + 1 + p] = new Color(color.r * 0.82f, color.g * 0.82f, color.b * 0.82f, 1f);

                    var t = (i * points + p) * 3;
                    triangles[t] = v;
                    triangles[t + 1] = v + 1 + p;
                    triangles[t + 2] = v + 1 + (p + 1) % points;
                }
            }

            var mesh = new Mesh { name = "Star field" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetColors(colors);
            mesh.RecalculateBounds();
            AirsideMeshUtil.UploadStatic(mesh);

            var go = new GameObject("Star mesh");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = StarSharedMaterial();
            SetRendererColor(renderer, Color.white, new Color(1.2f, 1.2f, 1.35f));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            root.gameObject.SetActive(false);
        }

        private static Material StarSharedMaterial()
        {
            return AirsideMaterialLibrary.CreateSharedStars();
        }

        /// <summary>Star brightness for the daylight level: full at night, gone by mid-dawn.</summary>
        public static float StarFieldFade(float daylight) => StarFieldFade(daylight, 0f);

        public static float StarFieldFade(float daylight, float cloudCover)
        {
            var daylightFade = (1.1f - Mathf.Clamp01(daylight))
                * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.12f, daylight));
            var cloudFade = Mathf.Lerp(1f, 0.04f,
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 0.88f, cloudCover)));
            return daylightFade * cloudFade;
        }

        private float ObserverHeight => _mainCamera != null ? _mainCamera.transform.position.y : 0f;
        private float ObserverInCloud
        {
            get
            {
                if (!AirsideSettings.Current.WeatherLayers) return 0f;
                var cloud = CockpitWeatherEnvelope.InCloud(ObserverHeight, CurrentWeatherLook.CloudCover);
                if (_cloudRoot == null || _mainCamera == null || _stormDepth < 0.01f) return cloud;
                for (var i = 0; i < _cloudRoot.childCount; i += 3)
                {
                    var body = _cloudRoot.GetChild(i).GetChild(0);
                    if (body.name != "Cloud volume") continue;
                    if (!_cloudTints.TryGetValue(i, out var tint)) continue;
                    var cloudPosition = _cloudRoot.GetChild(i).position;
                    var edge = WeatherCoverage.CloudEdge(cloudPosition.x, cloudPosition.z,
                        _mainCamera.transform.position.x, _mainCamera.transform.position.z);
                    var point = body.InverseTransformPoint(_mainCamera.transform.position);
                    var density = CockpitWeatherEnvelope.StormBody(point.x, point.y, point.z) * _stormDepth * tint.a * edge;
                    cloud = Mathf.Max(cloud, density);
                }
                return cloud;
            }
        }

        // Keep the ground/deck weather intact; only the sky above the observer clears.
        private float CockpitAboveDeck => AirsideSettings.Current.WeatherLayers
            ? CockpitWeatherEnvelope.AboveDeck(ObserverHeight, CurrentWeatherLook.CloudCover, _stormDepth) : 0f;
        private float ObserverSkyCover => AirsideSettings.Current.WeatherLayers
            ? CockpitWeatherEnvelope.SkyCover(ObserverHeight, CurrentWeatherLook.CloudCover, _stormDepth)
            : CurrentWeatherLook.CloudCover;

        private void UpdateStarField()
        {
            if (_starFieldRoot == null)
            {
                _starFieldRoot = AirsideSceneIndex.Find("Star field");
            }

            if (_starFieldRoot == null)
                return;

            var daylight = PresentationDaylight;
            // Stars used to switch off at daylight 0.35 while still three-quarters bright,
            // so the whole sky blinked once every dawn and dusk. Fade them out instead.
            var fade = StarFieldFade(daylight, ObserverSkyCover);
            var show = fade > 0.002f;
            if (_starFieldRoot.gameObject.activeSelf != show)
                _starFieldRoot.gameObject.SetActive(show);
            if (!show)
                return;

            // The Adelaide field spans kilometres. Keep the celestial shell centred on
            // the active camera so stars cannot be left behind by overview/follow pans.
            // The camera moves in LateUpdate, so the anchor re-applies this after it has moved;
            // a position taken here alone is a frame stale and the stars slid against the view.
            CameraShellAnchor.Place(_mainCamera, _starFieldRoot, Vector3.zero);

            var twinkle = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * AirsideReusableMotion.StarTwinkleHz);
            if (_starFieldRenderer == null)
            {
                _starFieldRenderer = _starFieldRoot.childCount > 0
                    ? _starFieldRoot.GetChild(0).GetComponent<Renderer>()
                    : _starFieldRoot.GetComponent<Renderer>();
            }

            if (_starFieldRenderer == null)
                return;
            var c = new Color(twinkle, twinkle, 1f) * fade;
            SetRendererColor(_starFieldRenderer, c, c);
        }

        private static void BuildSunAndMoonDiscs()
        {
            // Visible sun/moon so the Adelaide path reads from overview (0025 item 5 / ADR 0086).
            var sun = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sun.name = "Sun disc";
            DestroyPresentationObject(sun.GetComponent<Collider>());
            sun.transform.localScale = Vector3.one * 7.5f;
            var sunMat = AirsideMaterialLibrary.CreateShared(
                new Color(1f, 0.94f, 0.72f, 1f),
                AirsideMaterialLibrary.SurfaceKind.UnlitSky);
            sunMat.SetInt("_ZWrite", 0);
            sunMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Background;
            var sunRenderer = sun.GetComponent<Renderer>();
            sunRenderer.sharedMaterial = sunMat;
            SetRendererColor(sunRenderer, new Color(1f, 0.94f, 0.72f, 1f), new Color(1.8f, 1.45f, 0.7f));
            sunRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sunRenderer.receiveShadows = false;

            var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.name = "Sun glow";
            DestroyPresentationObject(glow.GetComponent<Collider>());
            glow.transform.SetParent(sun.transform, false);
            glow.transform.localScale = Vector3.one * 3.2f;
            var glowMat = AirsideMaterialLibrary.CreateShared(
                new Color(1f, 0.72f, 0.35f, 1f),
                AirsideMaterialLibrary.SurfaceKind.UnlitSky);
            glowMat.SetInt("_ZWrite", 0);
            glowMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Background;
            var glowRenderer = glow.GetComponent<Renderer>();
            glowRenderer.sharedMaterial = glowMat;
            SetRendererColor(glowRenderer, new Color(1f, 0.7f, 0.32f, 1f), new Color(0.55f, 0.28f, 0.06f));
            glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glowRenderer.receiveShadows = false;

            var moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            moon.name = "Moon disc";
            DestroyPresentationObject(moon.GetComponent<Collider>());
            moon.transform.localScale = Vector3.one * 6.5f;
            var moonMat = AirsideMaterialLibrary.CreateShared(
                new Color(0.78f, 0.80f, 0.84f, 1f),
                AirsideMaterialLibrary.SurfaceKind.Concrete);
            moonMat.SetInt("_ZWrite", 0);
            moonMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Background;
            var moonRenderer = moon.GetComponent<Renderer>();
            moonRenderer.sharedMaterial = moonMat;
            moonRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            moonRenderer.receiveShadows = false;
            moon.SetActive(false);
        }

        private void UpdateSunAndMoonDiscs(float daylight, float warm, CelestialSky sky)
        {
            if (_sunDisc == null)
                _sunDisc = AirsideSceneIndex.Find("Sun disc");
            if (_moonDisc == null)
                _moonDisc = AirsideSceneIndex.Find("Moon disc");

            if (PinDaylightPresentation)
            {
                if (_sunDisc != null)
                    _sunDisc.gameObject.SetActive(false);
                if (_moonDisc != null)
                    _moonDisc.gameObject.SetActive(false);
                return;
            }

            var cloudCover = ObserverSkyCover;
            var discVisibility = Mathf.Clamp01(1f - Mathf.InverseLerp(0.3f, 0.75f, cloudCover));

            SkyDirection.ToWorld(sky.Sun, out var sx, out var sy, out var sz);
            var sunDir = new Vector3((float)sx, (float)sy, (float)sz);
            if (sunDir.sqrMagnitude > 1e-6f)
                sunDir.Normalize();
            else
                sunDir = Vector3.up;

            if (_sunDisc != null)
            {
                var showSun = sky.Sun.ElevationDegrees > -0.8 && discVisibility > 0.02f;
                _sunDisc.gameObject.SetActive(showSun);
                if (showSun)
                {
                    CameraShellAnchor.Place(_mainCamera, _sunDisc, sunDir * 420f);
                    var sunColor = Color.Lerp(
                        new Color(1f, 0.52f, 0.24f),
                        new Color(1f, 0.96f, 0.82f),
                        Mathf.Clamp01((float)(sky.Sun.ElevationDegrees / 18.0)));
                    sunColor = Color.Lerp(sunColor, new Color(1f, 0.7f, 0.4f), warm * 0.55f);
                    if (_sunDiscRenderer == null)
                        _sunDiscRenderer = _sunDisc.GetComponent<Renderer>();
                    if (_sunDiscRenderer != null)
                        SetRendererColor(_sunDiscRenderer, sunColor * discVisibility,
                            sunColor * ((1.35f + warm * 0.5f) * discVisibility));
                }
            }

            SkyDirection.ToWorld(sky.Moon, out var mx, out var my, out var mz);
            var moonDir = new Vector3((float)mx, (float)my, (float)mz);
            if (moonDir.sqrMagnitude > 1e-6f)
                moonDir.Normalize();
            else
                moonDir = -sunDir;

            if (_moonDisc != null)
            {
                var showMoon = sky.Moon.ElevationDegrees > -0.6 && discVisibility > 0.02f;
                _moonDisc.gameObject.SetActive(showMoon);
                if (showMoon)
                {
                    CameraShellAnchor.Place(_mainCamera, _moonDisc, moonDir * 420f);
                    if (_moonDiscRenderer == null)
                        _moonDiscRenderer = _moonDisc.GetComponent<Renderer>();
                    if (_moonDiscRenderer != null)
                    {
                        // Pale body; the directional sun lights the correct hemisphere so
                        // phases read. A little earthshine keeps the night side from vanishing.
                        var pale = Color.Lerp(
                            new Color(0.72f, 0.74f, 0.80f, 1f),
                            new Color(0.88f, 0.89f, 0.92f, 1f),
                            daylight * 0.45f);
                        var earthshine = 0.045f + 0.04f * (1f - (float)sky.MoonIllumination);
                        SetRendererColor(_moonDiscRenderer, pale * discVisibility,
                            new Color(0.18f, 0.19f, 0.22f) * (earthshine * discVisibility));
                    }
                }
            }
        }

        /// <summary>Restrained weather growth keeps atlas cards readable without hiding the field.</summary>
        public static float CloudCardScale(float cloudCover) =>
            Mathf.Lerp(0.88f, 1.15f, Mathf.Clamp01(cloudCover));

        /// <summary>The atlas already contains shaded bodies, so cards remain translucent.</summary>
        public static float CloudCardAlpha(float cloudCover) =>
            Mathf.Lerp(0.42f, 0.72f, Mathf.Clamp01(cloudCover));

        private static void BuildCloudBands()
        {
            // One bounded 3D density volume per cluster. The authored atlas remains a fallback.
            // Cloud count stays fixed; weather reveals more bodies and grows storm towers.
            var cloudRoot = new GameObject("Cloud bands").transform;
            var umbraRoot = new GameObject("Cloud umbras").transform;
            var atlas = AirsideArtTextures.Load(
                "Textures/Environment/tx_cloud_atlas_cumulus_v01.png",
                wrap: TextureWrapMode.Clamp);
            var volumeShader = Shader.Find("Airside/WeatherVolume");
            var shader = volumeShader ?? Shader.Find("Airside/CloudAtlas");
            if (shader == null || (volumeShader == null && atlas == null))
                return;
            var cloudMaterial = new Material(shader) { name = "Airside cloud bodies" };
            if (volumeShader == null)
                cloudMaterial.SetTexture("_BaseMap", atlas);
            cloudMaterial.SetColor("_BaseColor", Color.white);
            if (volumeShader == null)
                cloudMaterial.SetFloat("_AlphaFloor", 0.10f);

            var rng = new System.Random(90210);
            var adelaide = AirsideBareField.Enabled;
            // ADR 0075 approved sixteen atlas clusters. A later change silently raised this
            // to 24, then cover scaling grew individual cards past a kilometre wide, so the
            // airport was hidden behind low photo cut-outs in the normal overview.
            var clusterCount = adelaide ? AdelaideCloudClusterCount : 9;
            var spreadX = adelaide ? WeatherCoverage.CloudHalfWidth : 110f;
            var spreadZ = adelaide ? WeatherCoverage.CloudHalfDepth : 100f;
            var yBase = adelaide ? 650f : 24f;
            var ySpan = adelaide ? 300f : 26f;
            for (var i = 0; i < clusterCount; i++)
            {
                var cluster = new GameObject($"Cloud {i}").transform;
                cluster.SetParent(cloudRoot, false);
                // One jittered cluster per cell avoids an airport-only concentration and
                // large empty quarters; the same sixteen bodies cover each moving view.
                var x = adelaide ? ((i % 4 + 0.2f + (float)rng.NextDouble() * 0.6f) / 4f * 2f - 1f) * spreadX
                    : (float)(rng.NextDouble() * spreadX * 2f - spreadX);
                var z = adelaide ? ((i / 4 + 0.2f + (float)rng.NextDouble() * 0.6f) / 4f * 2f - 1f) * spreadZ
                    : (float)(rng.NextDouble() * spreadZ * 2f - spreadZ);
                var y = yBase + (float)rng.NextDouble() * ySpan;
                cluster.position = new Vector3(x, y, z);

                var sx = (adelaide ? 240f : 16f) + (float)rng.NextDouble() * (adelaide ? 180f : 30f);
                var sy = (adelaide ? 105f : 3.4f) + (float)rng.NextDouble() * (adelaide ? 65f : 4.5f);
                var sz = (adelaide ? 140f : 9f) + (float)rng.NextDouble() * (adelaide ? 140f : 18f);
                var yaw = (float)rng.NextDouble() * 360f;
                cluster.rotation = Quaternion.Euler(0f, yaw, 0f);

                var card = GameObject.CreatePrimitive(volumeShader != null ? PrimitiveType.Cube : PrimitiveType.Quad);
                card.name = volumeShader != null ? "Cloud volume" : "Cloud card fallback";
                DestroyPresentationObject(card.GetComponent<Collider>());
                card.transform.SetParent(cluster, false);
                card.transform.localScale = volumeShader != null
                    ? new Vector3(sx * 2.6f, sy * 2.5f, sz * 3f)
                    : new Vector3(sx, sy, 1f);
                var renderer = card.GetComponent<Renderer>();
                renderer.sharedMaterial = cloudMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.GetPropertyBlock(RendererTintBlock);
                var cellX = i % 4;
                var cellY = (i / 4) % 4;
                RendererTintBlock.SetVector(CloudAtlasRectId,
                    new Vector4(0.25f, 0.25f, cellX * 0.25f, cellY * 0.25f));
                RendererTintBlock.SetFloat("_Seed", i * 13.71f);
                renderer.SetPropertyBlock(RendererTintBlock);
                SetRendererColor(renderer, Color.white);

                // Soft ground umbra under each cloud cluster — drifts with UpdateCloudDrift.
                var umbra = GameObject.CreatePrimitive(PrimitiveType.Quad);
                umbra.name = $"Cloud umbra {i}";
                DestroyPresentationObject(umbra.GetComponent<Collider>());
                umbra.transform.SetParent(umbraRoot, false);
                umbra.transform.position = new Vector3(x, 0.06f, z);
                umbra.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
                umbra.transform.localScale = new Vector3(sx * 2.6f, sz * 3f, 1f);
                var umbraMat = SoftLayerMaterial();
                var umbraRenderer = umbra.GetComponent<Renderer>();
                umbraRenderer.sharedMaterial = umbraMat;
                SetRendererColor(umbraRenderer, new Color(0.05f, 0.07f, 0.1f, 0.18f));
                umbraRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                umbraRenderer.receiveShadows = false;
            }
        }

        private void UpdateCloudDrift()
        {
            if (_cloudRoot == null)
            {
                _cloudRoot = AirsideSceneIndex.Find("Cloud bands");
            }

            if (_cloudUmbraRoot == null)
            {
                _cloudUmbraRoot = AirsideSceneIndex.Find("Cloud umbras");
            }

            if (_cloudRoot == null)
                return;

            var layers = AirsideSettings.Current.WeatherLayers;
            _cloudRoot.gameObject.SetActive(layers);
            if (_cloudUmbraRoot != null) _cloudUmbraRoot.gameObject.SetActive(layers);
            if (!layers) return;
            // Orbit and zoom move the lens, not the weather over the watched area.
            // Cockpit follows the aircraft; ordinary views use their stable look/focus point.
            var coverage = InCockpit && _cockpitView != null ? _cockpitView.position
                : _cameraController != null ? _cameraController.FocusPoint : Vector3.zero;

            // Drift with the real surface wind (ADR 0068) rather than a fixed eastward slide —
            // clouds and rain used to move the same direction regardless of what the windsock
            // (the only other wind-reactive visual) was pointing.
            var daylight = PresentationDaylight;
            var look = CurrentWeatherLook;
            // ADR 0143 speeds remain unchanged; direction is the downwind flow.
            var flow = WeatherWindFlow.Cloud(PresentationWind, AirsideBareField.Enabled);
            var driftX = flow.X * Time.unscaledDeltaTime;
            var driftZ = flow.Z * Time.unscaledDeltaTime;
            var weather = CurrentWeather;
            var overcast = look.CloudCover >= 0.7f;
            var cloudy = look.CloudCover > 0.3f && !overcast;
            var thickSky = overcast || cloudy;
            // The old solid cylinder needed a stronger value under opaque geometry. Atlas alpha
            // already describes a soft cloud edge, so its companion umbra must stay broad/subtle.
            var umbraAlpha = Mathf.Lerp(0.025f, 0.045f + look.CloudCover * 0.065f, daylight);
            var cloudBand = Mathf.RoundToInt(look.CloudCover * 20f);
            var cloudShade = _atmosphere.CloudShade > 0f ? _atmosphere.CloudShade : 1f;
            var tintKey = ((int)weather << 12) ^ (cloudBand << 5)
                ^ AirsideRuntimeQuality.ProbeBand(daylight, 0f) ^ (Mathf.RoundToInt(cloudShade * 10f) << 20);
            var tintChanged = tintKey != _cloudTintKey;
            if (tintChanged)
                _cloudTintKey = tintKey;
            for (var i = 0; i < _cloudRoot.childCount; i++)
            {
                var cloud = _cloudRoot.GetChild(i);
                var p = cloud.position;
                p.x += driftX;
                p.z += driftZ;
                if (!_cloudRestHeights.TryGetValue(i, out var restHeight))
                    _cloudRestHeights[i] = restHeight = p.y;
                var isVolume = cloud.GetChild(0).name == "Cloud volume";
                var development = i % 3 == 0 ? _stormDepth : 0f;
                var cloudScale = CloudCardScale(look.CloudCover);
                if (AirsideBareField.Enabled && isVolume)
                {
                    var height = cloud.GetChild(0).localScale.y;
                    var cirrus = WeatherAppearance.Cirrus(i, look.CloudCover, _stormDepth);
                    var stratus = WeatherAppearance.Stratus(look.CloudCover, _stormDepth);
                    var fairHeight = Mathf.Lerp(restHeight, 6000f + (i % 3) * 350f, cirrus);
                    p.y = Mathf.Lerp(fairHeight, (900f + CockpitWeatherEnvelope.StormTopMetres) * 0.5f, development);
                    var breadth = Mathf.Lerp(cloudScale, cloudScale * 2.4f, stratus);
                    breadth = Mathf.Lerp(breadth, cloudScale * 5f, cirrus);
                    var thickness = cloudScale * Mathf.Lerp(1f, 0.45f, stratus) * Mathf.Lerp(1f, 0.22f, cirrus);
                    cloud.localScale = new Vector3(Mathf.Lerp(breadth, 5.6f, development),
                        Mathf.Lerp(thickness, (CockpitWeatherEnvelope.StormTopMetres - 900f) / height, development),
                        Mathf.Lerp(breadth, 6.2f, development));
                    var bodyRenderer = cloud.GetChild(0).GetComponent<Renderer>();
                    bodyRenderer.GetPropertyBlock(RendererTintBlock);
                    RendererTintBlock.SetFloat("_Storm", development);
                    RendererTintBlock.SetFloat("_Cirrus", cirrus);
                    RendererTintBlock.SetFloat("_Stratus", stratus);
                    bodyRenderer.SetPropertyBlock(RendererTintBlock);
                }
                else cloud.localScale = Vector3.one * cloudScale;
                var wrapX = AirsideBareField.Enabled ? WeatherCoverage.CloudHalfWidth : 100f;
                var wrapZ = AirsideBareField.Enabled ? WeatherCoverage.CloudHalfDepth : 100f;
                var anchorX = AirsideBareField.Enabled ? coverage.x : 0f;
                var anchorZ = AirsideBareField.Enabled ? coverage.z : 0f;
                p.x = WeatherCoverage.WrapNearView(p.x, anchorX, wrapX);
                p.z = WeatherCoverage.WrapNearView(p.z, anchorZ, wrapZ);
                cloud.position = p;

                if (_mainCamera != null && cloud.GetChild(0).name != "Cloud volume")
                {
                    // ADR 0143: turn about the vertical, tipping only part-way toward a high camera,
                    // so a card reads as a body of cloud rather than a cut-out held up to the lens.
                    var toCamera = _mainCamera.transform.position - p;
                    var flat = new Vector3(toCamera.x, 0f, toCamera.z);
                    if (flat.sqrMagnitude > 1f)
                    {
                        var facing = Quaternion.LookRotation(flat.normalized, Vector3.up);
                        var pitch = -Mathf.Atan2(toCamera.y, flat.magnitude) * Mathf.Rad2Deg * 0.35f;
                        cloud.rotation = facing * Quaternion.Euler(pitch, 0f, 0f);
                    }
                }

                if (_cloudUmbraRoot != null && i < _cloudUmbraRoot.childCount)
                {
                    // ADR 0143: the shadow falls along the sun, not straight down.
                    var umbraTransform = _cloudUmbraRoot.GetChild(i);
                    var toSun = _sun != null ? -_sun.transform.forward : Vector3.up;
                    var along = toSun.y > 0.15f ? p.y / toSun.y : 0f;
                    umbraTransform.position = new Vector3(p.x - toSun.x * along, 0.06f, p.z - toSun.z * along);
                }

                // ADR 0143: fade out near the wrap edges and back in on the far side, instead of popping.
                var edge = AirsideBareField.Enabled
                    ? WeatherCoverage.CloudEdge(p.x, p.z, coverage.x, coverage.z)
                    : Mathf.Min(Mathf.InverseLerp(wrapX, 0f, Mathf.Abs(p.x)),
                        Mathf.InverseLerp(wrapZ, 0f, Mathf.Abs(p.z)));
                if (!tintChanged)
                {
                    ApplyCloudEdgeFade(cloud, i, edge);
                    continue;
                }

                var dusk = Mathf.Clamp01(Mathf.Min(daylight, 1f - daylight) * 3f);
                var tint = Color.Lerp(new Color(0.55f, 0.6f, 0.75f), new Color(0.95f, 0.96f, 0.98f), daylight);
                tint = Color.Lerp(tint, new Color(0.95f, 0.7f, 0.55f), dusk * 0.55f);
                if (thickSky)
                    tint = Color.Lerp(tint, new Color(0.62f, 0.66f, 0.72f), 0.22f + look.CloudCover * 0.4f);
                // ADR 0143: rain and storm clouds are darker bodies, and storm clusters tower.
                tint = new Color(tint.r * cloudShade, tint.g * cloudShade, tint.b * cloudShade, tint.a);
                var baseAlpha = CloudCardAlpha(look.CloudCover);
                tint.a = cloud.GetChild(0).name == "Cloud volume"
                    ? Mathf.Lerp(0.94f, 1f, daylight)
                    : Mathf.Lerp(baseAlpha * 0.85f, baseAlpha, daylight);

                // One authored atlas card per cluster, updated only when the weather band changes.
                // More cover shows more clusters, not just denser-looking ones (ADR 0068):
                // each of the fixed cluster count has its own cloud-cover reveal threshold,
                // spread evenly across 0..1, so a clear day genuinely has fewer clusters lit
                // up than an overcast one instead of the same 16 always present at a
                // different opacity. Ramped over 0.08 cover so a cluster fades in rather
                // than popping solid the instant cover crosses its threshold.
                var revealAt = (float)((i * 7) % _cloudRoot.childCount) / _cloudRoot.childCount;
                var visibility = Mathf.InverseLerp(revealAt, revealAt + 0.08f, look.CloudCover);

                var cardTint = tint;
                cardTint.a *= visibility;
                StoreCloudTint(i, cardTint);
                if (_cloudUmbraRoot != null && i < _cloudUmbraRoot.childCount)
                {
                    var umbraRenderer = _cloudUmbraRoot.GetChild(i).GetComponent<Renderer>();
                    if (umbraRenderer != null)
                    {
                        var umbraColor = GetRendererColor(umbraRenderer);
                        umbraColor.a = umbraAlpha * visibility;
                        _cloudUmbraTints[i] = umbraColor;
                    }
                }
                ApplyCloudEdgeFade(cloud, i, edge);
            }
        }

        private static Transform BuildWindsock()
        {
            // WLD-003 windsock — denser authored kit pole/fabric; animated sock parent stays procedural.
            var propsKit = PreferArtKit(
                "Models/Props/mdl_airfield_props_kit_authored_v01.gltf",
                "Models/Props/mdl_airfield_props_kit_v02.gltf",
                "Models/Props/mdl_airfield_props_kit_v01.gltf");
            var poleOrigin = new Vector3(-12f, 0f, 12f);
            var placedPole = ArtGltfLoader.TryPlaceCombined(
                    propsKit,
                    new[]
                    {
                        ("sock_base", new Color(0.35f, 0.36f, 0.38f)),
                        ("sock_pole", new Color(0.75f, 0.75f, 0.72f)),
                        ("windsock_pole", new Color(0.75f, 0.75f, 0.72f)),
                        ("sock_frame", new Color(0.55f, 0.55f, 0.52f)),
                        ("sock_swivel", new Color(0.45f, 0.46f, 0.48f)),
                        ("sock_guy_l", new Color(0.4f, 0.4f, 0.42f)),
                        ("sock_guy_r", new Color(0.4f, 0.4f, 0.42f)),
                        ("sock_counterweight", new Color(0.3f, 0.32f, 0.34f)),
                        ("sock_light", new Color(0.95f, 0.95f, 0.85f)),
                        ("sock_ring", new Color(0.55f, 0.55f, 0.52f))
                    },
                    poleOrigin, Quaternion.identity, "Windsock pole", out _);

            if (!placedPole && ArtPresentationLoader.TryInstantiatePrefab("mdl_windsock_pole_v01", out var polePrefab))
            {
                polePrefab.name = "Windsock pole";
                polePrefab.position = poleOrigin;
            }
            else if (!placedPole)
            {
                CreateBlock("Windsock pole", new Vector3(-12f, 1.6f, 12f), new Vector3(0.12f, 3.2f, 0.12f), new Color(0.75f, 0.75f, 0.72f));
                CreateBlock("Windsock hinge", new Vector3(-12f, 3.15f, 12f), new Vector3(0.22f, 0.22f, 0.22f), new Color(0.55f, 0.55f, 0.52f));
            }

            var sock = new GameObject("Windsock sock").transform;
            sock.position = new Vector3(-11.2f, 3.05f, 12f);
            sock.rotation = Quaternion.Euler(0f, 12f, 0f);
            var fabricPlaced = ArtGltfLoader.TryPlaceCombined(
                propsKit,
                new[]
                {
                    ("sock_fabric", new Color(0.92f, 0.55f, 0.12f)),
                    ("sock_fabric_mid", new Color(0.95f, 0.65f, 0.2f)),
                    ("sock_fabric_tip", new Color(0.95f, 0.95f, 0.92f))
                },
                sock.position, sock.rotation, "Windsock fabric", out var fabric);
            if (fabric != null)
            {
                fabric.SetParent(sock, true);
                fabricPlaced = true;
            }

            if (!fabricPlaced)
            {
                var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cylinder.name = "Windsock fabric";
                DestroyPresentationObject(cylinder.GetComponent<Collider>());
                cylinder.transform.SetParent(sock, false);
                cylinder.transform.localPosition = Vector3.zero;
                cylinder.transform.localScale = new Vector3(0.55f, 0.55f, 1.35f);
                cylinder.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                cylinder.GetComponent<Renderer>().sharedMaterial = CreateMaterial(new Color(0.92f, 0.55f, 0.12f));
                var stripe = CreateBlock("Windsock stripe", new Vector3(-10.6f, 3.05f, 12f), new Vector3(0.35f, 0.52f, 0.52f),
                    new Color(0.95f, 0.95f, 0.92f));
                stripe.transform.SetParent(sock, true);
            }

            return sock;
        }

        private static AudioClip CreateWindClip()
        {
            // Soft filtered noise bed for regional airfield air (presentation only).
            const int sampleRate = 22050;
            const float seconds = 2f;
            var samples = new float[(int)(sampleRate * seconds)];
            var state = 0f;
            var gustHz = LoopFrequency(0.35f, seconds);
            for (var i = 0; i < samples.Length; i++)
            {
                var white = (UnityEngine.Random.value * 2f - 1f);
                state = state * 0.92f + white * 0.08f;
                var gust = Mathf.Sin(i / (float)sampleRate * 2f * Mathf.PI * gustHz) * 0.15f;
                samples[i] = (state * 0.55f + white * 0.08f + gust * state) * 0.35f;
            }

            CrossfadeLoop(samples, sampleRate / 10);
            var clip = AudioClip.Create("Ambient wind", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateRainClip()
        {
            const int sampleRate = 22050;
            var samples = new float[sampleRate];
            var hushHz = LoopFrequency(0.015f * sampleRate / (2f * Mathf.PI), 1f);
            for (var i = 0; i < samples.Length; i++)
            {
                var crackle = UnityEngine.Random.value * 2f - 1f;
                var hush = Mathf.Sin(i / (float)sampleRate * 2f * Mathf.PI * hushHz) * 0.1f;
                samples[i] = crackle * 0.22f + hush * crackle;
            }

            CrossfadeLoop(samples, sampleRate / 20);
            var clip = AudioClip.Create("Ambient rain", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
