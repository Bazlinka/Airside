using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Save version 24 (ADR 2026-10-09-economy-v2-real-dollar-scale): older saves are converted once, current ones never.</summary>
    public sealed class EconomyMigrationTests
    {
        private static (ManualSimulationClock clock, AirlineSaveData data) CaptureFresh()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(7), Airline.Player("Gulf Air Link", "#2E7D32"));
            return (clock, AirlineSave.Capture(ops));
        }

        [Test]
        public void ANewSave_IsWrittenAtTheCurrentVersion()
        {
            var (_, data) = CaptureFresh();
            Assert.That(data.Version, Is.EqualTo(AirlineSaveData.CurrentVersion));
            Assert.That(AirlineSaveData.CurrentVersion, Is.GreaterThanOrEqualTo(24));
        }

        [Test]
        public void ACurrentSave_KeepsItsMoneyExactly()
        {
            var (clock, data) = CaptureFresh();
            data.CareerFunds = 1_234_567;
            data.CareerLifetimeRevenue = 89_000;
            var restored = AirlineSave.Restore(data, new ManualSimulationClock(clock.Now));
            Assert.That(restored.CareerState.Funds, Is.EqualTo(1_234_567));
            Assert.That(restored.CareerState.LifetimeRevenue, Is.EqualTo(89_000));
        }

        [Test]
        public void AVersion23Save_HasItsMoneyScaledOnce()
        {
            var (clock, data) = CaptureFresh();
            const long scale = FlightCostModel.LegacySaveMoneyScale;
            data.Version = 23;
            data.CareerFunds = 2_800;
            data.CareerLifetimeRevenue = 1_000;
            foreach (var aircraft in data.Fleet)
                aircraft.LifetimeRevenue = 100;

            var restored = AirlineSave.Restore(data, new ManualSimulationClock(clock.Now));

            Assert.That(restored.CareerState.Funds, Is.EqualTo(2_800 * scale));
            Assert.That(restored.CareerState.LifetimeRevenue, Is.EqualTo(1_000 * scale));
            var mine = restored.FleetOf(restored.PlayerAirline).First();
            Assert.That(mine.LifetimeRevenue, Is.EqualTo(100 * scale));
        }

        [Test]
        public void AVersion23Save_AfterMigration_CapturesAsACurrentSave()
        {
            var (clock, data) = CaptureFresh();
            data.Version = 23;
            data.CareerFunds = 2_800;
            var restored = AirlineSave.Restore(data, new ManualSimulationClock(clock.Now));

            var again = AirlineSave.Capture(restored);

            Assert.That(again.Version, Is.EqualTo(AirlineSaveData.CurrentVersion));
            var second = AirlineSave.Restore(again, new ManualSimulationClock(clock.Now));
            Assert.That(second.CareerState.Funds, Is.EqualTo(2_800 * FlightCostModel.LegacySaveMoneyScale),
                "saving and loading the migrated game must not scale it again");
        }

        [Test]
        public void TheLegacyScaleKeepsTheOpeningFloatInProportionToTheFirstStepUp()
        {
            // A Standard career that still had its opening float (2,800 against a 5,200 ATR 42) keeps roughly the same
            // standing against the new ATR 42 lease deposit: within a factor of two.
            var ratioBefore = 2_800.0 / 5_200.0;
            var ratioAfter = 2_800.0 * FlightCostModel.LegacySaveMoneyScale / AircraftAcquisition.Atr42.Price;
            Assert.That(ratioAfter / ratioBefore, Is.InRange(0.5, 2.0));
        }
    }
}
