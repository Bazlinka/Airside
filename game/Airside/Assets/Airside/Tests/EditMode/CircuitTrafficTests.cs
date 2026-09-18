using System;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Visible holding / go-around racetrack south of runway 05.</summary>
    public sealed class CircuitTrafficTests
    {
        [Test]
        public void Holding_StaysAtCircuitHeightSouthOfTheRunway()
        {
            CircuitTraffic.Holding(0, 0, out var x, out var y, out var z);
            Assert.That(y, Is.EqualTo(CircuitTraffic.CircuitHeightMetres).Within(0.01));
            Assert.That(z, Is.LessThanOrEqualTo(0.01), "right-hand circuit: south of the strip, off the terminal");
            Assert.That(x, Is.InRange(CircuitTraffic.WestX - CircuitTraffic.TurnRadiusMetres - 1,
                CircuitTraffic.EastX + CircuitTraffic.TurnRadiusMetres + 1));
        }

        [Test]
        public void HoldingProgress_LoopsAndSpreadsQueueSlots()
        {
            Assert.That(CircuitTraffic.HoldingProgress(0, 0), Is.EqualTo(0).Within(1e-9));
            Assert.That(CircuitTraffic.HoldingProgress(CircuitTraffic.LoopSeconds, 0), Is.EqualTo(0).Within(1e-9));
            Assert.That(CircuitTraffic.HoldingProgress(CircuitTraffic.LoopSeconds * 0.5, 0), Is.EqualTo(0.5).Within(1e-9));

            var a = CircuitTraffic.HoldingProgress(90, 0);
            var b = CircuitTraffic.HoldingProgress(90, 1);
            var gap = Math.Abs(a - b);
            gap = Math.Min(gap, 1 - gap);
            Assert.That(gap, Is.EqualTo(CircuitTraffic.SlotSpacing).Within(1e-9));
        }

        [Test]
        public void TwoHolders_AreDrawnApart()
        {
            CircuitTraffic.Holding(30, 0, out var x0, out _, out var z0);
            CircuitTraffic.Holding(30, 1, out var x1, out _, out var z1);
            var gap = Math.Sqrt((x0 - x1) * (x0 - x1) + (z0 - z1) * (z0 - z1));
            Assert.That(gap, Is.GreaterThan(400), "queue slots must not stack on one spot in the circuit");
        }

        [Test]
        public void Lap_IsClosedAndContinuous()
        {
            CircuitTraffic.OnLap(0, CircuitTraffic.CircuitHeightMetres, out var x0, out _, out var z0);
            CircuitTraffic.OnLap(CircuitTraffic.LapMetres, CircuitTraffic.CircuitHeightMetres, out var x1, out _, out var z1);
            Assert.That(x1, Is.EqualTo(x0).Within(0.05));
            Assert.That(z1, Is.EqualTo(z0).Within(0.05));

            var previous = 0.0;
            CircuitTraffic.OnLap(0, CircuitTraffic.CircuitHeightMetres, out var px, out _, out var pz);
            const double step = 40;
            for (var d = step; d <= CircuitTraffic.LapMetres; d += step)
            {
                CircuitTraffic.OnLap(d, CircuitTraffic.CircuitHeightMetres, out var x, out _, out var z);
                var jump = Math.Sqrt((x - px) * (x - px) + (z - pz) * (z - pz));
                Assert.That(jump, Is.LessThan(step * 1.6), $"discontinuity at {d} m along the lap");
                previous += jump;
                px = x;
                pz = z;
            }

            Assert.That(previous, Is.EqualTo(CircuitTraffic.LapMetres).Within(CircuitTraffic.LapMetres * 0.08));
        }

        [Test]
        public void GoAround_StartsOnShortFinalAndClimbsToCircuitHeight()
        {
            CircuitTraffic.GoAround(0, out var x0, out var y0, out var z0);
            Assert.That(x0, Is.EqualTo(CircuitProfile.ShortFinalX).Within(1.0));
            Assert.That(z0, Is.EqualTo(0).Within(1.0));
            Assert.That(y0, Is.EqualTo(CircuitProfile.ShortFinalHeight).Within(1.0));
            Assert.That(y0, Is.LessThan(CircuitTraffic.CircuitHeightMetres - 50));

            CircuitTraffic.GoAround(CircuitTraffic.LoopSeconds * 0.4, out _, out var yClimb, out _);
            Assert.That(yClimb, Is.EqualTo(CircuitTraffic.CircuitHeightMetres).Within(0.5));

            CircuitTraffic.GoAround(CircuitTraffic.LoopSeconds, out var xEnd, out var yEnd, out var zEnd);
            CircuitTraffic.GoAround(0, out var xStart, out _, out var zStart);
            var close = Math.Sqrt((xEnd - xStart) * (xEnd - xStart) + (zEnd - zStart) * (zEnd - zStart));
            Assert.That(close, Is.LessThan(80), "a finished go-around hands back to the same circuit");
            Assert.That(yEnd, Is.EqualTo(CircuitTraffic.CircuitHeightMetres).Within(0.5));
        }
    }
}
