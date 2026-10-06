using System;
using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;
using static Airside.Presentation.JetCockpitShellGeometry;

namespace Airside.Presentation
{
    /// <summary>Original simplified family-specific spectator decks. No flight controls or simulated avionics.</summary>
    [ExecuteAlways]
    public sealed class JetCockpitInterior : CockpitInterior
    {
        public JetCockpitProfile Profile { get; private set; }
        private Material _panel, _trim, _black, _white, _cyan, _green, _seatFabric;
        private readonly AttitudeDisc[] _horizons = new AttitudeDisc[2];
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
            // Family material treatments are project-authored, not operator-specific replicas.
            var panelColor = Profile.Deck switch
            {
                JetFlightDeck.Boeing737Ng or JetFlightDeck.Boeing737Max => new Color(0.36f, 0.32f, 0.27f),
                JetFlightDeck.Boeing787 => new Color(0.31f, 0.29f, 0.27f),
                JetFlightDeck.AirbusClassic => new Color(0.32f, 0.39f, 0.43f),
                JetFlightDeck.AirbusA350 => new Color(0.27f, 0.33f, 0.36f),
                JetFlightDeck.AirbusA220 => new Color(0.38f, 0.40f, 0.41f),
                _ => new Color(0.37f, 0.39f, 0.40f),
            };
            _panel = Surface("panel", panelColor);
            _seatFabric = Surface("woven pilot upholstery", panelColor * 0.55f);
            if (_seatFabric.HasProperty("_Smoothness")) _seatFabric.SetFloat("_Smoothness", 0.03f);
            _trim = Surface("dark trim", new Color(0.065f, 0.075f, 0.085f));
            _black = Surface("display black", new Color(0.006f, 0.012f, 0.017f), false);
            _white = Surface("markings", new Color(0.82f, 0.85f, 0.82f), false);
            _cyan = Surface("display cyan", new Color(0.12f, 0.70f, 0.83f), false);
            _green = Surface("display green", new Color(0.30f, 0.87f, 0.43f), false);
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
                MakePilotSeat(x, side);
                foreach (var foot in new[] { -1f, 1f })
                    Box("Rudder pedal", new Vector3(x + foot * 0.10f, -1.22f, 0.65f), new Vector3(0.16f, 0.08f, 0.20f), _trim);
                if (Profile.Sidestick)
                {
                    Box("Sidestick console", new Vector3(side * (Profile.HalfWidth - 0.19f), -0.64f, 0.03f), new Vector3(0.31f, 0.20f, 0.65f), _panel);
                    var stickX = side * (Profile.HalfWidth - 0.19f);
                    Box("Sidestick gaiter", new Vector3(stickX, -0.535f, 0.12f), new Vector3(0.12f, 0.05f, 0.13f), _trim);
                    Beam("Sidestick stem", new Vector3(stickX, -0.53f, 0.12f), new Vector3(stickX, -0.41f, 0.085f), 0.032f, _trim);
                    Beam("Sidestick grip", new Vector3(stickX, -0.44f, 0.095f), new Vector3(stickX + side * 0.015f, -0.34f, 0.08f), 0.06f, _trim);
                    Box("Sidestick trigger", new Vector3(stickX, -0.38f, 0.047f), new Vector3(0.024f, 0.031f, 0.018f), _white);
                    Box("Sidestick thumb switch", new Vector3(side * (Profile.HalfWidth - 0.19f), -0.33f, 0.08f), new Vector3(0.022f, 0.018f, 0.025f), _white);
                    if (Profile.Deck != JetFlightDeck.AirbusA220)
                        Box("Stowed pilot tray", new Vector3(x, -0.39f, 0.55f), new Vector3(0.43f, 0.026f, 0.12f), _panel);
                }
                else
                {
                    Beam("Control column", new Vector3(x, -1.32f, 0.40f), new Vector3(x, -0.53f, 0.30f), 0.065f, _trim);
                    MakeYoke(x);
                }
            }
            SetReadout("GS 0 kt\nHEIGHT 0 ft\nHDG 000°");
        }

        private void MakePilotSeat(float x, float side)
        {
            // Project-authored family seating: all backs/harnesses remain behind
            // the fixed pilot-eye plane, with no headrest wrapping into the view.
            var wideDeck = Profile.Deck == JetFlightDeck.AirbusA350 || Profile.Deck == JetFlightDeck.Boeing787;
            var backWidth = wideDeck ? 0.46f : 0.43f;
            var tilt = Profile.Deck == JetFlightDeck.Embraer ? -7f : -10f;
            Box("Pilot seat pan", new Vector3(x, -0.845f, -0.19f), new Vector3(0.48f, 0.065f, 0.52f), _trim);
            Box("Pilot seat cushion", new Vector3(x, -0.765f, -0.18f), new Vector3(0.42f, 0.13f, 0.49f), _seatFabric);
            var back = Box("Pilot seat back", new Vector3(x, -0.32f, -0.48f), new Vector3(backWidth, 0.78f, 0.10f), _seatFabric);
            back.localRotation = Quaternion.Euler(tilt, 0f, 0f);
            var shell = Box("Pilot seat rear shell", new Vector3(x, -0.33f, -0.55f), new Vector3(backWidth + 0.018f, 0.75f, 0.045f), _panel);
            shell.localRotation = back.localRotation;
            var head = Box("Pilot headrest", new Vector3(x, 0.22f, -0.54f), new Vector3(wideDeck ? 0.30f : 0.27f, 0.20f, 0.13f), _seatFabric);
            head.localRotation = Quaternion.Euler(tilt, 0f, 0f);
            Box("Pilot lumbar cushion", new Vector3(x, -0.56f, -0.38f), new Vector3(backWidth * 0.74f, 0.17f, 0.09f), _seatFabric);
            Box("Seat suspension base", new Vector3(x, -1.11f, -0.27f), new Vector3(0.25f, 0.49f, 0.30f), _trim);
            foreach (var hand in new[] { -1f, 1f })
            {
                var bolster = Box("Pilot seat side bolster", new Vector3(x + hand * backWidth * 0.44f, -0.33f, -0.42f),
                    new Vector3(0.065f, 0.63f, 0.11f), _seatFabric);
                bolster.localRotation = back.localRotation;
                Box("Seat floor rail", new Vector3(x + hand * 0.14f, -1.3775f, -0.23f), new Vector3(0.035f, 0.045f, 0.72f), _trim);
                Beam("Headrest support", new Vector3(x + hand * 0.075f, 0.04f, -0.53f),
                    new Vector3(x + hand * 0.075f, 0.15f, -0.55f), 0.015f, _panel);
                var outboardStick = Profile.Sidestick && hand == side;
                var armZ = outboardStick ? -0.22f : -0.06f;
                Box("Pilot armrest", new Vector3(x + hand * 0.255f, -0.53f, armZ),
                    new Vector3(0.062f, 0.060f, outboardStick ? 0.30f : 0.42f), _trim);
                Beam("Armrest support", new Vector3(x + hand * 0.24f, -0.80f, -0.28f),
                    new Vector3(x + hand * 0.255f, -0.56f, -0.23f), 0.025f, _panel);
                // Static stowed harness; no restraint animation or seat movement.
                Beam("Pilot shoulder harness", new Vector3(x + hand * 0.12f, -0.03f, -0.405f),
                    new Vector3(x + hand * 0.055f, -0.60f, -0.345f), 0.023f, _trim);
                Beam("Pilot lap harness", new Vector3(x + hand * 0.18f, -0.70f, -0.17f),
                    new Vector3(x + hand * 0.025f, -0.70f, -0.30f), 0.023f, _trim);
            }
            Box("Pilot harness buckle", new Vector3(x, -0.69f, -0.29f), new Vector3(0.05f, 0.018f, 0.042f), _panel);
        }

        private void MakeYoke(float x)
        {
            const float y = -0.49f;
            Box("Yoke hub", new Vector3(x, y, 0.29f), new Vector3(0.11f, 0.09f, 0.08f), _trim);
            foreach (var hand in new[] { -1f, 1f })
            {
                var horn = Profile.Deck == JetFlightDeck.Embraer;
                var modern = Profile.Deck == JetFlightDeck.Boeing787;
                // Bent, tapered segments retain family silhouettes without a square crossbar.
                var a = new Vector3(x + hand * 0.045f, y, 0.29f);
                var b = new Vector3(x + hand * (horn ? 0.11f : 0.14f), y + (horn ? 0.03f : -0.015f), 0.27f);
                var c = new Vector3(x + hand * (horn ? 0.16f : 0.18f), y + (horn ? 0.09f : 0.025f), modern ? 0.24f : 0.27f);
                var d = new Vector3(x + hand * (horn ? 0.13f : modern ? 0.15f : 0.17f), y + 0.15f, 0.255f);
                var name = horn ? "Embraer ram horn" : modern ? "787 moulded yoke" : "737 yoke";
                Beam(name + " arm", a, b, 0.047f, _trim);
                Beam(name + " shoulder", b, c, 0.052f, _trim);
                Beam(name + " grip", c, d, horn ? 0.043f : 0.057f, _trim);
                Box("Yoke thumb switch", d + new Vector3(0f, -0.015f, -0.028f), new Vector3(0.023f, 0.015f, 0.013f), _white);
            }
            Box("Yoke checklist clip", new Vector3(x, y + 0.035f, 0.24f), new Vector3(0.045f, 0.013f, 0.017f), _white);
        }

        private void MakeShell(Material lining)
        {
            var w = Profile.HalfWidth;
            var geometry = JetCockpitShellGeometry.Build(Profile);
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
            foreach (var frame in geometry.Frames)
                Beam(frame.Name, new Vector3(frame.Start.X, frame.Start.Y, frame.Start.Z),
                    new Vector3(frame.End.X, frame.End.Y, frame.End.Z), frame.Width, frame.PanelMaterial ? _panel : lining);
            foreach (var side in new[] { -1f, 1f })
                Wiper(new Vector3(side * 0.12f, SillY + 0.035f, 1.31f),
                    new Vector3(side * w * 0.64f, SillY + 0.055f, 1.28f), 0.013f, _trim, side);

        }

        private void MakePanel()
        {
            var first = transform.childCount;
            BuildPanel();
            // Authored with the glareshield 0.0575 below the eye; drop the whole panel so its leading
            // edge sits at the real over-the-nose angle and the runway stays visible past the nose.
            var drop = GlareTopY - 0.0325f - (-0.09f);
            for (var i = first; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                child.localPosition += new Vector3(0f, drop, 0f);
            }
        }

        private void BuildPanel()
        {
            var w = Profile.HalfWidth;
            Box("Main instrument panel", new Vector3(0f, -0.51f, 1.13f), new Vector3(w * 1.75f, 0.79f, 0.20f), _panel);
            Box("Glareshield", new Vector3(0f, -0.09f, 1.10f), new Vector3(w * 1.78f, 0.065f, 0.50f), _trim);
            Box("Flight guidance rail", new Vector3(0f, -0.18f, 0.91f), new Vector3(w * 1.02f, 0.13f, 0.065f), _panel);
            Label("Guidance legends", Profile.Sidestick ? "SPD    HDG    ALT    VS     AP" : "COURSE   IAS/MACH   HDG   ALT   V/S", new Vector3(0f, -0.145f, 0.87f), 0.0036f, Color.white);
            for (var i = 0; i < 4; i++)
            {
                var x = (i - 1.5f) * w * 0.24f;
                Box("Guidance value aperture", new Vector3(x, -0.175f, 0.869f), new Vector3(w * 0.16f, 0.035f, 0.004f), _black);
                Box("Guidance rotary collar", new Vector3(x, -0.215f, 0.865f), new Vector3(0.049f, 0.044f, 0.013f), _trim);
                Box("Guidance knob", new Vector3(x, -0.215f, 0.851f), new Vector3(0.030f, 0.031f, 0.027f), _panel);
            }
            foreach (var side in new[] { -1f, 1f })
                for (var button = 0; button < 2; button++)
                    Box("Autopilot pushbutton", new Vector3(side * w * (0.43f + button * 0.055f), -0.195f, 0.863f),
                        new Vector3(0.031f, 0.029f, 0.011f), _trim);
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
                foreach (var station in JetCockpitProfile.A350Displays)
                    Display(station.X, station.Y, station.Width, station.Height, station.Title, station.Pilot, station.Yaw);
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

        private void Display(float x, float y, float width, float height, string title, int pilot = -1, float yaw = 0f)
        {
            var first = transform.childCount;
            const float z = 1.008f;
            Box(title + " bezel", new Vector3(x, y, z + 0.015f), new Vector3(width, height, 0.025f), _trim);
            Box(title + " screen", new Vector3(x, y, z - 0.002f), new Vector3(width - 0.026f, height - 0.03f, 0.003f), _black);
            Label(title + " title", title, new Vector3(x, y + height * 0.39f, z - 0.01f), 0.0038f, Color.white);
            if (pilot >= 0)
            {
                _horizons[pilot] = MakeAttitudeDisc("Live local attitude", new Vector3(x, y, z - 0.013f), Mathf.Min(width, height) * 0.36f);
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
            if (yaw != 0f)
            {
                // Rotate bezel, page details and keys together around the screen centre.
                var group = new GameObject(title + " inward display station").transform;
                group.SetParent(transform, false);
                group.localPosition = new Vector3(x, y, z);
                for (var child = transform.childCount - 2; child >= first; child--)
                    transform.GetChild(child).SetParent(group, true);
                group.localRotation = Quaternion.Euler(0f, yaw, 0f);
                // Pull the complete angled housing forward so its inner edge cannot
                // disappear inside the otherwise flat main-panel face (Z = 1.03).
                group.localPosition -= new Vector3(0f, 0f, Mathf.Abs(Mathf.Sin(yaw * Mathf.Deg2Rad)) * width * 0.5f + 0.012f);
            }
        }

        private void MakePedestal()
        {
            Box("Centre pedestal", new Vector3(0f, -0.94f, -0.03f), new Vector3(0.40f, 0.42f, 1.75f), _panel);
            foreach (var side in new[] { -1f, 1f })
            {
                var x = side * 0.105f;
                var cursorDeck = Profile.Deck == JetFlightDeck.AirbusA350 || Profile.Deck == JetFlightDeck.Boeing787;
                Box(cursorDeck ? "Cursor controller plinth" : "FMS bezel", new Vector3(x, -0.69f, 0.63f), new Vector3(0.19f, 0.045f, 0.29f), _trim);
                if (cursorDeck)
                {
                    Box("Cursor palm rest", new Vector3(x, -0.648f, 0.59f), new Vector3(0.13f, 0.065f, 0.11f), _panel);
                    Box("Cursor touch surface", new Vector3(x, -0.66f, 0.70f), new Vector3(0.105f, 0.009f, 0.075f), _black);
                    foreach (var keySide in new[] { -1f, 1f })
                        Box("Cursor select key", new Vector3(x + keySide * 0.07f, -0.655f, 0.70f), new Vector3(0.022f, 0.012f, 0.05f), _panel);
                }
                else
                {
                    Box("FMS display", new Vector3(x, -0.662f, 0.70f), new Vector3(0.15f, 0.003f, 0.10f), _black);
                    for (var row = 0; row < 4; row++)
                        for (var col = 0; col < 4; col++)
                            Box("FMS key", new Vector3(x - 0.060f + col * 0.04f, -0.655f, 0.61f - row * 0.035f), new Vector3(0.022f, 0.012f, 0.017f), _panel);
                }
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
            // Unequal system groups break the old six identical rows. These are
            // restrained spectator fittings, with no invented operational indications.
            var boeing = Profile.Deck == JetFlightDeck.Boeing737Ng || Profile.Deck == JetFlightDeck.Boeing737Max || Profile.Deck == JetFlightDeck.Boeing787;
            var modern = Profile.Deck == JetFlightDeck.AirbusA350 || Profile.Deck == JetFlightDeck.AirbusA220 || Profile.Deck == JetFlightDeck.Boeing787;
            OverheadGroup("ELEC", -0.155f, 0.27f, 0.28f, 0.42f, boeing ? 3 : 2, modern);
            OverheadGroup("FUEL", 0.155f, 0.27f, 0.28f, 0.42f, boeing ? 4 : 3, modern);
            OverheadGroup("HYD", -0.155f, -0.13f, 0.28f, 0.32f, 2, modern);
            OverheadGroup("AIR", 0.155f, -0.13f, 0.28f, 0.32f, 3, modern);
            OverheadGroup("LIGHTS / ANTI ICE", 0f, -0.49f, 0.59f, 0.29f, boeing ? 5 : 4, false);

        }

        private void OverheadGroup(string legend, float x, float z, float width, float depth, int count, bool pushbuttons)
        {
            Box(legend + " overhead section", new Vector3(x, 0.602f, z), new Vector3(width, 0.010f, depth), _trim);
            var label = Label("Overhead legend", legend, new Vector3(x, 0.592f, z + depth * 0.31f), 0.0038f, Color.white);
            label.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            for (var i = 0; i < count; i++)
            {
                var controlX = x + (i - (count - 1) * 0.5f) * width / (count + 1);
                Box("Overhead switch mounting", new Vector3(controlX, 0.589f, z - depth * 0.12f), new Vector3(0.038f, 0.009f, 0.047f), _panel);
                if (pushbuttons)
                    Box("Overhead pushbutton", new Vector3(controlX, 0.58f, z - depth * 0.12f), new Vector3(0.026f, 0.014f, 0.033f), _trim);
                else
                    Beam("Overhead toggle", new Vector3(controlX, 0.587f, z - depth * 0.12f),
                        new Vector3(controlX, 0.558f, z - depth * 0.12f + 0.012f), 0.012f, _white);
            }
            if (depth > 0.30f)
                foreach (var side in new[] { -1f, 1f })
                    Box("Overhead rotary selector", new Vector3(x + side * width * 0.22f, 0.571f, z - depth * 0.34f),
                        new Vector3(0.038f, 0.03f, 0.038f), _panel);
        }

        public override void SetAttitude(float pitchUpDegrees, float bankLeftDegrees)
        {
            foreach (var horizon in _horizons) horizon?.Set(pitchUpDegrees, bankLeftDegrees);
        }

        public void SetFlightState(Transform aircraft, EngineState engines, string phase)
        {
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
