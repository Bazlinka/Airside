using Airside.Domain;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// Fuselage title and registration sizing. The bug this guards against: Unity renders a
    /// TextMesh at ten font pixels per world unit, so at font size 64 a line is 6.4x its
    /// character size tall — sizing anything around the text in character-size units makes
    /// it a fraction of the size it should be.
    /// </summary>
    public sealed class AircraftTitlePaintTests
    {
        [Test]
        public void LineHeight_IsFontSizeTimesCharacterSizeOverTenPixelsPerMetre()
        {
            Assert.That(AircraftTitlePaint.LineHeightMetres(0.22f), Is.EqualTo(1.408f).Within(0.0005f));
            Assert.That(AircraftTitlePaint.LineHeightMetres(0.13f), Is.EqualTo(0.832f).Within(0.0005f));
            Assert.That(AircraftTitlePaint.LineHeightMetres(0.22f),
                Is.GreaterThan(0.22f * 6f),
                "a line is several times its character size — this is the factor that was missed");
        }

        [Test]
        public void TitleWidth_GrowsWithTheNameAndTheCharacterSize()
        {
            var shortName = AircraftTitlePaint.WidthMetres("REX", 0.13f);
            var longName = AircraftTitlePaint.WidthMetres("SOUTHERN CROSS REGIONAL", 0.13f);
            Assert.That(longName, Is.GreaterThan(shortName * 5f));
            Assert.That(AircraftTitlePaint.WidthMetres("REX", 0.26f),
                Is.EqualTo(shortName * 2f).Within(0.001f));
            Assert.That(AircraftTitlePaint.WidthMetres(null, 0.13f), Is.EqualTo(0f));
        }

        [Test]
        public void ALongAirlineNameIsShrunkToFitTheFuselage()
        {
            const string name = "SOUTHERN CROSS REGIONAL AIRLINES";
            var authored = 0.13f;
            var budget = AircraftTitlePaint.TitleLengthBudgetMetres(AircraftType.Atr42);
            var fitted = AircraftTitlePaint.OperatorCharacterSize(AircraftType.Atr42, name, authored);

            Assert.That(fitted, Is.LessThan(authored), "it does not fit at the authored size");
            Assert.That(AircraftTitlePaint.WidthMetres(name, fitted), Is.LessThanOrEqualTo(budget + 0.001f));
        }

        [Test]
        public void AShortAirlineNameKeepsTheAuthoredSize()
        {
            const float authored = 0.22f;
            Assert.That(AircraftTitlePaint.OperatorCharacterSize(AircraftType.Boeing7378, "REX", authored),
                Is.EqualTo(authored));
        }

        [Test]
        public void TheTitleBudgetComesFromTheTypesRealFuselageLength()
        {
            var atr = AircraftTitlePaint.TitleLengthBudgetMetres(AircraftType.Atr42);
            var widebody = AircraftTitlePaint.TitleLengthBudgetMetres(AircraftType.AirbusA350900);
            Assert.That(widebody, Is.GreaterThan(atr * 2f));
            Assert.That(atr, Is.EqualTo(
                (float)AircraftCatalogue.For(AircraftType.Atr42).LengthMetres
                * AircraftTitlePaint.TitleLengthFraction).Within(0.001f));
        }

        [Test]
        public void EveryTypesAuthoredTitleFitsItsOwnFuselage()
        {
            // Airside's own default airline name, at the size each type authored for it.
            const string name = "SOUTHERN CROSS REGIONAL";
            foreach (var spec in AircraftCatalogue.All)
            {
                var layout = AircraftIdentityMarkings.For(spec.Type);
                var fitted = AircraftTitlePaint.OperatorCharacterSize(spec.Type, name,
                    layout.OperatorCharacterSize);
                Assert.That(AircraftTitlePaint.WidthMetres(name, fitted),
                    Is.LessThanOrEqualTo(AircraftTitlePaint.TitleLengthBudgetMetres(spec.Type) + 0.001f),
                    spec.Name);
                Assert.That(AircraftTitlePaint.LineHeightMetres(fitted), Is.GreaterThan(0.2f),
                    $"{spec.Name}: titles must stay legible at overview distance");
            }
        }
    }
}
