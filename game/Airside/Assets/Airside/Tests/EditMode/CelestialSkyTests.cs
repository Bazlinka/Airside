using System;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// Adelaide sun/moon: southern-hemisphere noon is north, the sun walks east → west,
    /// and world directions use the same 05/23 frame as the pavement.
    /// </summary>
    public sealed class CelestialSkyTests
    {
        // Solar noon at 138.531°E is ~02:46 UTC. Equinox 2026 is 23 September.
        private static readonly DateTime EquinoxNoon = Utc(2026, 9, 23, 2, 50);
        private static readonly DateTime EquinoxMorning = Utc(2026, 9, 23, 20, 50).AddDays(-1);
        private static readonly DateTime EquinoxEvening = Utc(2026, 9, 23, 8, 40);
        private static readonly DateTime JuneNoon = Utc(2026, 6, 21, 2, 50);
        private static readonly DateTime DecemberNoon = Utc(2026, 12, 21, 2, 50);

        [Test]
        public void EquinoxNoon_SitsDueNorthAtAdelaide()
        {
            var sky = CelestialSky.AtAdelaide(EquinoxNoon);
            Assert.That(sky.Sun.ElevationDegrees, Is.EqualTo(55.0).Within(3.0),
                "equinox noon elevation is 90 − |latitude|");
            Assert.That(Northish(sky.Sun.AzimuthDegrees), Is.True,
                $"noon must be north, not south; azimuth was {sky.Sun.AzimuthDegrees:0.0}");
            Assert.That(sky.Daylight, Is.EqualTo(1.0).Within(0.01));
        }

        [Test]
        public void EquinoxSun_RisesEastAndSetsWest()
        {
            var morning = CelestialSky.AtAdelaide(EquinoxMorning).Sun;
            var evening = CelestialSky.AtAdelaide(EquinoxEvening).Sun;

            Assert.That(morning.ElevationDegrees, Is.InRange(-2.0, 14.0), "just after sunrise");
            Assert.That(evening.ElevationDegrees, Is.InRange(-2.0, 14.0), "around sunset");
            morning.Horizontal(out var morningEast, out _, out _);
            evening.Horizontal(out var eveningEast, out _, out _);
            Assert.That(morningEast, Is.GreaterThan(0.35), "morning sun is in the east");
            Assert.That(eveningEast, Is.LessThan(-0.35), "evening sun is in the west");
        }

        [Test]
        public void Sun_WalksWestwardThroughTheDay()
        {
            var nine = CelestialSky.AtAdelaide(Utc(2026, 9, 23, 23, 30).AddDays(-1)).Sun;
            var noon = CelestialSky.AtAdelaide(EquinoxNoon).Sun;
            var three = CelestialSky.AtAdelaide(Utc(2026, 9, 23, 5, 30)).Sun;
            nine.Horizontal(out var nineEast, out var nineNorth, out _);
            noon.Horizontal(out var noonEast, out var noonNorth, out _);
            three.Horizontal(out var threeEast, out var threeNorth, out _);

            Assert.That(nineEast, Is.GreaterThan(noonEast), "late morning still east of the meridian");
            Assert.That(noonEast, Is.GreaterThan(threeEast), "afternoon has crossed to the west");
            Assert.That(noonNorth, Is.GreaterThan(nineNorth));
            Assert.That(noonNorth, Is.GreaterThan(threeNorth));
        }

        [Test]
        public void WinterNoon_IsLowerThanSummerNoon()
        {
            var winter = CelestialSky.AtAdelaide(JuneNoon).Sun;
            var summer = CelestialSky.AtAdelaide(DecemberNoon).Sun;
            Assert.That(winter.ElevationDegrees, Is.EqualTo(31.6).Within(3.0));
            Assert.That(summer.ElevationDegrees, Is.EqualTo(78.5).Within(3.0));
            Assert.That(Northish(winter.AzimuthDegrees), Is.True);
            Assert.That(Northish(summer.AzimuthDegrees), Is.True);
        }

        [Test]
        public void Moon_IsNotGluedOppositeTheSun()
        {
            var sky = CelestialSky.AtAdelaide(EquinoxNoon);
            var sun = sky.Sun;
            var moon = sky.Moon;
            var azimuthGap = Math.Abs(DeltaDegrees(sun.AzimuthDegrees, moon.AzimuthDegrees));
            var elevationGap = Math.Abs(sun.ElevationDegrees - moon.ElevationDegrees);
            Assert.That(azimuthGap > 20.0 || elevationGap > 15.0, Is.True,
                "a fake 180° yaw from the sun would put the moon opposite; a real moon is not");
            Assert.That(sky.MoonPhase, Is.InRange(0.0, 1.0));
            Assert.That(sky.MoonIllumination, Is.InRange(0.0, 1.0));
        }

        [Test]
        public void FullMoon_IsNearlyFullyLit()
        {
            // 26 September 2026 is a full moon.
            var sky = CelestialSky.AtAdelaide(Utc(2026, 9, 26, 16, 49));
            Assert.That(sky.MoonIllumination, Is.GreaterThan(0.85));
            Assert.That(sky.MoonPhase, Is.EqualTo(0.5).Within(0.12));
        }

        [Test]
        public void WorldFrame_NoonIsNorthOfTheRunway()
        {
            var noon = CelestialSky.AtAdelaide(EquinoxNoon).Sun;
            SkyDirection.ToWorld(noon, out var x, out var y, out var z);
            SkyDirection.ToWorld(0, 0, out var northX, out _, out var northZ);
            Assert.That(y, Is.GreaterThan(0.7), "high in the sky");
            var alongNorth = x * northX + z * northZ;
            Assert.That(alongNorth, Is.GreaterThan(0.5), "noon sits on the north side of 05/23");
        }

        [Test]
        public void WorldFrame_MorningIsEastOfEvening()
        {
            SkyDirection.ToWorld(CelestialSky.AtAdelaide(EquinoxMorning).Sun, out var mx, out _, out var mz);
            SkyDirection.ToWorld(CelestialSky.AtAdelaide(EquinoxEvening).Sun, out var ex, out _, out var ez);
            SkyDirection.ToWorld(90, 0, out var eastX, out _, out var eastZ);
            var morningEast = mx * eastX + mz * eastZ;
            var eveningEast = ex * eastX + ez * eastZ;
            Assert.That(morningEast, Is.GreaterThan(0.3));
            Assert.That(eveningEast, Is.LessThan(-0.3));
        }

        [Test]
        public void Daylight_RampsThroughCivilTwilight()
        {
            Assert.That(CelestialSky.DaylightFromSunElevation(-10), Is.EqualTo(0.0));
            Assert.That(CelestialSky.DaylightFromSunElevation(30), Is.EqualTo(1.0));
            Assert.That(CelestialSky.DaylightFromSunElevation(0), Is.GreaterThan(0.05).And.LessThan(0.6));
            Assert.That(CelestialSky.GoldenHour(6), Is.GreaterThan(0.4));
            Assert.That(CelestialSky.GoldenHour(45), Is.EqualTo(0.0));
        }

        private static DateTime Utc(int year, int month, int day, int hour, int minute) =>
            new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

        private static bool Northish(double azimuth) =>
            azimuth <= 25.0 || azimuth >= 335.0;

        private static double DeltaDegrees(double a, double b)
        {
            var d = Math.Abs(a - b) % 360.0;
            return d > 180.0 ? 360.0 - d : d;
        }
    }
}
