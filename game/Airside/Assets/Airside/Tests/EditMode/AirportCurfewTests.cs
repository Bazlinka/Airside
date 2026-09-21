using System;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AirportCurfewTests
    {
        [Test]
        public void Closed_From2300To0600Adelaide()
        {
            Assert.That(AirportCurfew.IsClosed(new DateTime(2026, 9, 21, 22, 59, 0)), Is.False);
            Assert.That(AirportCurfew.IsClosed(new DateTime(2026, 9, 21, 23, 0, 0)), Is.True);
            Assert.That(AirportCurfew.IsClosed(new DateTime(2026, 9, 22, 3, 15, 0)), Is.True);
            Assert.That(AirportCurfew.IsClosed(new DateTime(2026, 9, 22, 5, 59, 0)), Is.True);
            Assert.That(AirportCurfew.IsClosed(new DateTime(2026, 9, 22, 6, 0, 0)), Is.False);
        }

        [Test]
        public void OpensAt_JumpsOvernightToSix()
        {
            var clock = AirlineClock.Default;
            var evening = clock.AtLocal(new DateTime(2026, 9, 14, 23, 30, 0));
            var open = AirportCurfew.OpensAt(evening, clock);
            Assert.That(clock.LocalAt(open), Is.EqualTo(new DateTime(2026, 9, 15, 6, 0, 0)).Within(TimeSpan.FromSeconds(1)));
        }

        [Test]
        public void NewGame_HasRfdsOnARegionalBay()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(2026),
                Airline.Player("Curfew Air", "#1F3A93"));
            var rfds = ops.Fleet.Single(a => a.Airline.Id.Value == "RFDS");
            Assert.That(rfds.Airline.IsEmergency, Is.True);
            Assert.That(AirlineOperations.ExemptFromCurfew(rfds), Is.True);
            Assert.That(AirlineOperations.ExemptFromCurfew(ops.Fleet.Single(a => a.Airline.IsPlayer)), Is.True);
            Assert.That(AirlineOperations.ExemptFromCurfew(ops.Fleet.First(a => a.Airline.Id.Value == "REX")),
                Is.False);
        }

        [Test]
        public void CommercialAi_DoesNotMoveDuringCurfew_PlayerAndRfdsMay()
        {
            var simClock = new ManualSimulationClock(new SimulationTime(0));
            var utc = new DateTime(2026, 9, 14, 14, 0, 0, DateTimeKind.Utc); // 23:30 ACST
            var ops = AirlineOperations.StartAtAdelaide(simClock, new SeededRandomSource(11),
                Airline.Player("Night Air", "#1F3A93"), AirlineClock.Aligned(simClock.Now, utc));
            var player = ops.Fleet.Single(a => a.Airline.IsPlayer);
            Assert.That(DestinationCatalogue.TryFind("PLO", out var portLincoln), Is.True);
            Assert.That(ops.ScheduleDeparture(player, portLincoln, simClock.Now.Advance(5 * 60)).Accepted, Is.True);

            for (var steps = 0; steps < 800; steps++)
            {
                var next = ops.NextEventAt();
                if (next == null || next.Value.ElapsedSeconds > 90 * 60)
                    break;
                simClock.Set(next.Value);
                ops.Update();
                var local = ops.Clock.LocalAt(simClock.Now);
                if (!AirportCurfew.IsClosed(local))
                    break;
                foreach (var aircraft in ops.Fleet)
                {
                    if (aircraft.State is FleetState.TaxiOut or FleetState.HoldingShort or FleetState.TakingOff
                        or FleetState.Landing or FleetState.HoldingForLanding)
                    {
                        Assert.That(AirlineOperations.ExemptFromCurfew(aircraft), Is.True,
                            $"{aircraft.Registration} {aircraft.State} at {local:HH:mm} during curfew");
                    }
                }
            }
        }
    }
}
