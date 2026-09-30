using Airside.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    /// <summary>The cargo livery colour (ADR 0194). Needs UnityEngine, so the headless harness skips it.</summary>
    public sealed class FreightPaintTests
    {
        [Test]
        public void CargoPaint_IsADeeperVersionOfTheOperatorColour()
        {
            var orange = new Color(0.82f, 0.29f, 0.12f);
            var cargo = AircraftLiveryPaint.FreightPrimary(orange);
            Color.RGBToHSV(orange, out var hue, out _, out var value);
            Color.RGBToHSV(cargo, out var cargoHue, out _, out var cargoValue);
            Assert.That(cargoHue, Is.EqualTo(hue).Within(0.02f));
            Assert.That(cargoValue, Is.LessThan(value * 0.6f));

            var grey = AircraftLiveryPaint.FreightPrimary(new Color(0.5f, 0.5f, 0.5f));
            Assert.That(grey.b, Is.GreaterThan(grey.r), "a colourless livery keeps a slate cargo paint");
        }
    }
}
