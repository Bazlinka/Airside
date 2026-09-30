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

        [Test]
        public void NewGame_OpeningArrivalReachesLandingWithinAutoLandingCaptureWindow()
        {
            // Auto-landing re-ranks every frame; the still delay must reach FleetState.Landing
            // (flare / tyre contact), not stop at the first drawn HoldingForLanding/Inbound.
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(20260913),
                Airline.Player("Soak Air", "#6A3FA0"));

            var drawnAt = -1L;
            var landingAt = -1L;
            for (var t = 180L; t <= 15 * 60; t += 15)
            {
                clock.Set(new SimulationTime(t));
                ops.Update();
                if (drawnAt < 0 && BestDrawnAutoLandingRank(ops, clock.Now) >= 0)
                    drawnAt = t;
                if (landingAt < 0 && ops.Fleet.Any(a => a.State == FleetState.Landing))
                {
                    landingAt = t;
                    break;
                }
            }

            Assert.That(drawnAt, Is.GreaterThan(0), "opening arrival should become followable");
            Assert.That(landingAt, Is.GreaterThan(0), "opening arrival should enter Landing");
            Assert.That(landingAt, Is.GreaterThanOrEqualTo(drawnAt),
                "Landing is at or after the first drawn auto-landing candidate");
            // Packaged remaining.sh landing stills wait ~360s live so the upgrade can land.
            Assert.That(landingAt, Is.LessThanOrEqualTo(8 * 60),
                "first Landing should arrive within the remaining.sh auto-landing capture window");
        }

        [Test]
        public void NewGame_OpeningDepartureReachesTakingOffWithinAutoTakeoffCaptureWindow()
        {
            // Auto-takeoff re-ranks every frame; the still delay must reach FleetState.TakingOff
            // (lineup / roll — tyre rotation), not stop at the first TaxiOut/HoldingShort view.
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(20260913),
                Airline.Player("Soak Air", "#6A3FA0"));

            var drawnAt = -1L;
            var takingOffAt = -1L;
            for (var t = 60L; t <= 15 * 60; t += 15)
            {
                clock.Set(new SimulationTime(t));
                ops.Update();
                if (drawnAt < 0 && BestDrawnAutoTakeoffRank(ops, clock.Now) >= 0)
                    drawnAt = t;
                if (takingOffAt < 0 && ops.Fleet.Any(a => a.State == FleetState.TakingOff))
                {
                    takingOffAt = t;
                    break;
                }
            }

            Assert.That(drawnAt, Is.GreaterThan(0), "opening departure should become followable");
            Assert.That(takingOffAt, Is.GreaterThan(0), "opening departure should enter TakingOff");
            Assert.That(takingOffAt, Is.GreaterThanOrEqualTo(drawnAt),
                "TakingOff is at or after the first drawn auto-takeoff candidate");
            // Opening departures reach TaxiOut/HoldingShort early, but TakingOff (roll / tyre
            // rotation) is much later — remaining.sh waits ~830s live (mid first TakingOff window).
            Assert.That(takingOffAt, Is.LessThanOrEqualTo(15 * 60),
                "first TakingOff should arrive within the remaining.sh auto-takeoff capture window");
            Assert.That(takingOffAt, Is.GreaterThan(8 * 60),
                "TakingOff is after the early landing window — do not reuse auto-landing delay");
        }

        [Test]
        public void PackagedAutoTakeoffDelay_SelectsTakingOffNotHoldingShort()
        {
            // remaining.sh follow-jet-takeoff CAPTURE_DELAY — must land mid TakingOff, not after
            // the first roll ends (900s was HoldingShort on soak seed 20260913).
            const long delaySeconds = 830;
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(20260913),
                Airline.Player("Soak Air", "#6A3FA0"));
            clock.Set(new SimulationTime(delaySeconds));
            ops.Update();
            var pick = BestAutoTakeoff(ops, clock.Now);
            Assert.That(pick, Is.Not.Null, "auto-takeoff should have a drawn candidate at capture delay");
            Assert.That(pick.State, Is.EqualTo(FleetState.TakingOff),
                $"packaged delay {delaySeconds}s must follow TakingOff (tyre roll), not {pick.State}");
        }

        [TestCase(780)]
        [TestCase(783)]
        [TestCase(786)]
        public void PackagedAutoLandingDelay_SelectsJetLanding(long delaySeconds)
        {
            // remaining.sh landing batch: day@780 / close@783 / storm@786 — 360s was turboprop.
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(20260913),
                Airline.Player("Soak Air", "#6A3FA0"));
            clock.Set(new SimulationTime(delaySeconds));
            ops.Update();
            var pick = BestAutoLanding(ops, clock.Now);
            Assert.That(pick, Is.Not.Null, "auto-landing should have a drawn candidate at capture delay");
            Assert.That(pick.State, Is.EqualTo(FleetState.Landing),
                $"packaged delay {delaySeconds}s must follow Landing, not {pick.State}");
            Assert.That(AirlineOperations.NeedsTerminalGate(pick.Type), Is.True,
                $"follow-jet stills need a jet at delay {delaySeconds}s (got {pick.Registration} {pick.Type.Id})");
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

        private static int BestDrawnAutoTakeoffRank(AirlineOperations ops, SimulationTime now)
        {
            var best = -1;
            foreach (var aircraft in ops.Fleet)
            {
                var hasView = FleetVisual.For(aircraft, now).Visible;
                var preferJet = AirlineOperations.NeedsTerminalGate(aircraft.Type);
                var rank = ReviewAircraftFollow.AutoTakeoffRank(aircraft.State, hasView, preferJet);
                if (rank >= 0 && (best < 0 || rank < best))
                    best = rank;
            }

            return best;
        }

        private static FleetAircraft BestAutoTakeoff(AirlineOperations ops, SimulationTime now)
        {
            FleetAircraft best = null;
            var bestRank = int.MaxValue;
            foreach (var aircraft in ops.Fleet)
            {
                var hasView = FleetVisual.For(aircraft, now).Visible;
                var preferJet = AirlineOperations.NeedsTerminalGate(aircraft.Type);
                var rank = ReviewAircraftFollow.AutoTakeoffRank(aircraft.State, hasView, preferJet);
                if (rank >= 0 && rank < bestRank)
                {
                    bestRank = rank;
                    best = aircraft;
                }
            }

            return best;
        }

        private static FleetAircraft BestAutoLanding(AirlineOperations ops, SimulationTime now)
        {
            FleetAircraft best = null;
            var bestRank = int.MaxValue;
            foreach (var aircraft in ops.Fleet)
            {
                var hasView = FleetVisual.For(aircraft, now).Visible;
                var preferJet = AirlineOperations.NeedsTerminalGate(aircraft.Type);
                var rank = ReviewAircraftFollow.AutoLandingRank(aircraft.State, hasView, preferJet);
                if (rank >= 0 && rank < bestRank)
                {
                    bestRank = rank;
                    best = aircraft;
                }
            }

            return best;
        }
    }
}
