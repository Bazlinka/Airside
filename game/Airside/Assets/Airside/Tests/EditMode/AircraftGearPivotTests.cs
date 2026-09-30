using Airside.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    public sealed class AircraftGearPivotTests
    {
        // A 737-class kit: rooted at the nose stop, main tyres ~17 m aft, tyre bottoms at the ground offset.
        private static readonly Vector3 JetMainContact = new(0f, -0.68f, -17f);

        [Test]
        public void Level_NeedsNoLift()
        {
            Assert.AreEqual(0f, AircraftGearPivot.LiftMetres(Quaternion.Euler(0f, 40f, 0f), JetMainContact), 1e-4f);
        }

        [Test]
        public void NoseUpRotation_LiftsTheRootSoTheMainsKeepTheirHeight()
        {
            var rotation = Quaternion.Euler(-9f, 0f, 0f);
            var lift = AircraftGearPivot.LiftMetres(rotation, JetMainContact);
            Assert.That(lift, Is.GreaterThan(2.5f), "9 degrees over 17 m swings the mains down ~2.7 m");
            var mainsY = lift + (rotation * JetMainContact).y;
            Assert.AreEqual(JetMainContact.y, mainsY, 1e-3f);
        }

        [Test]
        public void HeadingDoesNotChangeTheLift()
        {
            var north = AircraftGearPivot.LiftMetres(Quaternion.Euler(-6.5f, 0f, 0f), JetMainContact);
            var west = AircraftGearPivot.LiftMetres(Quaternion.Euler(0f, 250f, 0f) * Quaternion.Euler(-6.5f, 0f, 0f), JetMainContact);
            Assert.AreEqual(north, west, 1e-3f);
        }

        [Test]
        public void MainsAtTheRoot_NeedNoLift()
        {
            Assert.AreEqual(0f, AircraftGearPivot.LiftMetres(Quaternion.Euler(-9f, 0f, 0f), new Vector3(0f, -0.7f, 0f)), 0.1f);
        }
    }
}
