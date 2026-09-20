using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// The stroke-painted glyph alphabet (ADR 0068): every digit plus the specific letters
    /// real Adelaide stand/gate references use (A-G, L, R), extending the runway designation
    /// numerals' own 7-segment system rather than a second, different one.
    /// </summary>
    public sealed class AirsideStripMarkingsTests
    {
        private const string SupportedGlyphs = "0123456789ABCDEFGLR";

        private static int GlyphMask(char glyph)
        {
            var method = typeof(AirsideStripMarkings).GetMethod("GlyphMask",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "AirsideStripMarkings.GlyphMask must exist");
            return (int)method!.Invoke(null, new object[] { glyph });
        }

        [Test]
        public void EveryRealStandReferenceCharacterHasASupportedGlyph()
        {
            // Exact letters/digits AdelaideLayout's bay and gate references actually use
            // (e.g. "50D", "18L", "10A") — not a guess at what a stand label alphabet needs.
            foreach (var glyph in SupportedGlyphs)
                Assert.That(GlyphMask(glyph), Is.Not.Zero, $"'{glyph}' must paint at least one stroke");
        }

        [Test]
        public void NoTwoSupportedGlyphsPaintIdentically()
        {
            var masks = SupportedGlyphs.Select(GlyphMask).ToList();
            Assert.That(masks.Distinct().Count(), Is.EqualTo(masks.Count),
                "two different characters sharing a mask would be indistinguishable when painted");
        }

        [Test]
        public void RunwayDigitsKeepTheirOriginalStandardSevenSegmentShapes()
        {
            // 0,1,2,3,5 were already authored for 05/23/12/30 before this pass — must not move.
            Assert.That(GlyphMask('0'), Is.EqualTo(1 | 2 | 4 | 8 | 16 | 32));
            Assert.That(GlyphMask('1'), Is.EqualTo(2 | 4));
            Assert.That(GlyphMask('2'), Is.EqualTo(1 | 2 | 8 | 16 | 64));
            Assert.That(GlyphMask('3'), Is.EqualTo(1 | 2 | 4 | 8 | 64));
            Assert.That(GlyphMask('5'), Is.EqualTo(1 | 4 | 8 | 32 | 64));
        }

        [Test]
        public void UnsupportedCharactersPaintNothingRatherThanGarbage()
        {
            foreach (var glyph in new[] { ' ', '-', 'Z', 'Q', '#' })
                Assert.That(GlyphMask(glyph), Is.Zero, $"'{glyph}' is not an authored glyph");
        }

        [Test]
        public void DesignationNumerals_StillPaintsTheRealRunwayEndsUnchanged()
        {
            var marks = AirsideStripMarkings.DesignationNumerals(3000f, "05", "23");
            Assert.That(marks.Length, Is.GreaterThan(0));
            Assert.That(marks.All(m => m.LengthX > 0f && m.WidthZ > 0f), Is.True,
                "every stroke must be a real rectangle, not a degenerate zero-size mark");
        }

        [Test]
        public void Label_PaintsEveryRealStandReferenceWithNoDegenerateStrokes()
        {
            // Real references from AdelaideLayout: digits and letters together, mixed lengths.
            foreach (var reference in new[] { "50D", "18L", "10A", "2A", "28R" })
            {
                var marks = AirsideStripMarkings.Label(reference, baselineAlong: 0f, alongDirection: 1f,
                    centerAcross: 100f);
                Assert.That(marks, Is.Not.Empty, reference);
                Assert.That(marks.All(m => m.LengthX > 0f && m.WidthZ > 0f), Is.True, reference);
            }
        }

        [Test]
        public void Label_CentresTheCharacterCellsOnTheRequestedAcrossPosition()
        {
            // "8" and "0" paint on both the near and far edge of their cell, so the ink
            // bounding box is a true proxy for the cell layout's own centring here — an
            // asymmetric glyph like "L" (paints only its bottom-left) would not be, since its
            // own ink does not reach every edge of its cell.
            var marks = AirsideStripMarkings.Label("808", baselineAlong: 0f, alongDirection: 1f, centerAcross: 100f);
            Assert.That(marks, Is.Not.Empty);
            var minZ = marks.Min(m => m.MinZ);
            var maxZ = marks.Max(m => m.MaxZ);
            Assert.That((minZ + maxZ) * 0.5f, Is.EqualTo(100f).Within(0.1f));
        }

        [Test]
        public void Label_EmptyOrNullTextPaintsNothing()
        {
            Assert.That(AirsideStripMarkings.Label(string.Empty, 0f, 1f, 0f), Is.Empty);
            Assert.That(AirsideStripMarkings.Label(null, 0f, 1f, 0f), Is.Empty);
        }
    }
}
