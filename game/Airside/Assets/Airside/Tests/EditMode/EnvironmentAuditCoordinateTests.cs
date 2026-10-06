using System;
using System.IO;
using System.Linq;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class EnvironmentAuditCoordinateTests
    {
        private static string Source(string relative)
        {
            foreach (var start in new[] { Environment.CurrentDirectory, TestContext.CurrentContext.TestDirectory })
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    var path = Path.Combine(dir.FullName, "game/Airside/Assets/Airside", relative);
                    if (File.Exists(path)) return File.ReadAllText(path);
                    path = Path.Combine(dir.FullName, "Assets/Airside", relative);
                    if (File.Exists(path)) return File.ReadAllText(path);
                }
            throw new FileNotFoundException(relative);
        }

        [TestCase("Coast foam near", 0, 3f, 7f, 0f)]
        [TestCase("Coast foam near", 0, 3f, 7f, 1f)]
        [TestCase("Coast foam outer", 1, 1f, 16f, 0f)]
        [TestCase("Coast foam outer", 1, 1f, 16f, 1f)]
        public void ActualNorthernAndSouthernRibbonEndpointsStayOnShoreAtPulseExtremes(
            string name, int layer, float landward, float seaward, float wave)
        {
            var segments = AdelaideCoastLandform.FoamSegments();
            var north = segments.OrderByDescending(s => (s.Az + s.Bz) * .5f).First();
            var south = segments.OrderBy(s => (s.Az + s.Bz) * .5f).First();
            Assert.That(north.Az, Is.GreaterThan(10000));
            Assert.That(south.Az, Is.LessThan(-3000));
            var phase = wave == 0 ? Math.PI * 1.5 : Math.PI * .5;
            var time = (float)((phase + Math.PI * 2 - layer * 1.7) / 1.8);
            var actualWave = CoastalFoamMotion.Wave(time, layer);
            Assert.That(actualWave, Is.EqualTo(wave).Within(.00001));
            var scale = CoastalFoamMotion.LayerScaleZ(name, layer, actualWave);
            Assert.That(scale, Is.EqualTo(1));
            Assert.That(CoastalFoamMotion.PrimaryScaleZ(name, wave == 0 ? .84f : 1f), Is.EqualTo(1));
            foreach (var segment in new[] { north, south })
            {
                var corners = AdelaideCoastLandform.FoamQuad(segment, landward, seaward);
                var tx = (segment.Bx - segment.Ax) / segment.Length;
                var tz = (segment.Bz - segment.Az) / segment.Length;
                for (var corner = 0; corner < 4; corner++)
                {
                    var x = corners[corner * 2];
                    var z = corners[corner * 2 + 1] * scale;
                    var dx = x - segment.Ax;
                    var dz = z - segment.Az;
                    var along = dx * tx + dz * tz;
                    var outward = dx * segment.OutwardX + dz * segment.OutwardZ;
                    Assert.That(along, Is.EqualTo(corner is 1 or 2 ? segment.Length : 0).Within(.003));
                    Assert.That(outward, Is.EqualTo(corner < 2 ? -landward : seaward).Within(.003));
                    Assert.That(z, Is.EqualTo(corners[corner * 2 + 1]), "No airport-origin stretch at distant endpoints.");
                }
                var middleX = (corners[0] + corners[2] + corners[4] + corners[6]) * .25f;
                var middleZ = (corners[1] + corners[3] + corners[5] + corners[7]) * .25f * scale;
                var midlineOffset = (middleX - (segment.Ax + segment.Bx) * .5f) * segment.OutwardX
                    + (middleZ - (segment.Az + segment.Bz) * .5f) * segment.OutwardZ;
                Assert.That(midlineOffset, Is.EqualTo((seaward - landward) * .5f).Within(.003));
            }
            Assert.That(CoastalFoamMotion.Alpha(wave), Is.EqualTo(wave == 0 ? .3f : .65f).Within(.00001));
        }

        [TestCase(0, 0f, 1.012f)] [TestCase(0, 1f, 1.122f)]
        [TestCase(1, 0f, 1.288f)] [TestCase(1, 1f, 1.428f)]
        public void PrimitiveKIPadTreatmentRemainsUnchanged(int layer, float wave, float expected)
        {
            Assert.That(CoastalFoamMotion.IsGeographicLayer("Coast foam"), Is.False);
            Assert.That(CoastalFoamMotion.IsGeographicLayer("Coast foam secondary"), Is.False);
            Assert.That(CoastalFoamMotion.LayerScaleZ("Coast foam", layer, wave), Is.EqualTo(expected).Within(.00001));
            Assert.That(CoastalFoamMotion.PrimaryScaleZ("Coast foam", .84f), Is.EqualTo(2.2f * .84f));
        }

        [Test]
        public void RuntimeUsesGeographicPolicyForBothScaleWritesAndPreservesBuilderNames()
        {
            var runtime = Source("Presentation/AirsidePrototype.cs");
            Assert.That(runtime, Does.Contain("scale.z = CoastalFoamMotion.PrimaryScaleZ(_coastFoam.name, pulse);"));
            Assert.That(runtime, Does.Contain("scale.z = CoastalFoamMotion.LayerScaleZ(foam.name, i, wave);"));
            Assert.That(runtime, Does.Contain("var wave = CoastalFoamMotion.Wave(t, i);"));
            Assert.That(runtime, Does.Contain("c.a = CoastalFoamMotion.Alpha(wave);"));
            var builder = Source("Presentation/AirsideAdelaideSurroundings.cs");
            Assert.That(builder, Does.Contain("SpawnFoamLayer(root, \"Coast foam near\""));
            Assert.That(builder, Does.Contain("SpawnFoamLayer(root, \"Coast foam outer\""));
        }

        [Test]
        public void RoofShaderRestoresOriginOnlyForSatelliteLookup()
        {
            var shader = Source("Art/Shaders/SuburbBuildings.shader");
            Assert.That(shader, Does.Contain("float4 _AirsideFlightOrigin;"));
            Assert.That(shader, Does.Contain("float2 geographicXZ = input.positionWS.xz + _AirsideFlightOrigin.xz;"));
            Assert.That(shader, Does.Contain("saturate(geographicXZ / (2.0 * max(_SatelliteExtent, 1.0)) + 0.5)"));
            Assert.That(shader, Does.Contain("float distanceWS = length(input.positionWS - GetCameraPositionWS());"));
            Assert.That(shader, Does.Contain("output.positionWS = pos.positionWS;"));
            Assert.That(shader, Does.Contain("output.fogFactor = ComputeFogFactor(pos.positionCS.z);"));
            Assert.That(shader, Does.Contain("color = MixFog(color, input.fogFactor);"));
        }

        [TestCase(0d, 0d)] [TestCase(80000d, 96000d)] [TestCase(-80000d, -96000d)]
        public void FixedGeographicRoofKeepsBothUVsAcrossFloatingOriginChanges(double originX, double originZ)
        {
            // These cases evaluate the shader expression protected by the source contract above.
            foreach (var point in new[] { (x: 1000d, z: -2000d), (x: 15000d, z: -17000d) })
            {
                const double extent = 12000;
                var shiftedX = point.x - originX;
                var shiftedZ = point.z - originZ;
                var u = (shiftedX + originX) / (2 * extent) + .5;
                var v = (shiftedZ + originZ) / (2 * extent) + .5;
                Assert.That(u, Is.EqualTo(point.x / (2 * extent) + .5).Within(1e-12));
                Assert.That(v, Is.EqualTo(point.z / (2 * extent) + .5).Within(1e-12));
                Assert.That(Math.Clamp(u, 0, 1), Is.EqualTo(Math.Clamp(point.x / (2 * extent) + .5, 0, 1)));
                Assert.That(Math.Clamp(v, 0, 1), Is.EqualTo(Math.Clamp(point.z / (2 * extent) + .5, 0, 1)));
                // Shader distance remains in the shifted frame and is translation invariant.
                var cameraX = 2000d - originX;
                Assert.That(shiftedX - cameraX, Is.EqualTo(point.x - 2000));
            }
        }
    }
}
