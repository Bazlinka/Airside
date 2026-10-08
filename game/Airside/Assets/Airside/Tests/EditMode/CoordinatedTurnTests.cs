using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class CoordinatedTurnTests
    {
        [Test]
        public void RateOneTurn_BanksByAirspeedUpToTheLimit()
        {
            // 3 deg/s at 140 m/s (about 270 kt) would need about 36 degrees, so it is held at the limit.
            Assert.AreEqual(-CoordinatedTurn.MaxBankDegrees, CoordinatedTurn.BankDegrees(140f, 3f), 0.01f);
            // 3 deg/s at 70 m/s (about 136 kt): about 20 degrees.
            Assert.AreEqual(-20.5f, CoordinatedTurn.BankDegrees(70f, 3f), 0.6f);
        }

        [Test]
        public void LeftTurnBanksOppositeToRight_AndStraightIsLevel()
        {
            Assert.Greater(CoordinatedTurn.BankDegrees(70f, -2f), 0f);
            Assert.Less(CoordinatedTurn.BankDegrees(70f, 2f), 0f);
            Assert.AreEqual(0f, CoordinatedTurn.BankDegrees(70f, 0f), 1e-4f);
        }

        [Test]
        public void SlowerAircraftBankLessForTheSameTurnRate()
        {
            Assert.Less(System.Math.Abs(CoordinatedTurn.BankDegrees(40f, 2f)), System.Math.Abs(CoordinatedTurn.BankDegrees(100f, 2f)));
        }

        [Test]
        public void DeltaAngle_WrapsAcrossNorth()
        {
            Assert.AreEqual(20f, CoordinatedTurn.DeltaAngle(350f, 10f), 1e-3f);
            Assert.AreEqual(-20f, CoordinatedTurn.DeltaAngle(10f, 350f), 1e-3f);
        }
    }
}
