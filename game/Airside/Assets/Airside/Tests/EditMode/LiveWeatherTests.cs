using Airside.Simulation;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class LiveWeatherTests
    {
        private const string ClearSample = @"{
          ""latitude"":-34.90334,
          ""current_units"":{""temperature_2m"":""°C"",""wind_speed_10m"":""kn""},
          ""current"":{
            ""time"":""2026-09-22T14:30"",""interval"":900,
            ""temperature_2m"":17.2,""relative_humidity_2m"":51,
            ""precipitation"":0.00,""rain"":0.00,""showers"":0.00,
            ""weather_code"":0,""cloud_cover"":0,""visibility"":62000.00,
            ""wind_speed_10m"":5.8,""wind_direction_10m"":226,""wind_gusts_10m"":16.1
          }
        }";

        [Test]
        public void OpenMeteoCurrent_ParsesIntoPresentationSnapshot()
        {
            Assert.That(LiveWeather.TryParse(ClearSample, out var weather), Is.True);
            Assert.That(weather.Kind, Is.EqualTo(WeatherKind.Clear));
            Assert.That(weather.TemperatureCelsius, Is.EqualTo(17.2f).Within(0.01f));
            Assert.That(weather.HumidityPercent, Is.EqualTo(51f));
            Assert.That(weather.Wind.DirectionDegrees, Is.EqualTo(226));
            Assert.That(weather.Wind.Knots, Is.EqualTo(6));
            Assert.That(weather.Look.CloudCover, Is.LessThan(0.1f));
            Assert.That(weather.Look.Visibility, Is.GreaterThan(0.95f));
        }

        [TestCase(0, 0f, 0f, 62000f, WeatherKind.Clear)]
        [TestCase(2, 55f, 0f, 30000f, WeatherKind.Cloudy)]
        [TestCase(3, 96f, 0f, 15000f, WeatherKind.Overcast)]
        [TestCase(61, 92f, 0.8f, 9000f, WeatherKind.Rain)]
        [TestCase(45, 80f, 0f, 700f, WeatherKind.Fog)]
        [TestCase(95, 100f, 2.4f, 3500f, WeatherKind.Storm)]
        public void WmoWeather_MapsToAirsideKinds(int code, float cloudPercent, float rain,
            float visibility, WeatherKind expected)
        {
            Assert.That(LiveWeather.Classify(code, cloudPercent / 100f, rain, visibility), Is.EqualTo(expected));
        }

        [Test]
        public void ContinuousLook_PreservesDrizzleAndVisibility()
        {
            var drizzle = LiveWeather.LookFor(WeatherKind.Rain, 0.72f, 0.15f, 6500f);
            var heavy = LiveWeather.LookFor(WeatherKind.Rain, 0.98f, 2.5f, 1800f);
            Assert.That(drizzle.Precipitation, Is.GreaterThan(0f));
            Assert.That(heavy.Precipitation, Is.GreaterThan(drizzle.Precipitation));
            Assert.That(heavy.Wetness, Is.GreaterThan(drizzle.Wetness));
            Assert.That(heavy.Visibility, Is.LessThan(drizzle.Visibility));
        }

        [Test]
        public void Feed_IsFixedToAdelaideAndRequestsNoPlayerLocation()
        {
            var url = LiveWeather.RequestUrl();
            Assert.That(url, Does.Contain("latitude=-34.945"));
            Assert.That(url, Does.Contain("longitude=138.531"));
            Assert.That(url, Does.Contain("wind_speed_unit=kn"));
            Assert.That(url, Does.Not.Contain("apikey"));
        }

        [Test]
        public void Credit_NamesOpenMeteoOnlyWhileLiveForecastIsUsed()
        {
            Assert.That(MapAttribution.FieldCredit(true, true), Does.Not.Contain("Open-Meteo"));
            var live = MapAttribution.FieldCredit(true, true, usesLiveWeather: true);
            Assert.That(live, Does.Contain("OpenStreetMap"));
            Assert.That(live, Does.Contain("Open-Meteo (CC BY 4.0)"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("<html>offline</html>")]
        [TestCase(@"{""current"":{""weather_code"":0}}")]
        public void InvalidPayload_FailsSoft(string json)
        {
            Assert.That(LiveWeather.TryParse(json, out _), Is.False);
        }
    }
}
