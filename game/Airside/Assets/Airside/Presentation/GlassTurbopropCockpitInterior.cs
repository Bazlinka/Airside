using Airside.Domain;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>Original ATR 42-600 and Dash 8-400 layouts, fitted to their own aircraft kits.</summary>
    [ExecuteAlways]
    public sealed class GlassTurbopropCockpitInterior : TurbopropCockpitInterior
    {
        private bool _dash;
        private Vector3 _origin;
        private Material _lining, _trim;
        private float _width, _front, _roof;
        private readonly AttitudeDisc[] _attitude = new AttitudeDisc[2];

        public override void SetAttitude(float pitchUpDegrees, float bankLeftDegrees)
        {
            foreach (var disc in _attitude) disc?.Set(pitchUpDegrees, bankLeftDegrees);
        }

        public static GlassTurbopropCockpitInterior Build(Transform aircraft, AircraftType type)
        {
            if (aircraft == null || type == null
                || (type.Id != AircraftType.Atr42.Id && type.Id != AircraftType.Dash8Q400.Id)) return null;
            var host = new GameObject(type.Id + " cockpit interior");
            host.transform.SetParent(aircraft, false);
            host.transform.localPosition = new Vector3(0f, AircraftVisualProfiles.For(type).ModelGroundOffsetMetres, 0f);
            var rig = host.AddComponent<GlassTurbopropCockpitInterior>();
            rig._dash = type.Id == AircraftType.Dash8Q400.Id;
            // Eye stations are fitted to each kit's windscreen bounds, not airframe length scaling.
            rig._origin = rig._dash ? new Vector3(0f, 2.68f, 13.32f) : new Vector3(0f, 2.67f, 8.60f);
            rig._width = rig._dash ? 1.02f : 1.10f;
            rig._front = rig._dash ? 1.30f : 1.35f;
            rig._roof = rig._dash ? 0.57f : 0.48f;
            rig.MakeInterior(type);
            return rig;
        }

        private Vector3 P(float x, float y, float z) => _origin + new Vector3(x, y, z);

        private void MakeInterior(AircraftType type)
        {
            _lining = Surface("flight deck lining", _dash ? new Color(0.57f, 0.57f, 0.52f) : new Color(0.65f, 0.66f, 0.64f));
            _trim = Surface("window seals", new Color(0.12f, 0.15f, 0.16f));
            _panel = Surface("instrument panel", _dash ? new Color(0.24f, 0.29f, 0.31f) : new Color(0.39f, 0.44f, 0.47f));
            _black = Surface("instrument black", new Color(0.018f, 0.025f, 0.028f), false);
            _white = Surface("instrument markings", new Color(0.81f, 0.85f, 0.81f), false);
            _metal = Surface("switch rims", new Color(0.29f, 0.33f, 0.34f));
            var fabric = Surface("seat fabric", new Color(0.32f, 0.35f, 0.34f));
            Seat = new GameObject("Left pilot eye").transform;
            Seat.SetParent(transform, false);
            Seat.localPosition = P(_dash ? -0.47f : -0.48f, 0f, 0f);
            Seat.localRotation = Quaternion.Euler(_dash ? 8f : 9f, 0f, 0f);
            MakeShell();
            var panelZ = _dash ? 0.84f : 0.89f;
            var panelStart = transform.childCount;
            Face("Instrument panel face", new[] { P(-0.97f, -0.96f, panelZ), P(0.97f, -0.96f, panelZ),
                P(0.97f, -0.49f, panelZ), P(0.85f, -0.23f, panelZ),
                P(-0.85f, -0.23f, panelZ), P(-0.97f, -0.49f, panelZ) }, _panel);
            Box("Glareshield", P(0f, -0.20f, panelZ + 0.10f), new Vector3(1.84f, 0.06f, 0.31f), _trim);
            Box("Flight guidance panel", P(0f, -0.29f, panelZ - 0.02f), new Vector3(0.76f, 0.095f, 0.045f), _metal);
            for (var i = 0; i < 10; i++)
                Box("Guidance control", P(-0.31f + i * 0.069f, -0.29f, panelZ - 0.05f), new Vector3(0.035f, 0.025f, 0.02f), _trim);
            Label("Guidance legends", "HDG   NAV   ALT   VS", P(0f, -0.245f, panelZ - 0.057f), 0.0035f, Color.white);
            MakeDisplays(panelZ);
            DropPanelParts(panelStart);
            foreach (var side in new[] { -1f, 1f })
            {
                var x = side * 0.47f;
                Box("Pilot cushion", P(x, -0.80f, -0.20f), new Vector3(0.52f, 0.13f, 0.48f), fabric);
                Box("Pilot seat back", P(x, -0.47f, -0.48f), new Vector3(0.52f, 0.70f, 0.12f), fabric);
                Box("Pilot headrest", P(x, -0.05f, -0.49f), new Vector3(0.29f, 0.20f, 0.13f), fabric);
                Box("Yoke column", P(x, -0.76f, 0.42f), new Vector3(0.09f, 0.58f, 0.11f), _panel);
                Beam("Yoke crossbar", P(x - 0.17f, -0.51f, 0.40f), P(x + 0.17f, -0.51f, 0.40f), 0.033f, _trim);
                foreach (var grip in new[] { -1f, 1f })
                {
                    Beam("Yoke grip", P(x + grip * 0.17f, -0.51f, 0.40f), P(x + grip * 0.145f, -0.32f, 0.44f), 0.037f, _trim);
                    Beam("Yoke shoulder", P(x + grip * 0.145f, -0.32f, 0.44f), P(x + grip * 0.05f, -0.31f, 0.44f), 0.033f, _trim);
                }
                Box("Yoke hub", P(x, -0.44f, 0.38f), new Vector3(0.09f, 0.10f, 0.055f), _metal);
                Box("Side console", P(side * (_width - 0.14f), -0.91f, -0.04f), new Vector3(0.17f, 0.12f, 0.80f), _panel);
                Box("Side air vent", P(side * (_width - 0.13f), -0.40f, 0.31f), new Vector3(0.08f, 0.08f, 0.04f), _black);
            }
            MakePedestal();
            MakeOverhead();
            var telemetryStart = transform.childCount;
            Box("Local telemetry inset", P(0f, -0.85f, panelZ - 0.028f), new Vector3(0.27f, 0.11f, 0.026f), _black);
            _readout = Label("Local telemetry", "", P(0f, -0.85f, panelZ - 0.045f), 0.0038f, new Color(0.65f, 0.85f, 0.69f));
            Label("Panel identity", type.Id + " • LOCAL VIEW", P(-0.46f, -0.91f, panelZ - 0.045f), 0.0036f, Color.white);
            DropPanelParts(telemetryStart);
            SetReadout("GS 0 kt\nHEIGHT 0 ft\nHDG 000°");
        }

        /// <summary>The panel was authored with its glareshield about 11 degrees below the eye. Real
        /// ATR and Dash 8 decks give a pilot roughly 15 degrees over the nose, so the whole panel group
        /// sits PanelDrop lower and the runway stays in view past the nose.</summary>
        private const float PanelDrop = 0.06f;
        private void DropPanelParts(int first)
        {
            for (var i = first; i < transform.childCount; i++)
                transform.GetChild(i).localPosition += new Vector3(0f, -PanelDrop, 0f);
        }

        private void MakeShell()
        {
            const float floor = -1.22f, rear = -1.16f, windowRear = -0.62f;
            var sill = _dash ? -0.36f : -0.40f;
            var frontWidth = _dash ? 0.72f : 0.77f;
            var cornerZ = _dash ? 0.50f : 0.48f;
            var centreLow = P(0f, (_dash ? -0.25f : -0.22f) - PanelDrop, _front);
            var centreHigh = P(0f, _roof - 0.08f, _front - 0.24f);
            Face("Flight deck floor", new[] { P(-_width, floor, rear), P(-_width, floor, cornerZ),
                P(-frontWidth, floor, _front - 0.18f), P(0f, floor, _front),
                P(frontWidth, floor, _front - 0.18f), P(_width, floor, cornerZ), P(_width, floor, rear) }, _trim);
            Face("Flight deck roof", new[] { P(-_width, _roof, rear), P(_width, _roof, rear),
                P(_width, _roof, windowRear), P(-_width, _roof, windowRear) }, _lining);
            Face("Rear bulkhead", new[] { P(-_width, floor, rear), P(_width, floor, rear),
                P(_width, _roof, rear), P(-_width, _roof, rear) }, _lining);
            Beam("Centre windscreen seal", centreLow, centreHigh, _dash ? 0.029f : 0.033f, _trim);
            foreach (var side in new[] { -1f, 1f })
            {
                var frontLow = P(side * frontWidth, sill + 0.055f, _front - 0.18f);
                var frontHigh = P(side * (frontWidth - 0.10f), _roof - 0.12f, _front - 0.36f);
                var cornerLow = P(side * _width, sill, cornerZ);
                var cornerHigh = P(side * (_width - 0.08f), _roof - 0.10f, cornerZ - 0.15f);
                var rearLow = P(side * _width, sill, windowRear);
                var rearHigh = P(side * (_width - 0.08f), _roof - 0.05f, windowRear);
                Beam("Forward sill seal", centreLow, frontLow, 0.037f, _trim);
                Beam("Forward brow seal", centreHigh, frontHigh, 0.040f, _trim);
                Beam("Angled windscreen post", frontLow, frontHigh, _dash ? 0.044f : 0.050f, _lining);
                Beam("Quarter window sill", frontLow, cornerLow, 0.037f, _trim);
                Beam("Quarter window brow", frontHigh, cornerHigh, 0.041f, _lining);
                Beam("Side window front post", cornerLow, cornerHigh, 0.047f, _lining);
                Beam("Sliding window sill", cornerLow, rearLow, 0.042f, _trim);
                Beam("Sliding window rail", cornerHigh, rearHigh, 0.043f, _lining);
                Beam("Sliding window rear post", rearLow, rearHigh, 0.046f, _lining);
                Face("Forward roof taper", new[] { centreHigh, frontHigh, cornerHigh, rearHigh,
                    P(side * _width, _roof, windowRear), P(0f, _roof, windowRear) }, _lining);
                Face("Lower side lining", new[] { cornerLow, rearLow, P(side * _width, sill, rear),
                    P(side * _width, floor, rear), P(side * _width, floor, cornerZ) }, _lining);
                Face("Forward footwell shell", new[] { centreLow, frontLow, cornerLow,
                    P(side * _width, floor, cornerZ), P(side * frontWidth, floor, _front - 0.18f), P(0f, floor, _front) }, _lining);
                Face("Rear side lining", new[] { rearLow, rearHigh, P(side * _width, _roof, windowRear),
                    P(side * _width, _roof, rear), P(side * _width, sill, rear) }, _lining);
                Beam("Window latch", P(side * (_width - 0.025f), sill + 0.045f, -0.06f),
                    P(side * (_width - 0.025f), sill + 0.045f, 0.09f), 0.02f, _metal);
                Beam("Parked windscreen wiper", P(side * 0.09f, sill + 0.16f, _front - 0.03f),
                    P(side * 0.52f, sill + 0.10f, _front - 0.13f), 0.011f, _trim);
            }
        }

        private void MakeDisplays(float z)
        {
            // ATR -600: broad LCD suite. Q400: taller, more compact display units.
            var width = _dash ? 0.275f : 0.315f;
            var height = _dash ? 0.315f : 0.27f;
            var pitch = _dash ? 0.345f : 0.355f;
            var y = _dash ? -0.54f : -0.51f;
            var pfd = DisplayArtwork("primary flight display", 0);
            var nav = DisplayArtwork("navigation display", 1);
            var engines = DisplayArtwork("engine display", 2);
            for (var i = 0; i < 5; i++)
            {
                var x = (i - 2) * pitch;
                Box("LCD bezel " + i, P(x, y, z - 0.015f), new Vector3(width + 0.043f, height + 0.040f, 0.038f), _black);
                var front = z - 0.038f;
                Face("LCD face " + i, new[] { P(x - width * 0.5f, y - height * 0.5f, front),
                    P(x + width * 0.5f, y - height * 0.5f, front), P(x + width * 0.5f, y + height * 0.5f, front),
                    P(x - width * 0.5f, y + height * 0.5f, front) }, i == 2 ? engines : i == 0 || i == 4 ? pfd : nav);
                if (i == 0 || i == 4)
                    _attitude[i == 0 ? 0 : 1] = MakeAttitudeDisc("Live attitude " + i, P(x, y + height * 0.04f, front - 0.004f), height * 0.34f);
                Box("Display selector", P(x + width * 0.43f, y - height * 0.54f, front - 0.008f), new Vector3(0.020f, 0.020f, 0.016f), _metal);
            }
        }

        private Material DisplayArtwork(string name, int kind)
        {
            const int size = 192;
            var pixels = new Color32[size * size];
            var dark = new Color32(5, 13, 17, 255);
            var white = new Color32(208, 225, 217, 255);
            var accent = _dash ? new Color32(119, 217, 110, 255) : new Color32(110, 212, 231, 255);
            for (var i = 0; i < pixels.Length; i++) pixels[i] = dark;
            void Fill(int x, int y, int w, int h, Color32 colour)
            {
                for (var row = Mathf.Max(0, y); row < Mathf.Min(size, y + h); row++)
                    for (var col = Mathf.Max(0, x); col < Mathf.Min(size, x + w); col++) pixels[row * size + col] = colour;
            }
            if (kind == 0)
            {
                Fill(38, 34, 116, 58, new Color32(104, 70, 32, 255));
                Fill(38, 92, 116, 65, new Color32(26, 92, 145, 255));
                Fill(38, 91, 116, 2, white);
                for (var row = -3; row <= 3; row++) Fill(85 - (row == 0 ? 9 : 0), 92 + row * 14, row == 0 ? 40 : 22, 2, white);
                Fill(70, 92, 52, 3, new Color32(232, 173, 52, 255));
                Fill(17, 30, 14, 132, new Color32(24, 34, 39, 255)); Fill(161, 30, 14, 132, new Color32(24, 34, 39, 255));
                for (var y = 36; y < 160; y += 14) { Fill(24, y, 8, 2, white); Fill(160, y, 8, 2, white); }
            }
            else if (kind == 1)
            {
                for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
                {
                    var distance = Mathf.Sqrt((x - 96) * (x - 96) + (y - 70) * (y - 70));
                    if (Mathf.Abs(distance - 70) < 1.3f || Mathf.Abs(distance - 40) < 1f) pixels[y * size + x] = accent;
                }
                Fill(95, 47, 2, 98, white); Fill(86, 62, 20, 3, white);
                for (var y = 86; y < 152; y++) Fill(95 + (y - 86) / 4, y, 2, 1, new Color32(214, 129, 220, 255));
            }
            else
            {
                for (var row = 0; row < 4; row++) for (var side = 0; side < 2; side++)
                {
                    var cx = 53 + side * 84; var cy = 142 - row * 34;
                    for (var y = cy - 13; y <= cy + 13; y++) for (var x = cx - 13; x <= cx + 13; x++)
                    {
                        var d = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                        if (d > 110 && d < 166) pixels[y * size + x] = accent;
                    }
                    Fill(cx - 1, cy - 1, 11, 2, white); Fill(cx - 12, cy - 19, 23, 3, white);
                }
            }
            for (var x = 12; x < 180; x += 20) Fill(x, 177, 13, 3, accent);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(pixels); texture.Apply(true, true); _textures.Add(texture);
            var material = Surface(name, Color.white, false); material.SetTexture("_BaseMap", texture);
            return material;
        }

        private void MakePedestal()
        {
            Box("Centre pedestal", P(0f, -0.92f, 0.05f), new Vector3(0.37f, 0.45f, 1.18f), _panel);
            foreach (var side in new[] { -1f, 1f })
            {
                var x = side * 0.096f;
                Box("FMS unit", P(x, -0.678f, 0.46f), new Vector3(0.16f, 0.025f, 0.23f), _metal);
                Box("FMS display", P(x, -0.661f, 0.51f), new Vector3(0.13f, 0.008f, 0.10f), _black);
                for (var row = 0; row < 3; row++) for (var col = 0; col < 4; col++)
                    Box("FMS key", P(x - 0.045f + col * 0.030f, -0.656f, 0.40f - row * 0.027f), new Vector3(0.018f, 0.010f, 0.018f), _trim);
                for (var group = 0; group < 2; group++)
                {
                    var leverX = side * 0.034f + (group - 0.5f) * 0.17f;
                    Box("Lever slot", P(leverX, -0.686f, 0.025f), new Vector3(0.012f, 0.008f, 0.30f), _black);
                    Beam(group == 0 ? "Power lever" : "Condition lever", P(leverX, -0.68f, 0f), P(leverX, -0.48f, 0.11f), 0.016f, _metal);
                    Box(group == 0 ? "Power handle" : "Condition handle", P(leverX, -0.48f, 0.11f), new Vector3(0.045f, 0.028f, 0.055f), group == 0 ? _trim : _white);
                }
            }
            Label("Lever legends", "POWER     CONDITION", P(0f, -0.73f, 0.27f), 0.0031f, Color.white);
        }

        private void MakeOverhead()
        {
            var width = _dash ? 0.50f : 0.66f;
            Box("Overhead systems panel", P(0f, _roof - 0.06f, 0.12f), new Vector3(width, 0.035f, 1.14f), _panel);
            for (var row = 0; row < 7; row++) for (var col = 0; col < 6; col++)
                Box("Overhead switch", P(-width * 0.38f + col * width * 0.152f, _roof - 0.095f, -0.34f + row * 0.15f),
                    new Vector3(0.019f, 0.030f, 0.025f), (row + col) % 4 == 0 ? _white : _trim);
            if (_dash)
                Box("Roof escape hatch", P(0f, _roof - 0.008f, -0.82f), new Vector3(0.55f, 0.018f, 0.40f), _metal);
        }
    }
}
