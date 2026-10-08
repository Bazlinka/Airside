using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    /// <summary>
    /// The real-scale Adelaide field never got its aerodrome beacon: the one-off mast only existed on the
    /// legacy miniature circuit, built behind <c>ShowDecorativeLights</c>, which is off for the true-scale
    /// world. This puts a rotating white/green beacon on the control tower cab at night: alternating
    /// flashes about 24 a minute in all (ICAO beacons run 12-30 per minute per colour pair), with a lens,
    /// a point light and a glow that reads from the far side of the field. Presentation only.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        private const float BeaconCycleSeconds = 2.5f;      // one white then one green flash
        private const float BeaconFlashSeconds = 0.32f;
        private const float BeaconNightDaylight = 0.38f;

        private Transform _ypadBeaconRoot;
        private Light _ypadBeaconLight;
        private Renderer _ypadBeaconLens, _ypadBeaconHalo;
        private MaterialPropertyBlock _ypadBeaconBlock;
        private int _ypadBeaconSearchFrame;
        private bool _ypadBeaconGaveUp;

        /// <summary>Flash level 0..1 and colour (white first, green half a cycle later) at a time.</summary>
        public static float BeaconFlash(float seconds, out bool green)
        {
            var t = Mathf.Repeat(seconds, BeaconCycleSeconds);
            green = t >= BeaconCycleSeconds * 0.5f;
            var into = green ? t - BeaconCycleSeconds * 0.5f : t;
            if (into >= BeaconFlashSeconds) return 0f;
            var u = into / BeaconFlashSeconds;
            return Mathf.Sin(u * Mathf.PI);
        }

        /// <summary>True when this handled the beacon (the real-scale field); false for the legacy mast.</summary>
        private bool UpdateYpadAerodromeBeacon(float daylight)
        {
            if (_aerodromeBeacon != null || !AirsideBareField.Enabled || _ypadBeaconGaveUp)
                return _aerodromeBeacon == null && AirsideBareField.Enabled;
            if (_ypadBeaconRoot == null && !TryBuildYpadBeacon())
                return true;

            var green = false;
            var level = daylight < BeaconNightDaylight ? BeaconFlash(PresentationClock, out green) : 0f;
            var colour = green ? new Color(0.25f, 1f, 0.5f) : new Color(1f, 0.97f, 0.9f);
            _ypadBeaconLight.enabled = level > 0.01f;
            _ypadBeaconLight.color = colour;
            _ypadBeaconLight.intensity = 14f * level;
            SetRendererColor(_ypadBeaconLens, Color.Lerp(colour * 0.35f, colour, level), colour * (0.4f + 4f * level));
            if (_mainCamera == null)
                return true;
            _ypadBeaconHalo.enabled = level > 0.02f;
            if (_ypadBeaconHalo.enabled)
            {
                var t = _ypadBeaconHalo.transform;
                var distance = Vector3.Distance(_mainCamera.transform.position, t.position);
                var worldPerPixel = 2f * distance * Mathf.Tan(_mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad)
                                    / Mathf.Max(1f, _mainCamera.pixelHeight);
                t.localScale = Vector3.one * Mathf.Max(6f, worldPerPixel * 26f);
                t.rotation = Quaternion.LookRotation(t.position - _mainCamera.transform.position, _mainCamera.transform.up);
                _ypadBeaconBlock ??= new MaterialPropertyBlock();
                _ypadBeaconBlock.SetColor("_BaseColor", colour * (1.6f * level));
                _ypadBeaconHalo.SetPropertyBlock(_ypadBeaconBlock);
            }

            return true;
        }

        private bool TryBuildYpadBeacon()
        {
            // The scene index is captured after the world is built; look again every couple of seconds.
            if (Time.frameCount - _ypadBeaconSearchFrame < 120 && _ypadBeaconSearchFrame != 0)
                return false;
            _ypadBeaconSearchFrame = Time.frameCount;
            var cab = AirsideSceneIndex.FindGameObject(TowerCabGlassName);
            var renderer = cab != null ? cab.GetComponent<Renderer>() : null;
            if (renderer == null)
            {
                if (Time.frameCount > 60 * 90)
                    _ypadBeaconGaveUp = true;      // no tower in this build: stop searching
                return false;
            }

            var bounds = renderer.bounds;
            var top = new Vector3(bounds.center.x, bounds.max.y + 3.2f, bounds.center.z);
            _ypadBeaconRoot = new GameObject("YPAD aerodrome beacon").transform;
            _ypadBeaconRoot.position = top;

            var mast = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mast.name = "Beacon mast";
            DestroyPresentationObject(mast.GetComponent<Collider>());
            mast.transform.SetParent(_ypadBeaconRoot, false);
            mast.transform.localPosition = new Vector3(0f, -1.6f, 0f);
            mast.transform.localScale = new Vector3(0.18f, 1.6f, 0.18f);
            SetRendererColor(mast.GetComponent<Renderer>(), new Color(0.55f, 0.56f, 0.58f));

            var lens = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lens.name = "Beacon lens";
            DestroyPresentationObject(lens.GetComponent<Collider>());
            lens.transform.SetParent(_ypadBeaconRoot, false);
            lens.transform.localScale = Vector3.one * 0.7f;
            _ypadBeaconLens = lens.GetComponent<Renderer>();
            _ypadBeaconLens.shadowCastingMode = ShadowCastingMode.Off;

            var lightGo = new GameObject("Beacon light");
            lightGo.transform.SetParent(_ypadBeaconRoot, false);
            _ypadBeaconLight = lightGo.AddComponent<Light>();
            _ypadBeaconLight.type = LightType.Point;
            _ypadBeaconLight.range = 140f;
            _ypadBeaconLight.shadows = LightShadows.None;
            _ypadBeaconLight.intensity = 0f;
            _ypadBeaconLight.enabled = false;

            var material = HaloMaterial();
            var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
            card.name = "Beacon glow";
            DestroyPresentationObject(card.GetComponent<Collider>());
            card.transform.SetParent(_ypadBeaconRoot, false);
            _ypadBeaconHalo = card.GetComponent<Renderer>();
            if (material != null) _ypadBeaconHalo.sharedMaterial = material;
            _ypadBeaconHalo.shadowCastingMode = ShadowCastingMode.Off;
            _ypadBeaconHalo.receiveShadows = false;
            _ypadBeaconHalo.enabled = false;
            return true;
        }
    }
}
