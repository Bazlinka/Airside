using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// The drawn arrival flies in down an extended final timed by
    /// <see cref="AirlineOperations.ExpectedLandingClearance"/>. If that estimate is right the
    /// aircraft reaches the hold point exactly as it is cleared; if it is wrong it either
    /// stops there (the old freeze) or is still out on final when cleared (a catch-up slide).
    /// </summary>
    public sealed class ArrivalClearanceTests
    {
        [Test]
        public void ExpectedClearance_MatchesWhenTheTowerActuallyClears()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(2026),
                Airline.Player("Arrival Test Air", "#1F3A93"));

            var predicted = new Dictionary<string, long>();
            var previous = ops.Fleet.ToDictionary(a => a.Registration, a => a.State);
            var errors = new List<long>();

            for (var steps = 0; steps < 60000 && clock.Now.ElapsedSeconds < 2 * 24 * 3600; steps++)
            {
                var next = ops.NextEventAt();
                if (next == null)
                    break;
                clock.Set(next.Value);
                ops.Update();

                foreach (var aircraft in ops.Fleet)
                {
                    var was = previous[aircraft.Registration];
                    if (aircraft.State == FleetState.HoldingForLanding && was != FleetState.HoldingForLanding
                        && !AirlineOperations.ExemptFromCurfew(aircraft))
                    {
                        var eta = ops.ExpectedLandingClearance(aircraft, out var runway);
                        Assert.That(eta.HasValue, Is.True);
                        Assert.That(runway, Is.EqualTo(aircraft.AssignedRunway));
                        Assert.That(eta.Value.CompareTo(clock.Now), Is.GreaterThanOrEqualTo(0));
                        predicted[aircraft.Registration] = eta.Value.ElapsedSeconds;
                    }

                    if (was == FleetState.HoldingForLanding && aircraft.State != FleetState.HoldingForLanding
                        && aircraft.State != FleetState.Landing)
                        predicted.Remove(aircraft.Registration);

                    if (was == FleetState.HoldingForLanding && aircraft.State == FleetState.Landing
                        && predicted.TryGetValue(aircraft.Registration, out var at))
                    {
                        var error = aircraft.StateStartedAt.ElapsedSeconds - at;
                        // Curfew overnight waits and 5-minute reopen lumps are not estimate misses.
                        if (Math.Abs(error) <= 60)
                            errors.Add(error);
                        predicted.Remove(aircraft.Registration);
                    }

                    if (aircraft.State is not (FleetState.HoldingForLanding or FleetState.Inbound))
                        Assert.That(ops.ExpectedLandingClearance(aircraft, out _), Is.Null);
                    previous[aircraft.Registration] = aircraft.State;
                }
            }

            Assert.That(errors.Count, Is.GreaterThan(20), "the run should land plenty of arrivals");
            var exact = errors.Count(e => Math.Abs(e) <= 2);
            TestContext.WriteLine($"exact {exact}/{errors.Count}; late {string.Join(",", errors.Where(e => e > 2))}; early {string.Join(",", errors.Where(e => e < -2))}");
            // ADR 0111: a realistic Adelaide day (~110 departures) interleaves more long-held
            // departures ahead of arrivals than the estimate can foresee at hand-off; 85 %
            // still keeps the drawn final honest.
            Assert.That(exact, Is.GreaterThanOrEqualTo((int)(errors.Count * 0.85)),
                $"cleared within 2 s of the estimate {exact}/{errors.Count}; worst late {errors.Max()} s, early {errors.Min()} s");
        }

        [Test]
        public void Inbound_ExpectsClearanceNoEarlierThanItArrives()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(7),
                Airline.Player("Arrival Test Air", "#1F3A93"));
            for (var steps = 0; steps < 4000 && clock.Now.ElapsedSeconds < 12 * 3600; steps++)
            {
                var next = ops.NextEventAt();
                if (next == null)
                    break;
                clock.Set(next.Value);
                ops.Update();
                foreach (var inbound in ops.Fleet.Where(a => a.State == FleetState.Inbound))
                {
                    var eta = ops.ExpectedLandingClearance(inbound, out _);
                    Assert.That(eta.HasValue, Is.True);
                    Assert.That(eta.Value.CompareTo(inbound.StateEndsAt.Value), Is.GreaterThanOrEqualTo(0));
                }
            }
        }
    }
}
