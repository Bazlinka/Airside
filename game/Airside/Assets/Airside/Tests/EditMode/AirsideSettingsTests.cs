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
            Assert.That(settings.UncappedFrameRate, Is.False,
                "high-refresh Macs cap at 60 fps unless the player opts into display max");
            Assert.That(settings.CameraSpeedIndex, Is.EqualTo(1));
            Assert.That(settings.CameraSpeed, Is.EqualTo(0.65f));
            // ADR 0155: graphics tests start on, so the game looks the same until one is turned off.
            Assert.That(settings.WeatherLayers, Is.True);
            Assert.That(settings.PropellerBlur, Is.True);
            Assert.That(settings.DistantGlows, Is.True);
            Assert.That(settings.AircraftLights, Is.True);
            Assert.That(settings.SuburbBuildings, Is.True, "ADR 0159: the suburbs are on unless switched off");
        }

        [Test]
        public void GraphicsOffFlag_SwitchesOnlyTheNamedEffectsOff()
        {
            var settings = new AirsideSettings();
            AirsideSettings.ApplyGraphicsOff(settings, new[] { "Airside", "-airsideGraphicsOff", "weather, LIGHTS" });
            Assert.That(settings.WeatherLayers, Is.False);
            Assert.That(settings.AircraftLights, Is.False);
            Assert.That(settings.PropellerBlur, Is.True);
            Assert.That(settings.DistantGlows, Is.True);
            Assert.That(settings.SuburbBuildings, Is.True);

            var suburbs = new AirsideSettings();
            AirsideSettings.ApplyGraphicsOff(suburbs, new[] { "-airsideGraphicsOff", "suburbs" });
            Assert.That(suburbs.SuburbBuildings, Is.False);
            Assert.That(suburbs.WeatherLayers, Is.True);

            var all = new AirsideSettings();
            AirsideSettings.ApplyGraphicsOff(all, new[] { "-airsideGraphicsOff", "all" });
            Assert.That(all.WeatherLayers || all.PropellerBlur || all.DistantGlows || all.AircraftLights || all.SuburbBuildings, Is.False);

            var none = new AirsideSettings();
            AirsideSettings.ApplyGraphicsOff(none, new[] { "-airsideGraphicsOff" });
            Assert.That(none.WeatherLayers && none.PropellerBlur && none.DistantGlows && none.AircraftLights, Is.True);
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
