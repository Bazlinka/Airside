using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AdelaideTreeLodTests
    {
        [Test]
        public void Detail_IsFullNearFieldMediumMidAndBillboardFar()
        {
            Assert.That(AdelaideTreeLod.ForDistance(500f), Is.EqualTo(AdelaideTreeLod.Detail.Full));
            Assert.That(AdelaideTreeLod.ForDistance(AdelaideTreeLod.FullRangeMetres),
                Is.EqualTo(AdelaideTreeLod.Detail.Full));
            Assert.That(AdelaideTreeLod.ForDistance(AdelaideTreeLod.FullRangeMetres + 1f),
                Is.EqualTo(AdelaideTreeLod.Detail.Medium));
            Assert.That(AdelaideTreeLod.ForDistance(AdelaideTreeLod.MediumRangeMetres),
                Is.EqualTo(AdelaideTreeLod.Detail.Medium));
            Assert.That(AdelaideTreeLod.ForDistance(AdelaideTreeLod.MediumRangeMetres + 1f),
                Is.EqualTo(AdelaideTreeLod.Detail.Billboard));
        }

        [Test]
        public void Lobes_FullHasThreeMediumHasOneBillboardHasNone()
        {
            Assert.That(AdelaideTreeLod.LobesForDetail(100f, 200f, AdelaideTreeLod.Detail.Full).Length,
                Is.EqualTo(3));
            Assert.That(AdelaideTreeLod.LobesForDetail(100f, 200f, AdelaideTreeLod.Detail.Medium).Length,
                Is.EqualTo(1));
            Assert.That(AdelaideTreeLod.LobesForDetail(100f, 200f, AdelaideTreeLod.Detail.Billboard).Length,
                Is.EqualTo(0));
        }

        [Test]
        public void CrownBudget_DropsWithDistance()
        {
            Assert.That(AdelaideTreeLod.MaxCrownTriangles(AdelaideTreeLod.Detail.Full),
                Is.GreaterThan(AdelaideTreeLod.MaxCrownTriangles(AdelaideTreeLod.Detail.Medium)));
            Assert.That(AdelaideTreeLod.MaxCrownTriangles(AdelaideTreeLod.Detail.Medium),
                Is.GreaterThan(AdelaideTreeLod.MaxCrownTriangles(AdelaideTreeLod.Detail.Billboard)));
            Assert.That(
                AdelaideTreeGeometry.CrownTriangleCount(
                    AdelaideTreeLod.LobesForDetail(10f, 10f, AdelaideTreeLod.Detail.Medium)),
                Is.LessThanOrEqualTo(AdelaideTreeLod.MaxCrownTriangles(AdelaideTreeLod.Detail.Medium)));
        }

        [Test]
        public void ForPosition_UsesHorizontalDistanceFromArp()
        {
            Assert.That(AdelaideTreeLod.ForPosition(100f, 100f), Is.EqualTo(AdelaideTreeLod.Detail.Full));
            Assert.That(AdelaideTreeLod.ForPosition(3000f, 0f), Is.EqualTo(AdelaideTreeLod.Detail.Billboard));
        }
    }
}
