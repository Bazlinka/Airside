using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AdelaideLandsideTests
    {
        [Test]
        public void Precinct_SitsNorthOfTheT1FacadeNotOnTheApron()
        {
            Assert.That(AdelaideLandside.Contains(1270f, 508f), Is.True, "drop-off is landside");
            Assert.That(AdelaideLandside.Contains(1270f, 700f), Is.True, "short-stay pad is landside");
            Assert.That(AdelaideLandside.Contains(1225f, 425f), Is.False, "GATE-21 nose is airside");
            Assert.That(AdelaideLandside.Contains(0f, 0f), Is.False, "threshold is not landside");
            Assert.That(AdelaideLandCover.InOperationalCore(1270f, 508f), Is.True,
                "the drop-off is inside the core the OSM roads used to skip");
        }

        [Test]
        public void DropOff_RunsAlongTheTerminalNorthFace()
        {
            Assert.That(AdelaideLandside.DropOff[1], Is.InRange(490f, 530f));
            Assert.That(AdelaideLandside.DropOff[0], Is.LessThan(AdelaideLandside.DropOff[2]));
            Assert.That(AdelaideLandside.DropOff[0], Is.GreaterThan(800f));
            Assert.That(AdelaideLandside.DropOff[2], Is.LessThan(1700f));
            foreach (var (width, xz) in AdelaideLandside.Ribbons)
            {
                Assert.That(width, Is.GreaterThanOrEqualTo(8f));
                Assert.That(xz.Length, Is.GreaterThanOrEqualTo(4));
                Assert.That(xz.Length % 2, Is.EqualTo(0));
            }
        }

        [Test]
        public void CarsAndLights_StayInsideThePrecinct()
        {
            var cars = AdelaideLandside.CarParkSlots();
            Assert.That(cars.Length, Is.GreaterThanOrEqualTo(20));
            foreach (var slot in cars)
                Assert.That(AdelaideLandside.Contains(slot.X, slot.Z), Is.True, $"{slot.X},{slot.Z}");

            var lamps = AdelaideLandside.Streetlights();
            Assert.That(lamps.Length, Is.GreaterThanOrEqualTo(8));
            foreach (var lamp in lamps)
                Assert.That(AdelaideLandside.Contains(lamp.X, lamp.Z), Is.True, $"{lamp.X},{lamp.Z}");
        }

        [Test]
        public void OsmRoads_AlreadyCrossTheT1TrafficSide()
        {
            Assert.That(AdelaideLandside.CountOsmRoadPointsInPrecinct(), Is.GreaterThan(20),
                "OSM already has Sir Richard Williams / car-park roads here; they just were not drawn");
        }
    }
}
