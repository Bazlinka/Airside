using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// Locks the landing-gear spin contract. The kit renames tyre / wheel / rim
    /// nodes to these runtime names, then nests them under the strut, and both the
    /// pivot rebake and the ground roll select parts through
    /// <see cref="AirsideAircraftParts.RollsInPlace"/>. If the kit map or the nest
    /// renames change, these expectations catch the drift the spin pass warns about.
    /// </summary>
    public sealed class AircraftPartsTests
    {
        [TestCase("Tire")]
        [TestCase("Tire nose")]
        [TestCase("Tire L")]
        [TestCase("Wheel")]
        [TestCase("Wheel nose")]
        [TestCase("Wheel L")]
        [TestCase("Rim")]
        [TestCase("Rim nose")]
        public void RollsInPlace_TrueForTyreWheelAndRim(string partName)
        {
            Assert.That(AirsideAircraftParts.RollsInPlace(partName), Is.True, partName);
        }

        [TestCase("Gear nose")]
        [TestCase("Gear L")]
        [TestCase("Gear R")]
        [TestCase("Oleo")]
        [TestCase("Gear oleo L")]
        [TestCase("Scissors")]
        [TestCase("Gear scissors L")]
        [TestCase("Gear door L")]
        [TestCase("Prop hub L")]
        [TestCase("Wheel arch FL")]
        [TestCase("Fuselage")]
        [TestCase("")]
        [TestCase(null)]
        public void RollsInPlace_FalseForStrutsOleosScissorsDoorsAndHubs(string partName)
        {
            Assert.That(AirsideAircraftParts.RollsInPlace(partName), Is.False, partName ?? "<null>");
        }

        [TestCase("Gear nose")]
        [TestCase("Gear L")]
        [TestCase("Gear R")]
        public void IsGearStrut_TrueForTheThreeRetractingLegs(string partName)
        {
            Assert.That(AirsideAircraftParts.IsGearStrut(partName), Is.True, partName);
        }

        [TestCase("Gear scissors L")]
        [TestCase("Gear oleo nose")]
        [TestCase("Gear door R")]
        [TestCase("Scissors")]
        [TestCase("Oleo")]
        [TestCase("Tire")]
        [TestCase("Wheel")]
        [TestCase("Rim")]
        [TestCase("Gear")]
        [TestCase("")]
        [TestCase(null)]
        public void IsGearStrut_FalseForCarriedPartsAndDensifiedGear(string partName)
        {
            Assert.That(AirsideAircraftParts.IsGearStrut(partName), Is.False, partName ?? "<null>");
        }

        [Test]
        public void GearStrut_AndRollingWheel_AreDisjoint()
        {
            // A leg that retracts must never also be spun in place, or the fold and
            // the roll would fight over the same transform.
            foreach (var strut in new[] { "Gear nose", "Gear L", "Gear R" })
                Assert.That(AirsideAircraftParts.RollsInPlace(strut), Is.False, strut);
            foreach (var wheel in new[] { "Tire", "Wheel", "Rim" })
                Assert.That(AirsideAircraftParts.IsGearStrut(wheel), Is.False, wheel);
        }
    }
}
