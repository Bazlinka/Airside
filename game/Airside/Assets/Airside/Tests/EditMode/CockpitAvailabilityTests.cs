using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class CockpitAvailabilityTests
    {
        [Test] public void FirstSpoolIsAvailableBeforeAnyRunningThreshold()
        {
            var engines = new EngineState(0f, 0.001f, true, false);
            Assert.That(engines.AnyRunning, Is.False);
            Assert.That(CockpitAvailability.Reason(AircraftType.Saab340, true, engines), Is.Empty);
        }
        [Test] public void ColdStandAndCompletedShutdownAreUnavailable()
        {
            Assert.That(CockpitAvailability.Reason(AircraftType.Saab340, true, EngineState.ColdAndOpen),
                Is.EqualTo("Available after engine start"));
        }
        [Test] public void RunningEngineDoesNotPermitOffFieldView()
        {
            Assert.That(CockpitAvailability.Reason(AircraftType.Saab340, false, EngineState.Running),
                Is.EqualTo("Aircraft outside the local area"));
        }
        [Test] public void ColdCockpitActionIsDisabledAndFitsWithDepartureActions()
        {
            var data = new SelectionCardData { IsPlayer = true, PrimaryLabel = "View plan", CanCancel = true,
                ShowCameraActions = true, CanFollow = true, CanCockpit = false,
                CockpitHint = "Available after engine start" };
            data.Prep.Add(new SelectionPrepStage("Fuel", 0f, false));
            var height = SelectionCardPainter.HeightFor(data);
            var box = new HudBox(0f, 0f, 640f, height);
            var draw = new HudDrawList();
            SelectionCardPainter.Paint(draw, box, data);
            Assert.That(draw.Commands.Single(c => c.ActionId == "camera-cockpit").Enabled, Is.False);
            var primary = draw.Commands.Single(c => c.ActionId == HudAction.Primary).Box;
            var camera = draw.Commands.Single(c => c.ActionId == "camera-cockpit").Box;
            Assert.That(primary.Bottom, Is.LessThan(camera.Y));
            Assert.That(camera.Bottom, Is.LessThanOrEqualTo(height));
        }

        [Test] public void EveryCatalogueJetHasAnExplicitUniqueProfileAndAccess()
        {
            var jets = AircraftCatalogue.All.Where(s => s.Id != "SF34" && s.Id != AircraftType.Atr42.Id && s.Id != "DH8D").ToArray();
            Assert.That(jets.Length, Is.EqualTo(10));
            Assert.That(JetCockpitProfile.All.Select(p => p.TypeId), Is.EquivalentTo(jets.Select(s => s.Id)));
            foreach (var spec in jets)
            {
                Assert.That(JetCockpitProfile.TryFor(spec.Id, out var profile), Is.True, spec.Id);
                Assert.That(profile.EyeY, Is.GreaterThan(4f));
                Assert.That(profile.EyeZ, Is.LessThan(-2f));
                Assert.That(CockpitAvailability.Reason(spec.Type, true, EngineState.Running), Is.Empty);
                Assert.That(CockpitAvailability.Reason(spec.Type, false, EngineState.Running), Is.Not.Empty);
                Assert.That(CockpitAvailability.Reason(spec.Type, true, EngineState.ColdAndOpen), Is.Not.Empty);
            }
        }

        [TestCase("B738", false, 6)]
        [TestCase("B38M", false, 4)]
        [TestCase("E190", false, 5)]
        [TestCase("A223", true, 5)]
        [TestCase("A320", true, 6)]
        [TestCase("A21N", true, 6)]
        [TestCase("A339", true, 6)]
        [TestCase("A359", true, 6)]
        [TestCase("B789", false, 5)]
        [TestCase("B78X", false, 5)]
        public void FamilyControlsAndDisplayCounts(string id, bool sidestick, int displays)
        {
            Assert.That(JetCockpitProfile.TryFor(id, out var profile), Is.True);
            Assert.That(profile.Sidestick, Is.EqualTo(sidestick));
            Assert.That(profile.DisplayCount, Is.EqualTo(displays));
        }

        [TestCase("SF34")]
        [TestCase("ATR42")]
        [TestCase("DH8D")]
        public void EveryCurrentTurbopropUsesTheSameEngineAndVisibilityRules(string id)
        {
            Assert.That(AircraftType.TryFromId(id, out var type), Is.True);
            Assert.That(CockpitAvailability.Supported(type), Is.True);
            Assert.That(CockpitAvailability.Reason(type, true, new EngineState(0f, 0.001f, true, false)), Is.Empty);
            Assert.That(CockpitAvailability.Reason(type, true, EngineState.ColdAndOpen), Is.EqualTo("Available after engine start"));
            Assert.That(CockpitAvailability.Reason(type, false, EngineState.Running), Is.EqualTo("Aircraft outside the local area"));
        }

        [Test] public void BellCockpitRequiresLocalVisibilityAndNonzeroRotorcraftSpool()
        {
            Assert.That(CockpitAvailability.Supported(AircraftType.Bell412), Is.True);
            Assert.That(CockpitAvailability.Reason(AircraftType.Bell412, true, new EngineState(0f, 0.001f, true, false)), Is.Empty);
            Assert.That(CockpitAvailability.Reason(AircraftType.Bell412, true, EngineState.ColdAndOpen), Is.EqualTo("Available after engine start"));
            Assert.That(CockpitAvailability.Reason(AircraftType.Bell412, false, EngineState.Running), Is.EqualTo("Aircraft outside the local area"));
        }

        [TestCase("Main rotor")]
        [TestCase("Tail rotor")]
        [TestCase("Rotor disc")]
        [TestCase("rotor_mast")]
        [TestCase("main_rotor_blade_1")]
        [TestCase("tail_rotor_hub")]
        [TestCase("landing_skid_left")]
        [TestCase("skid_strut_right_front")]
        public void BellWindowViewsRetainRotorsBlurAndSkids(string name)
        {
            Assert.That(CockpitExteriorVisibility.KeepsDuringCockpit(name), Is.True);
        }

        [TestCase("fuselage")]
        [TestCase("cockpit_glass_left")]
        [TestCase("sliding_door_right")]
        public void BellOpaqueHullAndGlazingAreReplacedByTheClearInteriorShell(string name)
        {
            Assert.That(CockpitExteriorVisibility.KeepsDuringCockpit(name), Is.False);
        }

        [Test] public void NullTypeRemainsUnavailable()
        {
            Assert.That(CockpitAvailability.Supported(null), Is.False);
        }
    }
}
