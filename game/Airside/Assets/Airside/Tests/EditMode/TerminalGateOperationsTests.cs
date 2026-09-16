using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    /// <summary>
    /// Gate 13 as a real terminal stand for one AI 737-8 (ADR 0047): pavement, route
    /// continuity, heading, reservations, save/catch-up and type-aware presentation.
    /// </summary>
    public sealed class TerminalGateOperationsTests
    {
        private static readonly StableId Gate13 = new("GATE-13");
        private const string JetRegistration = "VH-8IA";

        private static AirlineOperations NewGame(out ManualSimulationClock clock, int seed = 77)
        {
            clock = new ManualSimulationClock(new SimulationTime(0));
            return AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource((uint)seed), Airline.Player("Gate Test Air", "#1F3A93"));
        }

        private static FleetAircraft Jet(AirlineOperations ops) => ops.Fleet.Single(a => a.Registration == JetRegistration);

        // ---- Stand system ---------------------------------------------------------------

        [Test]
        public void NewGame_HasExactlyOne737_ParkedAtGate13()
        {
            var ops = NewGame(out _);
            var jets = ops.Fleet.Where(a => a.Type == AircraftType.Boeing7378).ToList();
            Assert.That(jets.Count, Is.EqualTo(1), "only one 737 exists");
            var jet = jets[0];
            Assert.That(jet.Airline.Name, Is.EqualTo("Virgin Australia"));
            Assert.That(jet.Airline.IsPlayer, Is.False, "the player is not given a 737");
            Assert.That(jet.State, Is.EqualTo(FleetState.AtStand));
            Assert.That(jet.Stand, Is.EqualTo(Gate13));
            Assert.That(jet.Scheduled.HasValue, Is.True);
            Assert.That(AirlineOperations.VirginRotation, Does.Contain(jet.Scheduled.Value.Destination.Code));
        }

        [Test]
        public void Gate13_NeverResolvesAsARegionalBay()
        {
            Assert.That(AdelaideGround.IsTerminalGate(Gate13), Is.True);
            Assert.Throws<InvalidOperationException>(() => AdelaideGround.Bay(Gate13));
            Assert.That(AirlineOperations.AdelaideRegionalBays, Has.No.Member(Gate13));

            var gate = AdelaideLayout.TerminalGates.Single(g => g.Id == "GATE-13");
            var parked = AdelaideGround.StandPose(Gate13);
            Assert.That(parked.X, Is.EqualTo(gate.NoseX).Within(0.01f));
            Assert.That(parked.Z, Is.EqualTo(gate.NoseZ).Within(0.01f));
            Assert.That(AdelaideGround.TaxiOut(Gate13).PoseAt(0).X, Is.EqualTo(gate.NoseX).Within(0.01f));
            Assert.That(AdelaideGround.StandLabel(Gate13), Is.EqualTo("Gate 13"));

            var bay1 = AdelaideLayout.Bays[0];
            Assert.That(Math.Abs(parked.X - bay1.StopX), Is.GreaterThan(300f), "not the 50D fallback");
        }

        [Test]
        public void StandsAreTypeSeparated_BothWays()
        {
            var ops = NewGame(out _);
            Assert.That(ops.FreeStands(), Has.No.Member(Gate13), "HUD/player stand lists never offer a gate");
            Assert.That(AirlineOperations.StandFits(AircraftType.Atr42, Gate13), Is.False);
            Assert.That(AirlineOperations.StandFits(AircraftType.Boeing7378, Gate13), Is.True);
            Assert.That(AirlineOperations.StandFits(AircraftType.Boeing7378, new StableId("BAY-1")), Is.False);
            Assert.That(ops.FreeStandsFor(AircraftType.Boeing7378).All(AdelaideGround.IsTerminalGate), Is.True);
        }

        // ---- Pavement, continuity, heading -------------------------------------------------

        private static IEnumerable<(double t, GroundPose pose)> Sample(GroundLeg leg, double step)
        {
            for (var t = 0.0; t <= leg.Seconds + 1e-6; t += step)
                yield return (t, leg.PoseAt(t));
            yield return (leg.Seconds, leg.PoseAt(leg.Seconds));
        }

        private static float Degrees(float ax, float az, float bx, float bz) =>
            Mathf.Acos(Mathf.Clamp(ax * bx + az * bz, -1f, 1f)) * Mathf.Rad2Deg;

        [Test]
        public void Gate13Routes_NoseAndMainGearStayOnRenderedPavement()
        {
            foreach (var (name, leg) in new[] { ("taxi-in", AdelaideGround.TaxiIn(Gate13)), ("taxi-out", AdelaideGround.TaxiOut(Gate13)) })
            {
                foreach (var (t, pose) in Sample(leg, 0.5))
                {
                    Assert.That(AirsideAdelaidePavement.DistanceToPavement(pose.X, pose.Z), Is.LessThanOrEqualTo(0.5f),
                        $"{name} nose off pavement at {t:0.0}s ({pose.X:0.0}, {pose.Z:0.0})");
                    var mainsX = pose.X - pose.NoseX * AdelaideGround.JetTrackMetres;
                    var mainsZ = pose.Z - pose.NoseZ * AdelaideGround.JetTrackMetres;
                    Assert.That(AirsideAdelaidePavement.DistanceToPavement(mainsX, mainsZ), Is.LessThanOrEqualTo(0.5f),
                        $"{name} main gear off pavement at {t:0.0}s ({mainsX:0.0}, {mainsZ:0.0})");
                }
            }
        }

        [Test]
        public void Gate13ApronLink_FillsTheGapWithoutOverlappingTheOsmApron()
        {
            var link = AdelaideLayout.Aprons.Single(a => a.Name == "Gate 13 apron link");
            // The old unpaved band on the parking line is now inside the link.
            Assert.That(AirsideAdelaidePavement.ContainsPolygon(link.Xz, 1571f, 330f), Is.True);
            foreach (var other in AdelaideLayout.Aprons.Where(a => a.Name != link.Name))
                for (var i = 0; i + 1 < link.Xz.Length; i += 2)
                {
                    // Vertices may touch the OSM edge but none lies strictly inside another apron.
                    var inset = new Vector2(link.Xz[i], link.Xz[i + 1]) + (new Vector2(1573f, 326f) - new Vector2(link.Xz[i], link.Xz[i + 1])).normalized * 0.5f;
                    Assert.That(AirsideAdelaidePavement.ContainsPolygon(other.Xz, inset.x, inset.y), Is.False,
                        $"link vertex {i / 2} overlaps {other.Name}");
                }
        }

        [Test]
        public void Gate13Routes_ContinuousWithoutTeleportOrUnexpectedNoseReversal()
        {
            foreach (var (name, leg) in new[] { ("taxi-in", AdelaideGround.TaxiIn(Gate13)), ("taxi-out", AdelaideGround.TaxiOut(Gate13)) })
            {
                GroundPose? previous = null;
                const double step = 0.1;
                foreach (var (t, pose) in Sample(leg, step))
                {
                    if (previous is { } p)
                    {
                        var jump = Mathf.Sqrt(Mathf.Pow(pose.X - p.X, 2) + Mathf.Pow(pose.Z - p.Z, 2));
                        var physicalLimit = Math.Max(p.Speed, pose.Speed) * (float)step + 0.05f;
                        Assert.That(jump, Is.LessThan(physicalLimit), $"{name} teleport at {t:0.0}s");
                        Assert.That(Degrees(pose.NoseX, pose.NoseZ, p.NoseX, p.NoseZ), Is.LessThan(4f), $"{name} nose snapped at {t:0.0}s");
                    }

                    previous = pose;
                }
            }
        }

        [Test]
        public void Gate13_NoseInToTheStop_TailFirstPushback_ThenForwardTurnout()
        {
            var gate = AdelaideLayout.TerminalGates.Single(g => g.Id == "GATE-13");
            var hx = Mathf.Sin(gate.HeadingDegrees * Mathf.Deg2Rad);
            var hz = Mathf.Cos(gate.HeadingDegrees * Mathf.Deg2Rad);

            var taxiIn = AdelaideGround.TaxiIn(Gate13);
            var arrived = taxiIn.PoseAt(taxiIn.Seconds);
            Assert.That(Mathf.Sqrt(Mathf.Pow(arrived.X - gate.NoseX, 2) + Mathf.Pow(arrived.Z - gate.NoseZ, 2)), Is.LessThan(0.1f));
            Assert.That(Degrees(arrived.NoseX, arrived.NoseZ, hx, hz), Is.LessThan(3f), "parks facing the terminal");
            Assert.That(arrived.TailFirst, Is.False, "taxis in nose first");

            var taxiOut = AdelaideGround.TaxiOut(Gate13);
            var push = taxiOut.Parts[0];
            Assert.That(push.TailFirst, Is.True, "pushback is tail first");
            var startPush = taxiOut.PoseAt(0);
            Assert.That(Degrees(startPush.NoseX, startPush.NoseZ, hx, hz), Is.LessThan(3f), "pushback starts at the parked heading");

            var early = taxiOut.PoseAt(push.Path.Seconds * 0.2);
            var travelX = early.X - gate.NoseX;
            var travelZ = early.Z - gate.NoseZ;
            Assert.That(travelX * hx + travelZ * hz, Is.LessThan(-1f), "the aircraft moves backwards, away from the terminal");

            var pushed = taxiOut.PoseAt(push.Path.Seconds);
            var disconnected = taxiOut.PoseAt(push.Path.Seconds + AdelaideGround.TugDisconnectSeconds * 0.5);
            var taxi = taxiOut.Parts[1];
            var rolling = taxiOut.PoseAt(push.Seconds + taxi.PauseBeforeSeconds + 8.0);
            Assert.That(disconnected.Speed, Is.EqualTo(0f), "stopped while the tug disconnects");
            Assert.That(Degrees(pushed.NoseX, pushed.NoseZ, rolling.NoseX, rolling.NoseZ), Is.LessThan(2f), "no heading step at the seam");
            Assert.That(rolling.TailFirst, Is.False, "then taxis out forward");
            Assert.That((rolling.X - pushed.X) * rolling.NoseX + (rolling.Z - pushed.Z) * rolling.NoseZ, Is.GreaterThan(0f),
                "moving the way the nose points");
            Assert.That(Degrees(pushed.NoseX, pushed.NoseZ, hx, hz), Is.GreaterThan(60f), "turned out of the gate");
        }

        [Test]
        public void RegionalBays_KeepTangentSteering()
        {
            foreach (var part in AdelaideGround.TaxiOut(new StableId("BAY-1")).Parts)
                Assert.That(part.TrackMetres, Is.EqualTo(0f));
            foreach (var part in AdelaideGround.TaxiIn(new StableId("BAY-6")).Parts)
                Assert.That(part.TrackMetres, Is.EqualTo(0f));
        }

        // ---- Reservations -----------------------------------------------------------------------

        [Test]
        public void Reservations_GateLeadInAndRunwayHeldBeforeMovementAndReleased()
        {
            var ops = NewGame(out var clock);
            var jet = Jet(ops);
            var lead = AdelaideGround.LeadInResource(Gate13);
            var sawTaxiOut = false;
            var sawTaxiIn = false;
            var previous = jet.State;

            for (var steps = 0; steps < 40000 && clock.Now.ElapsedSeconds < 3 * 24 * 3600; steps++)
            {
                var next = ops.NextEventAt();
                if (next == null)
                    break;
                clock.Set(next.Value);
                ops.Update();

                Assert.That(ops.Fleet.Count(a => a.State is FleetState.TakingOff or FleetState.Landing), Is.LessThanOrEqualTo(1), "runway");
                var gateHolders = ops.Fleet.Count(a =>
                    (a.State is FleetState.AtStand or FleetState.TaxiIn && a.Stand.Equals(Gate13))
                    || (a.State == FleetState.TaxiOut && a.DepartureStand.Equals(Gate13)));
                Assert.That(gateHolders, Is.LessThanOrEqualTo(1), "gate double-booked");
                Assert.That(ops.Fleet.Where(a => a != jet).All(a => !a.Stand.Equals(Gate13) && !a.DepartureStand.Equals(Gate13)), Is.True,
                    "only the jet ever uses Gate 13");

                switch (jet.State)
                {
                    case FleetState.TaxiOut:
                        sawTaxiOut = true;
                        Assert.That(ops.GroundResourceHolder(lead), Is.SameAs(jet), "lead-in held for the pushback and taxi out");
                        Assert.That(ops.IsStandFree(Gate13), Is.False, "gate held until the jet has left it");
                        break;
                    case FleetState.TaxiIn:
                        sawTaxiIn = true;
                        Assert.That(jet.Stand, Is.EqualTo(Gate13), "never resolves to a regional bay");
                        Assert.That(ops.GroundResourceHolder(lead), Is.SameAs(jet));
                        Assert.That(ops.GroundResourceHolder(Gate13.Value), Is.SameAs(jet));
                        break;
                    case FleetState.HoldingShort:
                    case FleetState.Outbound:
                        Assert.That(ops.GroundResourceHolder(lead), Is.Null, "lead-in released after taxi out");
                        Assert.That(ops.IsStandFree(Gate13), Is.True, "gate released once the jet has taxied away");
                        break;
                    case FleetState.AtStand:
                        Assert.That(ops.GroundResourceHolder(lead), Is.Null, "lead-in released after taxi in");
                        break;
                }

                if (previous == FleetState.AwaitingStand && jet.State == FleetState.TaxiIn)
                    Assert.That(jet.Stand, Is.EqualTo(Gate13));
                previous = jet.State;
            }

            Assert.That(sawTaxiOut && sawTaxiIn, Is.True, "the jet completed a full taxi out and taxi in");
            Assert.That(jet.CompletedTrips, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void Reservations_ASecondJetWaitsForTheGateAndItsLeadIn()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(3), DestinationCatalogue.Adelaide, AirlineOperations.AdelaideStands);
            var operatorAirline = Airline.VirginAustralia();
            ops.AddAirline(operatorAirline);
            var first = ops.AddAircraft(operatorAirline, "VH-8IA", AircraftType.Boeing7378, Gate13);
            Assert.Throws<InvalidOperationException>(() => ops.AddAircraft(operatorAirline, "VH-8IB", AircraftType.Boeing7378, Gate13));
            Assert.Throws<InvalidOperationException>(() => ops.AddAircraft(operatorAirline, "VH-8IC", AircraftType.Boeing7378, new StableId("BAY-1")));
            Assert.That(ops.AssignStand(first, new StableId("BAY-1")).Accepted, Is.False);
            Assert.That(ops.GroundResourceHolder(Gate13.Value), Is.SameAs(first));
        }

        // ---- Save, catch-up, backfill -----------------------------------------------------------

        private static string Fingerprint(AirlineOperations ops) => string.Join("|", ops.Fleet
            .OrderBy(a => a.Registration)
            .Select(a => $"{a.Registration}:{a.State}:{a.StateStartedAt.ElapsedSeconds}:{a.Stand.Value}:{a.DepartureStand.Value}:{a.CurrentDestination?.Code}:{a.Scheduled?.Destination.Code}:{a.Scheduled?.DepartAt.ElapsedSeconds}:{a.CompletedTrips}"));

        [Test]
        public void SaveAndCatchUp_JetResumesExactlyAsLivePlay()
        {
            var live = NewGame(out var liveClock, seed: 1234);
            var saved = NewGame(out var savedClock, seed: 1234);
            liveClock.Set(new SimulationTime(9 * 3600));
            live.Update();
            savedClock.Set(new SimulationTime(9 * 3600));
            saved.Update();

            var json = JsonUtility.ToJson(AirlineSave.Capture(saved));
            Assert.That(json, Does.Contain("GATE-13").And.Contain("B38M").And.Contain("VOZ"), "saved with the existing schema");
            var restoredClock = new ManualSimulationClock(new SimulationTime(9 * 3600));
            var restored = AirlineSave.Restore(JsonUtility.FromJson<AirlineSaveData>(json), restoredClock);
            Assert.That(restored.AddMissingTerminalOperators(), Is.EqualTo(0), "no duplicate after a normal reload");

            // Catch up a long absence in one jump; live play steps through it.
            restoredClock.Set(new SimulationTime(40 * 3600));
            restored.Update();
            for (var t = 9 * 3600L; t < 40 * 3600L; t += 60)
            {
                liveClock.Set(new SimulationTime(t));
                live.Update();
            }
            liveClock.Set(new SimulationTime(40 * 3600));
            live.Update();

            Assert.That(Fingerprint(restored), Is.EqualTo(Fingerprint(live)));
            Assert.That(Jet(restored).CompletedTrips, Is.GreaterThan(0));
        }

        [Test]
        public void OldSave_GainsTheJetOnce_WithoutDuplicates()
        {
            var ops = NewGame(out var clock);
            var data = JsonUtility.FromJson<AirlineSaveData>(JsonUtility.ToJson(AirlineSave.Capture(ops)));
            // Make it a save from before this change: no Virgin Australia, no 737.
            data.Fleet.RemoveAll(r => r.Registration == JetRegistration);
            data.Airlines.RemoveAll(r => r.Id == "VOZ");

            var restored = AirlineSave.Restore(data, new ManualSimulationClock(new SimulationTime(data.ClockSeconds)));
            Assert.That(restored.Fleet.Any(a => a.Type == AircraftType.Boeing7378), Is.False);
            var regionalBefore = Fingerprint(restored);

            Assert.That(restored.AddMissingTerminalOperators(), Is.EqualTo(1));
            Assert.That(restored.AddMissingTerminalOperators(), Is.EqualTo(0), "idempotent");
            Assert.That(restored.Fleet.Count(a => a.Type == AircraftType.Boeing7378), Is.EqualTo(1));
            Assert.That(Jet(restored).Stand, Is.EqualTo(Gate13));
            Assert.That(Fingerprint(restored).Replace(Fingerprint(restored).Split('|').Single(f => f.StartsWith(JetRegistration)) + "|", string.Empty)
                .Replace("|" + Fingerprint(restored).Split('|').Single(f => f.StartsWith(JetRegistration)), string.Empty),
                Is.EqualTo(regionalBefore), "backfill leaves every other aircraft exactly as it was");

            // And the backfilled save round-trips without gaining a second jet.
            var again = AirlineSave.Restore(JsonUtility.FromJson<AirlineSaveData>(JsonUtility.ToJson(AirlineSave.Capture(restored))),
                new ManualSimulationClock(new SimulationTime(data.ClockSeconds)));
            Assert.That(again.AddMissingTerminalOperators(), Is.EqualTo(0));
            Assert.That(again.Fleet.Count(a => a.Type == AircraftType.Boeing7378), Is.EqualTo(1));
        }

        [Test]
        public void Backfill_SkipsWhenTheGateIsNotAvailable()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var regionalOnly = new AirlineOperations(clock, new SeededRandomSource(9), DestinationCatalogue.Adelaide, AirlineOperations.AdelaideRegionalBays);
            regionalOnly.AddAirline(Airline.Player("No Gate Air", "#2E7D32"));
            Assert.That(regionalOnly.AddMissingTerminalOperators(), Is.EqualTo(0));
            Assert.That(regionalOnly.Airlines.Any(a => a.Id.Value == "VOZ"), Is.False, "no orphan airline without its aircraft");
        }

        // ---- Presentation identity ---------------------------------------------------------------

        [Test]
        public void JetIdentity_RendersAsAir005WithOperationalLabels()
        {
            var ops = NewGame(out _);
            var jet = Jet(ops);
            Assert.That(AircraftVisualProfiles.For(jet.Type), Is.EqualTo(AircraftVisualProfiles.Boeing7378));
            Assert.That(AircraftVisualProfiles.For(AircraftType.Atr42), Is.EqualTo(AircraftVisualProfiles.RegionalTurboprop));
            Assert.That(StandNames.Display(jet.Stand), Is.EqualTo("Gate 13"));
            Assert.That(FlightBoard.RouteText(jet), Does.Contain("ADL"));
            Assert.That(FlightBoard.PhaseLabel(jet), Is.EqualTo("Scheduled"));
        }
    }
}
