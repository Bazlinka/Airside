using System;
using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>Original simplified family-specific spectator decks. No flight controls or simulated avionics.</summary>
    [ExecuteAlways]
    public sealed class JetCockpitInterior : CockpitInterior
    {
        public JetCockpitProfile Profile { get; private set; }
        private Material _panel, _trim, _black, _white, _cyan, _green, _sky, _earth;
        private readonly Transform[] _horizons = new Transform[2];
        private readonly List<(Transform bar, float bottom, bool left)> _engineBars = new();
        private TextMesh _engineReadout;
        private TextMesh _phaseReadout;
        private string _lastEngines, _lastPhase;

        public static JetCockpitInterior Build(Transform aircraft, AircraftType type)
        {
            if (aircraft == null || type == null || !JetCockpitProfile.TryFor(type.Id, out var profile))
                throw new ArgumentException("A fitted jet cockpit profile is required.");
            var host = new GameObject(type.Id + " cockpit interior");
            host.transform.SetParent(aircraft, false);
            host.transform.localPosition = new Vector3(0f,
                AircraftVisualProfiles.For(type).ModelGroundOffsetMetres + profile.EyeY, profile.EyeZ);
            var rig = host.AddComponent<JetCockpitInterior>();
            rig.Profile = profile;
            rig.MakeInterior();
            return rig;
        }

        private void MakeInterior()
        {
            var boeing = !Profile.Sidestick && Profile.Deck != JetFlightDeck.Embraer;
            _panel = Surface("panel", boeing ? new Color(0.36f, 0.32f, 0.27f) : new Color(0.32f, 0.39f, 0.43f));
            _trim = Surface("dark trim", new Color(0.065f, 0.075f, 0.085f));
            _black = Surface("display black", new Color(0.006f, 0.012f, 0.017f), false);
            _white = Surface("markings", new Color(0.82f, 0.85f, 0.82f), false);
            _cyan = Surface("display cyan", new Color(0.12f, 0.70f, 0.83f), false);
            _green = Surface("display green", new Color(0.30f, 0.87f, 0.43f), false);
            _sky = Surface("attitude sky", new Color(0.12f, 0.33f, 0.53f), false);
            _earth = Surface("attitude earth", new Color(0.38f, 0.23f, 0.10f), false);
            var lining = Surface("lining", new Color(0.48f, 0.49f, 0.47f));
            var seat = new GameObject("Left pilot eye").transform;
            seat.SetParent(transform, false);
            seat.localPosition = new Vector3(-Profile.HalfWidth * 0.43f, 0f, 0f);
            Seat = seat;
            MakeShell(lining);
            MakePanel();
            MakePedestal();
            MakeOverhead();
            foreach (var side in new[] { -1f, 1f })
            {
                var x = side * Profile.HalfWidth * 0.43f;
                Box("Pilot seat cushion", new Vector3(x, -0.77f, -0.19f), new Vector3(0.48f, 0.15f, 0.52f), _trim);
                Box("Pilot seat back", new Vector3(x, -0.32f, -0.48f), new Vector3(0.48f, 0.83f, 0.12f), _trim);
                Box("Pilot headrest", new Vector3(x, 0.22f, -0.49f), new Vector3(0.29f, 0.23f, 0.12f), _trim);
                foreach (var foot in new[] { -1f, 1f })
                    Box("Rudder pedal", new Vector3(x + foot * 0.10f, -1.22f, 0.65f), new Vector3(0.16f, 0.08f, 0.20f), _trim);
                if (Profile.Sidestick)
                {
                    Box("Sidestick console", new Vector3(side * (Profile.HalfWidth - 0.19f), -0.64f, 0.03f), new Vector3(0.31f, 0.20f, 0.65f), _panel);
                    Beam("Sidestick grip", new Vector3(side * (Profile.HalfWidth - 0.19f), -0.54f, 0.12f),
                        new Vector3(side * (Profile.HalfWidth - 0.19f), -0.34f, 0.08f), 0.055f, _trim);
                    Box("Sidestick thumb switch", new Vector3(side * (Profile.HalfWidth - 0.19f), -0.33f, 0.08f), new Vector3(0.022f, 0.018f, 0.025f), _white);
                    if (Profile.Deck != JetFlightDeck.AirbusA220)
                        Box("Stowed pilot tray", new Vector3(x, -0.39f, 0.55f), new Vector3(0.43f, 0.026f, 0.12f), _panel);
                }
                else
                {
                    Beam("Control column", new Vector3(x, -1.32f, 0.40f), new Vector3(x, -0.53f, 0.30f), 0.065f, _trim);
                    var y = -0.49f;
                    if (Profile.Deck == JetFlightDeck.Embraer)
                    {
                        Beam("Embraer ram horn left", new Vector3(x, y, 0.29f), new Vector3(x - 0.17f, y + 0.12f, 0.27f), 0.042f, _trim);
                        Beam("Embraer ram horn right", new Vector3(x, y, 0.29f), new Vector3(x + 0.17f, y + 0.12f, 0.27f), 0.042f, _trim);
                    }
                    else
                    {
                        Box("Yoke crossbar", new Vector3(x, y, 0.28f), new Vector3(0.34f, 0.045f, 0.05f), _trim);
                        foreach (var hand in new[] { -1f, 1f })
                            Box("Yoke grip", new Vector3(x + hand * 0.17f, y + 0.07f, 0.28f), new Vector3(0.045f, 0.18f, 0.055f), _trim);
                    }
                }
            }
            SetReadout("GS 0 kt\nHEIGHT 0 ft\nHDG 000°");
        }

        private void MakeShell(Material lining)
        {
            var w = Profile.HalfWidth;
            var geometry = JetCockpitShellGeometry.Build(w);
            var vertexCount = geometry.Vertices.Count;
            var vertices = new Vector3[vertexCount * 2];
            for (var i = 0; i < vertexCount; i++)
            {
                var v = geometry.Vertices[i];
                vertices[i] = vertices[i + vertexCount] = new Vector3(v.X, v.Y, v.Z);
            }
            var triangles = new int[geometry.Triangles.Count * 2];
            geometry.Triangles.CopyTo(triangles, 0);
            for (var i = 0; i < geometry.Triangles.Count; i += 3)
            {
                var offset = geometry.Triangles.Count + i;
                triangles[offset] = geometry.Triangles[i] + vertexCount;
                triangles[offset + 1] = geometry.Triangles[i + 2] + vertexCount;
                triangles[offset + 2] = geometry.Triangles[i + 1] + vertexCount;
            }
            var shell = new GameObject("Closed cockpit shell");
            shell.transform.SetParent(transform, false);
            var mesh = new Mesh { name = "Continuous jet cockpit shell", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); _meshes.Add(mesh);
            shell.AddComponent<MeshFilter>().sharedMesh = mesh;
            shell.AddComponent<MeshRenderer>().sharedMaterial = lining;
            Box("Flight deck door", new Vector3(0f, -0.40f, -1.665f), new Vector3(0.56f, 1.90f, 0.035f), _panel);
            foreach (var side in new[] { -1f, 1f })
            {
                Beam("Side window upper rail", new Vector3(side * w, 0.67f, -0.61f), new Vector3(side * w, 0.67f, 0.72f), 0.07f, lining);
                Beam("Side window sill", new Vector3(side * w, -0.05f, -0.61f), new Vector3(side * w, -0.05f, 0.72f), 0.07f, _panel);
                Beam("Forward side window upper rail", new Vector3(side * w, 0.67f, 0.72f), new Vector3(side * w * 0.73f, 0.51f, 1.05f), 0.07f, lining);
                Beam("Forward side window sill", new Vector3(side * w, -0.05f, 0.72f), new Vector3(side * w * 0.84f, -0.05f, 1.30f), 0.07f, _panel);
                Beam("Rear window pillar", new Vector3(side * w, -0.05f, -0.61f), new Vector3(side * w, 0.67f, -0.61f), 0.06f, lining);
                Beam("Front windscreen outer pillar", new Vector3(side * w * 0.84f, -0.05f, 1.30f), new Vector3(side * w * 0.73f, 0.51f, 1.05f), 0.065f, lining);
                if (Profile.Deck != JetFlightDeck.Boeing787)
                    Beam("Side quarterlight pillar", new Vector3(side * w, -0.05f, 0.41f), new Vector3(side * w, 0.67f, 0.41f), 0.045f, lining);
                Beam("Parked windscreen wiper", new Vector3(side * 0.12f, -0.015f, 1.31f), new Vector3(side * w * 0.64f, 0.005f, 1.28f), 0.013f, _trim);
            }
            Beam("Windscreen centre post", new Vector3(0f, -0.05f, 1.30f), new Vector3(0f, 0.51f, 1.05f), 0.047f, lining);
            Beam("Windscreen brow", new Vector3(-w * 0.73f, 0.51f, 1.05f), new Vector3(w * 0.73f, 0.51f, 1.05f), 0.075f, lining);

        }

        private void MakePanel()
        {
            var w = Profile.HalfWidth;
            Box("Main instrument panel", new Vector3(0f, -0.51f, 1.13f), new Vector3(w * 1.75f, 0.79f, 0.20f), _panel);
            Box("Glareshield", new Vector3(0f, -0.09f, 1.10f), new Vector3(w * 1.78f, 0.065f, 0.50f), _trim);
            Box("Flight guidance rail", new Vector3(0f, -0.18f, 0.91f), new Vector3(w * 1.02f, 0.13f, 0.065f), _panel);
            Label("Guidance legends", Profile.Sidestick ? "SPD    HDG    ALT    VS     AP" : "COURSE   IAS/MACH   HDG   ALT   V/S", new Vector3(0f, -0.145f, 0.87f), 0.0036f, Color.white);
            for (var i = 0; i < 8; i++)
            {
                var x = -w * 0.43f + i * w * 0.123f;
                Box("Guidance knob", new Vector3(x, -0.20f, 0.857f), new Vector3(0.036f, 0.036f, 0.023f), _trim);
            }
            var deck = Profile.Deck;
            if (deck == JetFlightDeck.Boeing737Ng || deck == JetFlightDeck.AirbusClassic)
            {
                for (var side = 0; side < 2; side++)
                {
                    var sign = side == 0 ? -1f : 1f;
                    Display(sign * w * 0.65f, -0.43f, 0.30f, 0.30f, "PFD", side);
                    Display(sign * w * 0.36f, -0.43f, 0.30f, 0.30f, "ND");
                }
                Display(0f, -0.37f, 0.28f, 0.23f, deck == JetFlightDeck.AirbusClassic ? "ECAM" : "ENG");
                Display(0f, -0.66f, 0.28f, 0.23f, "SYS");
            }
            else if (deck == JetFlightDeck.AirbusA350)
            {
                for (var i = 0; i < 5; i++)
                    Display((i - 2) * 0.49f, -0.45f, 0.46f, 0.32f, i == 0 || i == 4 ? "OIS" : i == 2 ? "ECAM" : "PFD / ND", i == 1 ? 0 : i == 3 ? 1 : -1);
                Display(0f, -0.78f, 0.43f, 0.22f, "SYSTEM");
            }
            else
            {
                var count = deck == JetFlightDeck.Boeing737Max || deck == JetFlightDeck.Boeing787 || deck == JetFlightDeck.AirbusA220 ? 4 : 5;
                var width = w * 1.62f / count;
                for (var i = 0; i < count; i++)
                    Display((i - (count - 1) * 0.5f) * width, -0.46f, width - 0.025f, 0.33f,
                        i == 0 || i == count - 1 ? "PFD" : deck == JetFlightDeck.Embraer && i == 2 ? "EICAS" : "ND / SYS", i == 0 ? 0 : i == count - 1 ? 1 : -1);
                if (deck == JetFlightDeck.Boeing787 || deck == JetFlightDeck.AirbusA220)
                    Display(0f, -0.79f, 0.38f, 0.21f, "SYSTEM");
            }
            _readout = Label("Live local telemetry", "", new Vector3(-w * 0.44f, -0.77f, 1.005f), 0.0048f, new Color(0.65f, 0.88f, 0.73f));
            _engineReadout = Label("Live engine spool", "", new Vector3(w * 0.45f, -0.75f, 1.005f), 0.005f, Color.green);
            _phaseReadout = Label("Local phase", "", new Vector3(0f, -0.925f, 0.985f), 0.0048f, Color.white);
            Label("Deck identity", Profile.TypeId + " · LOCAL VIEW", new Vector3(-w * 0.62f, -0.935f, 1.005f), 0.0038f, Color.white);
            // Small standby instrument between main displays and guidance.
            Box("Standby bezel", new Vector3(0f, -0.235f, 1.001f), new Vector3(0.10f, 0.06f, 0.025f), _trim);
        }

        private void Display(float x, float y, float width, float height, string title, int pilot = -1)
        {
            const float z = 1.008f;
            Box(title + " bezel", new Vector3(x, y, z + 0.015f), new Vector3(width, height, 0.025f), _trim);
            Box(title + " screen", new Vector3(x, y, z - 0.002f), new Vector3(width - 0.026f, height - 0.03f, 0.003f), _black);
            Label(title + " title", title, new Vector3(x, y + height * 0.39f, z - 0.01f), 0.0038f, Color.white);
            if (pilot >= 0)
            {
                var horizon = new GameObject("Live local attitude").transform;
                horizon.SetParent(transform, false); horizon.localPosition = new Vector3(x, y, z - 0.012f);
                var sky = Box("Attitude sky", new Vector3(x, y + 0.043f, z - 0.013f), new Vector3(width * 0.48f, 0.086f, 0.002f), _sky);
                var earth = Box("Attitude ground", new Vector3(x, y - 0.043f, z - 0.013f), new Vector3(width * 0.48f, 0.086f, 0.002f), _earth);
                sky.SetParent(horizon, true); earth.SetParent(horizon, true);
                _horizons[pilot] = horizon;
                Box("Fixed attitude reference", new Vector3(x, y, z - 0.024f), new Vector3(width * 0.22f, 0.005f, 0.002f), _white);
                for (var tick = -2; tick <= 2; tick++)
                {
                    Box("Display tape tick", new Vector3(x - width * 0.34f, y + tick * 0.036f, z - 0.015f), new Vector3(0.018f, 0.004f, 0.002f), _white);
                    Box("Display tape tick", new Vector3(x + width * 0.34f, y + tick * 0.036f, z - 0.015f), new Vector3(0.018f, 0.004f, 0.002f), _white);
                }
            }
            else if (title == "ENG" || title == "ECAM" || title == "EICAS")
            {
                // Two real startup-state bars, rather than a decorative compass on an engine display.
                foreach (var side in new[] { -1f, 1f })
                {
                    var bottom = y - height * 0.28f;
                    Box("Engine spool track", new Vector3(x + side * width * 0.22f, y - 0.005f, z - 0.013f),
                        new Vector3(0.038f, height * 0.51f, 0.002f), _panel);
                    var bar = Box("Live engine spool bar", new Vector3(x + side * width * 0.22f, bottom + height * 0.25f, z - 0.017f),
                        new Vector3(0.027f, height * 0.50f, 0.002f), _green);
                    var host = new GameObject("Engine bar datum").transform;
                    host.SetParent(transform, false); host.localPosition = new Vector3(0f, bottom, 0f);
                    bar.SetParent(host, true);
                    _engineBars.Add((bar, height * 0.50f, side < 0));
                }
            }
            else if (title == "SYS" || title == "SYSTEM" || title == "OIS")
            {
                // Restrained system/status layout; no fictitious pressures, routes or avionics values.
                for (var row = 0; row < 3; row++)
                    Box("System page row", new Vector3(x, y + 0.04f - row * 0.04f, z - 0.014f),
                        new Vector3(width * 0.58f, 0.005f, 0.002f), row == 0 ? _cyan : _white);
            }
            else
            {
                // A compass rose with no invented route or avionics values.
                for (var i = 0; i < 12; i++)
                {
                    var angle = i * Mathf.PI / 6f;
                    var radius = Mathf.Min(width, height) * 0.24f;
                    Box("Compass tick", new Vector3(x + Mathf.Sin(angle) * radius, y + Mathf.Cos(angle) * radius, z - 0.014f), new Vector3(0.006f, 0.011f, 0.002f), _green);
                }
            }
            for (var i = 0; i < 5; i++)
                Box("Display key", new Vector3(x - width * 0.32f + i * width * 0.16f, y - height * 0.46f, z - 0.01f), new Vector3(0.022f, 0.008f, 0.008f), _panel);
        }

        private void MakePedestal()
        {
            Box("Centre pedestal", new Vector3(0f, -0.94f, -0.03f), new Vector3(0.40f, 0.42f, 1.75f), _panel);
            foreach (var side in new[] { -1f, 1f })
            {
                var x = side * 0.105f;
                Box("FMS bezel", new Vector3(x, -0.69f, 0.63f), new Vector3(0.19f, 0.045f, 0.29f), _trim);
                Box("FMS display", new Vector3(x, -0.662f, 0.70f), new Vector3(0.15f, 0.003f, 0.10f), _black);
                for (var row = 0; row < 4; row++)
                    for (var col = 0; col < 4; col++)
                        Box("FMS key", new Vector3(x - 0.060f + col * 0.04f, -0.655f, 0.61f - row * 0.035f), new Vector3(0.022f, 0.012f, 0.017f), _panel);
                Box("Thrust lever slot", new Vector3(side * 0.054f, -0.72f, 0.02f), new Vector3(0.018f, 0.012f, 0.35f), _trim);
                Beam("Thrust lever", new Vector3(side * 0.054f, -0.70f, 0.0f), new Vector3(side * 0.054f, -0.47f, 0.10f), 0.021f, _white);
                Box("Thrust handle", new Vector3(side * 0.054f, -0.47f, 0.10f), new Vector3(0.07f, 0.046f, 0.10f), _trim);
                Box("Engine master", new Vector3(side * 0.055f, -0.68f, -0.27f), new Vector3(0.032f, 0.052f, 0.03f), _trim);
            }
            Beam("Speedbrake lever", new Vector3(-0.16f, -0.70f, 0.07f), new Vector3(-0.16f, -0.52f, 0.02f), 0.015f, _trim);
            Beam("Flap lever", new Vector3(0.16f, -0.70f, -0.10f), new Vector3(0.16f, -0.53f, -0.05f), 0.018f, _white);
            for (var row = 0; row < 3; row++)
            {
                Box("Radio panel", new Vector3(0f, -0.71f, -0.39f - row * 0.14f), new Vector3(0.34f, 0.025f, 0.105f), _trim);
                Box("Radio display", new Vector3(-0.035f, -0.691f, -0.39f - row * 0.14f), new Vector3(0.18f, 0.003f, 0.047f), _black);
                Box("Radio tuning knob", new Vector3(0.12f, -0.68f, -0.39f - row * 0.14f), new Vector3(0.035f, 0.024f, 0.035f), _panel);
            }
        }

        private void MakeOverhead()
        {
            Box("Overhead panel", new Vector3(0f, 0.635f, -0.10f), new Vector3(0.64f, 0.052f, 1.30f), _panel);
            var legends = new[] { "ELEC", "FUEL", "HYD", "AIR", "LIGHTS", "ANTI ICE" };
            for (var row = 0; row < 6; row++)
            {
                var z = 0.43f - row * 0.20f;
                Box("Overhead section", new Vector3(0f, 0.602f, z), new Vector3(0.59f, 0.010f, 0.18f), _trim);
                var label = Label("Overhead legend", legends[row], new Vector3(0f, 0.592f, z + 0.05f), 0.0048f, Color.white);
                label.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                for (var col = 0; col < 6; col++)
                    Box("Overhead toggle", new Vector3(-0.23f + col * 0.092f, 0.575f, z - 0.015f), new Vector3(0.017f, 0.036f, 0.022f), _panel);
            }
        }

        public void SetFlightState(Transform aircraft, EngineState engines, string phase)
        {
            // Attitude follows the already-rendered aircraft, not a second flight model.
            foreach (var horizon in _horizons)
                if (horizon != null)
                    horizon.localRotation = Quaternion.Euler(0f, 0f, -Mathf.DeltaAngle(0f, aircraft.eulerAngles.z));
            foreach (var (bar, height, left) in _engineBars)
            {
                var fill = Mathf.Clamp01(left ? engines.Left : engines.Right);
                var size = bar.localScale; size.y = Mathf.Max(0.001f, height * fill); bar.localScale = size;
                var position = bar.localPosition; position.y = size.y * 0.5f; bar.localPosition = position;
            }
            var text = $"SPOOL L {engines.Left * 100f:0}%\nSPOOL R {engines.Right * 100f:0}%";
            if (text != _lastEngines) { _engineReadout.text = text; _lastEngines = text; }
            if (phase != _lastPhase) { _phaseReadout.text = phase; _lastPhase = phase; }
        }
    }
}
