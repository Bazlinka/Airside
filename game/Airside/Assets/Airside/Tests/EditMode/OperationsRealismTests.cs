using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// ADR 0110: a 05:00–23:00 commercial day, departures clustered on published bank
    /// marks, the apron holding its aircraft until their own departure, and gates sized
    /// to the aircraft on them.
    /// </summary>
    public sealed class OperationsRealismTests
    {
        private static AirlineOperations NewGame(ManualSimulationClock clock, uint seed = 2026) =>
            AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(seed),
                Airline.Player("Realism Air", "#1F3A93"));

        [Test]
        public void TwoDaySoak_CommercialAiPushesOnlyBetweenFiveAndEleven()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = NewGame(clock);
            var seen = new HashSet<string>();
            var pushes = 0;
            for (var step = 0; step < 2 * 24 * 120; step++)
            {
                clock.Advance(30);
                ops.Update();
                foreach (var aircraft in ops.Fleet)
                {
                    if (aircraft.State != FleetState.TaxiOut || AirlineOperations.ExemptFromCurfew(aircraft))
                        continue;
                    var key = $"{aircraft.Registration}@{aircraft.StateStartedAt.ElapsedSeconds}";
                    if (!seen.Add(key))
                        continue;
                    pushes++;
                    var local = ops.Clock.LocalAt(aircraft.StateStartedAt);
                    Assert.That(AirportCurfew.IsClosed(local), Is.False,
                        $"{aircraft.Registration} pushed at {local:HH:mm:ss}, outside 05:00–23:00");
                }
            }

            Assert.That(pushes, Is.GreaterThan(40), "a busy two days of commercial departures");
        }

        [Test]
        public void TwoDaySoak_ParkedAircraftStayOnTheirStandUntilTheirOwnDeparture()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = NewGame(clock);
            var parked = new Dictionary<string, (StableId Stand, SimulationTime? DepartAt)>();
            for (var step = 0; step < 2 * 24 * 120; step++)
            {
                clock.Advance(30);
                ops.Update();
                var present = ops.Fleet.ToDictionary(a => a.Registration);
                foreach (var (registration, before) in parked)
                {
                    Assert.That(present.TryGetValue(registration, out var aircraft), Is.True,
                        $"{registration} vanished from {before.Stand} instead of departing");
                    if (aircraft.State == FleetState.AtStand)
                    {
                        Assert.That(aircraft.Stand, Is.EqualTo(before.Stand),
                            $"{registration} moved stands without taxiing");
                        continue;
                    }

                    Assert.That(aircraft.State, Is.EqualTo(FleetState.TaxiOut).Or.EqualTo(FleetState.HoldingShort)
                            .Or.EqualTo(FleetState.TakingOff),
                        $"{registration} left {before.Stand} as {aircraft.State}, not by pushing back");
                    Assert.That(aircraft.DepartureStand, Is.EqualTo(before.Stand));
                    if (before.DepartAt.HasValue)
                        Assert.That(aircraft.StateStartedAt.ElapsedSeconds,
                            Is.GreaterThanOrEqualTo(before.DepartAt.Value.ElapsedSeconds - 30),
                            $"{registration} pushed before its scheduled departure");
                }

                parked.Clear();
                foreach (var aircraft in ops.Fleet)
                    if (aircraft.State == FleetState.AtStand && !aircraft.Airline.IsPlayer)
                        parked[aircraft.Registration] = (aircraft.Stand,
                            aircraft.Scheduled is { Cancelled: false } booked ? booked.DepartAt : (SimulationTime?)null);
            }
        }

        [Test]
        public void BusierDay_EightyPlusDeparturesWithABigDawnFirstWave()
        {
            // ADR 0111: more Qantas / Virgin / Jetstar / Rex / QantasLink frames, most of
            // them night-stopping on the apron so 05:00–06:59 is the day's biggest bank.
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = NewGame(clock);
            var start = ops.Clock.AtLocal(ops.Clock.LocalAt(clock.Now).Date.AddDays(1));
            var end = start.Advance(86400);
            var seen = new HashSet<string>();
            var dawn = 0;
            var parkedAtFour = -1;
            while (clock.Now.CompareTo(end) < 0)
            {
                clock.Advance(30);
                ops.Update();
                if (clock.Now.CompareTo(start) < 0)
                    continue;
                var local = ops.Clock.LocalAt(clock.Now);
                if (parkedAtFour < 0 && local.Hour == 4 && local.Minute == 30)
                    parkedAtFour = ops.Fleet.Count(a => !a.Airline.IsPlayer && a.State == FleetState.AtStand);
                foreach (var aircraft in ops.Fleet)
                {
                    if (aircraft.Airline.IsPlayer || aircraft.State != FleetState.TaxiOut)
                        continue;
                    if (!seen.Add($"{aircraft.Registration}@{aircraft.StateStartedAt.ElapsedSeconds}"))
                        continue;
                    var hour = ops.Clock.LocalAt(aircraft.StateStartedAt).Hour;
                    if (hour is 5 or 6)
                        dawn++;
                }
            }

            Assert.That(ops.Fleet.Count(a => !a.Airline.IsPlayer), Is.GreaterThanOrEqualTo(30));
            Assert.That(seen.Count, Is.InRange(75, 110), "a realistic busy regional-capital day");
            Assert.That(parkedAtFour, Is.GreaterThanOrEqualTo(15), "most of the fleet night-stops on the apron");
            Assert.That(dawn, Is.GreaterThanOrEqualTo(15), "the first wave is the big morning bank");
        }

        [Test]
        public void SnapToBankLocal_PullsReadyTimesOntoSharedPublishedMarks()
        {
            var day = new DateTime(2026, 9, 24);
            DateTime Snap(int h, int m) =>
                AdelaideHourProfile.SnapToBankLocal(day.AddHours(h).AddMinutes(m), 5, 22);

            Assert.That(Snap(5, 50), Is.EqualTo(day.AddHours(6)), "05:50 publishes on the 06:00 bank");
            Assert.That(Snap(5, 52), Is.EqualTo(Snap(5, 58)), "two ready times share one departure time");
            Assert.That(Snap(6, 47), Is.EqualTo(day.AddHours(7)));
            Assert.That(Snap(7, 2), Is.EqualTo(day.AddHours(7).AddMinutes(5)), "no anchor near: next 5-minute mark");
            Assert.That(Snap(22, 58), Is.EqualTo(day.AddHours(23)), "23:00 is the last flight");
            Assert.That(Snap(20, 10).Date, Is.EqualTo(day), "the evening wind-down does not roll to tomorrow");
            var late = AdelaideHourProfile.SnapToBankLocal(day.AddHours(23).AddMinutes(10), 5, 22);
            Assert.That(late.Date, Is.EqualTo(day.AddDays(1)));
            Assert.That(late.Hour * 60 + late.Minute, Is.InRange(5 * 60, 6 * 60 + 30), "tomorrow's first wave");
            Assert.That(Snap(9, 7), Is.GreaterThanOrEqualTo(day.AddHours(9).AddMinutes(7)), "never earlier");
        }

        [Test]
        public void FirstWave_SpreadsNightStoppedAircraftAcrossFiveToSixThirty()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = NewGame(clock);
            var day = new DateTime(2026, 9, 25);
            var times = ops.Fleet.Where(a => !a.Airline.IsPlayer && !a.Airline.IsEmergency)
                .Select(a => AdelaideHourProfile.FirstWaveLocal(day, AirlineOperations.FirstWaveKey(a)))
                .ToList();
            Assert.That(times.All(t => t >= day.AddHours(5) && t <= day.AddHours(6).AddMinutes(30)), Is.True);
            Assert.That(times.Distinct().Count(), Is.GreaterThanOrEqualTo(3), "not everyone at one minute");
            Assert.That(times.GroupBy(t => t).Max(g => g.Count()), Is.GreaterThanOrEqualTo(2),
                "but some share a departure time, like a real first wave");
        }

        [Test]
        public void Gates_AreSizedToTheAircraft()
        {
            Assert.That(AircraftCatalogue.CodeLetter(AircraftType.AirbusA350900), Is.EqualTo('E'));
            Assert.That(AircraftCatalogue.CodeLetter(AircraftType.Boeing7378), Is.EqualTo('C'));
            Assert.That(AircraftCatalogue.CodeLetter(AircraftType.Saab340), Is.EqualTo('B'));
            Assert.That(AirlineOperations.StandFits(AircraftType.AirbusA350900, new StableId("GATE-13")), Is.False,
                "no widebody on a narrowbody gate");
            Assert.That(AirlineOperations.StandFits(AircraftType.AirbusA350900, new StableId("GATE-18")), Is.True);
            Assert.That(AirlineOperations.StandFits(AircraftType.Boeing78710, new StableId("GATE-28R")), Is.False);
            Assert.That(AirlineOperations.StandFits(AircraftType.Boeing7378, new StableId("GATE-18")), Is.True);
            Assert.That(AirlineOperations.IsOversized(AircraftType.Boeing7378, new StableId("GATE-18")), Is.True);
            Assert.That(AirlineOperations.StandFits(AircraftType.AirbusA350900, AirlineOperations.AdelaideRegionalBays[0]),
                Is.False);
        }

        [Test]
        public void NewGameAndADay_EveryParkedAircraftFitsItsGate()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = NewGame(clock);
            for (var step = 0; step <= 24 * 60; step++)
            {
                if (step > 0)
                {
                    clock.Advance(60);
                    ops.Update();
                }

                foreach (var aircraft in ops.Fleet)
                    if (aircraft.State is FleetState.AtStand or FleetState.TaxiIn)
                        Assert.That(AirlineOperations.StandFits(aircraft.Type, aircraft.Stand), Is.True,
                            $"{aircraft.Registration} ({aircraft.Type.Name}) on {aircraft.Stand}");
            }
        }

        [Test]
        public void OldSave_WidebodyOnANarrowbodyGateStillLoads()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(3), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideStands);
            var singapore = Airline.SingaporeAirlines();
            ops.AddAirline(singapore);
            var restore = typeof(AirlineOperations).GetMethod("RestoreAircraft",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.DoesNotThrow(() => restore!.Invoke(ops, new object[]
            {
                "9V-SCA", singapore, AircraftType.Boeing78710, FleetState.AtStand, clock.Now, null,
                new StableId("GATE-13"), default(StableId), null, null, 0
            }));
            Assert.That(ops.Fleet.Single().Stand, Is.EqualTo(new StableId("GATE-13")),
                "keeps its gate until it departs");
        }

        [Test]
        public void AirborneAt_OnlyDrawsTheLiveFleetsOwnLegs()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = NewGame(clock);
            var drawnAny = false;
            for (var step = 0; step < 12 * 30; step++)
            {
                clock.Advance(120);
                ops.Update();
                var flights = AdelaideDayPlan.AirborneAt(ops, clock.Now);
                foreach (var flight in flights)
                {
                    drawnAny = true;
                    var owner = ops.Fleet.SingleOrDefault(a => a.Airline.Id.Value + a.Registration == flight.Callsign);
                    Assert.That(owner, Is.Not.Null, $"{flight.Callsign} is not a live aircraft");
                    Assert.That(owner.State, Is.EqualTo(FleetState.Outbound).Or.EqualTo(FleetState.Inbound));
                    Assert.That(flight.From.Code == "ADL" || flight.To.Code == "ADL", Is.True);
                }
            }

            Assert.That(drawnAny, Is.True, "some of the fleet is en route during a day");
        }

        [Test]
        public void GateStatus_ReadsLikeADeparturesScreen()
        {
            var clock = new ManualSimulationClock(new SimulationTime(4 * 3600));
            var ops = NewGame(clock);
            var jet = ops.Fleet.First(a => a.Airline.Id.Value == "QFA" && a.State == FleetState.AtStand
                                           && a.Scheduled.HasValue);
            var std = jet.Scheduled.Value.DepartAt;
            Assert.That(FlightBoard.GateStatus(jet, std.Advance(-60 * 60)), Is.EqualTo("Scheduled"));
            Assert.That(FlightBoard.GateStatus(jet, std.Advance(-30 * 60)), Is.EqualTo("Boarding"));
            Assert.That(FlightBoard.GateStatus(jet, std.Advance(-10 * 60)), Is.EqualTo("Final call"));
            Assert.That(FlightBoard.GateStatus(jet, std.Advance(-2 * 60)), Is.EqualTo("Gate closed"));
        }
    }
}
