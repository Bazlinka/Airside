using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AiTimetableTests
    {
        [Test]
        public void Rex_FliesItsRegionalNetworkInsideOperatingHours()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            // Start just before midnight in Adelaide so the first turnarounds land at night.
            var utc = new DateTime(2026, 9, 14, 14, 20, 0, DateTimeKind.Utc);
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(99),
                Airline.Player("Test Air", "#C8102E"), AirlineClock.Aligned(clock.Now, utc));
            var emu = ops.Airlines.First(a => a.Id.Value == "REX");
            var network = new HashSet<string>(AirlineOperations.RexNetwork.Select(n => n.Code));

            var previous = ops.FleetOf(emu).ToDictionary(a => a.Registration, a => a.State);
            var departures = 0;
            for (var t = 0; t < 4 * 24 * 3600; t += 30)
            {
                clock.Advance(30);
                ops.Update();
                foreach (var aircraft in ops.FleetOf(emu))
                {
                    if (aircraft.State == FleetState.TaxiOut && previous[aircraft.Registration] != FleetState.TaxiOut)
                    {
                        departures++;
                        Assert.That(network, Does.Contain(aircraft.CurrentDestination.Value.Code));
                        if (clock.Now.ElapsedSeconds > 3600)
                        {
                            var local = ops.Clock.LocalAt(aircraft.StateStartedAt);
                            Assert.That(local.Hour, Is.InRange(AirlineOperations.AiFirstDepartureHour,
                                AirlineOperations.AiLastDepartureHour),
                                $"{aircraft.Registration} pushed back at {local:HH:mm}");
                        }
                    }

                    previous[aircraft.Registration] = aircraft.State;
                }
            }

            Assert.That(departures, Is.GreaterThan(6), "Rex should keep flying across four days");
        }

        [Test]
        public void AtLocal_RoundTripsWithLocalAt()
        {
            var clock = AirlineClock.Aligned(new SimulationTime(0), new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc));
            var local = new DateTime(2026, 9, 15, 6, 0, 0);
            Assert.That(clock.LocalAt(clock.AtLocal(local)), Is.EqualTo(local).Within(TimeSpan.FromSeconds(1)));
        }
    }
}
