using Airside.Domain;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>Original representative analog Bell 412EP deck. Decorative controls, not an EPX retrofit.</summary>
    [ExecuteAlways]
    public sealed class BellCockpitInterior : CockpitInterior
    {
        private Material _lining, _rubber, _panel, _metal, _face, _marks, _dial;
        private readonly AttitudeDisc[] _attitude = new AttitudeDisc[2];

        public static BellCockpitInterior Build(Transform aircraft)
        {
            if (aircraft == null) return null;
            var host = new GameObject("B412 analog cockpit interior");
            host.transform.SetParent(aircraft, false);
            host.transform.localPosition = new Vector3(0f, AircraftVisualProfiles.Bell412.ModelGroundOffsetMetres, 0f);
            var interior = host.AddComponent<BellCockpitInterior>();
            interior.MakeInterior();
            return interior;
        }

        public override void SetAttitude(float pitchUpDegrees, float bankLeftDegrees)
        {
            foreach (var disc in _attitude) disc?.Set(pitchUpDegrees, bankLeftDegrees);
        }

        private Material Finish(string name, Color colour, float smoothness)
        {
            var material = Surface(name, colour);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        // The kit's actual glass transforms: ±0.67/2.42/2.83, 1.04 × 0.82 m,
        // rotated first -20° about X, then ±14° about Y (generator AIR-017).
        private static Vector3 Pane(float side, float x, float y) => new Vector3(side * 0.67f, 2.42f, 2.83f)
            + Quaternion.AngleAxis(side * 14f, Vector3.up)
            * Quaternion.AngleAxis(-20f, Vector3.right) * new Vector3(x, y, 0f);

        private void MakeInterior()
        {
            _lining = Finish("Bell ivory lining", new Color(0.60f, 0.60f, 0.55f), 0.16f);
            _rubber = Finish("Bell rubber seals", new Color(0.055f, 0.065f, 0.06f), 0.04f);
            _panel = Finish("Bell analog panel", new Color(0.25f, 0.28f, 0.27f), 0.12f);
            _metal = Finish("Bell machined bezels", new Color(0.36f, 0.39f, 0.37f), 0.40f);
            _face = Surface("Bell dark dial", new Color(0.015f, 0.025f, 0.024f), false);
            _marks = Surface("Bell dial markings", new Color(0.82f, 0.84f, 0.76f), false);
            _dial = MakeDialArtwork();
            var fabric = Finish("Bell seat cloth", new Color(0.30f, 0.34f, 0.31f), 0.025f);
            Seat = new GameObject("Right pilot eye").transform;
            Seat.SetParent(transform, false);
            Seat.localPosition = new Vector3(0.55f, 2.48f, 1.78f);
            Seat.localRotation = Quaternion.Euler(8f, 0f, 0f);
            MakeShell();
            Face("Analog panel face", new[] { new Vector3(-0.98f, 1.49f, 2.58f), new Vector3(0.98f, 1.49f, 2.58f),
                new Vector3(0.98f, 1.96f, 2.58f), new Vector3(0.78f, 2.16f, 2.58f),
                new Vector3(-0.78f, 2.16f, 2.58f), new Vector3(-0.98f, 1.96f, 2.58f) }, _panel);
            Box("Low padded panel brow", new Vector3(0f, 2.18f, 2.68f), new Vector3(1.64f, 0.06f, 0.28f), _rubber);
            foreach (var side in new[] { -1f, 1f })
            {
                var x = side * 0.53f;
                for (var row = 0; row < 2; row++)
                    for (var col = -1; col <= 1; col++)
                    {
                        var centre = new Vector3(x + col * 0.17f, 1.98f - row * 0.20f, 2.552f);
                        Gauge("Flight instrument", centre, 0.072f, col * 25f + row * 30f, row == 0 && col == 0, side);
                    }
                MakeSeatAndControls(x, fabric);
            }
            Box("Twin engine gauge inset", new Vector3(0f, 1.84f, 2.56f), new Vector3(0.27f, 0.46f, 0.025f), _metal);
            for (var row = 0; row < 3; row++)
                foreach (var side in new[] { -1f, 1f })
                    Gauge("Twin engine instrument", new Vector3(side * 0.075f, 1.99f - row * 0.14f, 2.538f), 0.042f, 20f + row * 15f, false, side);
            Box("Central radio console", new Vector3(0f, 1.35f, 1.86f), new Vector3(0.27f, 0.34f, 0.96f), _panel);
            for (var row = 0; row < 3; row++)
            {
                var z = 1.59f + row * 0.25f;
                Box("Radio module", new Vector3(0f, 1.53f, z), new Vector3(0.24f, 0.025f, 0.20f), _metal);
                Box("Radio dark display", new Vector3(0f, 1.548f, z + 0.035f), new Vector3(0.12f, 0.008f, 0.06f), _face);
                foreach (var side in new[] { -1f, 1f })
                    Box("Radio selector", new Vector3(side * 0.088f, 1.558f, z - 0.04f), new Vector3(0.03f, 0.02f, 0.03f), _rubber);
            }
            Box("Local spectator telemetry", new Vector3(0f, 1.55f, 2.54f), new Vector3(0.32f, 0.09f, 0.02f), _face);
            _readout = Label("Local telemetry", "", new Vector3(0f, 1.55f, 2.525f), 0.0031f, new Color(0.66f, 0.85f, 0.70f));
            for (var bank = 0; bank < 2; bank++)
            {
                var z = 1.43f + bank * 0.38f;
                Box("Overhead systems tray", new Vector3(0f, 2.998f, z), new Vector3(0.44f, 0.035f, 0.29f), _panel);
                foreach (var side in new[] { -1f, 1f })
                    for (var i = 0; i < 3; i++)
                        Box("Overhead paired toggle", new Vector3(side * 0.12f, 2.965f, z - 0.08f + i * 0.08f), new Vector3(0.018f, 0.03f, 0.025f), _rubber);
            }
        }

        private void MakeShell()
        {
            Box("Flight deck floor", new Vector3(0f, 1.02f, 1.88f), new Vector3(2.34f, 0.045f, 2.68f), _rubber);
            Box("Rear cockpit partition", new Vector3(0f, 2.02f, 0.55f), new Vector3(2.34f, 2.0f, 0.06f), _lining);
            Box("Cockpit roof", new Vector3(0f, 3.06f, 1.36f), new Vector3(2.34f, 0.07f, 1.62f), _lining);
            foreach (var side in new[] { -1f, 1f })
            {
                var innerLow = Pane(side, -side * 0.52f, -0.41f);
                var innerHigh = Pane(side, -side * 0.52f, 0.41f);
                var outerLow = Pane(side, side * 0.52f, -0.41f);
                var outerHigh = Pane(side, side * 0.52f, 0.41f);
                Beam("Front pane sill", innerLow, outerLow, 0.036f, _rubber);
                Beam("Front pane brow", innerHigh, outerHigh, 0.038f, _rubber);
                Beam("Windscreen centre frame", innerLow, innerHigh, 0.035f, _lining);
                Beam("Windscreen outer post", outerLow, outerHigh, 0.045f, _lining);
                var sideLow = new Vector3(side * 1.335f, 1.79f, 1.425f);
                var sideHigh = new Vector3(side * 1.335f, 2.67f, 1.425f);
                Beam("Pilot door window sill", outerLow, sideLow, 0.036f, _rubber);
                Beam("Pilot door window brow", outerHigh, sideHigh, 0.042f, _lining);
                Beam("Pilot door rear post", sideLow, sideHigh, 0.045f, _lining);
                Face("Forward nose footwell", new[] { innerLow, outerLow,
                    new Vector3(side * 0.90f, 1.04f, 3.22f), new Vector3(0f, 1.04f, 3.22f) }, _lining);
                Face("Lower pilot door lining", new[] { sideLow, outerLow,
                    new Vector3(side * 0.90f, 1.04f, 3.22f), new Vector3(side * 1.17f, 1.04f, 1.425f) }, _lining);
                Face("Rear side lining", new[] { new Vector3(side * 1.17f, 1.04f, 0.55f),
                    new Vector3(side * 1.17f, 3.06f, 0.55f), new Vector3(side * 1.335f, 3.06f, 1.425f),
                    sideHigh, sideLow, new Vector3(side * 1.17f, 1.04f, 1.425f) }, _lining);
                Face("Roof windscreen transition", new[] { innerHigh, outerHigh, sideHigh,
                    new Vector3(side * 1.17f, 3.06f, 1.425f), new Vector3(0f, 3.06f, 2.17f) }, _lining);
                Wiper(innerLow + Vector3.up * 0.03f, outerLow + Vector3.up * 0.05f, 0.009f, _rubber, side);
            }
            Face("Central nose lining", new[] { Pane(-1f, 0.52f, -0.41f), Pane(1f, -0.52f, -0.41f),
                new Vector3(0f, 1.04f, 3.22f) }, _lining);
            Face("Central brow lining", new[] { Pane(-1f, 0.52f, 0.41f), Pane(1f, -0.52f, 0.41f),
                new Vector3(0f, 3.06f, 2.17f) }, _lining);
            Face("Windscreen central bridge", new[] { Pane(-1f, 0.52f, -0.41f), Pane(1f, -0.52f, -0.41f),
                Pane(1f, -0.52f, 0.41f), Pane(-1f, 0.52f, 0.41f) }, _lining);
        }

        private void MakeSeatAndControls(float x, Material fabric)
        {
            Box("Seat suspension", new Vector3(x, 1.43f, 1.45f), new Vector3(0.32f, 0.26f, 0.36f), _metal);
            Box("Pilot cushion", new Vector3(x, 1.62f, 1.46f), new Vector3(0.42f, 0.13f, 0.48f), fabric);
            Box("Pilot reclined back", new Vector3(x, 2.01f, 1.19f), new Vector3(0.42f, 0.69f, 0.12f), fabric).localRotation = Quaternion.Euler(-7f, 0f, 0f);
            Box("Pilot headrest", new Vector3(x, 2.41f, 1.14f), new Vector3(0.27f, 0.19f, 0.15f), fabric);
            foreach (var side in new[] { -1f, 1f })
            {
                Beam("Seat shoulder bolster", new Vector3(x + side * 0.20f, 1.76f, 1.25f), new Vector3(x + side * 0.16f, 2.26f, 1.20f), 0.065f, fabric);
                Beam("Shoulder harness", new Vector3(x + side * 0.09f, 2.25f, 1.27f), new Vector3(x + side * 0.13f, 1.70f, 1.45f), 0.027f, _rubber);
                Box("Anti-torque pedal", new Vector3(x + side * 0.13f, 1.24f, 2.33f), new Vector3(0.18f, 0.075f, 0.19f), _metal).localRotation = Quaternion.Euler(-20f, 0f, 0f);
            }
            Beam("Cyclic bent column", new Vector3(x, 1.15f, 1.99f), new Vector3(x, 1.63f, 1.96f), 0.032f, _metal);
            Beam("Cyclic upper stalk", new Vector3(x, 1.63f, 1.96f), new Vector3(x, 1.87f, 1.90f), 0.027f, _metal);
            Box("Cyclic shaped grip", new Vector3(x, 1.91f, 1.90f), new Vector3(0.065f, 0.12f, 0.085f), _rubber).localRotation = Quaternion.Euler(-12f, 0f, 0f);
            Box("Cyclic thumb switch", new Vector3(x, 1.965f, 1.865f), new Vector3(0.024f, 0.012f, 0.020f), _metal);
            Beam("Collective lever", new Vector3(x - 0.29f, 1.36f, 1.12f), new Vector3(x - 0.29f, 1.70f, 1.70f), 0.032f, _metal);
            Beam("Collective grip", new Vector3(x - 0.29f, 1.70f, 1.65f), new Vector3(x - 0.29f, 1.75f, 1.82f), 0.055f, _rubber);
        }

        private Material MakeDialArtwork()
        {
            // One shared original texture replaces separate tick meshes on every static dial.
            const int size = 64;
            var pixels = new Color32[size * size];
            var dark = new Color32(4, 7, 6, 255);
            var pale = new Color32(209, 214, 194, 255);
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var dx = x - 31.5f; var dy = y - 31.5f;
                    var radius = Mathf.Sqrt(dx * dx + dy * dy);
                    var angle = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) * 12f;
                    var onTick = radius >= 23f && radius <= 28f && Mathf.Abs(angle - Mathf.Round(angle)) < 0.08f;
                    pixels[y * size + x] = onTick ? pale : dark;
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
                { name = "Bell original dial marks", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(pixels); texture.Apply(true, true); _textures.Add(texture);
            var material = Surface("Bell shared analog dial artwork", Color.white, false);
            material.SetTexture("_BaseMap", texture);
            return material;
        }

        private void Disc(string name, Vector3 centre, float radius, Material material)
        {
            var vertices = new Vector3[24];
            for (var i = 0; i < vertices.Length; i++)
            {
                var angle = i * Mathf.PI * 2f / vertices.Length;
                vertices[i] = centre + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            }
            Face(name, vertices, material);
        }

        private void Gauge(string name, Vector3 centre, float radius, float needle, bool attitude, float side)
        {
            Disc(name + " metal rim", centre, radius, _metal);
            Disc(name + " rubber inset", centre + Vector3.back * 0.002f, radius * 0.93f, _rubber);
            if (attitude)
            {
                _attitude[side < 0f ? 0 : 1] = MakeAttitudeDisc("Live analog attitude", centre + Vector3.back * 0.004f, radius * 0.84f);
                return;
            }
            Disc(name + " dial", centre + Vector3.back * 0.004f, radius * 0.84f, _dial);
            var tip = new Vector3(Mathf.Sin(needle * Mathf.Deg2Rad), Mathf.Cos(needle * Mathf.Deg2Rad), 0f) * radius * 0.62f;
            Beam("Decorative dial needle", centre + Vector3.back * 0.008f, centre + tip + Vector3.back * 0.008f, radius * 0.04f, _marks);
        }
    }
}
