using System.Linq;
using Airside.Domain;
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

        [Test]
        public void AutoTakeoffToken_AndRank_PreferTakingOffJet()
        {
            Assert.That(ReviewAircraftFollow.IsAutoTakeoffToken("auto-takeoff"), Is.True);
            Assert.That(ReviewAircraftFollow.IsAutoFollowToken("auto-takeoff"), Is.True);
            var takingOffJet = ReviewAircraftFollow.AutoTakeoffRank(FleetState.TakingOff, hasView: true, preferJet: true);
            var holdingProp = ReviewAircraftFollow.AutoTakeoffRank(FleetState.HoldingShort, hasView: true, preferJet: false);
            Assert.That(takingOffJet, Is.LessThan(holdingProp));
            Assert.That(
                ReviewAircraftFollow.AutoTakeoffRank(FleetState.AtStand, hasView: true, preferJet: true),
                Is.LessThan(0));
        }

        [Test]
        public void NewGame_OpeningBankNeedsAFewMinutesBeforeAutoLandingHasADrawnCandidate()
        {
            // Soak / capture-game run live wall-clock. Opening AI inbound #1 joins the circuit
            // at ~3 minutes (ADR 0100), so a 90s still cannot follow an arrival.
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(20260913),
                Airline.Player("Soak Air", "#6A3FA0"));

            Assert.That(BestDrawnAutoLandingRank(ops, clock.Now), Is.LessThan(0),
                "at T+0 every opening arrival is still Inbound/off-map");

            clock.Set(new SimulationTime(90));
            ops.Update();
            Assert.That(BestDrawnAutoLandingRank(ops, clock.Now), Is.LessThan(0),
                "90s is still before the first opening inbound reaches the circuit");

            var foundAt = -1L;
            for (var t = 180L; t <= 12 * 60; t += 15)
            {
                clock.Set(new SimulationTime(t));
                ops.Update();
                if (BestDrawnAutoLandingRank(ops, clock.Now) >= 0)
                {
                    foundAt = t;
                    break;
                }
            }

            Assert.That(foundAt, Is.GreaterThan(0),
                "by ~12 minutes an opening arrival should be HoldingForLanding or Landing");
            Assert.That(foundAt, Is.GreaterThanOrEqualTo(180),
                "first drawn arrival is not earlier than the opening bank's first circuit join");
            Assert.That(ops.Fleet.Any(a =>
                    a.State is FleetState.HoldingForLanding or FleetState.Landing),
                Is.True);
        }

        /// <summary>
        /// Presentation only follows aircraft with a field view. <see cref="FleetVisual.Visible"/>
        /// is true for HoldingForLanding / Landing; bare Inbound is Hidden until arrival-final.
        /// </summary>
        private static int BestDrawnAutoLandingRank(AirlineOperations ops, SimulationTime now)
        {
            var best = -1;
            foreach (var aircraft in ops.Fleet)
            {
                var hasView = FleetVisual.For(aircraft, now).Visible;
                var preferJet = AirlineOperations.NeedsTerminalGate(aircraft.Type);
                var rank = ReviewAircraftFollow.AutoLandingRank(aircraft.State, hasView, preferJet);
                if (rank >= 0 && (best < 0 || rank < best))
                    best = rank;
            }

            return best;
        }
    }
}
