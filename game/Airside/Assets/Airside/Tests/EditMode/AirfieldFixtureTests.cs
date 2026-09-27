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
    }
}
