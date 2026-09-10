using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AerodromeAtcTests
    {
        [SetUp]
        public void SetUp() => TaxiLoopFixture.EnableFullTaxiLoop();

        [TearDown]
        public void TearDown() => TaxiLoopFixture.RestoreCircuit();

        [Test]
        public void LandingClearance_IsLoggedWithTowerPhraseology()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(42), new ReservationTable());

            for (var second = 1; second <= 25; second++)
            {
                clock.Advance(1);
                simulation.Update();
                if (simulation.ActiveAircraft.Phase == AircraftPhase.Landing)
                    break;
            }

            Assert.That(simulation.ActiveAircraft.Phase, Is.EqualTo(AircraftPhase.Landing));
            Assert.That(simulation.Atc.ActiveClearance, Is.EqualTo(AtcClearance.ClearedToLand));
            Assert.That(simulation.Atc.LastInstruction, Does.Contain("cleared to land runway 09"));
            Assert.That(HasAtcEvent(simulation, "cleared to land"), Is.True);
        }

        [Test]
        public void Departure_YieldsWhenArrivalNeedsTheRunway()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(24031996), new ReservationTable());

            for (var second = 1; second <= 20000 && simulation.Flights.Count < 2; second++)
            {
                clock.Advance(1);
                simulation.Update();
                if (simulation.Routes.Pending != null)
                    simulation.AcceptPendingRoute();
            }

            Assert.That(simulation.Flights.Count, Is.EqualTo(2), "need dual commercials for arrival-priority check");

            var sawHoldForArrival = false;
            for (var second = 1; second <= 4000; second++)
            {
                clock.Advance(1);
                simulation.Update();
                if (simulation.Routes.Pending != null)
                    simulation.DeclinePendingRoute();

                CommercialFlight departure = null;
                CommercialFlight arrival = null;
                foreach (var flight in simulation.Flights)
                {
                    if (flight.Operation.Phase == AircraftPhase.TaxiOut
                        && flight.Operation.SecondsRemaining(clock.Now) <= 0)
                        departure = flight;
                    if (flight.Operation.Phase is AircraftPhase.Approach or AircraftPhase.Landing)
                        arrival = flight;
                }

                if (departure == null || arrival == null)
                    continue;

                if (arrival.Operation.Phase is AircraftPhase.Approach or AircraftPhase.Landing
                    && (simulation.Atc.LastInstruction.IndexOf("arrival on approach", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || simulation.Atc.LastInstruction.IndexOf("landing on the runway", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || simulation.Atc.LastInstruction.IndexOf("hold short runway", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || simulation.Atc.ActiveClearance is AtcClearance.HoldShortRunway
                            or AtcClearance.TrafficAdvisory
                            ))
                {
                    Assert.That(departure.Operation.Phase, Is.EqualTo(AircraftPhase.TaxiOut));
                    sawHoldForArrival = true;
                    break;
                }
            }

            Assert.That(sawHoldForArrival, Is.True, "departure should hold short while an arrival needs the runway");
            Assert.That(simulation.ReservationConflicts, Is.Zero);
        }

        [Test]
        public void RunwaySeparation_BlocksImmediateReclearance()
        {
            var atc = new AerodromeAtc();
            var vacated = new SimulationTime(100);
            atc.NotifyRunwayVacated(vacated);

            Assert.That(atc.RunwaySeparationOpen(new SimulationTime(100)), Is.False);
            Assert.That(atc.RunwaySeparationOpen(new SimulationTime(111)), Is.False);
            Assert.That(atc.RunwaySeparationOpen(new SimulationTime(112)), Is.True);
            Assert.That(atc.SeparationRemainingSeconds(new SimulationTime(105)), Is.EqualTo(7));
        }

        [Test]
        public void GroundTraffic_OutboundStillFollowsLeadInChord()
        {
            var table = new ReservationTable();
            var traffic = new GroundTrafficAircraft(new StableId("GT-201"), table, GroundTrafficRole.ArriveDepart, 0);
            var monitor = new TrafficWaitMonitor();

            // Warm into the stand, then advance past dwell onto the outbound lead-in leg.
            for (var second = 1; second <= 120; second++)
            {
                traffic.Reposition(new SimulationTime(second), monitor, AirportSimulation.StandOne,
                    AirportCapacity.BaselineStands, true);
                if (traffic.CurrentPhase.StartsWith("Taxi out from", System.StringComparison.Ordinal))
                {
                    // Mid-leg should still be near the lead-in X corridor (not cutting to junction X=-12).
                    Assert.That(traffic.Position.X, Is.GreaterThan(7f));
                    return;
                }
            }

            Assert.Fail("never reached outbound lead-in leg");
        }

        [Test]
        public void LineUpAndWait_IssuesWhenSeparationAlmostOpen()
        {
            var atc = new AerodromeAtc();
            atc.NotifyRunwayVacated(new SimulationTime(100));
            // Separation opens at t=112; at t=109 remaining is 3s — line-up window.
            Assert.That(atc.SeparationRemainingSeconds(new SimulationTime(109)), Is.EqualTo(3));
            Assert.That(atc.IssueLineUpAndWait("AS-101"), Does.Contain("line up and wait"));
            Assert.That(atc.IssueLineUpAndWait("AS-101"), Does.Contain("holding point Alpha"));
            Assert.That(atc.IssueLineUpAndWait("AS-101"), Does.Contain("surface wind calm"));
            Assert.That(atc.IssueLineUpAndWait("AS-101"), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueLineUpAndWait("AS-101"), Does.Contain("hold position on the runway"));
            Assert.That(atc.IssueLineUpAndWait("AS-101"), Does.Contain("report ready for departure"));
            Assert.That(atc.IssueLineUpAndWait("AS-101"), Does.Contain("acknowledge"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.LineUpAndWait));
            Assert.That(atc.IssueLineUpAndWait("AS-101", behindLanding: true), Does.Contain("behind the landing"));
            Assert.That(atc.IssueLineUpAndWait("AS-101", traffic: "on short final"), Does.Contain("traffic on short final"));
            Assert.That(atc.IssueHoldShortRunway("AS-101", "runway occupied", "on short final"),
                Does.Contain("traffic on short final"));
            Assert.That(atc.IssueHoldShortRunway("AS-101", "runway occupied", "on short final"),
                Does.Contain("hold short runway 09"));
            Assert.That(atc.IssueHoldShortRunway("AS-101", "runway occupied", "on short final"),
                Does.Contain("holding point Alpha"));
            Assert.That(atc.IssueHoldShortRunway("AS-101", "runway occupied", "on short final"),
                Does.Contain("QNH 1013"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.HoldShortRunway));
        }

        [Test]
        public void ExpectLandingClearance_UsesApproachPhraseNotHoldShort()
        {
            var atc = new AerodromeAtc();
            Assert.That(atc.IssueExpectLandingClearance("AS-101", 7), Does.Contain("expect landing clearance"));
            Assert.That(atc.IssueExpectLandingClearance("AS-101", 7), Does.Contain("continue approach runway 09"));
            Assert.That(atc.IssueExpectLandingClearance("AS-101", 7), Does.Contain("wind calm"));
            Assert.That(atc.IssueExpectLandingClearance("AS-101", 7), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueExpectLandingClearance("AS-101", 7), Does.Contain("report short final"));
            Assert.That(atc.IssueExpectLandingClearance("AS-101", 7), Does.Contain("number one expected"));
            Assert.That(atc.IssueExpectLandingClearance("AS-101", 7), Does.Contain("vacate via Alpha when landed"));
            Assert.That(atc.IssueExpectLandingClearance("AS-101", 7), Does.Not.Contain("hold short"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.ExpectLanding));
            Assert.That(atc.IssueGroundHold("GT-201", "Alpha busy"), Does.Contain("hold position"));
            Assert.That(atc.IssueGroundHold("GT-201", "Alpha busy"), Does.Contain("on Alpha"));
            Assert.That(atc.IssueGroundHold("GT-201", "Alpha busy"), Does.Contain("wind calm"));
            Assert.That(atc.IssueGroundHold("GT-201", "Alpha busy"), Does.Contain("surface wind calm"));
            Assert.That(atc.IssueGroundHold("GT-201", "Alpha busy"), Does.Contain("caution vehicles on the apron"));
            Assert.That(atc.IssueGroundHold("GT-201", "Alpha busy"), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueGroundHold("GT-201", "Alpha busy"), Does.Contain("report when clear to continue via Alpha"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.GroundHold));
        }

        [Test]
        public void ReadyForDepartureAndVacate_UpdatePhraseology()
        {
            var atc = new AerodromeAtc();
            Assert.That(atc.IssueReadyForDeparture("AS-101"), Does.Contain("expect departure clearance"));
            Assert.That(atc.IssueReadyForDeparture("AS-101"), Does.Contain("when number one"));
            Assert.That(atc.IssueReadyForDeparture("AS-101"), Does.Contain("surface wind calm"));
            Assert.That(atc.IssueReadyForDeparture("AS-101"), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueReadyForDeparture("AS-101"), Does.Contain("holding point Alpha"));
            Assert.That(atc.IssueReadyForDeparture("AS-101"), Does.Contain("remain this frequency"));
            Assert.That(atc.IssueReadyForDeparture("AS-101"), Does.Contain("report when number one"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.ReadyForDeparture));
            atc.NotifyRunwayVacated(new SimulationTime(50));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.RunwayVacated));
            Assert.That(atc.LastInstruction, Does.Contain("vacated"));
            Assert.That(atc.SeparationRemainingSeconds(new SimulationTime(50)), Is.EqualTo(12));

            atc.NotifyRunwayVacated(new SimulationTime(80), "AS-101");
            Assert.That(atc.LastInstruction, Does.Contain("via Alpha"));
            Assert.That(atc.LastInstruction, Does.Contain("contact Kingscote Ground"));
            Assert.That(atc.LastInstruction, Does.Contain("expect taxi to stand"));
            Assert.That(atc.LastInstruction, Does.Contain("first available"));
            Assert.That(atc.LastInstruction, Does.Contain("caution wake"));
            Assert.That(atc.LastInstruction, Does.Contain("surface wind calm"));
            Assert.That(atc.LastInstruction, Does.Contain("QNH 1013"));
            Assert.That(atc.LastClearedFlight, Is.EqualTo("AS-101"));
        }

        [Test]
        public void PushbackApproved_IssuesPhrase()
        {
            var atc = new AerodromeAtc();
            Assert.That(atc.IssuePushbackApproved("AS-101"), Does.Contain("pushback approved"));
            Assert.That(atc.IssuePushbackApproved("AS-101"), Does.Contain("face west toward Alpha"));
            Assert.That(atc.IssuePushbackApproved("AS-101"), Does.Contain("jet blast"));
            Assert.That(atc.IssuePushbackApproved("AS-101"), Does.Contain("behind"));
            Assert.That(atc.IssuePushbackApproved("AS-101"), Does.Contain("caution vehicles on the apron"));
            Assert.That(atc.IssuePushbackApproved("AS-101"), Does.Contain("taxi via Alpha"));
            Assert.That(atc.IssuePushbackApproved("AS-101"), Does.Contain("QNH 1013"));
            Assert.That(atc.IssuePushbackApproved("AS-101"), Does.Contain("report when clear of the stand"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.PushbackApproved));
        }

        [Test]
        public void GoAround_PhraseAndRestartApproachClock()
        {
            var atc = new AerodromeAtc();
            Assert.That(atc.IssueGoAround("AS-101", "runway occupied"), Does.Contain("go around"));
            Assert.That(atc.IssueGoAround("AS-101", "runway occupied"), Does.Contain("make left circuit"));
            Assert.That(atc.IssueGoAround("AS-101", "runway occupied"), Does.Contain("circuit height"));
            Assert.That(atc.IssueGoAround("AS-101", "runway occupied"), Does.Contain("1000 ft"));
            Assert.That(atc.IssueGoAround("AS-101", "runway occupied"), Does.Contain("wind calm"));
            Assert.That(atc.IssueGoAround("AS-101", "runway occupied"), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueGoAround("AS-101", "runway occupied"), Does.Contain("mid-downwind then base"));
            Assert.That(atc.IssueGoAround("AS-101", "runway occupied"), Does.Contain("no turns below circuit height"));
            Assert.That(atc.IssueGoAround("AS-101", "runway occupied"), Does.Contain("report airborne on the go-around"));
            Assert.That(atc.IssueGoAround("AS-101", "runway occupied"), Does.Contain("acknowledge"));
            Assert.That(atc.IssueGoAround("AS-101", null), Does.Contain("acknowledge"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.GoAround));

            var op = new AircraftOperation("AS-101", new SimulationTime(0));
            // Drive approach to overdue.
            for (var t = 1; t <= 25; t++)
                op.AdvanceTo(new SimulationTime(t), _ => false);
            Assert.That(op.SecondsRemaining(new SimulationTime(25)), Is.EqualTo(0));
            op.RestartApproach(new SimulationTime(40));
            Assert.That(op.Phase, Is.EqualTo(AircraftPhase.Approach));
            Assert.That(op.SecondsRemaining(new SimulationTime(40)), Is.EqualTo(20));
        }

        [Test]
        public void ApproachNumberTwoWindow_IsPositive()
        {
            Assert.That(AerodromeAtc.ApproachNumberTwoWindowSeconds, Is.GreaterThan(0));
            Assert.That(AerodromeAtc.GoAroundAfterSeconds, Is.GreaterThan(0));
        }

        [Test]
        public void NumberTwoPhrases_AreIssuedForSequencing()
        {
            var atc = new AerodromeAtc();
            Assert.That(atc.IssueNumberTwoForLanding("AS-102"), Does.Contain("number two"));
            Assert.That(atc.IssueNumberTwoForLanding("AS-102"), Does.Contain("traffic ahead on final"));
            Assert.That(atc.IssueNumberTwoForLanding("AS-102"), Does.Contain("continue approach runway 09"));
            Assert.That(atc.IssueNumberTwoForLanding("AS-102"), Does.Contain("report short final"));
            Assert.That(atc.IssueNumberTwoForLanding("AS-102"), Does.Contain("vacate via Alpha when landed"));
            Assert.That(atc.IssueNumberTwoForLanding("AS-102"), Does.Contain("wind calm"));
            Assert.That(atc.IssueNumberTwoForLanding("AS-102"), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueNumberTwoForLanding("AS-102", "on short final"), Does.Contain("traffic ahead on short final"));
            Assert.That(atc.IssueMinimumApproachSpeed("AS-102", "on base"), Does.Contain("traffic on base"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.MinimumApproachSpeed));
            Assert.That(atc.IssueNumberTwoForDeparture("AS-103"), Does.Contain("number two for departure"));
            Assert.That(atc.IssueNumberTwoForDeparture("AS-103"), Does.Contain("expect further clearance"));
            Assert.That(atc.IssueNumberTwoForDeparture("AS-103"), Does.Contain("traffic on final"));
            Assert.That(atc.IssueNumberTwoForDeparture("AS-103"), Does.Contain("holding point Alpha"));
            Assert.That(atc.IssueNumberTwoForDeparture("AS-103"), Does.Contain("wind calm"));
            Assert.That(atc.IssueNumberTwoForDeparture("AS-103"), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueNumberTwoForDeparture("AS-103"), Does.Contain("report ready when clear"));
            Assert.That(atc.IssueNumberTwoForDeparture("AS-103"), Does.Contain("remain this frequency"));
            Assert.That(atc.IssueNumberTwoForDeparture("AS-103", "on short final"), Does.Contain("traffic on short final"));
            Assert.That(atc.IssueNumberTwoForDeparture("AS-103", "in the circuit"), Does.Contain("traffic in the circuit"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.NumberTwoDeparture));
        }

        [Test]
        public void ClearedToLand_IncludesNumberOneAndWeatherRemark()
        {
            var atc = new AerodromeAtc();
            Assert.That(atc.IssueClearedToLand("AS-101", WeatherKind.Rain), Does.Contain("number one"));
            Assert.That(atc.IssueClearedToLand("AS-101", WeatherKind.Rain), Does.Contain("runway wet"));
            Assert.That(atc.IssueClearedToLand("AS-101", WeatherKind.Rain), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueClearedToLand("AS-101", WeatherKind.Rain), Does.Contain("vacate via Alpha when able"));
            Assert.That(atc.IssueClearedToLand("AS-101", WeatherKind.Rain), Does.Contain("report runway vacated"));
            Assert.That(atc.IssueClearedToLand("AS-101", WeatherKind.Fog), Does.Contain("visibility reduced"));
            Assert.That(atc.IssueClearedToLand("AS-101", WeatherKind.Clear), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueClearedToLand("AS-101", WeatherKind.Clear), Does.Contain("runway is clear"));
            Assert.That(atc.IssueClearedToLand("AS-101", WeatherKind.Clear), Does.Contain("report runway vacated"));
            Assert.That(atc.IssueClearedToLand("AS-101", WeatherKind.Clear), Does.Contain("remain this frequency"));
            Assert.That(atc.IssueClearedForTakeoff("AS-101", WeatherKind.Storm), Does.Contain("thunderstorms"));
            Assert.That(atc.IssueClearedForTakeoff("AS-101", WeatherKind.Clear), Does.Contain("number one"));
            Assert.That(atc.IssueClearedForTakeoff("AS-101", WeatherKind.Clear), Does.Contain("from Alpha"));
            Assert.That(atc.IssueClearedForTakeoff("AS-101", WeatherKind.Clear), Does.Contain("no turns below circuit height"));
            Assert.That(atc.IssueClearedForTakeoff("AS-101", WeatherKind.Clear), Does.Contain("circuit height 1000 ft"));
            Assert.That(atc.IssueClearedForTakeoff("AS-101", WeatherKind.Clear), Does.Contain("report airborne when able"));
            atc.IssueLineUpAndWait("AS-101");
            Assert.That(atc.IssueClearedForTakeoff("AS-101", WeatherKind.Clear), Does.Contain("from line up and wait"));
            Assert.That(atc.IssueClearedForTakeoff("AS-101", WeatherKind.Clear), Does.Contain("from Alpha"));
        }

        [Test]
        public void ReportEstablished_IssuesPhrase()
        {
            var atc = new AerodromeAtc();
            Assert.That(atc.IssueReportEstablished("AS-101"), Does.Contain("report established"));
            Assert.That(atc.IssueReportEstablished("AS-101"), Does.Contain("continue approach"));
            Assert.That(atc.IssueReportEstablished("AS-101"), Does.Contain("number one expected"));
            Assert.That(atc.IssueReportEstablished("AS-101"), Does.Contain("runway is clear"));
            Assert.That(atc.IssueReportEstablished("AS-101"), Does.Contain("expect landing clearance"));
            Assert.That(atc.IssueReportEstablished("AS-101"), Does.Contain("report short final"));
            Assert.That(atc.IssueReportEstablished("AS-101"), Does.Contain("wind calm"));
            Assert.That(atc.IssueReportEstablished("AS-101"), Does.Contain("surface wind calm"));
            Assert.That(atc.IssueReportEstablished("AS-101"), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueReportEstablished("AS-101"), Does.Contain("vacate via Alpha when landed"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.ReportEstablished));
            Assert.That(atc.IssueShortFinal("AS-101"), Does.Contain("short final"));
            Assert.That(atc.IssueShortFinal("AS-101"), Does.Contain("runway is clear"));
            Assert.That(atc.IssueShortFinal("AS-101"), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueShortFinal("AS-101"), Does.Contain("vacate via Alpha when able"));
            Assert.That(atc.IssueShortFinal("AS-101"), Does.Contain("report runway vacated"));
            Assert.That(atc.IssueShortFinal("AS-101"), Does.Contain("number one"));
            Assert.That(atc.IssueShortFinal("AS-101"), Does.Contain("surface wind calm"));
            Assert.That(atc.IssueShortFinal("AS-101"), Does.Contain("cleared to land expected shortly"));
            Assert.That(atc.IssueShortFinal("AS-101"), Does.Contain("acknowledge"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.ShortFinal));
        }

        [Test]
        public void ReportAirborne_IssuesPhrase()
        {
            var atc = new AerodromeAtc();
            Assert.That(atc.IssueReportAirborne("AS-101"), Does.Contain("report airborne when able"));
            Assert.That(atc.IssueReportAirborne("AS-101"), Does.Contain("climb runway heading to circuit height"));
            Assert.That(atc.IssueReportAirborne("AS-101"), Does.Contain("1000 ft"));
            Assert.That(atc.IssueReportAirborne("AS-101"), Does.Contain("no turns below circuit height"));
            Assert.That(atc.IssueReportAirborne("AS-101"), Does.Contain("report frequency change when clear of the circuit"));
            Assert.That(atc.IssueReportAirborne("AS-101"), Does.Contain("wind calm"));
            Assert.That(atc.IssueReportAirborne("AS-101"), Does.Contain("QNH 1013"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.ReportAirborne));
        }

        [Test]
        public void JoinLeftDownwindAndReportBase_IssueCircuitPhrases()
        {
            var atc = new AerodromeAtc();
            Assert.That(atc.IssueJoinLeftDownwind("AS-101"), Does.Contain("join left"));
            Assert.That(atc.IssueJoinLeftDownwind("AS-101"), Does.Contain("Kingscote Tower"));
            Assert.That(atc.IssueJoinLeftDownwind("AS-101"), Does.Contain("make left circuit"));
            Assert.That(atc.IssueJoinLeftDownwind("AS-101"), Does.Contain("circuit height"));
            Assert.That(atc.IssueJoinLeftDownwind("AS-101"), Does.Contain("1000 ft"));
            Assert.That(atc.IssueJoinLeftDownwind("AS-101"), Does.Contain("wind calm"));
            Assert.That(atc.IssueJoinLeftDownwind("AS-101"), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueJoinLeftDownwind("AS-101"), Does.Contain("mid-downwind then base"));
            Assert.That(atc.IssueJoinLeftDownwind("AS-101"), Does.Contain("monitor this frequency"));
            Assert.That(atc.IssueJoinLeftDownwind("AS-101"), Does.Contain("squawk VFR"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.JoinLeftDownwind));
            Assert.That(atc.IssueReportMidDownwind("AS-101"), Does.Contain("report mid-downwind runway 09"));
            Assert.That(atc.IssueReportMidDownwind("AS-101"), Does.Contain("number one expected"));
            Assert.That(atc.IssueReportMidDownwind("AS-101"), Does.Contain("expect base report"));
            Assert.That(atc.IssueReportMidDownwind("AS-101"), Does.Contain("remain this frequency"));
            Assert.That(atc.IssueReportMidDownwind("AS-101"), Does.Contain("look for traffic in the circuit"));
            Assert.That(atc.IssueReportMidDownwind("AS-101"), Does.Contain("surface wind calm"));
            Assert.That(atc.IssueReportMidDownwind("AS-101"), Does.Contain("QNH 1013"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.ReportMidDownwind));
            Assert.That(atc.IssueReportBase("AS-101"), Does.Contain("report turning base runway 09"));
            Assert.That(atc.IssueReportBase("AS-101"), Does.Contain("continue approach"));
            Assert.That(atc.IssueReportBase("AS-101"), Does.Contain("expect further clearance on final"));
            Assert.That(atc.IssueReportBase("AS-101"), Does.Contain("number one expected"));
            Assert.That(atc.IssueReportBase("AS-101"), Does.Contain("wind calm"));
            Assert.That(atc.IssueReportBase("AS-101"), Does.Contain("surface wind calm"));
            Assert.That(atc.IssueReportBase("AS-101"), Does.Contain("vacate via Alpha when landed"));
            Assert.That(atc.IssueReportBase("AS-101"), Does.Contain("QNH 1013"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.ReportBase));
            Assert.That(atc.IssueReportFinal("AS-101"), Does.Contain("report turning final runway 09"));
            Assert.That(atc.IssueReportFinal("AS-101"), Does.Contain("wind calm"));
            Assert.That(atc.IssueReportFinal("AS-101"), Does.Contain("surface wind calm"));
            Assert.That(atc.IssueReportFinal("AS-101"), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueReportFinal("AS-101"), Does.Contain("number one expected"));
            Assert.That(atc.IssueReportFinal("AS-101"), Does.Contain("continue approach"));
            Assert.That(atc.IssueReportFinal("AS-101"), Does.Contain("runway is clear"));
            Assert.That(atc.IssueReportFinal("AS-101"), Does.Contain("expect landing clearance"));
            Assert.That(atc.IssueReportFinal("AS-101"), Does.Contain("vacate via Alpha when landed"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.ReportFinal));
        }

        [Test]
        public void ConditionalLanding_IssuesWhenVacatedPhrase()
        {
            var atc = new AerodromeAtc();
            Assert.That(atc.IssueConditionalLanding("AS-101", 3), Does.Contain("when the runway is vacated"));
            Assert.That(atc.IssueConditionalLanding("AS-101", 3), Does.Contain("vacate via Alpha when able"));
            Assert.That(atc.IssueConditionalLanding("AS-101", 3), Does.Contain("report runway vacated"));
            Assert.That(atc.IssueConditionalLanding("AS-101", 3), Does.Contain("wind calm"));
            Assert.That(atc.IssueConditionalLanding("AS-101", 3), Does.Contain("surface wind calm"));
            Assert.That(atc.IssueConditionalLanding("AS-101", 3), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueConditionalLanding("AS-101", 3), Does.Contain("number one"));
            Assert.That(atc.IssueConditionalLanding("AS-101", 3), Does.Contain("acknowledge"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.ConditionalLanding));
            Assert.That(AerodromeAtc.ConditionalLandingWindowSeconds, Is.EqualTo(5));
            Assert.That(AerodromeAtc.LineUpAndWaitWindowSeconds, Is.EqualTo(6));
        }

        [Test]
        public void OrbitLeftAndRadarContact_IssuePhrases()
        {
            var atc = new AerodromeAtc();
            Assert.That(atc.IssueOrbitLeft("AS-102"), Does.Contain("left orbit"));
            Assert.That(atc.IssueOrbitLeft("AS-102"), Does.Contain("circuit height"));
            Assert.That(atc.IssueOrbitLeft("AS-102"), Does.Contain("1000 ft"));
            Assert.That(atc.IssueOrbitLeft("AS-102"), Does.Contain("wind calm"));
            Assert.That(atc.IssueOrbitLeft("AS-102"), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueOrbitLeft("AS-102"), Does.Contain("report mid-downwind again"));
            Assert.That(atc.IssueOrbitLeft("AS-102"), Does.Contain("acknowledge"));
            Assert.That(atc.IssueOrbitLeft("AS-102", "on final"), Does.Contain("traffic on final"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.OrbitLeft));
            Assert.That(AerodromeAtc.OrbitAfterNumberTwoSeconds, Is.EqualTo(8));
            Assert.That(atc.IssueRadarContact("AS-101"), Does.Contain("radar contact"));
            Assert.That(atc.IssueRadarContact("AS-101"), Does.Contain("identified"));
            Assert.That(atc.IssueRadarContact("AS-101"), Does.Contain("climb runway heading to circuit height"));
            Assert.That(atc.IssueRadarContact("AS-101"), Does.Contain("1000 ft"));
            Assert.That(atc.IssueRadarContact("AS-101"), Does.Contain("leave the circuit when able"));
            Assert.That(atc.IssueRadarContact("AS-101"), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueRadarContact("AS-101"), Does.Contain("squawk VFR"));
            Assert.That(atc.IssueRadarContact("AS-101"), Does.Contain("report frequency change when clear"));
            Assert.That(atc.IssueRadarContact("AS-101"), Does.Not.Contain("report downwind"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.RadarContact));
        }

        [Test]
        public void MinimumApproachSpeedAndHoldShortAlpha_IssuePhrases()
        {
            var atc = new AerodromeAtc();
            Assert.That(atc.IssueMinimumApproachSpeed("AS-102"), Does.Contain("minimum approach speed"));
            Assert.That(atc.IssueMinimumApproachSpeed("AS-102"), Does.Contain("traffic ahead"));
            Assert.That(atc.IssueMinimumApproachSpeed("AS-102"), Does.Contain("wind calm"));
            Assert.That(atc.IssueMinimumApproachSpeed("AS-102"), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueMinimumApproachSpeed("AS-102"), Does.Contain("report base then short final"));
            Assert.That(atc.IssueMinimumApproachSpeed("AS-102"), Does.Contain("expect further clearance"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.MinimumApproachSpeed));
            Assert.That(atc.IssueHoldShortAlpha("AS-101"), Does.Contain("hold short Alpha"));
            Assert.That(atc.IssueHoldShortAlpha("AS-101"), Does.Contain("holding point"));
            Assert.That(atc.IssueHoldShortAlpha("AS-101"), Does.Contain("report when clear to continue via Alpha"));
            Assert.That(atc.IssueHoldShortAlpha("AS-101"), Does.Contain("opposing traffic"));
            Assert.That(atc.IssueHoldShortAlpha("AS-101"), Does.Contain("wind calm"));
            Assert.That(atc.IssueHoldShortAlpha("AS-101"), Does.Contain("surface wind calm"));
            Assert.That(atc.IssueHoldShortAlpha("AS-101"), Does.Contain("caution vehicles on the apron"));
            Assert.That(atc.IssueHoldShortAlpha("AS-101"), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueHoldShortAlpha("AS-101", "AS-102 on Alpha"), Does.Contain("AS-102 on Alpha"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.HoldShortAlpha));
            Assert.That(atc.IssueContinueTaxi("AS-101"), Does.Contain("continue taxi"));
            Assert.That(atc.IssueContinueTaxi("AS-101"), Does.Contain("hold short runway 09"));
            Assert.That(atc.IssueContinueTaxi("AS-101"), Does.Contain("holding point Alpha"));
            Assert.That(atc.IssueContinueTaxi("AS-101"), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueContinueTaxi("AS-101"), Does.Contain("report ready when number one"));
            Assert.That(atc.IssueContinueTaxi("AS-101", afterGiveWay: true), Does.Contain("traffic clear"));
            Assert.That(atc.IssueContinueTaxi("AS-101", afterGiveWay: true), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueContinueTaxi("AS-101", afterGiveWay: true, inbound: true),
                Does.Contain("continue taxi via Alpha to the apron"));
            Assert.That(atc.IssueContinueTaxi("AS-101", afterGiveWay: true, inbound: true),
                Does.Contain("hold short of the stand until marshaller"));
            Assert.That(atc.IssueContinueTaxi("AS-101", afterGiveWay: true, inbound: true),
                Does.Contain("caution vehicles on the apron"));
            Assert.That(atc.IssueContinueTaxi("AS-101", afterGiveWay: true, inbound: true),
                Does.Contain("report on stand when parked"));
            Assert.That(atc.IssueContinueTaxi("AS-101", afterGiveWay: true, inbound: true),
                Does.Not.Contain("hold short runway 09"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.ContinueTaxi));
            Assert.That(atc.IssueGiveWayTaxiing("AS-102"), Does.Contain("give way to taxiing"));
            Assert.That(atc.IssueGiveWayTaxiing("AS-102"), Does.Contain("on Alpha"));
            Assert.That(atc.IssueGiveWayTaxiing("AS-102"), Does.Contain("wind calm"));
            Assert.That(atc.IssueGiveWayTaxiing("AS-102"), Does.Contain("surface wind calm"));
            Assert.That(atc.IssueGiveWayTaxiing("AS-102"), Does.Contain("caution vehicles on the apron"));
            Assert.That(atc.IssueGiveWayTaxiing("AS-102"), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueGiveWayTaxiing("AS-102"), Does.Contain("report when clear to continue via Alpha"));
            Assert.That(atc.IssueGiveWayTaxiing("AS-102", "AS-101 on Alpha"), Does.Contain("AS-101 on Alpha"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.GiveWayTaxiing));
        }

        [Test]
        public void ClearedForTakeoff_AfterLandingPhraseInWakeWindow()
        {
            var atc = new AerodromeAtc();
            atc.NotifyRunwayVacated(new SimulationTime(100));
            var phrase = atc.IssueClearedForTakeoff("AS-101", WeatherKind.Clear, new SimulationTime(114));
            Assert.That(phrase, Does.Contain("after the landing"));
        }

        [Test]
        public void TaxiToStand_ExpeditesWhenRequested()
        {
            var atc = new AerodromeAtc();
            Assert.That(atc.IssueTaxiToStand("AS-101", "Stand 1", expedite: true), Does.Contain("expedite"));
            Assert.That(atc.IssueTaxiToStand("AS-101", "Stand 1", expedite: true), Does.Contain("taxi via Alpha"));
            Assert.That(atc.IssueTaxiToStand("AS-101", "Stand 1", expedite: true), Does.Contain("report on stand when parked"));
            Assert.That(atc.IssueTaxiToStand("AS-101", "Stand 1", expedite: true), Does.Contain("caution vehicles"));
            Assert.That(atc.IssueTaxiToStand("AS-101", "Stand 1", expedite: false), Does.Contain("marshaller"));
            Assert.That(atc.IssueTaxiToStand("AS-101", "Stand 1", expedite: false), Does.Contain("taxi via Alpha"));
            Assert.That(atc.IssueTaxiToStand("AS-101", "Stand 1", expedite: false), Does.Contain("caution vehicles"));
            Assert.That(atc.IssueTaxiToStand("AS-101", "Stand 1", expedite: false), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueTaxiToStand("AS-101", "Stand 1", expedite: false), Does.Contain("surface wind calm"));
            Assert.That(atc.IssueTaxiToStand("AS-101", "Stand 1", expedite: false), Does.Contain("remain this frequency"));
            Assert.That(atc.IssueTaxiToStand("AS-101", "Stand 1", expedite: false), Does.Contain("report on stand when parked"));
        }

        [Test]
        public void ClearedToLand_WarnsWakeSoonAfterSeparationOpens()
        {
            var atc = new AerodromeAtc();
            atc.NotifyRunwayVacated(new SimulationTime(100));
            // Separation opens at t=112; at t=114 wake window still open.
            var phrase = atc.IssueClearedToLand("AS-101", WeatherKind.Clear, new SimulationTime(114));
            Assert.That(phrase, Does.Contain("caution wake turbulence"));
            Assert.That(phrase, Does.Contain("traffic vacating via Alpha"));
            Assert.That(atc.IsWakeCautionActive(new SimulationTime(114)), Is.True);
        }

        [Test]
        public void EngineStart_IssuesBeforePushbackPhrase()
        {
            var atc = new AerodromeAtc();
            Assert.That(atc.IssueEngineStartApproved("AS-101"), Does.Contain("engine start approved"));
            Assert.That(atc.IssueEngineStartApproved("AS-101"), Does.Contain("park brake set"));
            Assert.That(atc.IssueEngineStartApproved("AS-101"), Does.Contain("surface wind calm"));
            Assert.That(atc.IssueEngineStartApproved("AS-101"), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueEngineStartApproved("AS-101"), Does.Contain("contact Ground when ready for taxi via Alpha"));
            Assert.That(atc.IssueEngineStartApproved("AS-101"), Does.Contain("remain this frequency"));
            Assert.That(atc.IssueEngineStartApproved("AS-101"), Does.Contain("via Alpha"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.EngineStart));
        }

        [Test]
        public void OnStand_IssuesMarshallerShutdown()
        {
            var atc = new AerodromeAtc();
            Assert.That(atc.IssueOnStand("AS-101", "Stand 1"), Does.Contain("marshaller"));
            Assert.That(atc.IssueOnStand("AS-101", "Stand 1"), Does.Contain("welcome to Kingscote"));
            Assert.That(atc.IssueOnStand("AS-101", "Stand 1"), Does.Contain("shutdown approved"));
            Assert.That(atc.IssueOnStand("AS-101", "Stand 1"), Does.Contain("chocks in"));
            Assert.That(atc.IssueOnStand("AS-101", "Stand 1"), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueOnStand("AS-101", "Stand 1"), Does.Contain("engines stopped"));
            Assert.That(atc.IssueOnStand("AS-101", "Stand 1"), Does.Contain("surface wind calm"));
            Assert.That(atc.IssueOnStand("AS-101", "Stand 1"), Does.Contain("remain this frequency for departure"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.OnStand));
        }

        [Test]
        public void HoldingPointAndReportVacated_Phrases()
        {
            var atc = new AerodromeAtc();
            Assert.That(atc.IssueTaxiToHoldShort("AS-101"), Does.Contain("holding point"));
            Assert.That(atc.IssueTaxiToHoldShort("AS-101"), Does.Contain("hold short at Alpha"));
            Assert.That(atc.IssueTaxiToHoldShort("AS-101"), Does.Contain("caution vehicles on the apron"));
            Assert.That(atc.IssueTaxiToHoldShort("AS-101"), Does.Contain("surface wind calm"));
            Assert.That(atc.IssueTaxiToHoldShort("AS-101"), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueTaxiToHoldShort("AS-101"), Does.Contain("report ready for departure"));
            Assert.That(atc.IssueTaxiToHoldShort("AS-101", WeatherKind.Rain), Does.Contain("wet surface"));
            Assert.That(atc.IssueTaxiToHoldShort("AS-101", WeatherKind.Fog), Does.Contain("reduced visibility"));
            Assert.That(atc.IssueReportRunwayVacated("AS-101"), Does.Contain("when vacated via Alpha"));
            Assert.That(atc.IssueReportRunwayVacated("AS-101"), Does.Contain("report runway vacated"));
            Assert.That(atc.IssueReportRunwayVacated("AS-101"), Does.Contain("first available taxiway"));
            Assert.That(atc.IssueReportRunwayVacated("AS-101"), Does.Contain("caution wake for following traffic"));
            Assert.That(atc.IssueReportRunwayVacated("AS-101"), Does.Contain("QNH 1013"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.ReportVacated));
        }

        [Test]
        public void TrafficAdvisory_IssuesHoldWithTraffic()
        {
            var atc = new AerodromeAtc();
            Assert.That(atc.IssueTrafficAdvisory("AS-101", "on short final"), Does.Contain("traffic on short final"));
            Assert.That(atc.IssueTrafficAdvisory("AS-101", "on short final"), Does.Contain("holding point Alpha"));
            Assert.That(atc.IssueTrafficAdvisory("AS-101", "on short final"), Does.Contain("surface wind calm"));
            Assert.That(atc.IssueTrafficAdvisory("AS-101", "on short final"), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueTrafficAdvisory("AS-101", "on short final"), Does.Contain("report ready when clear"));
            Assert.That(atc.IssueTrafficAdvisory("AS-101", "on short final"), Does.Contain("number two for departure"));
            Assert.That(atc.IssueTrafficAdvisory("AS-101", "on short final"), Does.Contain("expect further clearance"));
            Assert.That(atc.IssueTrafficAdvisory("AS-101", "on base"), Does.Contain("traffic on base"));
            Assert.That(atc.IssueTrafficAdvisory("AS-101", "landing on the runway"), Does.Contain("landing on the runway"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.TrafficAdvisory));
        }

        [Test]
        public void ContactGround_IssuesAfterVacatePhrase()
        {
            var atc = new AerodromeAtc();
            Assert.That(atc.IssueContactGround("AS-101"), Does.Contain("runway vacated"));
            Assert.That(atc.IssueContactGround("AS-101"), Does.Contain("Kingscote Ground"));
            Assert.That(atc.IssueContactGround("AS-101"), Does.Contain("taxi via Alpha"));
            Assert.That(atc.IssueContactGround("AS-101"), Does.Contain("hold short of the stand"));
            Assert.That(atc.IssueContactGround("AS-101"), Does.Contain("caution vehicles on the apron"));
            Assert.That(atc.IssueContactGround("AS-101"), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueContactGround("AS-101"), Does.Contain("report on stand when parked"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.ContactGround));
        }

        [Test]
        public void FrequencyChange_IssuesAfterDeparturePhrase()
        {
            var atc = new AerodromeAtc();
            Assert.That(atc.IssueFrequencyChangeApproved("AS-101"), Does.Contain("frequency change approved"));
            Assert.That(atc.IssueFrequencyChangeApproved("AS-101"), Does.Contain("radar service terminated"));
            Assert.That(atc.IssueFrequencyChangeApproved("AS-101"), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueFrequencyChangeApproved("AS-101"), Does.Contain("contact Adelaide Centre"));
            Assert.That(atc.IssueFrequencyChangeApproved("AS-101"), Does.Contain("125.3"));
            Assert.That(atc.IssueFrequencyChangeApproved("AS-101"), Does.Contain("squawk VFR"));
            Assert.That(atc.IssueFrequencyChangeApproved("AS-101"), Does.Contain("leave the circuit when able"));
            Assert.That(atc.IssueFrequencyChangeApproved("AS-101"), Does.Contain("remain clear of cloud"));
            Assert.That(atc.IssueFrequencyChangeApproved("AS-101"), Does.Contain("good day from Kingscote Tower"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.FrequencyChange));
            Assert.That(atc.IssueHoldApron("AS-101"), Does.Contain("hold on the apron"));
            Assert.That(atc.IssueHoldApron("AS-101"), Does.Contain("give way to apron traffic"));
            Assert.That(atc.IssueHoldApron("AS-101", "AS-102 on the apron"), Does.Contain("AS-102 on the apron"));
            Assert.That(atc.IssueHoldApron("AS-101"), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueHoldApron("AS-101"), Does.Contain("caution vehicles"));
            Assert.That(atc.IssueHoldApron("AS-101"), Does.Contain("surface wind calm"));
            Assert.That(atc.IssueHoldApron("AS-101"), Does.Contain("report when clear to continue to the stand"));
            Assert.That(atc.ActiveClearance, Is.EqualTo(AtcClearance.HoldApron));
        }

        [Test]
        public void ContinueApproach_IncludesWindCalm()
        {
            var atc = new AerodromeAtc();
            Assert.That(atc.IssueContinueApproach("AS-101"), Does.Contain("wind calm"));
            Assert.That(atc.IssueContinueApproach("AS-101"), Does.Contain("QNH 1013"));
            Assert.That(atc.IssueContinueApproach("AS-101"), Does.Contain("report short final"));
            Assert.That(atc.IssueContinueApproach("AS-101"), Does.Contain("number one expected"));
            Assert.That(atc.IssueContinueApproach("AS-101"), Does.Contain("vacate via Alpha when landed"));
            Assert.That(atc.IssueContinueApproach("AS-101", "on short final"), Does.Contain("traffic on short final"));
            Assert.That(atc.IssueContinueApproach("AS-101", "landing on the runway", wakeCaution: true),
                Does.Contain("caution wake turbulence"));
        }

        private static bool HasAtcEvent(AirportSimulation simulation, string phrase)
        {
            foreach (var entry in simulation.EventLog.Events)
            {
                if (entry.Title == "ATC"
                    && entry.Detail.IndexOf(phrase, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
    }
}
