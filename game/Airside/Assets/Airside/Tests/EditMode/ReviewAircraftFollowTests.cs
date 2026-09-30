using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests.EditMode
{
    public sealed class ReviewAircraftFollowTests
    {
        [Test]
        public void AutoLandingToken_IsRecognisedCaseInsensitively()
        {
            Assert.That(ReviewAircraftFollow.IsAutoLandingToken("auto-landing"), Is.True);
            Assert.That(ReviewAircraftFollow.IsAutoLandingToken("Auto-Landing"), Is.True);
            Assert.That(ReviewAircraftFollow.IsAutoLandingToken("VH-PAX"), Is.False);
            Assert.That(ReviewAircraftFollow.IsAutoLandingToken(null), Is.False);
            Assert.That(ReviewAircraftFollow.IsAutoLandingToken(""), Is.False);
        }

        [Test]
        public void AutoLandingRank_PrefersLandingJetOverHoldingTurboprop()
        {
            var landingJet = ReviewAircraftFollow.AutoLandingRank(FleetState.Landing, hasView: true, preferJet: true);
            var holdingProp = ReviewAircraftFollow.AutoLandingRank(FleetState.HoldingForLanding, hasView: true, preferJet: false);
            Assert.That(landingJet, Is.LessThan(holdingProp));
            Assert.That(landingJet, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void AutoLandingRank_RequiresAView()
        {
            Assert.That(
                ReviewAircraftFollow.AutoLandingRank(FleetState.Landing, hasView: false, preferJet: true),
                Is.LessThan(0));
        }

        [Test]
        public void AutoLandingRank_IgnoresParkedAircraft()
        {
            Assert.That(
                ReviewAircraftFollow.AutoLandingRank(FleetState.AtStand, hasView: true, preferJet: true),
                Is.LessThan(0));
        }
    }
}
