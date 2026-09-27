using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0132 — counting funds, flipping board tiles and the celebration card.</summary>
    public sealed class HudMomentsTests
    {
        [Test]
        public void FundsTicker_CountsToTheNewValueAndSettles()
        {
            var ticker = new FundsTicker();
            Assert.That(ticker.Show(1_000, 0f), Is.EqualTo(1_000), "first sight: no count");
            Assert.That(ticker.Direction, Is.Zero);

            var early = ticker.Show(3_000, 1f);
            Assert.That(early, Is.EqualTo(1_000), "the count starts where it was");
            var mid = ticker.Show(3_000, 1f + FundsTicker.Seconds * 0.5f);
            Assert.That(mid, Is.GreaterThan(1_000).And.LessThan(3_000));
            Assert.That(ticker.Direction, Is.EqualTo(1));
            Assert.That(ticker.Show(3_000, 1f + FundsTicker.Seconds + 0.01f), Is.EqualTo(3_000));
            Assert.That(ticker.Direction, Is.Zero);

            ticker.Show(500, 10f);
            ticker.Show(500, 10f + FundsTicker.Seconds * 0.3f);
            Assert.That(ticker.Direction, Is.EqualTo(-1), "spending counts down");
        }

        [Test]
        public void FlapAnimator_SpinsThenSettlesLeftToRight()
        {
            Assert.That(FlapAnimator.Frame("BOARDING", "BOARDING", 0f), Is.EqualTo("BOARDING"));
            Assert.That(FlapAnimator.Frame("FINAL CALL", "DEPARTED", FlapAnimator.Seconds), Is.EqualTo("DEPARTED"));
            var mid = FlapAnimator.Frame("FINAL CALL", "DEPARTED", FlapAnimator.Seconds * 0.5f);
            Assert.That(mid, Does.StartWith("DEPA"), "the left half has settled");
            Assert.That(mid, Is.Not.EqualTo("DEPARTED"));

            var board = new FlapBoardState();
            Assert.That(board.Display("SA1|remarks", "BOARDING", 0f), Is.EqualTo("BOARDING"), "no flip on first sight");
            Assert.That(board.Display("SA1|remarks", "FINAL CALL", 5f), Is.Not.EqualTo("FINAL CALL"), "a change flips");
            Assert.That(board.Display("SA1|remarks", "FINAL CALL", 5f + FlapAnimator.Seconds), Is.EqualTo("FINAL CALL"));
        }

        [Test]
        public void TierCards_NameWhatTheTierOpensAndFitTheirPanel()
        {
            foreach (var tier in new[] { OperatingTier.Regional, OperatingTier.Domestic, OperatingTier.International })
            {
                var card = CelebrationCard.ForTier(tier, "Southern Cross Regional");
                Assert.That(card.Title, Is.EqualTo($"{tier} tier!"));
                Assert.That(card.Lines, Is.Not.Empty, tier.ToString());
                AssertFits(card);
            }

            var domestic = CelebrationCard.ForTier(OperatingTier.Domestic, "Soak Air");
            Assert.That(domestic.Lines[0], Does.StartWith("New aircraft: E190"));
            Assert.That(domestic.ImagePath, Does.StartWith("UI/Aircraft/"));

            AssertFits(CelebrationCard.ForContract("Kingscote charter", 4_200, 4, "UI/Aircraft/thb_air_sf34_v01.png"));
            AssertFits(CelebrationCard.ForFinale("Southern Cross Regional", 18, 12, 140));
        }

        private static void AssertFits(CelebrationCard card)
        {
            foreach (var (width, height) in HudTestAirline.Viewports)
            {
                var panel = CelebrationPainter.Panel(width, height);
                var draw = new HudDrawList();
                CelebrationPainter.Paint(draw, panel, card, 1f);
                Assert.That(draw.Commands.Count(c => c.ActionId == CelebrationPainter.Close), Is.EqualTo(1));
                foreach (var text in draw.Commands.Where(c => c.Kind == HudDrawKind.Text))
                    Assert.That(HudShell.Measure(text.Text, text.FontSize), Is.LessThanOrEqualTo(text.Box.Width + 1f),
                        $"{width}x{height} '{text.Text}'");
                foreach (var line in card.Lines)
                    Assert.That(draw.Commands.Any(c => c.Text == line), Is.True, $"{width}x{height} lost '{line}'");
            }
        }
    }
}
