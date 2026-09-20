using System.Linq;
using System.Reflection;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// After jets moved to 05/23 and regionals to 12/30, the tower must clear each
    /// strip independently — a Dash 8 on 12 must not wait for a 787 wake on 05.
    /// </summary>
    public sealed class DualRunwayTowerTests
    {
        [Test]
        public void ParallelStrips_CanMoveAtTheSameTime()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(3), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideStands);
            var player = Airline.Player("Parallel Air", "#123456");
            ops.AddAirline(player);
            DestinationCatalogue.TryFind("MEL", out var melbourne);
            DestinationCatalogue.TryFind("KGC", out var kingscote);

            RestoreHolding(ops, player, "VH-JET", AircraftType.Boeing7378, FleetState.HoldingShort,
                RunwayDirection.Runway05, melbourne);
            RestoreHolding(ops, player, "VH-REG", AircraftType.Atr42, FleetState.HoldingShort,
                RunwayDirection.Runway12, kingscote);

            ops.Update();

            var jet = ops.Fleet.Single(a => a.Registration == "VH-JET");
            var reg = ops.Fleet.Single(a => a.Registration == "VH-REG");
            Assert.That(jet.State, Is.EqualTo(FleetState.TakingOff), "05/23 clears independently");
            Assert.That(reg.State, Is.EqualTo(FleetState.TakingOff), "12/30 clears independently");
            Assert.That(ops.RunwayFreeAt.ElapsedSeconds, Is.GreaterThan(0));
            Assert.That(ops.CrossRunwayFreeAt.ElapsedSeconds, Is.GreaterThan(0));
        }

        [Test]
        public void RegionalBay_StaysHeldThroughTaxiOut()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(3), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Bay Hold", "#123456");
            ops.AddAirline(player);
            var bay = AirlineOperations.AdelaideRegionalBays[0];
            var plane = ops.AddAircraft(player, "VH-BAY", AircraftType.Atr42, bay);
            DestinationCatalogue.TryFind("KGC", out var kingscote);
            var departAt = new SimulationTime(DeparturePrep.LeadSeconds(plane.Type));
            Assert.That(ops.ScheduleDeparture(plane, kingscote, departAt).Accepted, Is.True);

            clock.Set(departAt);
            ops.Update();
            Assert.That(plane.State, Is.EqualTo(FleetState.TaxiOut));
            Assert.That(ops.IsStandFree(bay), Is.False,
                "a regional bay must stay reserved while the aircraft is still on the apron");
        }

        [Test]
        public void QueueSlot_IgnoresHoldersOnTheOtherStrip()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(3), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideStands);
            var player = Airline.Player("Queue Air", "#123456");
            ops.AddAirline(player);
            DestinationCatalogue.TryFind("MEL", out var melbourne);
            DestinationCatalogue.TryFind("KGC", out var kingscote);

            RestoreHolding(ops, player, "VH-JET", AircraftType.Boeing7378, FleetState.HoldingShort,
                RunwayDirection.Runway05, melbourne, startedAt: new SimulationTime(0));
            RestoreHolding(ops, player, "VH-REG", AircraftType.Atr42, FleetState.HoldingShort,
                RunwayDirection.Runway12, kingscote, startedAt: new SimulationTime(10));

            var reg = ops.Fleet.Single(a => a.Registration == "VH-REG");
            Assert.That(FleetVisual.QueueSlot(ops.Fleet, reg), Is.EqualTo(0),
                "alone on 12 — the jet on 05 is not ahead in this queue");
        }

        private static void RestoreHolding(AirlineOperations ops, Airline airline, string registration,
            AircraftType type, FleetState state, RunwayDirection runway, Destination destination,
            SimulationTime? startedAt = null)
        {
            var restore = typeof(AirlineOperations).GetMethod("RestoreAircraft",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var at = startedAt ?? new SimulationTime(0);
            restore.Invoke(ops, new object[]
            {
                registration, airline, type, state, at, null, default(StableId), default(StableId),
                destination, null, 0
            });
            ops.RestoreMovementData(registration, runway, wentAroundThisTrip: false);
        }

        [Test]
        public void CrossStrip_ClearsFasterThanTheFullTaxiToE2()
        {
            var clear = AdelaideGround.ClearOfRunwaySeconds(AircraftType.Atr42, RunwayDirection.Runway12);
            var vacate = AdelaideGround.VacateFor(AircraftType.Atr42, RunwayDirection.Runway12).WholeSeconds;
            Assert.That(clear, Is.LessThan(vacate / 2),
                "12/30 must free for the next movement before the long taxi to E2 finishes");
            Assert.That(AdelaideGround.ClearOfRunwaySeconds(AircraftType.Boeing7378, RunwayDirection.Runway05),
                Is.EqualTo(AdelaideGround.VacateFor(AircraftType.Boeing7378, RunwayDirection.Runway05).WholeSeconds),
                "05/23 still frees at E2");
        }

        [Test]
        public void ReconcileRunwayFreeAt_HoldsBothStripsThroughAnInProgressMovement()
        {
            var clock = new ManualSimulationClock(new SimulationTime(100));
            var ops = new AirlineOperations(clock, new SeededRandomSource(3), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideStands);
            var player = Airline.Player("Reconcile Air", "#123456");
            ops.AddAirline(player);
            DestinationCatalogue.TryFind("MEL", out var melbourne);
            DestinationCatalogue.TryFind("KGC", out var kingscote);

            RestoreHolding(ops, player, "VH-JET", AircraftType.Boeing7378, FleetState.Landing,
                RunwayDirection.Runway05, melbourne, startedAt: new SimulationTime(0));
            RestoreHolding(ops, player, "VH-REG", AircraftType.Atr42, FleetState.Landing,
                RunwayDirection.Runway12, kingscote, startedAt: new SimulationTime(0));

            var jet = ops.Fleet.Single(a => a.Registration == "VH-JET");
            var reg = ops.Fleet.Single(a => a.Registration == "VH-REG");
            // Force short remaining landings so StateEndsAt is in the future.
            var enter = typeof(FleetAircraft).GetMethod("Enter", BindingFlags.Instance | BindingFlags.NonPublic);
            enter.Invoke(jet, new object[] { FleetState.Landing, new SimulationTime(0), (long?)180 });
            enter.Invoke(reg, new object[] { FleetState.Landing, new SimulationTime(0), (long?)200 });

            var restoreTower = typeof(AirlineOperations).GetMethod("RestoreTower",
                BindingFlags.Instance | BindingFlags.NonPublic);
            restoreTower.Invoke(ops, new object[]
            {
                new SimulationTime(50), new SimulationTime(40), 0L
            });
            var reconcile = typeof(AirlineOperations).GetMethod("ReconcileRunwayFreeAt",
                BindingFlags.Instance | BindingFlags.NonPublic);
            reconcile.Invoke(ops, null);

            Assert.That(ops.RunwayFreeAt.ElapsedSeconds, Is.GreaterThanOrEqualTo(180),
                "main strip stays busy through the jet landing plus wake");
            Assert.That(ops.CrossRunwayFreeAt.ElapsedSeconds, Is.GreaterThanOrEqualTo(200),
                "cross strip stays busy through the regional landing plus wake");
        }
    }
}
