using Airside.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    public sealed class HudCrispnessTests
    {
        [Test]
        public void RoundedTexture_IsBakedAtDevicePixels()
        {
            var one = AirsideTheme.RoundedTexture(new Color(0.1f, 0.2f, 0.3f), Color.clear, 7);
            var enlarged = AirsideTheme.RoundedTexture(new Color(0.1f, 0.2f, 0.3f), Color.clear, 7, 2.25f);
            Assert.That(one.width, Is.EqualTo(7 * 2 + 4));
            Assert.That(enlarged.width, Is.EqualTo(16 * 2 + 4), "a 7 pt corner at 2.25x is a 16 px corner");
        }

        [Test]
        public void ButtonStyle_ScalesBorderAndBackgroundTogether()
        {
            var basis = new GUIStyle();
            var plain = AirsideTheme.ButtonStyle(basis);
            var enlarged = AirsideTheme.ButtonStyle(basis, null, 2f);
            Assert.That(plain.border.left, Is.EqualTo(7));
            Assert.That(enlarged.border.left, Is.EqualTo(14));
            Assert.That(enlarged.normal.background.width, Is.GreaterThan(plain.normal.background.width));
        }
    }
}
