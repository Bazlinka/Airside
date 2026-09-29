using System.Linq;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AdelaideBoundaryTests
    {
        private static bool Inside(float[] poly, float x, float z)
        {
            var inside = false;
            var n = poly.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var xi = poly[i * 2]; var zi = poly[i * 2 + 1]; var xj = poly[j * 2]; var zj = poly[j * 2 + 1];
                if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi)
                    inside = !inside;
            }

            return inside;
        }

        private static double DistanceTo(float[] poly, float x, float z)
        {
            var best = double.MaxValue;
            var n = poly.Length / 2;
            for (var i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                double ax = poly[i * 2], az = poly[i * 2 + 1], dx = poly[j * 2] - ax, dz = poly[j * 2 + 1] - az;
                var len2 = dx * dx + dz * dz;
                var t = len2 < 1e-9 ? 0.0 : System.Math.Max(0.0, System.Math.Min(1.0, ((x - ax) * dx + (z - az) * dz) / len2));
                best = System.Math.Min(best, System.Math.Sqrt((x - ax - dx * t) * (x - ax - dx * t) + (z - az - dz * t) * (z - az - dz * t)));
            }

            return best;
        }

        [Test]
        public void EveryRunwayAndTerminalIsInsideTheBoundary()
        {
            foreach (var t in AdelaideLayout.Terminals)
                for (var i = 0; i + 1 < t.Xz.Length; i += 2)
                    // The real fence runs along the terminal's landside wall, so its corners sit on the line.
                    Assert.That(Inside(AdelaideBoundary.Outline, t.Xz[i], t.Xz[i + 1])
                                || DistanceTo(AdelaideBoundary.Outline, t.Xz[i], t.Xz[i + 1]) < 5.0,
                        $"terminal point {t.Xz[i]:0},{t.Xz[i + 1]:0}");
            foreach (var a in AdelaideLayout.Aprons)
                for (var i = 0; i + 1 < a.Xz.Length; i += 2)
                    Assert.That(Inside(AdelaideBoundary.Outline, a.Xz[i], a.Xz[i + 1]), $"apron point {a.Xz[i]:0},{a.Xz[i + 1]:0}");
            // Both ends of the main runway (05/23 threshold midpoint is the origin, ±1.55 km).
            Assert.That(Inside(AdelaideBoundary.Outline, -1550f, 0f));
            Assert.That(Inside(AdelaideBoundary.Outline, 1550f, 0f));
        }

        [Test]
        public void OutlineIsARealAirportPerimeter()
        {
            Assert.That(AdelaideBoundary.Outline.Length / 2, Is.InRange(50, 400));
            Assert.That(AdelaideBoundary.PerimeterMetres, Is.InRange(8000f, 25000f));
            Assert.That(AdelaideBoundary.Gates.Length, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void FenceRunsFollowThePerimeterAndOpenAtTheGates()
        {
            var runs = AdelaideBoundaryFence.Runs();
            Assert.That(runs.Length, Is.GreaterThan(200));
            Assert.That(runs.All(r => r.Length <= AdelaideBoundaryFence.PostSpacingMetres + 0.5f));
            var total = runs.Sum(r => r.Length);
            var gates = AdelaideBoundary.Gates.Length / 2;
            var expected = AdelaideBoundary.PerimeterMetres - gates * AdelaideBoundaryFence.GateGapMetres;
            Assert.That(total, Is.EqualTo(expected).Within(AdelaideBoundary.PerimeterMetres * 0.02f));
            for (var g = 0; g < AdelaideBoundary.Gates.Length; g += 2)
            {
                var gx = AdelaideBoundary.Gates[g];
                var gz = AdelaideBoundary.Gates[g + 1];
                Assert.That(runs.All(r => !Covers(r, gx, gz)), $"fence closes gate {g / 2}");
            }
        }

        private static bool Covers(FenceRun r, float x, float z)
        {
            var dx = r.X1 - r.X0; var dz = r.Z1 - r.Z0;
            var len2 = dx * dx + dz * dz;
            var t = ((x - r.X0) * dx + (z - r.Z0) * dz) / len2;
            if (t <= 0.02f || t >= 0.98f) return false;
            var px = r.X0 + dx * t; var pz = r.Z0 + dz * t;
            return (px - x) * (px - x) + (pz - z) * (pz - z) < 4f;
        }
    }
}
