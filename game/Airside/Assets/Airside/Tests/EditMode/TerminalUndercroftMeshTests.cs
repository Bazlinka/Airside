using System;
using System.Collections.Generic;
using System.Reflection;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    public sealed class TerminalUndercroftMeshTests
    {
        [Test]
        public void BaggageUndercroft_CutsARealVehiclePortalThroughTheAirsideShell()
        {
            var routeX = AdelaideServiceRoads.UndercroftSpur[0];
            Assert.That(AdelaideTerminalArchitecture.UndercroftPortalCentreX,
                Is.EqualTo(routeX).Within(0.01f));
            Assert.That(AdelaideTerminalArchitecture.UndercroftPortalHalfWidthMetres,
                Is.GreaterThan(4f), "the opening must clear the baggage tug, not just its centreline");
            Assert.That(AdelaideTerminalArchitecture.UndercroftPortalHeightMetres,
                Is.InRange(3f, 3.5f), "the vehicle must fit below the terminal glazing");

            var surfaceType = typeof(AirsidePrototype).GetNestedType("SurfaceMesh", BindingFlags.NonPublic);
            Assert.That(surfaceType, Is.Not.Null);
            var surface = Activator.CreateInstance(surfaceType, nonPublic: true);
            var addPrism = typeof(AirsidePrototype).GetMethod("AddPrism", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(addPrism, Is.Not.Null);
            addPrism.Invoke(null, new object[]
            {
                surface, AdelaideLayout.Terminals[0].Xz, 0f,
                AdelaideTerminalArchitecture.ShellHeightMetres, true
            });

            var vertices = (List<Vector3>)surfaceType.GetField("Vertices").GetValue(surface);
            var triangles = (List<int>)surfaceType.GetField("Triangles").GetValue(surface);
            bool FacadeCovers(float y)
            {
                for (var i = 0; i < triangles.Count; i += 3)
                {
                    var a = vertices[triangles[i]];
                    var b = vertices[triangles[i + 1]];
                    var c = vertices[triangles[i + 2]];
                    var wallZ = AdelaideTerminalArchitecture.AirsideWallZAt(routeX);
                    if (Mathf.Abs(a.z - wallZ) > 0.5f || Mathf.Abs(b.z - wallZ) > 0.5f
                        || Mathf.Abs(c.z - wallZ) > 0.5f)
                        continue;
                    if (TriangleCovers(routeX, y, a, b, c))
                        return true;
                }
                return false;
            }

            Assert.That(FacadeCovers(1.7f), Is.False,
                "no terminal wall triangle may cover the baggage vehicle's route");
            Assert.That(FacadeCovers(5f), Is.True,
                "the shell must remain above the opening rather than lose a whole wall bay");
        }

        private static bool TriangleCovers(float x, float y, Vector3 a, Vector3 b, Vector3 c)
        {
            float Cross(Vector3 p, Vector3 q) => (q.x - p.x) * (y - p.y) - (q.y - p.y) * (x - p.x);
            var area = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            if (Mathf.Abs(area) < 0.01f)
                return false;
            var ab = Cross(a, b);
            var bc = Cross(b, c);
            var ca = Cross(c, a);
            return (ab >= -0.001f && bc >= -0.001f && ca >= -0.001f)
                || (ab <= 0.001f && bc <= 0.001f && ca <= 0.001f);
        }

    }
}
