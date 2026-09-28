using Airside.Domain;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0136 — engines sound their size, carry their size, and dull with distance.</summary>
    public sealed class EngineVoiceTests
    {
        [Test]
        public void Classes_FollowTheCatalogue()
        {
            Assert.That(EngineVoice.ClassOf(AircraftType.Saab340), Is.EqualTo(EngineClass.Turboprop));
            Assert.That(EngineVoice.ClassOf(AircraftType.Dash8Q400), Is.EqualTo(EngineClass.Turboprop));
            Assert.That(EngineVoice.ClassOf(AircraftType.EmbraerE190), Is.EqualTo(EngineClass.RegionalJet));
            Assert.That(EngineVoice.ClassOf(AircraftType.AirbusA220300), Is.EqualTo(EngineClass.RegionalJet));
            Assert.That(EngineVoice.ClassOf(AircraftType.Boeing7378), Is.EqualTo(EngineClass.Narrowbody));
            Assert.That(EngineVoice.ClassOf(AircraftType.AirbusA350900), Is.EqualTo(EngineClass.Widebody));
        }

        [Test]
        public void BiggerAircraftSoundDeeperLouderAndCarryFurther()
        {
            var regional = EngineVoice.Pitch(EngineClass.RegionalJet, 1f, 1f, 1f);
            var narrow = EngineVoice.Pitch(EngineClass.Narrowbody, 1f, 1f, 1f);
            var wide = EngineVoice.Pitch(EngineClass.Widebody, 1f, 1f, 1f);
            Assert.That(regional, Is.GreaterThan(narrow));
            Assert.That(narrow, Is.GreaterThan(wide));
            Assert.That(EngineVoice.Volume(EngineClass.Widebody, 1f, 1f), Is.GreaterThan(EngineVoice.Volume(EngineClass.Turboprop, 1f, 1f)));
            Assert.That(EngineVoice.Range(EngineClass.Widebody).Max, Is.GreaterThan(EngineVoice.Range(EngineClass.Turboprop).Max));
            Assert.That(EngineVoice.Volume(EngineClass.Narrowbody, 1f, 1f), Is.EqualTo(EngineVoice.RunningVolume));
            Assert.That(EngineVoice.Volume(EngineClass.Narrowbody, 0f, 1f), Is.LessThan(EngineVoice.RunningVolume * 0.5f), "idle is quieter");
        }

        [Test]
        public void DetuneIsSmallSteadyAndDiffersBetweenAircraft()
        {
            Assert.That(EngineVoice.Detune("VH-VZX"), Is.EqualTo(EngineVoice.Detune("VH-VZX")));
            Assert.That(EngineVoice.Detune("VH-VZX"), Is.InRange(0.97f, 1.03f));
            Assert.That(EngineVoice.Detune("VH-VZX"), Is.Not.EqualTo(EngineVoice.Detune("VH-VZY")));
            Assert.That(EngineVoice.Detune(null), Is.EqualTo(1f));
        }

        [Test]
        public void RollNoise_BuildsWithSpeedAndMatchesTaxiAtRest()
        {
            Assert.That(EngineVoice.HeardPower(0.14f, 0f), Is.EqualTo(0.14f).Within(1e-4f),
                "standing still is just the thrust, so the lineup and the roll start on the same note");
            Assert.That(EngineVoice.HeardPower(0.14f, 0.5f), Is.GreaterThan(0.14f));
            Assert.That(EngineVoice.HeardPower(0.14f, 1f), Is.GreaterThan(EngineVoice.HeardPower(0.14f, 0.5f)));
            Assert.That(EngineVoice.HeardPower(1f, 1f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(EngineVoice.RollPitch(0f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(EngineVoice.RollPitch(1f), Is.GreaterThan(1f));
        }

        [Test]
        public void DistantEnginesAreMuffled()
        {
            var near = EngineVoice.LowPassHz(10f, 400f, 1f);
            var far = EngineVoice.LowPassHz(380f, 400f, 1f);
            Assert.That(near, Is.GreaterThan(15000f));
            Assert.That(far, Is.LessThan(2500f));
            Assert.That(EngineVoice.LowPassHz(100f, 400f, 0f), Is.LessThan(EngineVoice.LowPassHz(100f, 400f, 1f)),
                "idle is duller than full power");
        }
    }
}
