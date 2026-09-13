using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// Locks the one name contract shared by the wheel pivot rebake and the ground
    /// roll. These drifted apart once already: the rebake was lost while the roll
    /// survived, and every tyre swung in an arc around the fuselage instead of
    /// turning on its axle. If the rebake set and the spun set stop agreeing, the
    /// wheels break again in exactly that way.
    /// </summary>
    public sealed class AircraftPartsTests
    {
        // Authored kit names, straight out of mdl_atr42_starter_v02.gltf.
        [TestCase("tire_left_forward")]
        [TestCase("tire_left_aft")]
        [TestCase("tire_right_forward")]
        [TestCase("tire_right_aft")]
        [TestCase("tire_nose_left")]
        [TestCase("tire_nose_right")]
        [TestCase("wheel_left_forward")]
        [TestCase("wheel_nose_right")]
        [TestCase("rim_left_aft")]
        [TestCase("rim_nose_left")]
        // Presentation names the kit parts are renamed to at build time.
        [TestCase("Tire L forward")]
        [TestCase("Tire nose left")]
        [TestCase("Wheel R aft")]
        [TestCase("Rim nose right")]
        public void RollingParts_AreRecognised(string partName)
        {
            Assert.That(AirsideAircraftParts.RollsInPlace(partName), Is.True,
                $"{partName} should roll, so it must also be rebaked to its axle");
        }

        // Carried by the leg, never spun.
        [TestCase("gear_left")]
        [TestCase("gear_right")]
        [TestCase("gear_nose")]
        [TestCase("gear_oleo_left")]
        [TestCase("gear_scissors_nose")]
        [TestCase("gear_door_left")]
        [TestCase("gear_fairing_left")]
        [TestCase("gear_fairing_right")]
        [TestCase("Gear L")]
        [TestCase("Gear nose")]
        [TestCase("Propeller L")]
        [TestCase("fuselage")]
        [TestCase("wing_left")]
        // Car-style caps that carry a wheel word but do not roll.
        [TestCase("wheel_arch_left")]
        [TestCase("wheel_hub_cap")]
        [TestCase("")]
        [TestCase(null)]
        public void StaticParts_AreNotSpun(string partName)
        {
            Assert.That(AirsideAircraftParts.RollsInPlace(partName), Is.False,
                $"{partName} is carried by the leg and must not be spun or re-origined");
        }

        [Test]
        public void EveryRollingPartOfTheV02Kit_IsCovered()
        {
            // The full rolling set of the shipped kit: 4 main + 2 nose positions,
            // each as a tyre, a wheel and a rim.
            var sides = new[] { "left_forward", "left_aft", "right_forward", "right_aft" };
            var nose = new[] { "nose_left", "nose_right" };
            var covered = 0;

            foreach (var prefix in new[] { "tire_", "wheel_", "rim_" })
            {
                foreach (var position in sides)
                {
                    Assert.That(AirsideAircraftParts.RollsInPlace(prefix + position), Is.True);
                    covered++;
                }

                foreach (var position in nose)
                {
                    Assert.That(AirsideAircraftParts.RollsInPlace(prefix + position), Is.True);
                    covered++;
                }
            }

            Assert.That(covered, Is.EqualTo(18), "the v02 kit ships 18 rolling parts");
        }
    }
}
