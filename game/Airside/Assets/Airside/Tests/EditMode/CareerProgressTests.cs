using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// CareerProgress.NextTier: a real answer to "what do I still need for the next tier",
    /// reading the exact same thresholds AirlineCareerState.EvaluateTier checks.
    /// </summary>
    public sealed class CareerProgressTests
    {
        private static AirlineCareerState Fresh() => new();

        [Test]
        public void FreshCareer_NeedsRotationsAndReliabilityForRegional()
        {
            var career = Fresh();
            var next = CareerProgress.NextTier(career, new[] { AircraftType.Atr42 });

            Assert.That(next.Tier, Is.EqualTo(OperatingTier.Regional));
            Assert.That(next.IsMaxTier, Is.False);
            Assert.That(next.RotationsRemaining, Is.EqualTo(AirlineCareerState.RegionalRotations));
            Assert.That(next.ReliabilityRemaining, Is.EqualTo(0), "starting reliability (100) already clears Regional's floor");
            Assert.That(next.AllMet, Is.False);
        }

        [Test]
        public void FreshCareer_NextAircraftIsTheAtrWithFundsAndRotationShortfalls()
        {
            var career = Fresh();
            var next = CareerProgress.NextAircraft(career, ownedCount: 1);

            Assert.That(next.HasOffer, Is.True);
            Assert.That(next.Offer.Type.Id, Is.EqualTo(AircraftType.Atr42.Id));
            Assert.That(next.ReadyToBuy, Is.False);
            Assert.That(next.FundsShort, Is.EqualTo(AircraftAcquisition.Atr42.Price - AirlineCareerState.StartingFunds));
            Assert.That(next.RotationsShort, Is.EqualTo(AircraftAcquisition.Atr42.RequiredRotations));
            Assert.That(next.ReliabilityShort, Is.EqualTo(0));
            Assert.That(next.NeedsTier, Is.False);
            Assert.That(next.BaseRequirementLine, Does.Contain("expand your Adelaide base"));
        }

        [Test]
        public void NextAircraft_ReadyWhenEveryGateClears()
        {
            var career = new AirlineCareerState(funds: 20_000, reliability: 80, tier: OperatingTier.Provisional,
                completedPlayerRotations: 6, baseLevel: PlayerBaseLevel.ExpandedRegional);
            var next = CareerProgress.NextAircraft(career, ownedCount: 1);
            Assert.That(next.ReadyToBuy, Is.True);
            Assert.That(next.Offer.Type.Id, Is.EqualTo(AircraftType.Atr42.Id));
        }

        [Test]
        public void NextAircraft_ReportsFleetFull()
        {
            var next = CareerProgress.NextAircraft(Fresh(), ownedCount: AircraftAcquisition.MaxPlayerAircraft);
            Assert.That(next.FleetFull, Is.True);
            Assert.That(next.HasOffer, Is.False);
        }

        [Test]
        public void RegionalTier_FlagsAMissingQualifyingAircraftForDomestic()
        {
            var career = new AirlineCareerState(reliability: 100, tier: OperatingTier.Regional,
                completedPlayerRotations: AirlineCareerState.DomesticRotations);
            var next = CareerProgress.NextTier(career, new[] { AircraftType.Atr42, AircraftType.Saab340 });

            Assert.That(next.Tier, Is.EqualTo(OperatingTier.Domestic));
            Assert.That(next.RotationsRemaining, Is.EqualTo(0));
            Assert.That(next.ReliabilityRemaining, Is.EqualTo(0));
            Assert.That(next.MissingAircraftLine, Is.Not.Empty, "neither owned type qualifies for Domestic");
            Assert.That(next.AllMet, Is.False);

            var withDash = CareerProgress.NextTier(career, new[] { AircraftType.Dash8Q400 });
            Assert.That(withDash.MissingAircraftLine, Is.Empty);
            Assert.That(withDash.AllMet, Is.True, "rotations, reliability and aircraft all satisfied");
        }

        [Test]
        public void InternationalTier_ReportsMaxTierReached()
        {
            var career = new AirlineCareerState(tier: OperatingTier.International);
            var next = CareerProgress.NextTier(career, new AircraftType[0]);
            Assert.That(next.IsMaxTier, Is.True);
        }

        [Test]
        public void NullCareer_ReportsMaxTierRatherThanThrowing()
        {
            Assert.That(CareerProgress.NextTier(null, null).IsMaxTier, Is.True);
        }
    }
}
