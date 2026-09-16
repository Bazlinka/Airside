using System.Linq;
using System.Reflection;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Airside.Tests
{
    /// <summary>
    /// Each aircraft type is built from its own model (ADR 0048/0049): the real
    /// AirsidePrototype.BuildAircraftForType dispatch, measured, not just the profile lookup.
    /// </summary>
    public sealed class AircraftDispatchTests
    {
        private static Transform Build(AircraftType type)
        {
            // Runtime builders use Object.Destroy for scratch parts; edit mode logs that as an error.
            LogAssert.ignoreFailingMessages = true;
            var method = typeof(AirsidePrototype).GetMethod("BuildAircraftForType", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "BuildAircraftForType dispatch");
            return (Transform)method.Invoke(null, new object[] { $"Dispatch {type.Id}", type, Color.white, null });
        }

        private static Bounds RenderedBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.name != "GroundShadow" && r.name != AircraftPickRouting.MarkerChildName)
                .ToArray();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers)
                bounds.Encapsulate(r.bounds);
            return bounds;
        }

        [Test]
        public void EveryType_DispatchesToItsOwnModelAndProfile()
        {
            foreach (var spec in AircraftCatalogue.All)
            {
                var root = Build(spec.Type);
                try
                {
                    var expected = AircraftVisualProfiles.For(spec.Type);
                    var profile = root.GetComponent<AircraftVisualProfileComponent>();
                    Assert.That(profile, Is.Not.Null, spec.Name);
                    Assert.That(profile.PickSizeMetres, Is.EqualTo(expected.PickSizeMetres), $"{spec.Name} pick volume");
                    Assert.That(profile.SelectionMarkerDiameterMetres, Is.EqualTo(expected.SelectionMarkerDiameterMetres), $"{spec.Name} marker");
                    Assert.That(profile.FollowDistanceMultiplier, Is.EqualTo(expected.FollowDistanceMultiplier), $"{spec.Name} follow framing");

                    // What is drawn has the dimensions of the model that type should use.
                    var drawn = RenderedBounds(root);
                    var model = spec.ModelStatus == ModelStatus.Genuine ? spec : AircraftCatalogue.Atr42;
                    Assert.That(AircraftModelBounds.WithinTolerance(drawn.size.z, model.LengthMetres), Is.True,
                        $"{spec.Name} drawn {drawn.size.z:0.00} m long; its model is {model.LengthMetres} m");
                    Assert.That(AircraftModelBounds.WithinTolerance(drawn.size.x, model.WingspanMetres), Is.True,
                        $"{spec.Name} drawn {drawn.size.x:0.00} m wide; its model is {model.WingspanMetres} m");
                }
                finally
                {
                    Object.DestroyImmediate(root.gameObject);
                }
            }
        }

        [Test]
        public void Dash8Q400_BuildsAir006_WithSixBladePropellersRatherThanTheAtr()
        {
            var root = Build(AircraftType.Dash8Q400);
            try
            {
                Assert.That(root.GetComponent<AircraftVisualProfileComponent>().PickSizeMetres, Is.EqualTo(AircraftVisualProfiles.Dash8Q400.PickSizeMetres));
                foreach (var side in new[] { "L", "R" })
                {
                    // The spin rig nests the other blades under "Propeller L/R" as Blade, Blade 2 …
                    var propeller = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == $"Propeller {side}");
                    var blades = 1 + propeller.Cast<Transform>().Count(c => c.name == "Blade" || c.name.StartsWith("Blade "));
                    Assert.That(blades, Is.EqualTo(6), $"six blades on the {side} propeller");
                }

                var drawn = RenderedBounds(root);
                Assert.That(drawn.size.z, Is.GreaterThan(AircraftCatalogue.Atr42.LengthMetres * 1.2f), "not the 22.7 m ATR");
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void Saab340_BuildsAir007_WithFourBladePropellersAndAnimationPartsRatherThanTheAtr()
        {
            var root = Build(AircraftType.Saab340);
            try
            {
                Assert.That(root.GetComponent<AircraftVisualProfileComponent>().PickSizeMetres,
                    Is.EqualTo(AircraftVisualProfiles.Saab340.PickSizeMetres));
                foreach (var side in new[] { "L", "R" })
                {
                    var propeller = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == $"Propeller {side}");
                    var blades = 1 + propeller.Cast<Transform>().Count(c => c.name == "Blade" || c.name.StartsWith("Blade "));
                    Assert.That(blades, Is.EqualTo(4), $"four Dowty blades on the {side} propeller");
                }

                Assert.That(root.GetComponentsInChildren<Transform>(true).Any(t => t.name == "CabinDoor"), Is.True);
                Assert.That(root.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Gear L"), Is.True);
                Assert.That(root.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Gear R"), Is.True);
                Assert.That(root.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Gear nose"), Is.True);
                Assert.That(root.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Flap L"), Is.True);
                Assert.That(root.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Flap R"), Is.True);

                var wing = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Wing L");
                var wingColor = wing.GetComponent<Renderer>().sharedMaterial.color;
                Assert.That(wingColor.r, Is.EqualTo(0.76f).Within(0.01f), "Saab wings use restrained painted aluminium");
                var window = root.GetComponentsInChildren<Transform>(true).First(t => t.name.StartsWith("Cabin window "));
                var windowColor = window.GetComponent<Renderer>().sharedMaterial.color;
                Assert.That(windowColor.r, Is.LessThan(0.1f), "Saab glazing remains readable at follow distance");

                var drawn = RenderedBounds(root);
                Assert.That(drawn.size.z, Is.EqualTo(AircraftCatalogue.Saab340.LengthMetres).Within(0.05f * AircraftCatalogue.Saab340.LengthMetres),
                    "compact Saab length, not the ATR stand-in");
                Assert.That(drawn.size.x, Is.EqualTo(AircraftCatalogue.Saab340.WingspanMetres).Within(0.05f * AircraftCatalogue.Saab340.WingspanMetres));
                Assert.That(drawn.size.z, Is.LessThan(AircraftCatalogue.Atr42.LengthMetres), "shorter than the high-wing ATR");
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void Boeing7378_BuildsArticulatedTurbofansAndUsesItsOwnWheelScale()
        {
            var root = Build(AircraftType.Boeing7378);
            try
            {
                var profile = root.GetComponent<AircraftVisualProfileComponent>();
                Assert.That(profile.MainTireRadiusMetres, Is.EqualTo(0.62f).Within(0.001f));
                Assert.That(profile.NoseTireRadiusMetres, Is.EqualTo(0.55f).Within(0.001f));

                foreach (var side in new[] { "L", "R" })
                {
                    var fan = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == $"Fan {side}");
                    Assert.That(fan.Cast<Transform>().Count(t => t.name.StartsWith($"Fan blade {side}") && t != fan),
                        Is.EqualTo(12), $"twelve nested blades in the {side} turbofan");
                    Assert.That(fan.Find("FanDisc"), Is.Not.Null,
                        "high-RPM intake blur exists rather than leaving a frozen fan face");
                }

                var wing = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Wing L");
                var wingColor = wing.GetComponent<Renderer>().sharedMaterial.color;
                Assert.That(wingColor.r, Is.EqualTo(0.58f).Within(0.01f), "737 wings use a restrained neutral finish");
                var winglet = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Winglet L");
                Assert.That(winglet.GetComponent<Renderer>().sharedMaterial.color, Is.EqualTo(Color.white),
                    "split winglets retain the airline accent");
                var window = root.GetComponentsInChildren<Transform>(true).First(t => t.name.StartsWith("Cabin window "));
                Assert.That(window.GetComponent<Renderer>().sharedMaterial.color.r, Is.LessThan(0.1f),
                    "737 glazing remains readable at follow distance");
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void Operators_FlyTheirCatalogueTypes_AndDrawThemWithThoseProfiles()
        {
            var ops = AirlineOperations.StartAtAdelaide(new ManualSimulationClock(new SimulationTime(0)), new SeededRandomSource(9),
                Airline.Player("Dispatch Air", "#1F3A93"));
            AircraftType TypeOf(string airline) => ops.Fleet.Where(a => a.Airline.Name == airline).Select(a => a.Type).Distinct().Single();

            Assert.That(TypeOf("QantasLink"), Is.SameAs(AircraftType.Dash8Q400));
            Assert.That(TypeOf("Rex"), Is.SameAs(AircraftType.Saab340));
            Assert.That(TypeOf("Emu Air"), Is.SameAs(AircraftType.Atr42));
            Assert.That(TypeOf("Wattlebird Jet"), Is.SameAs(AircraftType.Boeing7378));
            Assert.That(ops.Fleet.Single(a => a.Airline.IsPlayer).Type, Is.SameAs(AircraftType.Atr42));

            Assert.That(AircraftVisualProfiles.For(TypeOf("QantasLink")), Is.EqualTo(AircraftVisualProfiles.Dash8Q400));
            Assert.That(AircraftVisualProfiles.For(TypeOf("Wattlebird Jet")), Is.EqualTo(AircraftVisualProfiles.Boeing7378));
            Assert.That(AircraftVisualProfiles.For(TypeOf("Rex")), Is.EqualTo(AircraftVisualProfiles.Saab340),
                "Rex's Saab 340Bs draw with AIR-007, not the ATR stand-in");
        }
    }
}
