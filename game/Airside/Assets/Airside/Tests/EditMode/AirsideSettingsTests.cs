using Airside.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    public sealed class AirsideSettingsTests
    {
        [Test]
        public void Defaults_MatchAPlayableAdelaideSession()
        {
            var settings = new AirsideSettings();
            Assert.That(settings.SoundOn, Is.True);
            Assert.That(settings.FieldTags, Is.True);
            Assert.That(settings.MiniMap, Is.True);
            Assert.That(settings.FollowOnSelect, Is.True);
            Assert.That(settings.InvertOrbit, Is.False);
            Assert.That(settings.LiveTraffic, Is.False,
                "live ADS-B stays off until the game can own the field");
            Assert.That(settings.LiveWeather, Is.True,
                "Adelaide presentation follows the fixed-location forecast and fails back offline");
            Assert.That(settings.CameraSpeedIndex, Is.EqualTo(1));
            Assert.That(settings.CameraSpeed, Is.EqualTo(0.65f));
        }

        [Test]
        public void CycleCameraSpeed_WalksSlowNormalFast()
        {
            var settings = new AirsideSettings();
            Assert.That(settings.CycleCameraSpeed().CameraSpeedIndex, Is.EqualTo(2));
            Assert.That(settings.CameraSpeed, Is.EqualTo(1f));
            Assert.That(settings.CycleCameraSpeed().CameraSpeedIndex, Is.EqualTo(0));
            Assert.That(settings.CameraSpeed, Is.EqualTo(0.4f));
            Assert.That(settings.CycleCameraSpeed().CameraSpeedIndex, Is.EqualTo(1));
            Assert.That(settings.CameraSpeed, Is.EqualTo(0.65f));
        }

        [Test]
        public void OptionsMenu_FitsThePauseMenuFlow()
        {
            Assert.That(HudLayout.OptionsHeight, Is.GreaterThan(HudLayout.MenuHeight));
            var layout = HudLayout.Create(1440f, 900f);
            Assert.That(layout.OptionsMenu.Contains(layout.PauseMenu.center), Is.True);
            Assert.That(layout.OptionsMenu.height, Is.GreaterThanOrEqualTo(400f));
        }
    }
}
