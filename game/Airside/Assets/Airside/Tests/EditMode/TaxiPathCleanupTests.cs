using System;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class TaxiPathCleanupTests
    {
        [Test]
        public void WithoutInitialHook_DropsTheBay3TurnoutIntoTheNeighbour()
        {
            var raw = Array.Find(AdelaideLayout.Bays, b => b.Id == "BAY-3").TaxiOut;
            var cleaned = TaxiPathCleanup.WithoutInitialHook(raw);
            Assert.That(cleaned.Length, Is.LessThan(raw.Length));
            Assert.That(cleaned[0], Is.EqualTo(raw[0]));
            Assert.That(cleaned[1], Is.EqualTo(raw[1]));
            Assert.That(cleaned[cleaned.Length - 2], Is.EqualTo(raw[raw.Length - 2]));

            var startX = cleaned[0];
            var startZ = cleaned[1];
            var endX = cleaned[cleaned.Length - 2];
            var endZ = cleaned[cleaned.Length - 1];
            var vx = endX - startX;
            var vz = endZ - startZ;
            for (var i = 2; i + 1 < Math.Min(cleaned.Length, 20); i += 2)
            {
                var along = (cleaned[i] - startX) * vx + (cleaned[i + 1] - startZ) * vz;
                Assert.That(along, Is.GreaterThanOrEqualTo(-1f), "cleaned taxi-out starts toward the hold");
            }
        }

        [Test]
        public void TaxiOut_FromBay3DoesNotDriveIntoBay4()
        {
            var leg = AdelaideGround.TaxiOut(new StableId("BAY-3"));
            var push = leg.Parts[0];
            var startTaxi = push.Seconds + AdelaideGround.TugDisconnectSeconds;
            var early = leg.PoseAt(startTaxi + 1);
            var later = leg.PoseAt(startTaxi + 25);
            Assert.That(later.X, Is.LessThan(early.X), "50B rolls toward the 05 hold, not into 50A");
            Assert.That(later.X, Is.LessThan(1110f));
        }

        [Test]
        public void TaxiClear_WaitsUntilTheFirstAircraftHasLeftTheStands()
        {
            foreach (var bay in AdelaideLayout.Bays)
            {
                var stand = new StableId(bay.Id);
                var clear = AirlineOperations.TaxiClearSecondsFrom(stand);
                Assert.That(clear, Is.GreaterThan(120), $"{bay.Id} must wait out the push and the apron");
                Assert.That(clear, Is.LessThan(AirlineOperations.TaxiOutSecondsFrom(stand)));
            }
        }
    }
}
