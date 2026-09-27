using System;
using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// ADR 0123 — first-time airline setup: the wizard's choices, difficulty in the economy, the
    /// airline flight code, save v14 and the Flight Manual's honesty about the rules.
    /// </summary>
    public sealed class AirlineSetupTests
    {
        [Test]
        public void Wizard_WalksFourCardsAndOnlyAdvancesPastValidOnes()
        {
            var setup = new AirlineSetupModel { Name = "" };
            setup.Apply(AirlineSetupPainter.Next, out _);
            Assert.That(setup.Step, Is.EqualTo(SetupStep.Identity), "no name, no advance");
            Assert.That(setup.StepError, Is.Not.Empty);

            setup.Name = "Gulf Air Link";
            Assert.That(setup.EffectiveCode, Is.EqualTo("GA"), "suggested from the name");
            setup.Apply(AirlineSetupPainter.Next, out _);
            setup.Apply(AirlineSetupPainter.Next, out _);
            setup.Apply(AirlineSetupPainter.Next, out _);
            Assert.That(setup.Step, Is.EqualTo(SetupStep.Briefing));
            setup.Apply(AirlineSetupPainter.Start, out var outcome);
            Assert.That(outcome, Is.EqualTo(SetupOutcome.Start));

            setup.Apply(AirlineSetupPainter.Back, out _);
            Assert.That(setup.Step, Is.EqualTo(SetupStep.Difficulty));
            setup.Step = SetupStep.Identity;
            setup.Apply(AirlineSetupPainter.Back, out outcome);
            Assert.That(outcome, Is.EqualTo(SetupOutcome.Leave), "Cancel on the first card leaves the wizard");
        }

        [Test]
        public void Wizard_RefusesARealAirlinesCodeAndBadCodes()
        {
            var setup = new AirlineSetupModel { Name = "Test", Code = "QFA", CodeEdited = true };
            Assert.That(setup.CodeValid, Is.False);
            Assert.That(setup.StepError, Does.Contain("QFA"));
            setup.Code = "X";
            Assert.That(setup.CodeValid, Is.False);
            setup.Code = "SXR";
            Assert.That(setup.CodeValid, Is.True);
        }

        [Test]
        public void Wizard_ColourChoicesAlwaysProduceAValidLivery()
        {
            var setup = new AirlineSetupModel();
            foreach (var index in Enumerable.Range(0, AirlineSetupModel.Palette.Length))
            {
                setup.Apply(AirlineSetupPainter.PalettePrefix + index, out _);
                Assert.That(setup.LiveryHex, Is.EqualTo(AirlineSetupModel.Palette[index].Hex));
            }
            for (var hue = 0; hue < AirlineSetupModel.HueSteps; hue++)
            for (var shade = 0; shade < AirlineSetupModel.ShadeSteps; shade++)
            {
                setup.Apply(AirlineSetupPainter.HuePrefix + hue, out _);
                setup.Apply(AirlineSetupPainter.ShadePrefix + shade, out _);
                Assert.That(setup.PaletteIndex, Is.EqualTo(-1));
                Assert.DoesNotThrow(() => Airline.Player("Test", setup.LiveryHex), setup.LiveryHex);
            }
            setup.Apply(AirlineSetupPainter.DifficultyPrefix + "Demanding", out _);
            Assert.That(setup.Difficulty, Is.EqualTo(CareerDifficulty.Demanding));
            setup.Apply(AirlineSetupPainter.ToggleCoaching, out _);
            Assert.That(setup.Coaching, Is.False);
        }

        [Test]
        public void SetupCard_FitsEveryWindowOnEveryStep()
        {
            foreach (var (width, height) in HudTestAirline.Viewports)
            foreach (SetupStep step in Enum.GetValues(typeof(SetupStep)))
            {
                var model = new SplashModel { Step = SplashStep.NewAirline, HasSave = true };
                model.Setup.Step = step;
                var layout = SplashLayout.Create(width, height, SplashStep.NewAirline, true);
                var draw = new HudDrawList();
                SplashPainter.Paint(draw, layout, model);
                foreach (var c in draw.Commands.Where(c => c.Kind is not (HudDrawKind.Image or HudDrawKind.Gradient)))
                {
                    Assert.That(c.Box.Bottom, Is.LessThanOrEqualTo(height + 0.5f), $"{width}x{height} {step} '{c.Text}'");
                    Assert.That(c.Box.Right, Is.LessThanOrEqualTo(width + 0.5f), $"{width}x{height} {step} '{c.Text}'");
                }
                var card = layout.Setup.Card;
                foreach (var c in draw.Commands.Where(c => c.ActionId.StartsWith(AirlineSetupPainter.Prefix)))
                    Assert.That(card.Contains(c.Box.X + 1f, c.Box.Y + 1f), Is.True, $"{step} control outside the card");
                if (step == SetupStep.Difficulty)
                    Assert.That(draw.Commands.Count(c => c.ActionId.StartsWith(AirlineSetupPainter.DifficultyPrefix)),
                        Is.EqualTo(3));
            }
        }

        [Test]
        public void Difficulty_SetsTheFloatAndScalesRevenueCostAndPenaltiesOnly()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var relaxed = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(1),
                Airline.Player("Easy", "#1F3A93"), difficulty: CareerDifficulty.Relaxed);
            var demanding = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(1),
                Airline.Player("Hard", "#1F3A93"), difficulty: CareerDifficulty.Demanding);
            Assert.That(relaxed.CareerState.Funds, Is.EqualTo(Difficulty.Relaxed.StartingFunds));
            Assert.That(demanding.CareerState.Funds, Is.EqualTo(Difficulty.Demanding.StartingFunds));

            var kgc = HudTestAirline.Code("KGC");
            var plain = RouteForecast.For(DestinationCatalogue.Adelaide, kgc, AircraftType.Saab340);
            var easy = relaxed.Forecast(DestinationCatalogue.Adelaide, kgc, AircraftType.Saab340);
            var hard = demanding.Forecast(DestinationCatalogue.Adelaide, kgc, AircraftType.Saab340);
            Assert.That(easy.Revenue, Is.GreaterThan(plain.Revenue));
            Assert.That(easy.Cost, Is.LessThan(plain.Cost));
            Assert.That(hard.Revenue, Is.LessThan(plain.Revenue));
            Assert.That(hard.Cost, Is.GreaterThan(plain.Cost));
            Assert.That(relaxed.DispatchCost(AircraftType.Saab340, 125), Is.EqualTo(easy.Cost));

            Assert.That(Difficulty.Relaxed.ScalePenalty(-1), Is.EqualTo(0), "Relaxed forgives a small lateness");
            Assert.That(Difficulty.Relaxed.ScalePenalty(-2), Is.EqualTo(-1));
            Assert.That(Difficulty.Standard.ScalePenalty(-2), Is.EqualTo(-2));
            Assert.That(Difficulty.Demanding.ScalePenalty(-1), Is.EqualTo(-2));
            Assert.That(Difficulty.Demanding.ScalePenalty(1), Is.EqualTo(1), "gains are never scaled");
        }

        [Test]
        public void CoachingOff_SkipsTheFirstFlightGuide()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var coached = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(1), Airline.Player("A", "#1F3A93"));
            var solo = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(1), Airline.Player("B", "#1F3A93"),
                firstFlightCoaching: false);
            Assert.That(FirstFlightGuide.For(coached, out _), Is.EqualTo(GuideStep.PlanFirstFlight));
            Assert.That(FirstFlightGuide.For(solo, out _), Is.EqualTo(GuideStep.Complete));
        }

        [Test]
        public void Save_V14KeepsDifficultyCodeAndCoachingAndOlderSavesPlayStandard()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(1),
                Airline.Player("Gulf Link", "#0F8B8D", "GLK"), difficulty: CareerDifficulty.Demanding,
                firstFlightCoaching: false);
            var saved = AirlineSave.Capture(ops);
            Assert.That(saved.Version, Is.EqualTo(14));
            var restored = AirlineSave.Restore(saved, clock);
            Assert.That(restored.CareerState.Difficulty, Is.EqualTo(CareerDifficulty.Demanding));
            Assert.That(restored.PlayerAirline.Code, Is.EqualTo("GLK"));
            Assert.That(restored.FirstFlightCoaching, Is.False);
            Assert.That(FlightNumber.AirlineCode(restored.PlayerAirline), Is.EqualTo("GLK"));

            saved.Version = 13;
            var legacy = AirlineSave.Restore(saved, clock);
            Assert.That(legacy.CareerState.Difficulty, Is.EqualTo(CareerDifficulty.Standard));
            Assert.That(legacy.PlayerAirline.Code, Is.Null, "pre-v14 codes are derived from the name");
            Assert.That(legacy.FirstFlightCoaching, Is.True);

            saved.Version = 14;
            saved.Difficulty = "Nightmare";
            Assert.Throws<FormatException>(() => AirlineSave.Restore(saved, clock));
        }

        [Test]
        public void FlightManual_QuotesTheRulesTheGameRuns()
        {
            string All(string id) => string.Join(" ", FlightManual.Pages[FlightManual.IndexOf(id)].Sections
                .Select(s => s.Heading + " " + s.Body));
            Assert.That(All("reliability"), Does.Contain($"every {Maintenance.IntervalRotations} rotations"));
            Assert.That(All("growing"), Does.Contain($"{(int)Math.Round(AirlineOperations.ResaleFraction * 100)}%"));
            Assert.That(All("career"), Does.Contain($"{CareerRoadmap.FinalFleet} aircraft"));
            Assert.That(All("career"), Does.Contain($"{CareerRoadmap.FinalDestinations} destinations"));
            Assert.That(All("growing"), Does.Contain("After 12 hand-planned flights"));
            Assert.That(All(FlightManual.ControlsPageId), Does.Contain("F1"));
            Assert.That(FlightManual.Pages.Select(p => p.Id).Distinct().Count(), Is.EqualTo(FlightManual.Pages.Count));
        }

        [Test]
        public void FlightManual_FitsAndNavigates()
        {
            foreach (var (width, height) in HudTestAirline.Viewports)
            for (var page = 0; page < FlightManual.Pages.Count; page++)
            {
                var panel = FlightManualPainter.Panel(width, height);
                var draw = new HudDrawList();
                FlightManualPainter.Paint(draw, panel, page);
                foreach (var c in draw.Commands)
                {
                    Assert.That(c.Box.Right, Is.LessThanOrEqualTo(panel.Right + 0.5f), $"{width}x{height} p{page} '{c.Text}'");
                    Assert.That(c.Box.Bottom, Is.LessThanOrEqualTo(panel.Bottom + 0.5f), $"{width}x{height} p{page} '{c.Text}'");
                }
                Assert.That(draw.Commands.Any(c => c.ActionId == HudAction.Close), Is.True);
            }
            Assert.That(FlightManualPainter.Apply(FlightManualPainter.Next, 0), Is.EqualTo(1));
            Assert.That(FlightManualPainter.Apply(FlightManualPainter.Previous, 0), Is.EqualTo(0));
            Assert.That(FlightManualPainter.Apply(HudAction.Close, 3), Is.EqualTo(-1));
            Assert.That(FlightManualPainter.Apply(FlightManualPainter.PagePrefix + "2", 0), Is.EqualTo(2));
        }
    }
}
