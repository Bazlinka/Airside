using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// PROJECT_PLAN acceptance: a long run completes without a crash, deadlock or
    /// unexplained stop. Thirty simulated days of the Adelaide airline game with the
    /// player's aircraft flown continuously, checked at every event.
    /// </summary>
    public sealed class AirlineSoakTests
    {
        private const long Days = 30;

        [Test]
        public void ThirtyDays_NoDeadlockNoDoubleBookingEveryAircraftKeepsFlying()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(424242), Airline.Player("Soak Air", "#6A3FA0"));
            var mine = ops.FleetOf(ops.PlayerAirline).Single();
            var choices = new SeededRandomSource(99);
            var reachable = ops.MapDestinations().Where(d => ops.CanReach(mine, d)).ToList();
            var horizon = Days * DayCycle.DaySeconds;

            // The longest any aircraft may sit in one waiting state before it counts as
            // stuck: a full AI turnaround plus the longest runway queue imaginable.
            const long stuckAfter = 6 * 3600;
            var events = 0;

            while (clock.Now.ElapsedSeconds < horizon)
            {
                // Play like an attentive player: plan the next flight on arrival at the
                // stand, and park straight away on landing.
                if (mine.State == FleetState.AtStand && !mine.Scheduled.HasValue)
                {
                    var destination = reachable[choices.NextInt(0, reachable.Count)];
                    Assert.That(ops.ScheduleDeparture(mine, destination, clock.Now.Advance(choices.NextInt(0, 3600))).Accepted, Is.True);
                }

                if (mine.State == FleetState.AwaitingStand)
                    Assert.That(ops.AssignStand(mine, ops.FreeStands().First()).Accepted, Is.True, "a stand is always free for three aircraft on four bays");

                var next = ops.NextEventAt();
                Assert.That(next, Is.Not.Null, $"the airport stopped at {clock.Now}: nothing left to happen");
                clock.Set(next.Value);
                ops.Update();
                events++;

                var onRunway = ops.Fleet.Count(a => a.State is FleetState.TakingOff or FleetState.Landing);
                Assert.That(onRunway, Is.LessThanOrEqualTo(1), $"two runway movements at {clock.Now}");

                var held = ops.Fleet.Where(a => a.State is FleetState.AtStand or FleetState.TaxiIn).Select(a => a.Stand).ToList();
                Assert.That(held.Distinct().Count(), Is.EqualTo(held.Count), $"stand double-booked at {clock.Now}");

                foreach (var aircraft in ops.Fleet)
                {
                    if (aircraft.StateEndsAt.HasValue)
                        continue;
                    // Parked overnight with the first flight of the day booked is a schedule,
                    // not a stall — Emu Air only departs 06:00–21:00 — as long as it is booked.
                    if (aircraft.State == FleetState.AtStand && aircraft.Scheduled.HasValue)
                    {
                        Assert.That(aircraft.Scheduled.Value.DepartAt.ElapsedSeconds - clock.Now.ElapsedSeconds,
                            Is.LessThan(12 * 3600), $"{aircraft} booked too far ahead");
                        continue;
                    }
                    var waited = clock.Now.ElapsedSeconds - aircraft.StateStartedAt.ElapsedSeconds;
                    Assert.That(waited, Is.LessThan(stuckAfter), $"{aircraft} has waited {waited / 60} min");
                }
            }

            foreach (var aircraft in ops.Fleet)
                Assert.That(aircraft.CompletedTrips, Is.GreaterThan(Days * 2), $"{aircraft.Registration} flew too few trips");

            Assert.That(events, Is.LessThan(200_000), "event count stays bounded");
        }
    }
}
