using System;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Economy v2 bank loan (ADR 2026-10-09-economy-v2-real-dollar-scale), save version 26.</summary>
    public sealed class LoanTests
    {
        private const long Day = 24 * 3600;

        private static (ManualSimulationClock clock, AirlineOperations ops) Start()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(5), Airline.Player("Loan Air", "#245AA6"));
            ops.Update();
            return (clock, ops);
        }

        [Test]
        public void Borrowing_AddsCashAndDebtUpToTheTierCap()
        {
            var (_, ops) = Start();
            var funds = ops.CareerState.Funds;
            var cap = FlightCostModel.LoanCap(OperatingTier.Provisional);

            Assert.That(ops.TakeLoan(500_000).Accepted, Is.True);
            Assert.That(ops.CareerState.Loan, Is.EqualTo(500_000));
            Assert.That(ops.CareerState.Funds, Is.EqualTo(funds + 500_000));

            Assert.That(ops.TakeLoan(cap).Accepted, Is.True, "an over-ask is clamped to the headroom, not refused");
            Assert.That(ops.CareerState.Loan, Is.EqualTo(cap));
            var refused = ops.TakeLoan(1);
            Assert.That(refused.Accepted, Is.False);
            Assert.That(refused.Reason, Does.Contain("most the bank allows"));
        }

        [Test]
        public void Repaying_ReducesDebtFromCashOnly()
        {
            var (_, ops) = Start();
            ops.TakeLoan(1_000_000);
            var funds = ops.CareerState.Funds;

            Assert.That(ops.RepayLoan(400_000).Accepted, Is.True);
            Assert.That(ops.CareerState.Loan, Is.EqualTo(600_000));
            Assert.That(ops.CareerState.Funds, Is.EqualTo(funds - 400_000));

            Assert.That(ops.RepayLoan(50_000_000).Accepted, Is.True, "an over-repay clears the debt and no more");
            Assert.That(ops.CareerState.Loan, Is.EqualTo(0));
            Assert.That(ops.RepayLoan(1).Accepted, Is.False, "nothing left to repay");
        }

        [Test]
        public void InterestIsChargedDailyWithLeases()
        {
            var (clock, ops) = Start();
            ops.TakeLoan(1_000_000);
            var funds = ops.CareerState.Funds;
            var standing = ops.DailyStandingCost();
            var interest = (long)Math.Round(FlightCostModel.LoanInterestPerDay(1_000_000));

            clock.Set(new SimulationTime(Day));
            ops.Update();

            Assert.That(ops.CareerState.Funds, Is.EqualTo(funds - standing - interest));
            Assert.That(ops.CareerState.Loan, Is.EqualTo(1_000_000), "interest paid from cash does not grow the balance");
        }

        [Test]
        public void UnpayableInterest_GrowsTheDebtInsteadOfLockingTheAirline()
        {
            var (clock, ops) = Start();
            var cap = FlightCostModel.LoanCap(OperatingTier.Provisional);
            ops.TakeLoan(cap);
            ops.RestoreCareerState(0, 100, nameof(OperatingTier.Provisional), null, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), 0);
            ops.CareerState.RestoreLoan(cap);
            ops.RestoreStandingPaidDay(0);

            clock.Set(new SimulationTime(Day));
            ops.Update();

            Assert.That(ops.CareerState.Funds, Is.GreaterThanOrEqualTo(0));
            Assert.That(ops.CareerState.Loan, Is.GreaterThan(cap), "unpaid interest is added to the balance");
            Assert.That(ops.TakeLoan(1).Accepted, Is.False, "over the cap: no new borrowing until it is paid down");
        }

        [Test]
        public void TheLoan_SurvivesSaveAndLoad()
        {
            var (clock, ops) = Start();
            ops.TakeLoan(750_000);

            var data = AirlineSave.Capture(ops);
            Assert.That(data.Version, Is.EqualTo(AirlineSaveData.CurrentVersion));
            Assert.That(data.CareerLoan, Is.EqualTo(750_000));
            var restored = AirlineSave.Restore(data, new ManualSimulationClock(clock.Now));

            Assert.That(restored.CareerState.Loan, Is.EqualTo(750_000));
        }

        [Test]
        public void AnOlderSave_HasNoLoan()
        {
            var (clock, ops) = Start();
            var data = AirlineSave.Capture(ops);
            data.Version = 25;
            data.CareerLoan = 999_999;

            var restored = AirlineSave.Restore(data, new ManualSimulationClock(clock.Now));

            Assert.That(restored.CareerState.Loan, Is.EqualTo(0));
        }

        [Test]
        public void CommandsRefuseCleanlyWithoutAnAmount()
        {
            var (_, ops) = Start();
            Assert.That(ops.TakeLoan(0).Accepted, Is.False);
            Assert.That(ops.TakeLoan(-5).Accepted, Is.False);
            Assert.That(ops.RepayLoan(0).Accepted, Is.False);
        }
    }
}
