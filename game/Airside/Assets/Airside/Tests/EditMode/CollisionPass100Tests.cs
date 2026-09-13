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
        public void DayCycle_MidnightAndMiddayHelpersAreStable()
        {
            var midnight = DayCycle.MidnightOfDay(1);
            var midday = DayCycle.MiddayOfDay(1);
            Assert.That(midnight.ElapsedSeconds, Is.EqualTo(800));
            Assert.That(midday.ElapsedSeconds, Is.EqualTo(200));
            Assert.That(new DayCycle(midday).Hour, Is.EqualTo(12));
        }

    }
}
