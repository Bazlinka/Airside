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
        // Authored kit names, straight out of mdl_atr42_starter_v03.gltf (same as v02).
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

        /// <summary>
        /// The circuit's phase speed is zero for every phase but the takeoff roll and the
        /// landing rollout, so an aircraft taxiing the Adelaide ground network used to slide
        /// around on stationary wheels. Whenever a ground leg reports a pose speed, that is
        /// the speed the tyres must roll at.
        /// </summary>
        [TestCase(4.5f, 0f)]
        [TestCase(12.0f, 0f)]
        [TestCase(0.8f, 0f)]
        public void TaxiingWheels_RollAtTheGroundLegSpeed(float legSpeed, float circuitSpeed)
        {
            Assert.That(AirsideAircraftParts.TireRollMetresPerSecond(legSpeed, tailFirst: false, circuitSpeed),
                Is.EqualTo(legSpeed).Within(0.0001f),
                "a taxiing aircraft must roll its wheels at its real ground speed");
        }

        [Test]
        public void PushbackWheels_TurnBackwards()
        {
            var forward = AirsideAircraftParts.TireRollMetresPerSecond(2.5f, tailFirst: false, 0f);
            var pushback = AirsideAircraftParts.TireRollMetresPerSecond(2.5f, tailFirst: true, 0f);

            Assert.That(pushback, Is.EqualTo(-forward).Within(0.0001f),
                "pushback is drawn tail-first, so the wheels have to turn the other way");
        }

        [Test]
        public void StoppedInAQueue_TheWheelsStop()
        {
            Assert.That(AirsideAircraftParts.TireRollMetresPerSecond(0f, tailFirst: false, circuitSpeed: 48f),
                Is.EqualTo(0f).Within(0.0001f),
                "an aircraft held at the holding point must not spin its wheels");
        }

        [Test]
        public void OffTheGroundNetwork_TheCircuitSpeedStillApplies()
        {
            // No ground leg: the demo circuit's takeoff roll / landing rollout speed is all there is.
            Assert.That(AirsideAircraftParts.TireRollMetresPerSecond(null, tailFirst: false, circuitSpeed: 48f),
                Is.EqualTo(48f).Within(0.0001f));
            Assert.That(AirsideAircraftParts.TireRollMetresPerSecond(null, tailFirst: true, circuitSpeed: 0f),
                Is.EqualTo(0f).Within(0.0001f));
        }

        /// <summary>
        /// The livery filter used to accept any part whose name merely contained "nose", which
        /// swept up the nose gear's own tyres, wheels and rims — so every liveried aircraft
        /// rolled on livery-painted nose wheels.
        /// </summary>
        [TestCase("Fuselage")]
        [TestCase("FuselageMid")]
        [TestCase("Fuselage aft")]
        [TestCase("Nose")]
        [TestCase("fuselage_mid")]
        [TestCase("cabin_ring_04")]
        [TestCase("tail_cone")]
        [TestCase("nose_ring_02")]
        public void FuselageSkin_TakesTheLivery(string partName)
        {
            Assert.That(AirsideAircraftParts.TakesFuselageLivery(partName), Is.True,
                $"{partName} is fuselage skin and carries the operator livery");
        }

        [TestCase("Tire nose left")]
        [TestCase("Wheel nose right")]
        [TestCase("Rim nose left")]
        [TestCase("tire_nose_right")]
        [TestCase("wheel_nose_left")]
        [TestCase("rim_nose_right")]
        [TestCase("Gear nose")]
        [TestCase("gear_nose")]
        [TestCase("gear_scissors_nose")]
        [TestCase("gear_oleo_nose")]
        [TestCase("gear_door_nose")]
        [TestCase("nose_gear_bay")]
        [TestCase("gear_fairing_left")]
        [TestCase("Wing L")]
        [TestCase("Propeller L")]
        [TestCase("")]
        [TestCase(null)]
        public void LandingGearAndOtherParts_StayUnpainted(string partName)
        {
            Assert.That(AirsideAircraftParts.TakesFuselageLivery(partName), Is.False,
                $"{partName} is not fuselage skin and must keep its own material");
        }

        [Test]
        public void NoRollingPart_EverTakesTheLivery()
        {
            foreach (var prefix in new[] { "tire_", "wheel_", "rim_" })
            {
                foreach (var position in new[] { "nose_left", "nose_right", "left_forward", "right_aft" })
                {
                    var part = prefix + position;
                    Assert.That(AirsideAircraftParts.RollsInPlace(part), Is.True);
                    Assert.That(AirsideAircraftParts.TakesFuselageLivery(part), Is.False,
                        $"{part} rolls on the ground and must never be painted as fuselage skin");
                }
            }
        }

        [TestCase("nav_light_left", AircraftNavigationLight.Left)]
        [TestCase("NavLight L", AircraftNavigationLight.Left)]
        [TestCase("nav_light_right", AircraftNavigationLight.Right)]
        [TestCase("NavLight R", AircraftNavigationLight.Right)]
        [TestCase("tail_nav_light", AircraftNavigationLight.Tail)]
        [TestCase("Tail nav light", AircraftNavigationLight.Tail)]
        [TestCase("LandingLight L", AircraftNavigationLight.None)]
        public void NavigationLights_AreClassifiedByColourPosition(string name, AircraftNavigationLight expected)
        {
            Assert.That(AirsideAircraftParts.NavigationLightFor(name), Is.EqualTo(expected));
        }
    }
}
