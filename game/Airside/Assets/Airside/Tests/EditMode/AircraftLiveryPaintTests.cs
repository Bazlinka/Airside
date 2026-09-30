using System.Linq;
using System.Reflection;
using Airside.Domain;
using Airside.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    public sealed class AircraftLiveryPaintTests
    {
        [TestCase("ATR42")]
        [TestCase("SF34")]
        [TestCase("DH8D")]
        [TestCase("E190")]
        [TestCase("A223")]
        [TestCase("A320")]
        [TestCase("B738")]
        [TestCase("B38M")]
        [TestCase("A21N")]
        [TestCase("A359")]
        [TestCase("A339")]
        [TestCase("B789")]
        [TestCase("B78X")]
        public void RuntimeBuilder_PreservesPaintRolesAndCleanEnamel(string id)
        {
            Assert.That(AircraftType.TryFromId(id, out var type), Is.True);
            var accent = new Color(0.12f, 0.42f, 0.52f);
            var root = (Transform)typeof(AirsidePrototype)
                .GetMethod("BuildAircraftForType", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { "Livery regression " + id, type, accent, null });
            try
            {
                var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                var stripe = renderers.Single(r => r.name == "Livery stripe");
                var secondary = renderers.Single(r => r.name == "Livery secondary");
                var emblem = renderers.Single(r => r.name == "Livery emblem");
                Assert.That(stripe.sharedMaterial.GetColor("_BaseColor"), Is.EqualTo(accent));
                Assert.That(secondary.sharedMaterial.GetColor("_BaseColor"), Is.EqualTo(AircraftLiveryPaint.Secondary(accent)));
                Assert.That(emblem.sharedMaterial.GetColor("_BaseColor"), Is.EqualTo(AircraftLiveryPaint.Emblem(accent)));
                var fuselage = renderers.First(r => r.name == "Fuselage");
                foreach (var r in new[] { fuselage, stripe, secondary, emblem })
                {
                    Assert.That(r.sharedMaterial.GetTexture("_BaseMap"), Is.Null, id + ": no tiled building/skin albedo");
                    Assert.That(r.sharedMaterial.GetTexture("_BumpMap"), Is.Null, id + ": no coarse random bump");
                    Assert.That(r.sharedMaterial.GetFloat("_Metallic"), Is.LessThan(0.05f), id + ": enamel is dielectric");
                }
                foreach (var side in new[] { "L", "R" })
                {
                    var cowl = renderers.Single(r => r.name == "Engine livery " + side);
                    Assert.That(cowl.transform.parent.name, Is.EqualTo("Wing " + side),
                        "engine paint follows the same flexing wing as its nacelle");
                }
            }
            finally { Object.DestroyImmediate(root.gameObject); }
        }

        [TestCase("EnsureNavPointLight", "NavigationLight")]
        [TestCase("EnsureBeaconPointLight", "Beacon")]
        [TestCase("EnsureLandingSpotLight", "LandingLight")]
        [TestCase("EnsureTaxiSpotLight", "TaxiLight")]
        public void RefreshedLampCache_ReusesTheLightAtItsLens(string methodName, string kindName)
        {
            var lamp = new GameObject("Authored aircraft lamp");
            try
            {
                var prototype = typeof(AirsidePrototype);
                var partType = prototype.GetNestedType("LightGearPart", BindingFlags.NonPublic);
                var kindType = prototype.GetNestedType("LightGearKind", BindingFlags.NonPublic);
                var method = prototype.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
                Light original = null;
                for (var refresh = 0; refresh < 3; refresh++)
                {
                    // Rebuild the wrapper as the scene index does when a lamp gains a child.
                    var part = System.Activator.CreateInstance(partType, new object[] {
                        lamp.transform, System.Enum.Parse(kindType, kindName), AircraftNavigationLight.Left });
                    var arguments = methodName == "EnsureLandingSpotLight"
                        ? new object[] { part, true, true }
                        : new object[] { part, methodName == "EnsureBeaconPointLight" ? (object)1f : true };
                    method.Invoke(null, arguments);
                    var lights = lamp.GetComponentsInChildren<Light>(true);
                    Assert.That(lights.Length, Is.EqualTo(1));
                    Assert.That(lights[0].transform.parent, Is.EqualTo(lamp.transform));
                    Assert.That(lights[0].enabled, Is.True);
                    if (original != null) Assert.That(lights[0], Is.SameAs(original));
                    original = lights[0];
                }
            }
            finally { Object.DestroyImmediate(lamp); }
        }

        [Test]
        public void PresetSecondaryPaintMatchesThePlayersTwoToneSwatch()
        {
            for (var i = 0; i < AirlineSetupModel.Palette.Length; i++)
            {
                Assert.That(ColorUtility.TryParseHtmlString(AirlineSetupModel.Palette[i].Hex, out var primary), Is.True);
                Assert.That(ColorUtility.TryParseHtmlString(AirlineSetupModel.PaletteAccents[i], out var accent), Is.True);
                Assert.That(AircraftLiveryPaint.Colour("Livery secondary", primary), Is.EqualTo(accent));
                Assert.That(AircraftLiveryPaint.Colour("Livery stripe", primary), Is.EqualTo(primary));
                Assert.That(AircraftLiveryPaint.Emblem(primary), Is.Not.EqualTo(primary));
            }
        }

        [Test]
        public void WarmAndCoolAirlinesKeepContrastingSecondaryAndIvoryMark()
        {
            var cool = new Color(0.10f, 0.38f, 0.55f);
            var warm = new Color(0.85f, 0.55f, 0.12f);
            Assert.That(AircraftLiveryPaint.Secondary(cool), Is.Not.EqualTo(cool));
            Assert.That(AircraftLiveryPaint.Secondary(warm), Is.Not.EqualTo(warm));
            Assert.That(AircraftLiveryPaint.Secondary(cool), Is.Not.EqualTo(AircraftLiveryPaint.Secondary(warm)));
            Assert.That(AircraftLiveryPaint.Colour("Livery emblem", warm), Is.EqualTo(AircraftLiveryPaint.Ivory));
            Assert.That(AircraftLiveryPaint.Emblem(Color.white), Is.Not.EqualTo(AircraftLiveryPaint.Ivory),
                "white paint still needs a visible tail mark");
        }
    }
}
