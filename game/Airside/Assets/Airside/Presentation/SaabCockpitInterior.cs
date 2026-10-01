using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>Original, editable SF34 spectator interior, authored in the exterior kit's metre coordinates.</summary>
    [ExecuteAlways]
    public sealed class SaabCockpitInterior : TurbopropCockpitInterior
    {
        public static SaabCockpitInterior Build(Transform aircraft)
        {
            var host = new GameObject("SF34 cockpit interior");
            host.transform.SetParent(aircraft, false);
            host.transform.localPosition = new Vector3(0f, AircraftVisualProfiles.Saab340.ModelGroundOffsetMetres, 0f);
            var rig = host.AddComponent<SaabCockpitInterior>();
            rig.MakeInterior();
            return rig;
        }

        private void Dial(string name, float x, float y, float radius, float needleDegrees = 30f)
        {
            var z = 7.985f;
            Disc(name + " rim", new Vector3(x, y, z), radius, _metal);
            Disc(name + " face", new Vector3(x, y, z - 0.002f), radius * 0.88f, _dialMarks);
            var needle = new Vector3(Mathf.Sin(needleDegrees * Mathf.Deg2Rad), Mathf.Cos(needleDegrees * Mathf.Deg2Rad), 0f);
            Stroke(name + " needle", new Vector3(x, y, z - 0.006f),
                new Vector3(x, y, z - 0.006f) + needle * radius * 0.65f, Mathf.Max(0.003f, radius * 0.05f), _white);
            Disc(name + " hub", new Vector3(x, y, z - 0.007f), radius * 0.1f, _metal);
        }

        private void FlightDisplays(float x)
        {
            var sky = Surface("EFIS sky", new Color(0.18f, 0.36f, 0.46f), false);
            var earth = Surface("EFIS earth", new Color(0.39f, 0.27f, 0.15f), false);
            foreach (var y in new[] { 1.91f, 1.665f })
            {
                Box("CRT bezel", new Vector3(x, y, 8.0f), new Vector3(0.23f, 0.215f, 0.055f), _black);
                Box("CRT glass", new Vector3(x, y, 7.967f), new Vector3(0.185f, 0.17f, 0.009f), _black);
                for (var side = -1; side <= 1; side += 2)
                    Disc("CRT adjustment knob", new Vector3(x + side * 0.094f, y - 0.087f, 7.96f), 0.012f, _metal);
            }
            Box("EFIS blue sky", new Vector3(x, 1.945f, 7.958f), new Vector3(0.145f, 0.07f, 0.003f), sky);
            Box("EFIS brown ground", new Vector3(x, 1.875f, 7.957f), new Vector3(0.145f, 0.07f, 0.003f), earth);
            Box("EFIS horizon", new Vector3(x, 1.91f, 7.953f), new Vector3(0.145f, 0.002f, 0.002f), _white);
            for (var row = -2; row <= 2; row++)
                Box("Pitch ladder", new Vector3(x, 1.91f + row * 0.02f, 7.95f), new Vector3(row == 0 ? 0.09f : 0.035f, 0.0018f, 0.002f), _white);
            Box("Flight director", new Vector3(x, 1.91f, 7.947f), new Vector3(0.07f, 0.004f, 0.002f), _green);
            Disc("Navigation compass", new Vector3(x, 1.665f, 7.955f), 0.072f, _metal);
            Disc("Navigation background", new Vector3(x, 1.665f, 7.953f), 0.066f, _compassMarks);
            Beam("Course pointer", new Vector3(x, 1.615f, 7.945f), new Vector3(x, 1.715f, 7.945f), 0.003f, _green);
            Label("Compass north", "N", new Vector3(x, 1.712f, 7.94f), 0.0028f, Color.white);
        }

        private void MakeInterior()
        {
            var lining = Surface("warm ivory lining", new Color(0.64f, 0.63f, 0.57f));
            var trim = Surface("window seals", new Color(0.18f, 0.20f, 0.19f));
            _panel = Surface("grey instrument panel", new Color(0.46f, 0.49f, 0.47f));
            _black = Surface("black instrument faces", new Color(0.025f, 0.035f, 0.035f), false);
            _white = Surface("instrument markings", new Color(0.78f, 0.82f, 0.74f), false);
            _green = Surface("instrument green", new Color(0.45f, 0.78f, 0.51f), false);
            _metal = Surface("instrument rims", new Color(0.26f, 0.29f, 0.28f));
            _dialMarks = DialArtwork("dial face markings", 10, 150f, 27f);
            _compassMarks = DialArtwork("compass face markings", 12, 0f, 30f);
            var fabric = Surface("seat fabric", new Color(0.36f, 0.38f, 0.35f));
            Seat = new GameObject("Left pilot eye").transform;
            Seat.SetParent(transform, false);
            Seat.localPosition = new Vector3(-0.43f, 2.38f, 7.13f);
            Seat.localRotation = Quaternion.Euler(16f, 0f, 0f);
            // Continuous opaque shell: the exterior fuselage is hidden in cockpit mode.
            // The panel and trim are fittings, not a substitute for walls beneath the panes.
            Face("Flight deck floor", new[] {
                new Vector3(-0.90f, 1.23f, 5.86f), new Vector3(-0.90f, 1.23f, 7.69f),
                new Vector3(-0.69f, 1.23f, 8.18f), new Vector3(0f, 1.23f, 8.35f),
                new Vector3(0.69f, 1.23f, 8.18f), new Vector3(0.90f, 1.23f, 7.69f),
                new Vector3(0.90f, 1.23f, 5.86f) }, trim);
            Box("Flight deck roof", new Vector3(0f, 2.79f, 6.94f), new Vector3(1.7f, 0.08f, 2.20f), lining);
            Box("Rear bulkhead", new Vector3(0f, 1.98f, 5.9f), new Vector3(1.72f, 1.55f, 0.06f), lining);
            // Raked forward panes, angled corner panes and side panes; no flat bus windscreen.
            var centreLow = new Vector3(0f, 2.16f, 8.35f);
            var centreHigh = new Vector3(0f, 2.70f, 8.08f);
            Beam("Centre windscreen seal", centreLow, centreHigh, 0.027f, trim);
            foreach (var side in new[] { -1f, 1f })
            {
                var frontLow = new Vector3(side * 0.69f, 2.12f, 8.18f);
                var frontHigh = new Vector3(side * 0.60f, 2.66f, 7.99f);
                var cornerLow = new Vector3(side * 0.86f, 2.04f, 7.69f);
                var cornerHigh = new Vector3(side * 0.80f, 2.68f, 7.43f);
                Beam("Forward brow seal", centreHigh, frontHigh, 0.035f, trim);
                Beam("Forward sill seal", centreLow, frontLow, 0.032f, trim);
                Beam("Angled windscreen post", frontLow, frontHigh, 0.042f, lining);
                Beam("Quarter window sill", frontLow, cornerLow, 0.035f, trim);
                Beam("Quarter window brow", frontHigh, cornerHigh, 0.041f, lining);
                Beam("Side window front post", cornerLow, cornerHigh, 0.046f, lining);
                Beam("Sliding window sill", cornerLow, new Vector3(side * 0.86f, 2.04f, 6.38f), 0.039f, trim);
                Beam("Sliding window rail", cornerHigh, new Vector3(side * 0.81f, 2.72f, 6.38f), 0.042f, lining);
                Beam("Sliding window rear post", new Vector3(side * 0.86f, 2.04f, 6.38f), new Vector3(side * 0.81f, 2.72f, 6.38f), 0.043f, lining);
                Face("Forward roof taper", new[] { centreHigh, frontHigh, cornerHigh,
                    new Vector3(side * 0.85f, 2.79f, 6.8f), new Vector3(0f, 2.79f, 6.8f) }, lining);
                Face("Lower side lining", new[] { cornerLow,
                    new Vector3(side * 0.86f, 2.04f, 5.86f),
                    new Vector3(side * 0.90f, 1.23f, 5.86f),
                    new Vector3(side * 0.90f, 1.23f, 7.69f) }, lining);
                Face("Forward footwell shell", new[] { centreLow, frontLow, cornerLow,
                    new Vector3(side * 0.90f, 1.23f, 7.69f),
                    new Vector3(side * 0.69f, 1.23f, 8.18f),
                    new Vector3(0f, 1.23f, 8.35f) }, lining);
                Face("Rear side lining", new[] {
                    new Vector3(side * 0.86f, 2.04f, 5.86f),
                    new Vector3(side * 0.86f, 2.04f, 6.38f),
                    new Vector3(side * 0.81f, 2.79f, 6.38f),
                    new Vector3(side * 0.81f, 2.79f, 5.86f) }, lining);
                Beam("Window latch", new Vector3(side * 0.83f, 2.07f, 7.12f), new Vector3(side * 0.83f, 2.07f, 7.29f), 0.018f, _metal);
                Box("Side console", new Vector3(side * 0.74f, 1.49f, 7.18f), new Vector3(0.16f, 0.1f, 0.65f), _panel);
                Box("Pilot cushion", new Vector3(side * 0.43f, 1.55f, 6.91f), new Vector3(0.50f, 0.13f, 0.48f), fabric);
                Box("Pilot seat back", new Vector3(side * 0.43f, 1.90f, 6.64f), new Vector3(0.50f, 0.70f, 0.12f), fabric);
                Box("Pilot headrest", new Vector3(side * 0.43f, 2.31f, 6.62f), new Vector3(0.27f, 0.21f, 0.13f), fabric);
                Box("Yoke column", new Vector3(side * 0.43f, 1.50f, 7.57f), new Vector3(0.09f, 0.57f, 0.10f), lining);
                var x = side * 0.43f;
                Beam("Yoke lower crossbar", new Vector3(x - 0.15f, 1.77f, 7.52f), new Vector3(x + 0.15f, 1.77f, 7.52f), 0.027f, trim);
                foreach (var grip in new[] { -1f, 1f })
                {
                    Beam("Yoke squared grip", new Vector3(x + grip * 0.15f, 1.77f, 7.52f), new Vector3(x + grip * 0.15f, 1.94f, 7.56f), 0.032f, trim);
                    Beam("Yoke inward shoulder", new Vector3(x + grip * 0.15f, 1.94f, 7.56f), new Vector3(x + grip * 0.045f, 1.94f, 7.56f), 0.028f, trim);
                }
                Box("Yoke centre hub", new Vector3(x, 1.85f, 7.54f), new Vector3(0.085f, 0.07f, 0.05f), trim);
                Box("Yoke checklist clip", new Vector3(x, 1.81f, 7.49f), new Vector3(0.075f, 0.13f, 0.004f), _white);
                Label("Yoke identification", "SF34", new Vector3(x, 1.85f, 7.509f), 0.0033f, Color.white);
                // Separate vertical CRT pair with adjacent round instruments.
                FlightDisplays(x);
                Dial("Outboard upper flight gauge", x + side * 0.18f, 1.93f, 0.066f);
                Dial("Outboard lower flight gauge", x + side * 0.18f, 1.74f, 0.055f, -35f);
                Dial("Inboard upper flight gauge", x - side * 0.17f, 1.94f, 0.049f, 45f);
                Dial("Inboard lower flight gauge", x - side * 0.17f, 1.77f, 0.049f, -15f);
            }
            // Chamfered panel silhouette and shallow glare shield leave the instruments visible.
            Face("Instrument panel face", new[] { new Vector3(-0.78f, 1.53f, 8.015f), new Vector3(0.78f, 1.53f, 8.015f),
                new Vector3(0.78f, 1.96f, 8.015f), new Vector3(0.61f, 2.14f, 8.015f),
                new Vector3(-0.61f, 2.14f, 8.015f), new Vector3(-0.78f, 1.96f, 8.015f) }, _panel);
            foreach (var side in new[] { -1f, 1f })
            {
                Beam("Windscreen wiper arm", new Vector3(side * 0.06f, 2.18f, 8.32f), new Vector3(side * 0.40f, 2.20f, 8.23f), 0.009f, trim);
                Beam("Parked windscreen wiper blade", new Vector3(side * 0.22f, 2.205f, 8.275f), new Vector3(side * 0.58f, 2.17f, 8.205f), 0.012f, trim);
                for (var row = 0; row < 3; row++)
                    Box("Panel outboard switch", new Vector3(side * 0.725f, 1.88f - row * 0.085f, 7.987f), new Vector3(0.022f, 0.017f, 0.017f), trim);
            }
            Box("Shallow glareshield", new Vector3(0f, 2.17f, 8.12f), new Vector3(1.45f, 0.045f, 0.32f), trim);
            Box("Autopilot control rail", new Vector3(0f, 2.075f, 7.994f), new Vector3(0.70f, 0.083f, 0.028f), _metal);
            for (var i = 0; i < 12; i++)
            {
                Box("Flight guidance button", new Vector3(-0.29f + i * 0.052f, 2.075f, 7.975f), new Vector3(0.025f, 0.021f, 0.008f), _black);
                Box("Guidance indicator", new Vector3(-0.29f + i * 0.052f, 2.09f, 7.968f), new Vector3(0.018f, 0.003f, 0.002f), _green);
            }
            Label("Guidance legends", "HDG   NAV   AP   ALT   VS", new Vector3(0f, 2.11f, 7.969f), 0.0035f, Color.white);
            Box("Engine instrument inset", new Vector3(0f, 1.835f, 8.0f), new Vector3(0.27f, 0.36f, 0.025f), _metal);
            var titles = new[] { "TRQ", "ITT", "NP", "NG" };
            for (var row = 0; row < 4; row++)
            {
                var y = 1.965f - row * 0.082f;
                Dial("Left engine " + titles[row], -0.074f, y, 0.035f, 35f - row * 12f);
                Dial("Right engine " + titles[row], 0.074f, y, 0.035f, 32f - row * 10f);
                Label("Engine gauge legend", titles[row], new Vector3(0f, y, 7.97f), 0.0023f, Color.white);
            }
            Box("Local telemetry inset", new Vector3(0f, 1.575f, 7.995f), new Vector3(0.26f, 0.11f, 0.02f), _black);
            _readout = Label("Local telemetry", "", new Vector3(0f, 1.575f, 7.98f), 0.0038f, new Color(0.65f, 0.85f, 0.69f));
            Label("Panel identity", "SF34 • LOCAL VIEW", new Vector3(-0.43f, 1.535f, 7.978f), 0.0034f, Color.white);
            // Narrow radio pedestal with three paired turboprop lever groups.
            Box("Centre pedestal", new Vector3(0f, 1.48f, 7.19f), new Vector3(0.31f, 0.43f, 1.30f), _panel);
            for (var row = 0; row < 3; row++)
            {
                Box("Radio control unit", new Vector3(0f, 1.702f, 7.66f - row * 0.12f), new Vector3(0.26f, 0.018f, 0.095f), _metal);
                for (var col = 0; col < 5; col++)
                    Box("Radio key", new Vector3(-0.1f + col * 0.05f, 1.716f, 7.66f - row * 0.12f), new Vector3(0.018f, 0.014f, 0.035f), _black);
            }
            for (var group = 0; group < 3; group++)
                foreach (var side in new[] { -1f, 1f })
                {
                    var x = -0.10f + group * 0.10f + side * 0.022f;
                    Box("Lever slot", new Vector3(x, 1.708f, 7.16f), new Vector3(0.009f, 0.006f, 0.30f), _black);
                    Beam("Turboprop lever", new Vector3(x, 1.71f, 7.13f), new Vector3(x, 1.86f - group * 0.025f, 7.23f), 0.013f, _metal);
                    Box(group == 0 ? "Power handle" : group == 1 ? "Condition handle" : "Propeller handle",
                        new Vector3(x, 1.86f - group * 0.025f, 7.23f), new Vector3(0.039f, 0.027f, 0.06f), group == 1 ? _white : trim);
                }
            Box("Overhead electrical panel", new Vector3(0f, 2.742f, 7.31f), new Vector3(0.48f, 0.025f, 1.04f), _panel);
            for (var row = 0; row < 6; row++)
                for (var col = 0; col < 6; col++)
                    Box("Overhead switch", new Vector3(-0.19f + col * 0.076f, 2.719f, 6.88f + row * 0.155f), new Vector3(0.013f, 0.033f, 0.018f), trim);
            SetReadout("GS 0 kt\nHEIGHT 0 ft\nHDG 000°");
        }

    }
}
