using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// CareerProgress.NextAircraft: the next step up the fleet ladder, reading the same gates
    /// AirlineOperations.BuyAircraft enforces. Tier progress lives in CareerRoadmap alone.
    /// </summary>
    public sealed class CareerProgressTests
    {
        private static AirlineCareerState Fresh() => new();

        [Test]
        public void FreshCareer_NextAircraftIsTheAtrWithFundsAndRotationShortfalls()
        {
            var career = Fresh();
            var next = CareerProgress.NextAircraft(career, ownedCount: 1, new[] { AircraftType.Saab340 });

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
        public void NextAircraft_MovesPastTypesAlreadyOwned()
        {
            var career = new AirlineCareerState(funds: 1_000, tier: OperatingTier.Regional,
                completedPlayerRotations: 6, baseLevel: PlayerBaseLevel.JetGate);
            var next = CareerProgress.NextAircraft(career, ownedCount: 2,
                new[] { AircraftType.Saab340, AircraftType.Atr42 });
            Assert.That(next.Offer.Type.Id, Is.EqualTo(AircraftType.Dash8Q400.Id),
                "once an ATR is in the fleet the card must point at the Dash 8, not the ATR forever");
        }

        [Test]
        public void NextAircraft_ReportsFleetFull()
        {
            var next = CareerProgress.NextAircraft(Fresh(), ownedCount: AircraftAcquisition.MaxPlayerAircraft);
            Assert.That(next.FleetFull, Is.True);
            Assert.That(next.HasOffer, Is.False);
        }
    }
}
