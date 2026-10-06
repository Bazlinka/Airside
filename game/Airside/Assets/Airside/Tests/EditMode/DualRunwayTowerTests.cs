using System.Linq;
using System.Reflection;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// Adelaide's runways have separate queues but share an intersection occupancy reservation.
    /// </summary>
    public sealed class DualRunwayTowerTests
    {
        [Test]
        public void IntersectingStrips_HoldTheSecondDepartureWhileTheFirstOccupiesRunway()
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
            Assert.That(reg.State, Is.EqualTo(FleetState.HoldingShort), "intersecting strip waits for occupancy");
            Assert.That(ops.Why(reg).Kind, Is.EqualTo(HoldKind.RunwayOccupied));
            var release = AdelaideGround.LineupFor(jet.AssignedRunway, jet.Type).WholeSeconds
                + AircraftPerformance.For(jet.Type).TakeoffSeconds;
            clock.Set(new SimulationTime(release));
            ops.Update();
            Assert.That(reg.State, Is.EqualTo(FleetState.TakingOff), "its separate strip can clear after physical occupancy ends");
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

        [Test]
        public void QueueSlot_EachEndOfTheMainRunwayHasItsOwnHoldingQueue()
        {
            // The 05 and 23 holds are at opposite ends of the strip. A jet alone at the 23 hold
            // used to be drawn a place back because another held for 05, while a taxi-out behind
            // it counted only 23 and stopped on top of it (BusyDay_NoAircraftDriveThroughEachOther).
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(3), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideStands);
            var player = Airline.Player("Queue Air", "#123456");
            ops.AddAirline(player);
            DestinationCatalogue.TryFind("MEL", out var melbourne);

            RestoreHolding(ops, player, "VH-JTA", AircraftType.Boeing7378, FleetState.HoldingShort,
                RunwayDirection.Runway05, melbourne, startedAt: new SimulationTime(0));
            RestoreHolding(ops, player, "VH-JTB", AircraftType.Boeing7378, FleetState.HoldingShort,
                RunwayDirection.Runway23, melbourne, startedAt: new SimulationTime(10));
            RestoreHolding(ops, player, "VH-JTC", AircraftType.Boeing7378, FleetState.HoldingShort,
                RunwayDirection.Runway05, melbourne, startedAt: new SimulationTime(20));

            var fleet = ops.Fleet;
            Assert.That(FleetVisual.QueueSlot(fleet, fleet.Single(a => a.Registration == "VH-JTB")), Is.EqualTo(0),
                "alone at the 23 hold");
            Assert.That(FleetVisual.QueueSlot(fleet, fleet.Single(a => a.Registration == "VH-JTC")), Is.EqualTo(1),
                "behind the earlier 05 holder");
        }

        [Test]
        public void QueueAhead_CountsATaxiOutThatReachesTheHoldFirst()
        {
            // Two jets pushed together both aimed at the hold itself, and the one arriving second
            // drove into the first in the last seconds of its taxi.
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(3), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideStands);
            var player = Airline.Player("Queue Air", "#123456");
            ops.AddAirline(player);
            DestinationCatalogue.TryFind("MEL", out var melbourne);

            RestoreHolding(ops, player, "VH-TXA", AircraftType.Boeing7378, FleetState.TaxiOut,
                RunwayDirection.Runway05, melbourne, new SimulationTime(0), new SimulationTime(500));
            RestoreHolding(ops, player, "VH-TXB", AircraftType.Boeing7378, FleetState.TaxiOut,
                RunwayDirection.Runway05, melbourne, new SimulationTime(0), new SimulationTime(508));
            RestoreHolding(ops, player, "VH-TXC", AircraftType.Boeing7378, FleetState.TaxiOut,
                RunwayDirection.Runway23, melbourne, new SimulationTime(0), new SimulationTime(400));

            var fleet = ops.Fleet;
            var now = new SimulationTime(100);
            Assert.That(FleetVisual.QueueAhead(fleet, fleet.Single(a => a.Registration == "VH-TXA"), now), Is.EqualTo(0),
                "first to the 05 hold; the 23 taxi-out goes to the other end");
            Assert.That(FleetVisual.QueueAhead(fleet, fleet.Single(a => a.Registration == "VH-TXB"), now), Is.EqualTo(1),
                "stops a place back from the jet that gets there first");
        }

        private static void RestoreHolding(AirlineOperations ops, Airline airline, string registration,
            AircraftType type, FleetState state, RunwayDirection runway, Destination destination,
            SimulationTime? startedAt = null, SimulationTime? endsAt = null)
        {
            var restore = typeof(AirlineOperations).GetMethod("RestoreAircraft",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var at = startedAt ?? new SimulationTime(0);
            var stand = AirlineOperations.NeedsTerminalGate(type)
                ? AirlineOperations.AdelaideTerminalGates[0]
                : AirlineOperations.AdelaideRegionalBays[0];
            // Prefer a free stand from the ops list when available.
            foreach (var candidate in ops.Stands)
            {
                if (!ops.IsStandFree(candidate))
                    continue;
                if (AirlineOperations.NeedsTerminalGate(type) != AdelaideGround.IsTerminalGate(candidate))
                    continue;
                stand = candidate;
                break;
            }

            restore.Invoke(ops, new object[]
            {
                registration, airline, type, state, at, endsAt, default(StableId), stand,
                destination, null, 0
            });
            ops.RestoreMovementData(registration, runway, wentAroundThisTrip: false);
        }

        [Test]
        public void CrossStrip_ClearsFasterThanTheFullTaxiToE2()
        {
            var clear = AdelaideGround.ClearOfRunwaySeconds(AircraftType.Atr42, RunwayDirection.Runway12);
            var vacate = AdelaideGround.VacateFor(AircraftType.Atr42, RunwayDirection.Runway12).WholeSeconds;
            Assert.That(clear, Is.LessThan(vacate * 0.7),
                "12/30 must free for the next movement before the long taxi to E2 finishes");
            Assert.That(clear, Is.GreaterThan(vacate * 0.25),
                "clear-of-runway must leave enough exit before the next landing joins");
            // ADR 0183: 05/23 frees once the aircraft is off the pavement, not when it reaches E2.
            foreach (var runway in new[] { RunwayDirection.Runway05, RunwayDirection.Runway23 })
            foreach (var type in new[] { AircraftType.Boeing7378, AircraftType.Atr42 })
            {
                var mainClear = AdelaideGround.ClearOfRunwaySeconds(type, runway);
                var mainVacate = AdelaideGround.VacateFor(type, runway).WholeSeconds;
                Assert.That(mainClear, Is.LessThan(mainVacate * 0.7), $"{type.Id} {runway} frees before E2");
                Assert.That(mainClear, Is.GreaterThanOrEqualTo(30), $"{type.Id} {runway} still leaves time to leave the pavement");
            }
        }

        [Test]
        public void ReconcileRunwayFreeAt_KeepsTheTowersOwnFreeTimeMidTakeoff()
        {
            // The tower frees a strip after lineup + ground roll + wake. Reconcile used to hold it
            // for the whole TakingOff state (climb-out included), so loading a save mid-takeoff
            // moved the free time later and the resumed game drifted (ADR 0156).
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(2026),
                Airline.Player("Reconcile Air", "#123456"));
            var reconcile = typeof(AirlineOperations).GetMethod("ReconcileRunwayFreeAt",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var checkedTakeoffs = 0;
            for (var t = 1L; t < 6 * 3600 && checkedTakeoffs < 5; t++)
            {
                clock.Set(new SimulationTime(t));
                ops.Update();
                if (!ops.Fleet.Any(a => a.State == FleetState.TakingOff && a.StateStartedAt.ElapsedSeconds == t))
                    continue;
                var main = ops.RunwayFreeAt;
                var cross = ops.CrossRunwayFreeAt;
                reconcile.Invoke(ops, null);
                Assert.That(ops.RunwayFreeAt, Is.EqualTo(main), $"main strip free time at t={t}");
                Assert.That(ops.CrossRunwayFreeAt, Is.EqualTo(cross), $"cross strip free time at t={t}");
                checkedTakeoffs++;
            }

            Assert.That(checkedTakeoffs, Is.GreaterThan(0), "the morning has takeoffs to check");
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
