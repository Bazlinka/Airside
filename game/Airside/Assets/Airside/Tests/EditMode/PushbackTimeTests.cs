using System;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0137 — the board's pushback times: published, delayed, estimated and actual.</summary>
    public sealed class PushbackTimeTests
    {
        private static Destination Code(string code)
        {
            Assert.That(DestinationCatalogue.TryFind(code, out var destination), Is.True, code);
            return destination;
        }

        private static (ManualSimulationClock clock, AirlineOperations ops, FleetAircraft plane) PlayerOnly()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(11), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Test Air", "#39708A");
            ops.AddAirline(player);
            var plane = ops.AddAircraft(player, "VH-PAA", AircraftType.Atr42, AirlineOperations.AdelaideRegionalBays[0]);
            return (clock, ops, plane);
        }

        private static void RunTo(ManualSimulationClock clock, AirlineOperations ops, long seconds)
        {
            clock.Set(new SimulationTime(seconds));
            ops.Update();
        }

        private static string Clock(SimulationTime t) => $"T{t.ElapsedSeconds}";

        [Test]
        public void ADelayIsCountedOnce_FromThePublishedTime()
        {
            var published = new SimulationTime(36_000);
            var delayed = new ScheduledDeparture(Code("KGC"), published.Advance(20 * 60), publishedAt: published);
            Assert.That(delayed.DelayMinutes, Is.EqualTo(20));
            Assert.That(delayed.PublishedAt, Is.EqualTo(published));

            // An old save only kept the delayed time and the minutes: the published time comes back.
            var legacy = new ScheduledDeparture(Code("KGC"), published.Advance(20 * 60), 20);
            Assert.That(legacy.PublishedAt, Is.EqualTo(published));
            Assert.That(legacy.DelayMinutes, Is.EqualTo(20));

            // A published time after pushback is nonsense: it clamps to on time.
            var early = new ScheduledDeparture(Code("KGC"), published, publishedAt: published.Advance(600));
            Assert.That(early.DelayMinutes, Is.Zero);
        }

        [Test]
        public void ADelayedDeparture_ShowsItsPublishedTimeWithTheNewTimeUnderEst()
        {
            var (_, _, plane) = PlayerOnly();
            var published = new SimulationTime(3_600);
            plane.Scheduled = new ScheduledDeparture(Code("KGC"), published.Advance(15 * 60), publishedAt: published);

            Assert.That(FlightBoard.BoardTime(plane, arrivals: false, Clock), Is.EqualTo("T3600"));
            Assert.That(FlightBoard.EstimatedTime(plane, false, new SimulationTime(0), Clock), Is.EqualTo("T4500"));
            Assert.That(FlightBoard.PhaseLabel(plane, new SimulationTime(0)), Is.EqualTo("Delayed +15"));
            // Not late until the delayed pushback itself passes.
            Assert.That(FlightBoard.DepartureDelayMinutes(plane, new SimulationTime(4_000)), Is.Zero);
        }

        [Test]
        public void ADeparturesTime_StaysPutThroughTaxiTakeoffAndClimb()
        {
            var (clock, ops, plane) = PlayerOnly();
            var departAt = 1_200L;
            Assert.That(ops.ScheduleDeparture(plane, Code("KGC"), new SimulationTime(departAt)).Accepted, Is.True);
            var seen = new System.Collections.Generic.HashSet<FleetState>();
            for (var t = 0L; t < departAt + 3_600; t += 5)
            {
                RunTo(clock, ops, t);
                if (!FlightBoard.IsDeparture(plane))
                    continue;
                seen.Add(plane.State);
                Assert.That(FlightBoard.BoardTimeSeconds(plane, arrivals: false), Is.EqualTo(departAt),
                    $"TIME moved while {plane.State}");
            }

            Assert.That(seen, Has.Member(FleetState.TaxiOut).And.Member(FleetState.Outbound));
        }

        [Test]
        public void ALatePushback_ShowsWhenItActuallyLeftUnderEst()
        {
            var (_, _, plane) = PlayerOnly();
            plane.PublishedDepartureAt = new SimulationTime(600);
            plane.PushedBackAt = new SimulationTime(900);
            plane.Restore(FleetState.TaxiOut, new SimulationTime(900), new SimulationTime(1_200));
            Assert.That(FlightBoard.BoardTime(plane, arrivals: false, Clock), Is.EqualTo("T600"));
            Assert.That(FlightBoard.EstimatedTime(plane, false, new SimulationTime(1_000), Clock), Is.EqualTo("T900"));

            plane.PushedBackAt = new SimulationTime(630);
            Assert.That(FlightBoard.EstimatedTime(plane, false, new SimulationTime(1_000), Clock), Is.EqualTo("—"),
                "under a minute late is on time");
        }

        [Test]
        public void AHeldDeparture_ShowsAnEstimateThatKeepsUpWithTheClock()
        {
            var (_, _, plane) = PlayerOnly();
            plane.Scheduled = new ScheduledDeparture(Code("KGC"), new SimulationTime(600));
            plane.PrepStartedAt = new SimulationTime(0);
            Assert.That(FlightBoard.EstimatedTime(plane, false, new SimulationTime(100), Clock), Is.EqualTo("—"));
            // Five minutes past its time and still on the gate: the estimate is the next minute.
            Assert.That(FlightBoard.EstimatedTime(plane, false, new SimulationTime(900), Clock), Is.EqualTo("T960"));
        }

        [Test]
        public void PrepTime_ComesFromTheBaseEverywhere()
        {
            var (_, ops, plane) = PlayerOnly();
            ops.CareerState.BaseLevel = PlayerBaseLevel.JetGate;
            var faster = DeparturePrep.TotalSeconds(plane.Type, PlayerBaseLevel.JetGate);
            Assert.That(faster, Is.LessThan(DeparturePrep.TotalSeconds(plane.Type, PlayerBaseLevel.Starter)),
                "the test needs a base that speeds prep up");
            Assert.That(plane.BaseLevel, Is.EqualTo(PlayerBaseLevel.JetGate));

            Assert.That(ops.ScheduleDeparture(plane, Code("KGC"), new SimulationTime(3_600)).Accepted, Is.True);
            var now = new SimulationTime(3_600 - faster / 2);
            var simulation = DeparturePrep.For(plane, now, ops.CareerState.BaseLevel);
            Assert.That(DeparturePrep.For(plane, now).Label, Is.EqualTo(simulation.Label));
            Assert.That(FlightBoard.PhaseLabel(plane, now), Is.EqualTo(simulation.Label));
            Assert.That(OperationsSummary.CompactState(plane, now), Is.EqualTo(simulation.Label));
            Assert.That(DeparturePrep.ReadyAtSeconds(plane), Is.EqualTo(3_600));
        }

        [Test]
        public void ReopeningThePlanner_NeverPushesABookedFlightLater()
        {
            var (_, ops, plane) = PlayerOnly();
            var lead = DeparturePrep.LeadSeconds(plane.Type, plane.BaseLevel);
            Assert.That(ops.ScheduleDeparture(plane, Code("KGC"), new SimulationTime(lead)).Accepted, Is.True);
            var now = new SimulationTime(lead - 60);
            var reopened = FlightPlanner.ClampDelay(60, plane, now);
            Assert.That(now.Advance(reopened), Is.EqualTo(new SimulationTime(lead)));

            // A new booking still has to leave room for prep.
            var (_, _, idle) = PlayerOnly();
            Assert.That(FlightPlanner.ClampDelay(60, idle, now), Is.EqualTo(lead));
        }

        [Test]
        public void WholeMinute_RoundsUpOnly()
        {
            Assert.That(AirlineOperations.WholeMinute(new SimulationTime(600)).ElapsedSeconds, Is.EqualTo(600));
            Assert.That(AirlineOperations.WholeMinute(new SimulationTime(601)).ElapsedSeconds, Is.EqualTo(660));
            Assert.That(AirlineOperations.WholeMinute(new SimulationTime(659)).ElapsedSeconds, Is.EqualTo(660));
        }

        [Test]
        public void AiDepartures_ArePublishedOnWholeMinutes_AndDelaysCountFromThere()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(5),
                Airline.Player("Day Air", "#1F3A93"));
            var delayed = 0;
            for (var t = 0L; t < 36 * 3_600; t += 300)
            {
                RunTo(clock, ops, t);
                foreach (var aircraft in ops.Fleet)
                {
                    if (aircraft.Airline.IsPlayer || aircraft.Scheduled is not { } booked || booked.Cancelled)
                        continue;
                    Assert.That(booked.PublishedAt.CompareTo(booked.DepartAt), Is.LessThanOrEqualTo(0));
                    Assert.That(booked.DepartAt.ElapsedSeconds - booked.PublishedAt.ElapsedSeconds,
                        Is.EqualTo(booked.DelayMinutes * 60L), aircraft.Registration);
                    if (booked.DelayMinutes > 0)
                        delayed++;
                }
            }

            Assert.That(delayed, Is.GreaterThan(0), "the run should see at least one delayed flight");
        }
    }
}
