using System;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Economy v2 daily lease and insurance (ADR 2026-10-09-economy-v2-real-dollar-scale), save version 25.</summary>
    public sealed class StandingCostTests
    {
        private const long Day = 24 * 3600;

        private static (ManualSimulationClock clock, AirlineOperations ops) Start()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(5), Airline.Player("Standing Air", "#245AA6"));
            ops.Update(); // observe the first day: nothing is charged for it
            return (clock, ops);
        }

        private static void Lease(AirlineOperations ops, AircraftType type)
        {
            ops.RestoreCareerState(20_000 * FlightCostModel.LegacySaveMoneyScale, 100, nameof(OperatingTier.Domestic), null, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), 60, baseLevel: PlayerBaseLevel.JetGate);
            var result = ops.BuyAircraft(type);
            Assert.That(result.Accepted, Is.True, result.Reason);
        }

        private static void Advance(ManualSimulationClock clock, AirlineOperations ops, long seconds)
        {
            clock.Set(new SimulationTime(clock.Now.ElapsedSeconds + seconds));
            ops.Update();
        }

        [Test]
        public void TheFoundingSaab_PaysInsuranceOnly()
        {
            var (_, ops) = Start();
            var insurance = (long)Math.Round(FlightCostModel.InsurancePerDay(AircraftType.Saab340));
            Assert.That(ops.DailyStandingCost(), Is.EqualTo(insurance));
            Assert.That(insurance, Is.GreaterThan(0));
        }

        [Test]
        public void ALeasedAircraft_AddsLeaseAndInsurance()
        {
            var (_, ops) = Start();
            var before = ops.DailyStandingCost();
            Lease(ops, AircraftType.Atr42);
            var added = ops.DailyStandingCost() - before;
            Assert.That(added, Is.EqualTo((long)Math.Round(FlightCostModel.StandingPerDay(AircraftType.Atr42)))
                .Within(1));
            Assert.That(added, Is.GreaterThan(FlightCostModel.InsurancePerDay(AircraftType.Atr42)),
                "a leased aircraft pays lease as well as insurance");
        }

        [Test]
        public void EachDayCrossed_ChargesOnce()
        {
            var (clock, ops) = Start();
            var funds = ops.CareerState.Funds;
            var daily = ops.DailyStandingCost();

            Advance(clock, ops, Day);
            Assert.That(ops.CareerState.Funds, Is.EqualTo(funds - daily));

            ops.Update(); // the same day again
            Assert.That(ops.CareerState.Funds, Is.EqualTo(funds - daily), "no second charge within a day");
        }

        [Test]
        public void ALongAbsence_PaysEveryMissedDayInOneStep()
        {
            var (clock, ops) = Start();
            var funds = ops.CareerState.Funds;
            var daily = ops.DailyStandingCost();

            Advance(clock, ops, 2 * Day);

            Assert.That(ops.CareerState.Funds, Is.EqualTo(funds - 2 * daily));
        }

        [Test]
        public void SteppingTheClockInPieces_ChargesTheSameAsOneJump()
        {
            var (clockA, opsA) = Start();
            var (clockB, opsB) = Start();
            Advance(clockA, opsA, 2 * Day);
            for (var i = 0; i < 4; i++)
                Advance(clockB, opsB, Day / 2);
            Assert.That(opsB.CareerState.Funds, Is.EqualTo(opsA.CareerState.Funds));
        }

        [Test]
        public void ACashShortfall_IsCoveredByTheBankAndNeverOverdraws()
        {
            var (clock, ops) = Start();
            Lease(ops, AircraftType.Dash8Q400);
            ops.RestoreCareerState(1_000, 100, nameof(OperatingTier.Provisional), null, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), 0);
            ops.RestoreStandingPaidDay(0);

            Advance(clock, ops, 2 * Day);

            Assert.That(ops.CareerState.Funds, Is.GreaterThanOrEqualTo(0), "cash never goes negative");
            Assert.That(ops.CareerState.Loan, Is.GreaterThan(0), "the bank covered what cash could not");
            var told = false;
            while (ops.TryTakeCareerEvent(out var news))
                told |= news.Text.Contains("borrowed");
            Assert.That(told, Is.True, "the player is told the bank stepped in");
        }

        [Test]
        public void TheLedger_SurvivesSaveAndLoad_WithoutChargingTwice()
        {
            var (clock, ops) = Start();
            Advance(clock, ops, Day);
            var funds = ops.CareerState.Funds;

            var data = AirlineSave.Capture(ops);
            Assert.That(data.Version, Is.EqualTo(AirlineSaveData.CurrentVersion));
            Assert.That(data.HasStandingPaidDay, Is.True);
            var restored = AirlineSave.Restore(data, new ManualSimulationClock(clock.Now));
            restored.Update();

            Assert.That(restored.CareerState.Funds, Is.EqualTo(funds), "loading mid-day must not pay the day again");
        }

        [Test]
        public void AnOlderSave_IsNotChargedRetroactively()
        {
            var (clock, ops) = Start();
            Advance(clock, ops, 2 * Day);
            var data = AirlineSave.Capture(ops);
            data.Version = 24;
            data.HasStandingPaidDay = false;
            data.StandingPaidThroughDay = 0;
            var funds = data.CareerFunds;

            var restored = AirlineSave.Restore(data, new ManualSimulationClock(clock.Now));
            restored.Update();

            Assert.That(restored.CareerState.Funds, Is.EqualTo(funds),
                "days that passed before the lease costs existed are not billed");
        }
    }
}
