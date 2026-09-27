using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// ADR 0122 painters: the title screen, the Career track and the selected-aircraft card. Every
    /// command stays on its surface and every control carries an action the runtime dispatches.
    /// </summary>
    public sealed class GlassCockpitPainterTests
    {
        private static void AssertInside(HudDrawList draw, HudBox area, string label)
        {
            foreach (var c in draw.Commands)
            {
                if (c.Kind is HudDrawKind.Line or HudDrawKind.Image or HudDrawKind.Gradient)
                    continue;
                Assert.That(c.Box.X, Is.GreaterThanOrEqualTo(area.X - 0.5f), $"{label}: {c.Kind} '{c.Text}' off the left");
                Assert.That(c.Box.Y, Is.GreaterThanOrEqualTo(area.Y - 0.5f), $"{label}: {c.Kind} '{c.Text}' off the top");
                Assert.That(c.Box.Right, Is.LessThanOrEqualTo(area.Right + 0.5f), $"{label}: {c.Kind} '{c.Text}' off the right");
                Assert.That(c.Box.Bottom, Is.LessThanOrEqualTo(area.Bottom + 0.5f), $"{label}: {c.Kind} '{c.Text}' off the bottom");
                if (c.Kind is HudDrawKind.Button or HudDrawKind.Hotspot)
                    Assert.That(c.ActionId, Is.Not.Empty, $"{label}: a control with no action");
            }
        }

        private static SplashModel Model(SplashStep step, bool hasSave)
        {
            var model = new SplashModel
            {
                Step = step, HasSave = hasSave, SaveName = "Southern Cross Regional", SaveTier = "Regional",
                SaveSummary = "4 aircraft · $23,400 · 91% reliability", SavedWhen = "Saved 26 Sep 14:05",
                ClockText = "14:05"
            };
            return model;
        }

        [Test]
        public void Splash_FitsEveryWindowWithItsActions()
        {
            foreach (var (width, height) in HudTestAirline.Viewports)
            foreach (var step in new[] { SplashStep.Menu, SplashStep.NewAirline })
            foreach (var hasSave in new[] { false, true })
            {
                var draw = new HudDrawList();
                var layout = SplashLayout.Create(width, height, step, hasSave);
                SplashPainter.Paint(draw, layout, Model(step, hasSave));
                AssertInside(draw, new HudBox(0f, 0f, width, height), $"{width}x{height} {step} save={hasSave}");
                Assert.That(layout.Card.Bottom, Is.LessThanOrEqualTo(height), $"{width}x{height}");
                var actions = draw.Commands.Select(c => c.ActionId).ToList();
                if (step == SplashStep.Menu)
                {
                    Assert.That(actions, Does.Contain(SplashPainter.NewAirline));
                    Assert.That(actions, Does.Contain(SplashPainter.HowToPlay));
                    Assert.That(actions, Does.Contain(SplashPainter.Quit));
                    Assert.That(actions.Contains(SplashPainter.Continue), Is.EqualTo(hasSave));
                }
                else
                {
                    Assert.That(actions, Does.Contain(AirlineSetupPainter.Next));
                    Assert.That(actions, Does.Contain(AirlineSetupPainter.Back));
                    Assert.That(layout.Setup.NameField.Y, Is.GreaterThan(layout.Card.Y));
                    Assert.That(layout.Setup.CodeField.Bottom, Is.LessThan(layout.Card.Bottom));
                }
            }
        }

        [Test]
        public void Splash_UsesTheApprovedDawnIllustration()
        {
            var draw = new HudDrawList();
            SplashPainter.Paint(draw, SplashLayout.Create(1440f, 900f, SplashStep.Menu, true), Model(SplashStep.Menu, true));
            Assert.That(draw.Commands.Any(c => c.Kind == HudDrawKind.Image && c.Text == SplashLayout.SplashArt), Is.True);
            Assert.That(draw.Commands.Any(c => c.Kind == HudDrawKind.Image && c.Text == SplashLayout.WordmarkArt), Is.True);
        }

        [Test]
        public void CareerTrack_ShowsTheCurrentStageGoalsAndPinsOpenOnes()
        {
            var (_, ops, _) = HudTestAirline.Create();
            var model = new CareerTrackModel();
            model.Rebuild(ops);
            Assert.That(model.Nodes.Count, Is.EqualTo(5));
            Assert.That(model.Nodes.Count(n => n.State == CareerTrackNodeState.Current), Is.EqualTo(1));
            Assert.That(model.CurrentGoals.All(g => g.Stage == OperatingTier.Provisional), Is.True);
            Assert.That(model.CurrentCaption, Does.StartWith("TOWARD REGIONAL"));

            foreach (var (width, height) in HudTestAirline.Viewports)
            {
                var surface = HudShell.WorkspaceSurface(width, height);
                var layout = CareerTrackLayout.Create(surface, model.CurrentGoals.Count);
                var draw = new HudDrawList();
                CareerTrackPainter.Paint(draw, model, layout);
                AssertInside(draw, surface, $"{width}x{height}");
            }

            var desktop = new HudDrawList();
            CareerTrackPainter.Paint(desktop, model,
                CareerTrackLayout.Create(HudShell.WorkspaceSurface(1440f, 900f), model.CurrentGoals.Count));
            var open = model.CurrentGoals.Where(g => !g.Complete && g.Id != model.PinnedGoalId).ToList();
            foreach (var goal in open)
                Assert.That(desktop.Commands.Any(c => c.ActionId == HudAction.PinGoal(goal.Id)), Is.True, goal.Id);
        }

        [Test]
        public void SelectionCard_OffersOneActionOrTheStandChoices()
        {
            var card = new SelectionCardData
            {
                Registration = "VH-PAX", TypeName = "Saab 340B", RouteLine = "Adelaide → Kingscote",
                LiveLine = "Parked", PhaseLabel = "catering 40%", IsPlayer = true, PrimaryLabel = "View plan", CanCancel = true
            };
            card.Prep.Add(new SelectionPrepStage("Fuel", 1f, false));
            card.Prep.Add(new SelectionPrepStage("Catering", 0.4f, true));
            card.Prep.Add(new SelectionPrepStage("Baggage", 0f, false));
            card.Prep.Add(new SelectionPrepStage("Boarding", 0f, false));
            var slot = HudShell.Layout(1440f, 900f).SelectedCard;
            var box = slot.SliceBottom(SelectionCardPainter.HeightFor(card));
            var draw = new HudDrawList();
            SelectionCardPainter.Paint(draw, box, card);
            AssertInside(draw, box, "booked");
            Assert.That(draw.Commands.Count(c => c.ActionId == HudAction.Primary), Is.EqualTo(1));
            Assert.That(draw.Commands.Count(c => c.ActionId == HudAction.Cancel), Is.EqualTo(1));

            card.AwaitingStand = true;
            card.Prep.Clear();
            card.Stands.Add(new SelectionStandChoice("50A", "50A", true));
            card.Stands.Add(new SelectionStandChoice("50B", "50B", false));
            card.Stands.Add(new SelectionStandChoice("50C", "50C", false));
            box = slot.SliceBottom(SelectionCardPainter.HeightFor(card));
            draw.Clear();
            SelectionCardPainter.Paint(draw, box, card);
            AssertInside(draw, box, "landed");
            Assert.That(draw.Commands.Count(c => c.ActionId.StartsWith(HudAction.StandPrefix)), Is.EqualTo(3));
            Assert.That(draw.Commands.Any(c => c.ActionId == HudAction.Primary), Is.False);
        }

        [Test]
        public void ToastQueue_KeepsEachMessagesTone()
        {
            var queue = new ToastQueue();
            queue.Push("Regional operating tier reached.", 0f, HudTone.Caution);
            Assert.That(queue.History[0].Tone, Is.EqualTo(HudTone.Caution));
        }
    }
}
