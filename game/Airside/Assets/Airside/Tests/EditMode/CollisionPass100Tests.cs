using System;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Regressions for the 2026-09-09 layering / collision / route bugfix pass.</summary>
    public sealed class CollisionPass100Tests
    {
        [SetUp]
        public void SetUp() => TaxiLoopFixture.EnableFullTaxiLoop();

        [TearDown]
        public void TearDown() => TaxiLoopFixture.RestoreCircuit();

        [Test]
        public void StandCentres_AreAtLeastTenMetresApart()
        {
            var z1 = AirportTaxiNetwork.StandZ(AirportSimulation.StandOne);
            var z2 = AirportTaxiNetwork.StandZ(AirportSimulation.StandTwo);
            var z3 = AirportTaxiNetwork.StandZ(AirportSimulation.StandThree);
            Assert.That(Math.Abs(z2 - z1), Is.GreaterThanOrEqualTo(10f));
            Assert.That(Math.Abs(z3 - z2), Is.GreaterThanOrEqualTo(10f));
        }

        [Test]
        public void TaxiRoutes_UseDoglegThroatBeforeStandLeadIn()
        {
            var route = new AirportTaxiNetwork().RouteTo(AirportSimulation.StandTwo);
            Assert.That(route.SegmentIds.Count, Is.EqualTo(4));
            Assert.That(route.SegmentIds[2], Is.EqualTo(AirportTaxiNetwork.ApronThroat));
            Assert.That(route.Points[3].X, Is.EqualTo(AirportLayout.ApronThroatX).Within(0.01f));
            Assert.That(route.Points[3].Z, Is.EqualTo(AirportTaxiNetwork.StandZ(AirportSimulation.StandTwo)).Within(0.01f));
            Assert.That(route.Points[4].X, Is.EqualTo(AirportLayout.StandX).Within(0.01f));
        }

        [Test]
        public void LeadInChord_DoesNotPassThroughNeighbouringStandCentre()
        {
            var route = new AirportTaxiNetwork().RouteTo(AirportSimulation.StandThree);
            var throat = route.Points[3];
            var stand = route.Points[4];
            var neighbour = AirportTaxiNetwork.StandPoint(AirportSimulation.StandTwo);
            // Dogleg keeps the chord at stand Z after the throat, so mid-chord stays clear of Stand 2.
            var mid = new TaxiPoint((throat.X + stand.X) * 0.5f, (throat.Z + stand.Z) * 0.5f);
            var dx = mid.X - neighbour.X;
            var dz = mid.Z - neighbour.Z;
            Assert.That(Math.Sqrt(dx * dx + dz * dz), Is.GreaterThan(6.0));
        }

        [Test]
        public void GroundTraffic_OffFieldHoldSitsAtAwayPadNotRunwayEntry()
        {
            var table = new ReservationTable();
            var traffic = new GroundTrafficAircraft(new StableId("GT-201"), table, GroundTrafficRole.ArriveDepart, 0);
            var monitor = new TrafficWaitMonitor();
            var occupied = new[] { AirportSimulation.StandOne, AirportSimulation.StandTwo };

            traffic.Reposition(new SimulationTime(1), monitor, occupied, standCount: 2, mayEnterCorridor: true);

            Assert.That(traffic.IsHoldingOffField, Is.True);
            Assert.That(traffic.Position.X, Is.EqualTo(AirportTaxiNetwork.AwayHold.X).Within(0.01f));
            Assert.That(traffic.Position.Z, Is.EqualTo(AirportTaxiNetwork.AwayHold.Z).Within(0.01f));
            Assert.That(table.IsReserved(AirportTaxiNetwork.Corridor), Is.False);
        }

        [Test]
        public void GroundTraffic_RunUpBayIsOffAlphaCentreline()
        {
            Assert.That(AirportTaxiNetwork.RunUpBayPoint.Z, Is.GreaterThan(9.5f));
            var table = new ReservationTable();
            var traffic = new GroundTrafficAircraft(new StableId("GT-202"), table, GroundTrafficRole.Reposition, 0);
            var monitor = new TrafficWaitMonitor();

            for (var second = 1; second <= 40; second++)
                traffic.Reposition(new SimulationTime(second), monitor, Array.Empty<StableId>(), 2, true);

            if (traffic.CurrentPhase == "Run-up hold")
            {
                Assert.That(traffic.Position.Z, Is.EqualTo(AirportTaxiNetwork.RunUpBayPoint.Z).Within(0.01f));
                Assert.That(traffic.OnCorridor, Is.False);
                Assert.That(table.IsReserved(AirportTaxiNetwork.RunUpBay), Is.True);
            }
        }

        [Test]
        public void GroundTraffic_OutboundFollowsDoglegNotCornerCut()
        {
            var table = new ReservationTable();
            var traffic = new GroundTrafficAircraft(new StableId("GT-201"), table, GroundTrafficRole.ArriveDepart, 0);
            var monitor = new TrafficWaitMonitor();

            for (var second = 1; second <= 160; second++)
            {
                traffic.Reposition(new SimulationTime(second), monitor, AirportSimulation.StandOne, 2, true);
                if (traffic.CurrentPhase.StartsWith("Taxi out from", StringComparison.Ordinal)
                    || traffic.CurrentPhase.StartsWith("Clear", StringComparison.Ordinal))
                {
                    Assert.That(traffic.Position.X, Is.GreaterThan(7f),
                        "outbound must stay on lead-in/throat, not cut to junction");
                }
            }
        }

        [Test]
        public void GroundTraffic_YieldKeepsNonConflictingStand()
        {
            var table = new ReservationTable();
            var traffic = new GroundTrafficAircraft(new StableId("GT-201"), table, GroundTrafficRole.ArriveDepart, 0);
            var monitor = new TrafficWaitMonitor();

            var parked = false;
            for (var second = 1; second <= 200; second++)
            {
                traffic.Reposition(new SimulationTime(second), monitor, AirportSimulation.StandOne, 2, true);
                if (traffic.IsAtStand)
                {
                    parked = true;
                    break;
                }
            }

            Assert.That(parked, Is.True, "GT-201 should reach a stand before yield check");
            var stand = traffic.TargetStand;
            traffic.Yield(new[] { AirportTaxiNetwork.Corridor });
            Assert.That(table.IsReserved(stand), Is.True, "yielding corridor must not drop a parked stand");
        }

        [Test]
        public void CommercialTaxiOut_KeepsStandUntilLeadInCleared()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var table = new ReservationTable();
            var simulation = new AirportSimulation(clock, new SeededRandomSource(7), table);
            var flight = simulation.Flights[0];

            for (var second = 1; second <= 5000; second++)
            {
                clock.Advance(1);
                simulation.Update();
                if (flight.Operation.Phase != AircraftPhase.TaxiOut)
                    continue;
                if (flight.Operation.SecondsRemaining(clock.Now) <= 0)
                    break;

                var segment = flight.SegmentFor(clock.Now);
                if (segment.Equals(AirportTaxiNetwork.LeadInFor(flight.AssignedStand))
                    || segment.Equals(AirportTaxiNetwork.ApronThroat))
                {
                    Assert.That(table.TryGetOwner(flight.AssignedStand, out var owner), Is.True);
                    Assert.That(owner, Is.EqualTo(flight.OwnerId));
                    return;
                }
            }

            Assert.Fail("never observed taxi-out on lead-in/throat with stand held");
        }

        [Test]
        public void SegmentIndex_IsLengthWeightedNotEqualTime()
        {
            var route = new TaxiRoute(
                "weighted",
                new[] { new StableId("A"), new StableId("B"), new StableId("C") },
                new[] { new TaxiPoint(0f, 0f), new TaxiPoint(1f, 0f), new TaxiPoint(101f, 0f), new TaxiPoint(102f, 0f) });

            Assert.That(route.SegmentIndexAt(0.005, reverse: false), Is.EqualTo(0));
            Assert.That(route.SegmentIndexAt(0.50, reverse: false), Is.EqualTo(1));
            Assert.That(route.SegmentIndexAt(0.995, reverse: false), Is.EqualTo(2));
        }

        [Test]
        public void DualFlightSoak_StillCompletesWithoutConflicts()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(42), new ReservationTable());

            for (var second = 1; second <= 25000 && simulation.Flights.Count < 2; second++)
            {
                clock.Advance(1);
                simulation.Update();
                if (simulation.Routes.Pending != null)
                    simulation.AcceptPendingRoute();
            }

            Assert.That(simulation.Flights.Count, Is.EqualTo(2));
            for (var second = 1; second <= 20000 && simulation.CompletedCycles < 20; second++)
            {
                clock.Advance(1);
                simulation.Update();
                if (simulation.Routes.Pending != null)
                    simulation.DeclinePendingRoute();
            }

            Assert.That(simulation.CompletedCycles, Is.GreaterThanOrEqualTo(20));
            Assert.That(simulation.ReservationConflicts, Is.Zero);
        }

        [Test]
        public void DayCycle_MidnightAndMiddayHelpersAreStable()
        {
            var midnight = DayCycle.MidnightOfDay(1);
            var midday = DayCycle.MiddayOfDay(1);
            Assert.That(midnight.ElapsedSeconds, Is.EqualTo(800));
            Assert.That(midday.ElapsedSeconds, Is.EqualTo(200));
            Assert.That(new DayCycle(midday).Hour, Is.EqualTo(12));
        }

        [Test]
        public void TrafficWaitDescribe_JoinsMultipleWarnings()
        {
            var monitor = new TrafficWaitMonitor();
            var now = new SimulationTime(0);
            monitor.SetWaiting(new StableId("GT-201"), AirportTaxiNetwork.Corridor, now);
            monitor.SetWaiting(new StableId("AS-101"), AirportSimulation.Runway, now);
            var later = new SimulationTime(TrafficWaitMonitor.WarningAfterSeconds);
            var text = monitor.Describe(later);
            Assert.That(text, Does.Contain("GT-201"));
            Assert.That(text, Does.Contain("AS-101"));
        }

        [Test]
        public void EventLog_TrimsEvenWhenOnlyInsolventRowsRemain()
        {
            var log = new OperationalEventLog(capacity: 3);
            for (var i = 0; i < 5; i++)
                log.Add(new OperationalEvent(new SimulationTime(i), "", "Insolvent", $"n={i}"));
            Assert.That(log.Events.Count, Is.EqualTo(3));
        }

        [Test]
        public void SaveMigrate_NullCommandsBecomesEmptyList()
        {
            var save = new Airside.Persistence.AirsideSaveData
            {
                schemaVersion = 1,
                commands = null,
                randomSeed = 1,
                locationId = AirportLocation.Default.Id
            };
            save.Migrate();
            Assert.That(save.commands, Is.Not.Null);
            Assert.That(save.commands.Count, Is.EqualTo(0));
        }
    }
}
