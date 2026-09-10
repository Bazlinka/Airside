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
    }
}
