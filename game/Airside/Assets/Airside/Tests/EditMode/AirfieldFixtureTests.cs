using System;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0124 — domed airfield fixtures and how each lens group answers daylight.</summary>
    public sealed class AirfieldFixtureTests
    {
        private static void AssertFacesMatchNormals(AirfieldFixture.Geometry g, string label)
        {
            for (var t = 0; t < g.Triangles.Count; t += 3)
            {
                float P(int v, int k) => g.Positions[g.Triangles[t + v] * 3 + k];
                var e1 = new[] { P(1, 0) - P(0, 0), P(1, 1) - P(0, 1), P(1, 2) - P(0, 2) };
                var e2 = new[] { P(2, 0) - P(0, 0), P(2, 1) - P(0, 1), P(2, 2) - P(0, 2) };
                var cross = new[] { e1[1] * e2[2] - e1[2] * e2[1], e1[2] * e2[0] - e1[0] * e2[2], e1[0] * e2[1] - e1[1] * e2[0] };
                var n = g.Triangles[t] * 3;
                Assert.That(cross[0] * g.Normals[n] + cross[1] * g.Normals[n + 1] + cross[2] * g.Normals[n + 2],
                    Is.GreaterThan(0f), $"{label} triangle {t / 3} faces inward");
            }
        }

        [Test]
        public void Fixture_IsAUnitBaseAndADomeFacingOutward()
        {
            var body = AirfieldFixture.Base();
            var lens = AirfieldFixture.Lens();
            AssertFacesMatchNormals(body, "base");
            AssertFacesMatchNormals(lens, "lens");
            for (var i = 0; i < body.VertexCount; i++)
            {
                Assert.That(body.Positions[i * 3 + 1], Is.InRange(0f, AirfieldFixture.BaseFraction + 1e-5f));
                Assert.That(Math.Abs(body.Positions[i * 3]), Is.LessThanOrEqualTo(0.5f));
            }

            var top = 0f;
            for (var i = 0; i < lens.VertexCount; i++)
            {
                top = Math.Max(top, lens.Positions[i * 3 + 1]);
                Assert.That(lens.Normals[i * 3 + 1], Is.GreaterThan(0f), "every lens normal leans up");
            }

            Assert.That(top, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(body.Triangles.Count / 3 + lens.Triangles.Count / 3, Is.LessThan(60), "stays low-poly");
        }

        [Test]
        public void LensGroups_DimByDayButApproachLightsStayReadable()
        {
            foreach (LensDayResponse response in Enum.GetValues(typeof(LensDayResponse)))
            {
                Assert.That(AirfieldFixture.LensEmission(response, 1f), Is.GreaterThan(AirfieldFixture.LensEmission(response, 0f)));
                Assert.That(AirfieldFixture.LensEmission(response, 0f), Is.LessThan(0.6f), response.ToString());
            }

            Assert.That(AirfieldFixture.LensEmission(LensDayResponse.Approach, 0f),
                Is.GreaterThan(AirfieldFixture.LensEmission(LensDayResponse.Edge, 0f)));
            Assert.That(AirfieldFixture.HaloStrength(0f), Is.EqualTo(0f), "no halos by day");
            Assert.That(AirfieldFixture.HaloStrength(1f), Is.EqualTo(1f));
        }

        [Test]
        public void LensNames_MapToTheirResponse()
        {
            // Amber caution-zone edges are runway edge lenses: they keep their own colour now.
            Assert.That(AirfieldFixture.ResponseFor("Runway edge 05/23 N 03"), Is.EqualTo(LensDayResponse.Edge));
            Assert.That(AirfieldFixture.ResponseFor("PAPI 23 unit 1"), Is.EqualTo(LensDayResponse.Approach));
            Assert.That(AirfieldFixture.ResponseFor("Threshold light 05/23 1 3"), Is.EqualTo(LensDayResponse.Approach));
            Assert.That(AirfieldFixture.ResponseFor("ALS 23 centre 04"), Is.EqualTo(LensDayResponse.Approach));
            Assert.That(AirfieldFixture.ResponseFor("Runway guard 02 L"), Is.EqualTo(LensDayResponse.Guard));
            Assert.That(AirfieldFixture.ResponseFor("Stand marker 51"), Is.EqualTo(LensDayResponse.Stand));
            Assert.That(AirfieldFixture.ResponseFor("Stopbar 01 03"), Is.EqualTo(LensDayResponse.Guidance));
            Assert.That(AirfieldFixture.ResponseFor("Taxi edge blue 010 L"), Is.EqualTo(LensDayResponse.Guidance));
        }
        [Test]
        public void LightPoints_StayVisibleFarOutAndThroughHaze()
        {
            foreach (LensDayResponse response in Enum.GetValues(typeof(LensDayResponse)))
            {
                Assert.That(AirfieldFixture.PointMinPixels(response), Is.GreaterThanOrEqualTo(2f), response.ToString());
                Assert.That(AirfieldFixture.PointStrength(response, 1f), Is.GreaterThan(1f), "HDR at night: " + response);
            }

            // Runway edge, taxi and stand points are dark by day; approach and guard lights still show.
            Assert.That(AirfieldFixture.PointStrength(LensDayResponse.Edge, 0f), Is.Zero);
            Assert.That(AirfieldFixture.PointStrength(LensDayResponse.Stand, 0f), Is.Zero);
            Assert.That(AirfieldFixture.PointStrength(LensDayResponse.Approach, 0f), Is.GreaterThan(0f));
            Assert.That(AirfieldFixture.PointMinPixels(LensDayResponse.Approach),
                Is.GreaterThan(AirfieldFixture.PointMinPixels(LensDayResponse.Guidance)));

            // Dims with distance but a far runway still reads, and never brighter far than near.
            Assert.That(AirfieldFixture.PointDistanceFactor(0f), Is.EqualTo(1f));
            Assert.That(AirfieldFixture.PointDistanceFactor(AirfieldFixture.PointHalfBrightnessMetres), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(AirfieldFixture.PointDistanceFactor(30000f), Is.EqualTo(AirfieldFixture.PointDistanceFloor));
            Assert.That(AirfieldFixture.PointDistanceFactor(3000f), Is.LessThan(AirfieldFixture.PointDistanceFactor(1000f)));

            // Where fog leaves a surface 10 % visible, a light is still about half as bright.
            Assert.That(AirfieldFixture.PointHaze(0.1f), Is.GreaterThan(0.4f));
            Assert.That(AirfieldFixture.PointHaze(1f), Is.EqualTo(1f));
            Assert.That(AirfieldFixture.PointHaze(0f), Is.Zero);
            Assert.That(AirfieldFixture.PointWorldSize(0.34f), Is.LessThan(1.5f), "a lamp close up, not a blob");
        }

        [Test]
        public void GuardLights_AlternateTheirPair()
        {
            Assert.That(AirfieldFixture.FlashPhase("Runway edge 05/23 N 03"), Is.EqualTo(-1f));
            var left = AirfieldFixture.FlashPhase("Runway guard 02 L");
            var right = AirfieldFixture.FlashPhase("Runway guard 02 R");
            Assert.That(left, Is.Not.EqualTo(right));
            var together = 0;
            var lit = 0;
            for (var i = 0; i < 1000; i++)
            {
                var t = i * 0.01f;
                var l = AirfieldFixture.FlashOn(left, t);
                var r = AirfieldFixture.FlashOn(right, t);
                if (l && r)
                    together++;
                if (l)
                    lit++;
            }

            Assert.That(together, Is.Zero, "the pair never shows together");
            Assert.That(lit, Is.InRange(450, 550), "each lamp lit half the time");
            Assert.That(AirfieldFixture.GuardFlashHz * 60f, Is.InRange(30f, 60f), "ICAO 30-60 flashes a minute");
            Assert.That(AirfieldFixture.FlashOn(-1f, 12.3f), Is.True);
        }
        [Test]
        public void WetRunway_ReflectsTheLightsOnlyWhenItIsWetAndDark()
        {
            // ADR 0169: soaked at night, a long streak; damp, dry or daytime, none.
            Assert.That(AirfieldFixture.ReflectionStrength(1f, 1f), Is.EqualTo(1f));
            Assert.That(AirfieldFixture.ReflectionStrength(0.14f, 1f), Is.LessThan(0.05f),
                "clear-weather residual damp does not mirror the runway");
            Assert.That(AirfieldFixture.ReflectionStrength(0f, 1f), Is.Zero);
            Assert.That(AirfieldFixture.ReflectionStrength(1f, 0f), Is.Zero, "not by day");
            Assert.That(AirfieldFixture.ReflectionStrength(0.4f, 1f),
                Is.InRange(0.05f, AirfieldFixture.ReflectionStrength(0.8f, 1f)), "grows with the rain");
            Assert.That(AirfieldFixture.ReflectionGain, Is.LessThan(1f), "fainter than the light itself");
            Assert.That(AirfieldFixture.ReflectionStretch, Is.GreaterThan(4f), "a streak, not a second dot");
        }
    }
}
