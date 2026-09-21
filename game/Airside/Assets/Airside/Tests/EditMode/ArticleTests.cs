using Airside.Domain;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class ArticleTests
    {
        [Test]
        public void TypeAndPlaceNames_TakeTheArticleTheyAreSaidWith()
        {
            Assert.That(Article.A(AircraftCatalogue.Atr42.Type.Name), Is.EqualTo("an ATR 42-600"));
            Assert.That(Article.A(AircraftCatalogue.AirbusA321Neo.Type.Name), Does.StartWith("an Airbus"));
            Assert.That(Article.A(AircraftCatalogue.Saab340.Type.Name), Is.EqualTo("a Saab 340B"));
            Assert.That(Article.A("Alice Springs"), Is.EqualTo("an Alice Springs"));
            Assert.That(Article.A("Kingscote"), Is.EqualTo("a Kingscote"));
            Assert.That(Article.CapitalA(AircraftCatalogue.Atr42.Type.Name), Is.EqualTo("An ATR 42-600"));
            Assert.That(Article.CapitalA(AircraftCatalogue.Boeing7378.Type.Name), Does.StartWith("A Boeing"));
            Assert.That(Article.A(null), Is.EqualTo("a "));
        }
    }
}
