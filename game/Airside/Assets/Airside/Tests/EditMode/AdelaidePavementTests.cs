using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;
using UnityEngine;
using Airside.Domain;

namespace Airside.Tests
{
    /// <summary>The Adelaide pavement follows the real OpenStreetMap layout (ADR 0045).</summary>
    public sealed class AdelaidePavementTests
    {
        private static void EachPoint(float[] xz, Action<float, float> check)
        {
            for (var i = 0; i + 1 < xz.Length; i += 2)
                check(xz[i], xz[i + 1]);
        }

        [Test]
        public void Pavement_MainStripMatchesBareFieldMetres()
        {
            Assert.That(AirsideAdelaidePavement.MainRunwayName, Is.EqualTo("Runway 05/23"));
            Assert.That(AirsideBareField.RunwayObjectName, Is.EqualTo("Runway 05/23"));
            Assert.That(AirsideAdelaidePavement.MainLengthMetres, Is.EqualTo(3100f));
            Assert.That(AirsideAdelaidePavement.MainWidthMetres, Is.EqualTo(45f));
            Assert.That(AdelaideLayout.MainRunwayLengthMetres, Is.EqualTo(3100f).Within(40f), "OSM agrees with the published length");
        }

        [Test]
        public void Pavement_CrossRunwayCrossesWhereItReallyDoes()
        {
            Assert.That(AirsideAdelaidePavement.CrossRunwayName, Is.EqualTo("Runway 12/30"));
            Assert.That(AirsideAdelaidePavement.CrossLengthMetres, Is.EqualTo(1652f));
            // OSM: 12/30 (123°) against 05/23 (050°) is 73°, crossing ~377 m north-east of the midpoint.
            Assert.That(AirsideAdelaidePavement.CrossYawDegrees, Is.EqualTo(73.2f).Within(1f));
            Assert.That(AirsideAdelaidePavement.ContainsCrossRunway(377f, 0f), Is.True);
            Assert.That(AirsideAdelaidePavement.ContainsCrossRunway(0f, 0f), Is.False, "12/30 does not cross at the midpoint");
            Assert.That(AirsideAdelaidePavement.ContainsCrossRunway(20f, 1180f), Is.True, "runway 12 end is far out on the terminal side");
            Assert.That(AirsideAdelaidePavement.ContainsCrossRunway(485f, -360f), Is.True, "runway 30 end is just across 05/23");
        }

        [Test]
        public void Layout_TerminalAndAprons_AreOnTheNorthEastHalfNorthWestSide()
        {
            var main = Array.Find(AdelaideLayout.Terminals, t => t.Name.Contains("Domestic"));
            Assert.That(main.Xz, Is.Not.Null);
            EachPoint(main.Xz, (x, z) =>
            {
                Assert.That(x, Is.GreaterThan(900f), "the terminal is on the 23 half");
                Assert.That(z, Is.GreaterThan(400f), "and north-west of the runway");
            });
        }

        [Test]
        public void Layout_Gate13IsARealPavedTerminalGate_NotARegionalBay()
        {
            var gate = Array.Find(AdelaideLayout.TerminalGates, g => g.Id == "GATE-13");
            Assert.That(gate.Reference, Is.EqualTo("13"));
            Assert.That(gate.NoseX, Is.EqualTo(1570.5f).Within(1f));
            Assert.That(gate.NoseZ, Is.EqualTo(409.7f).Within(1f));
            Assert.That(Math.Abs(gate.HeadingDegrees), Is.LessThan(2f), "Gate 13 faces north toward the terminal");
            Assert.That(AirsideAdelaidePavement.DistanceToPavement(gate.NoseX, gate.NoseZ), Is.LessThan(6f),
                "the gate nose stop must stand on the rendered apron");
            Assert.That(AirsideAdelaideGround.IsOperationallyFlat(gate.NoseX, gate.NoseZ), Is.True);
            foreach (var bay in AdelaideLayout.Bays)
                Assert.That(bay.Id, Is.Not.EqualTo(gate.Id), "a terminal gate must never resolve as a regional bay");
        }

        [Test]
        public void Layout_ApronsStayClearOfTheMainRunwayStrip()
        {
            foreach (var apron in AdelaideLayout.Aprons)
                EachPoint(apron.Xz, (x, z) =>
                    Assert.That(Math.Abs(z), Is.GreaterThan(AirsideAdelaidePavement.RunwayStripHalfWidthMetres), apron.Name));
        }

        [Test]
        public void Layout_RoutesStayOnPavementAndOnThePlateau()
        {
            void OnPavement(float[] route, string name) =>
                EachPoint(route, (x, z) =>
                {
                    Assert.That(AirsideAdelaidePavement.DistanceToPavement(x, z), Is.LessThan(6f),
                        $"{name} leaves the pavement at ({x:0},{z:0})");
                    Assert.That(AirsideAdelaideGround.IsOperationallyFlat(x, z), Is.True, $"{name} at ({x:0},{z:0}) is off the plateau");
                });

            OnPavement(AdelaideLayout.Vacate, "vacate");
            OnPavement(AdelaideLayout.Lineup, "lineup");
            foreach (var bay in AdelaideLayout.Bays)
            {
                OnPavement(bay.TaxiIn, $"taxi-in {bay.Id}");
                OnPavement(bay.Pushback, $"pushback {bay.Id}");
                OnPavement(bay.TaxiOut, $"taxi-out {bay.Id}");
            }
        }

        [Test]
        public void Layout_RoutesJoinUpEndToEnd()
        {
            float[] First(float[] r) => new[] { r[0], r[1] };
            float[] Last(float[] r) => new[] { r[r.Length - 2], r[r.Length - 1] };
            void Near(float[] a, float[] b, string what) =>
                Assert.That(Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1])), Is.LessThan(1.5), what);

            Near(First(AdelaideLayout.Vacate), new[] { AdelaideLayout.RolloutEndX, 0f }, "vacate starts where the landing rolls out");
            Near(Last(AdelaideLayout.Vacate), AdelaideLayout.E2Hold, "vacate ends at the E2 holding point");
            Near(First(AdelaideLayout.Lineup), AdelaideLayout.Runway05Hold, "lineup starts at the 05 holding point");
            Near(Last(AdelaideLayout.Lineup), new[] { AdelaideLayout.TakeoffStartX, 0f }, "lineup ends at the takeoff start");
            foreach (var bay in AdelaideLayout.Bays)
            {
                Near(First(bay.TaxiIn), AdelaideLayout.E2Hold, $"{bay.Id} taxi-in starts at E2");
                Near(Last(bay.TaxiIn), new[] { bay.StopX, bay.StopZ }, $"{bay.Id} taxi-in ends on the stand");
                Near(First(bay.Pushback), new[] { bay.StopX, bay.StopZ }, $"{bay.Id} pushback starts on the stand");
                Near(Last(bay.Pushback), First(bay.TaxiOut), $"{bay.Id} taxi-out starts where the pushback ends");
                Near(Last(bay.TaxiOut), AdelaideLayout.Runway05Hold, $"{bay.Id} taxi-out ends at the 05 holding point");
            }
        }

        /// <summary>
        /// Parked plan-view outline of a regional type as the game draws it: its runtime model's
        /// root sits on the bay stop facing the bay heading (regional kits are centred on the
        /// airframe, not on the nose), with wing, fuselage and tailplane boxes measured from the
        /// runtime glTF. Every regional type (ATR, Saab 340B, Dash 8-400) uses its own model.
        /// </summary>
        private static List<Vector2[]> ParkedOutline(string modelPath, AdelaideBay bay)
        {
            var json = File.ReadAllText(ArtRuntimePaths.ResolveExisting(modelPath));
            var boxes = new List<(Vector3 min, Vector3 max)>();
            foreach (var parts in new[] { new[] { "wing_left", "wing_right" }, new[] { "fuselage" }, new[] { "tailplane" } })
            {
                var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                foreach (var part in parts)
                {
                    Assert.That(AircraftModelBounds.TryMeasurePart(json, part, out var pMin, out var pMax), Is.True, $"{modelPath} {part}");
                    min = Vector3.Min(min, pMin);
                    max = Vector3.Max(max, pMax);
                }

                boxes.Add((min, max));
            }

            var h = bay.HeadingDegrees * Mathf.Deg2Rad;
            var forward = new Vector2(Mathf.Sin(h), Mathf.Cos(h));
            var right = new Vector2(forward.y, -forward.x);
            var stop = new Vector2(bay.StopX, bay.StopZ);
            return boxes.Select(box => new[]
            {
                stop + right * box.min.x + forward * box.min.z,
                stop + right * box.max.x + forward * box.min.z,
                stop + right * box.max.x + forward * box.max.z,
                stop + right * box.min.x + forward * box.max.z
            }).ToList();
        }

        private static float OutlineGap(List<Vector2[]> a, List<Vector2[]> b)
        {
            float PointToSegment(Vector2 p, Vector2 s0, Vector2 s1)
            {
                var d = s1 - s0;
                var t = Mathf.Clamp01(Vector2.Dot(p - s0, d) / d.sqrMagnitude);
                return Vector2.Distance(p, s0 + d * t);
            }

            var best = float.MaxValue;
            foreach (var pa in a)
            foreach (var pb in b)
                for (var i = 0; i < 4; i++)
                for (var k = 0; k < 4; k++)
                {
                    best = Mathf.Min(best, PointToSegment(pa[i], pb[k], pb[(k + 1) % 4]));
                    best = Mathf.Min(best, PointToSegment(pb[i], pa[k], pa[(k + 1) % 4]));
                }

            return best;
        }

        [Test]
        public void Layout_ParkedRegionalAircraftKeepCodeCClearance()
        {
            // ICAO code C stand clearance. The previous version of this test assumed the stop was
            // the ATR's nose and only used the ATR span, reporting 16.7 m where the drawn gap at
            // 50D/50E is 5.7 m, and could not see the larger Dash 8-400 at all.
            const float codeC = 4.5f;
            var atr = AircraftCatalogue.Atr42.RuntimeModelPath;
            var dash8 = AircraftCatalogue.Dash8Q400.RuntimeModelPath;
            var types = new (string name, string model)[]
            {
                ("ATR 42-600", atr),
                ("Saab 340B", AircraftCatalogue.Saab340.RuntimeModelPath),
                ("Dash 8-400", dash8)
            };

            // Only one Dash 8-400 flies here, so two of them are never side by side.
            var ops = AirlineOperations.StartAtAdelaide(new ManualSimulationClock(new SimulationTime(0)), new SeededRandomSource(1),
                Airline.Player("Clearance Air", "#1F3A93"));
            Assert.That(ops.Fleet.Count(a => a.Type == AircraftType.Dash8Q400), Is.EqualTo(1),
                "a second Dash 8-400 needs a stand-assignment rule for 50D/50E first (1.2 m apart there)");

            var shortfalls = new List<string>();
            var bays = AdelaideLayout.Bays;
            for (var i = 0; i < bays.Length; i++)
            for (var j = i + 1; j < bays.Length; j++)
                foreach (var ta in types)
                foreach (var tb in types)
                {
                    if (ta.model == dash8 && tb.model == dash8)
                        continue;
                    var gap = OutlineGap(ParkedOutline(ta.model, bays[i]), ParkedOutline(tb.model, bays[j]));
                    var pair = $"{ta.name} on {bays[i].Reference} / {tb.name} on {bays[j].Reference}: {gap:0.0} m";
                    Assert.That(gap, Is.GreaterThan(1f), $"parked aircraft overlap or touch — {pair}");
                    if (gap < codeC)
                        shortfalls.Add($"{bays[i].Reference}-{bays[j].Reference}");
                    if (ta.model != dash8 && tb.model != dash8)
                        Assert.That(gap, Is.GreaterThanOrEqualTo(codeC), pair);
                }

            // Known, documented limit (GAME.md): a Dash 8-400 on 50D or 50E next to a turboprop on the
            // other is 3.3–3.5 m apart. Any other pair falling short of code C fails here.
            Assert.That(shortfalls.Distinct(), Is.EquivalentTo(new[] { "50D-50E" }));
        }

        [Test]
        public void StripMarkings_CrossPaintStaysOnLocalPavement()
        {
            var halfL = AirsideAdelaidePavement.CrossHalfLength;
            var halfW = AirsideAdelaidePavement.CrossHalfWidth;
            var marks = AirsideStripMarkings.CrossRunwayAll();
            Assert.That(marks.Length, Is.GreaterThan(20));
            foreach (var mark in marks)
            {
                Assert.That(Math.Abs(mark.MinX), Is.LessThanOrEqualTo(halfL + 0.01f), mark.MinX.ToString());
                Assert.That(Math.Abs(mark.MaxX), Is.LessThanOrEqualTo(halfL + 0.01f));
                Assert.That(Math.Abs(mark.MinZ), Is.LessThanOrEqualTo(halfW + 0.01f));
                Assert.That(Math.Abs(mark.MaxZ), Is.LessThanOrEqualTo(halfW + 0.01f));
            }
        }

        [Test]
        public void StripMarkings_TaxiGuideIsSolidCentrelineAndDoubleEdges()
        {
            var length = 3000f;
            var width = 23f;
            var marks = AirsideStripMarkings.TaxiwayGuide(length, width);

            // 4 edge lines (double each side) + 1 continuous centreline.
            Assert.That(marks.Length, Is.EqualTo(5));

            var centre = Array.FindAll(marks, m => Math.Abs(m.CenterZ) < 0.01f);
            Assert.That(centre.Length, Is.EqualTo(1),
                "a taxiway centreline is one continuous line, not runway dashes");
            Assert.That(centre[0].LengthX, Is.EqualTo(length).Within(0.01f),
                "taxiway centreline must run the full length");

            foreach (var m in marks)
            {
                Assert.That(Math.Abs(m.MaxZ), Is.LessThanOrEqualTo(width * 0.5f + 0.01f),
                    "taxi paint must stay on the taxiway");
            }
        }

        [Test]
        public void StripMarkings_HoldShortIsIcaoPatternA()
        {
            var from = AirsideAdelaidePavement.HoldShortFromRunwayEdgeMetres;

            // ICAO pattern A is four bars across the taxiway.
            var lanes = AirsideStripMarkings.HoldShortBarLanes(23f, from);
            Assert.That(lanes.Length, Is.EqualTo(4));

            var hold = AirsideStripMarkings.HoldShortBars(23f, from);
            Assert.That(hold.Length, Is.GreaterThan(4),
                "the two far bars are dashed, so they break into segments");

            // Two of the four lanes are solid, and they are the pair nearest the runway.
            var solid = Array.FindAll(hold, m => m.LengthX > 20f);
            Assert.That(solid.Length, Is.EqualTo(2), "pattern A has two solid bars");
            foreach (var s in solid)
            {
                Assert.That(s.CenterZ, Is.LessThan(from),
                    "solid bars sit on the runway side of the pattern");
            }

            foreach (var m in hold)
            {
                Assert.That(m.LengthX, Is.LessThanOrEqualTo(23f),
                    "hold bars must not overhang the taxiway");
            }
        }

        [Test]
        public void Perimeter_MatchesPublishedSiteRectangleAndGatePattern()
        {
            Assert.That(AirsideAdelaidePerimeter.HalfX, Is.EqualTo(AirsideBareField.GroundLengthMetres * 0.5f));
            Assert.That(AirsideAdelaidePerimeter.HalfZ, Is.EqualTo(AirsideBareField.GroundWidthMetres * 0.5f));
            Assert.That(AirsideAdelaidePerimeter.FenceHeightMetres, Is.EqualTo(2.44f));
            Assert.That(AirsideAdelaidePerimeter.VehicleGates.Length, Is.EqualTo(3));
            Assert.That(AirsideAdelaidePerimeter.IsInsideFence(0f, 0f), Is.True);
            Assert.That(AirsideAdelaidePerimeter.IsInsideFence(5000f, 0f), Is.False);
            Assert.That(AirsideAdelaidePerimeter.IsInGateGap("N", 420f), Is.True);
            Assert.That(AirsideAdelaidePerimeter.IsInGateGap("N", 0f), Is.False);
            Assert.That(AirsideAdelaidePerimeter.PerimeterLengthMetres, Is.GreaterThan(10000f));
        }

        [Test]
        public void Perimeter_FenceFollowsTheGroundItStandsOn()
        {
            // The bug this guards: every panel was pinned to world Y = 0 while the
            // authored ground drops ~2.4 m into the boundary lip at the fence line,
            // leaving the whole 11 km fence hanging in the air.
            var hx = AirsideAdelaidePerimeter.FenceHalfX;
            var hz = AirsideAdelaidePerimeter.FenceHalfZ;

            var groundAtFence = AirsideAdelaideGround.WorldHeight(0f, hz);
            Assert.That(groundAtFence, Is.LessThan(-1f),
                "sanity: the authored boundary lip really does drop at the fence line");

            foreach (var x in new[] { -hx * 0.9f, 0f, hx * 0.9f })
            {
                foreach (var z in new[] { -hz, hz })
                {
                    var baseY = AirsideAdelaidePerimeter.FenceBaseY(x, z);
                    var ground = AirsideAdelaideGround.WorldHeight(x, z);
                    Assert.That(baseY, Is.LessThanOrEqualTo(ground + 0.01f),
                        $"fence base at ({x:0},{z:0}) must not float above the ground");
                    Assert.That(baseY, Is.GreaterThanOrEqualTo(ground - 1f),
                        $"fence base at ({x:0},{z:0}) must not be buried");
                }
            }
        }

        [Test]
        public void Perimeter_FenceBaseTracksTerrainNotAFixedHeight()
        {
            var hz = AirsideAdelaidePerimeter.FenceHalfZ;
            var a = AirsideAdelaidePerimeter.FenceBaseY(-1000f, hz);
            var b = AirsideAdelaidePerimeter.FenceBaseY(1000f, hz);
            Assert.That(Math.Abs(a - b), Is.GreaterThan(0.01f),
                "fence base must vary with the landform, not sit on one constant");
        }
    }
}
