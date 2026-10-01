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

        [Test] public void JetsAndUnknownTypesRemainUnavailable()
        {
            foreach (var spec in AircraftCatalogue.All)
                if (spec.Type.Id != AircraftType.Saab340.Id && spec.Type.Id != AircraftType.Atr42.Id
                    && spec.Type.Id != AircraftType.Dash8Q400.Id)
                    Assert.That(CockpitAvailability.Reason(spec.Type, true, EngineState.Running),
                        Is.EqualTo("Turboprop cockpits only"), spec.Id);
            Assert.That(CockpitAvailability.Supported(null), Is.False);
        }
    }
}
