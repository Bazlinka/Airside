using System;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Terminal 1 aerobridges: where they stand and when they move (ADR 0113).</summary>
    public sealed class AerobridgeTests
    {
        private static readonly AircraftType[] Jets =
        {
            AircraftType.EmbraerE190, AircraftType.AirbusA220300, AircraftType.AirbusA320200,
            AircraftType.Boeing737800, AircraftType.Boeing7378, AircraftType.AirbusA321Neo,
            AircraftType.AirbusA350900, AircraftType.AirbusA330900, AircraftType.Boeing7879,
            AircraftType.Boeing78710
        };

        [Test]
        public void EveryTerminalFaceGateHasABridge_RemoteAndWestStandsDoNot()
        {
            var bridged = AdelaideAerobridges.Sites.Select(s => s.Gate.Value).ToHashSet();
            foreach (var id in new[] { "GATE-13", "GATE-15", "GATE-18", "GATE-20", "GATE-12L", "GATE-14L",
                         "GATE-16L", "GATE-16R", "GATE-17", "GATE-18R", "GATE-19", "GATE-21", "GATE-22L",
                         "GATE-23", "GATE-24", "GATE-25", "GATE-26L" })
                Assert.That(bridged, Does.Contain(id));
            foreach (var id in new[] { "GATE-20R", "GATE-22R", "GATE-27", "GATE-28L", "GATE-28R", "GATE-29" })
                Assert.That(bridged, Does.Not.Contain(id), $"{id}: remote stand, boards by stairs");
        }

        [Test]
        public void EveryJetsL1DoorIsWithinTheTunnelsReach()
        {
            foreach (var site in AdelaideAerobridges.Sites)
            {
                Assert.That(AdelaideGround.TryTerminalGate(site.Gate, out var gate), Is.True);
                foreach (var type in Jets)
                {
                    var length = AdelaideAerobridges.DockedLength(site, gate, type);
                    Assert.That(length, Is.InRange(AdelaideAerobridges.MinTunnelMetres, AdelaideAerobridges.MaxTunnelMetres),
                        $"{site.Gate} {type.Name}");
                }
            }
        }

        [Test]
        public void TheTunnelRunsBesideTheNoseAndTheParkedCabIsClearOfTheStand()
        {
            foreach (var site in AdelaideAerobridges.Sites)
            {
                AdelaideGround.TryTerminalGate(site.Gate, out var gate);
                var heading = gate.HeadingDegrees * Math.PI / 180.0;
                var fx = Math.Sin(heading);
                var fz = Math.Cos(heading);
                // Lateral position (to the aircraft's right, +) of a point relative to the nose line.
                double Lateral(double x, double z) => (x - gate.NoseX) * fz - (z - gate.NoseZ) * fx;
                double Along(double x, double z) => (x - gate.NoseX) * fx + (z - gate.NoseZ) * fz;

                Assert.That(Along(site.RotundaX, site.RotundaZ), Is.GreaterThan(0),
                    $"{site.Gate}: rotunda ahead of the nose, by the building");
                Assert.That(Lateral(site.RotundaX, site.RotundaZ), Is.LessThan(-8),
                    $"{site.Gate}: rotunda to the aircraft's left, so the tunnel never crosses the nose");
                // Parked cab: ahead of the nose stop (an arriving nose and wing never reach it)
                // and off to the left of the stand's centreline.
                Assert.That(Along(site.ParkedCabX, site.ParkedCabZ), Is.GreaterThan(0.5),
                    $"{site.Gate}: parked cab ahead of the nose stop");
                Assert.That(Lateral(site.ParkedCabX, site.ParkedCabZ), Is.LessThan(-12),
                    $"{site.Gate}: parked cab well clear of the fuselage");
            }
        }

        [Test]
        public void ParkedCabsStayClearOfNeighbouringStandsAircraft()
        {
            foreach (var site in AdelaideAerobridges.Sites)
            foreach (var other in AdelaideLayout.TerminalGates)
            {
                if (other.Id == site.Gate.Value)
                    continue;
                var heading = other.HeadingDegrees * Math.PI / 180.0;
                var fx = Math.Sin(heading);
                var fz = Math.Cos(heading);
                var along = (site.ParkedCabX - other.NoseX) * fx + (site.ParkedCabZ - other.NoseZ) * fz;
                var lateral = Math.Abs((site.ParkedCabX - other.NoseX) * fz - (site.ParkedCabZ - other.NoseZ) * fx);
                // Anything behind a neighbour's nose and inside a widebody's half-width is its fuselage.
                Assert.That(along > 0.5 || lateral > 3.5, Is.True,
                    $"{site.Gate} parked cab sits on {other.Id}'s fuselage");
            }
        }

        [Test]
        public void Arrival_DocksAfterTheBeaconIsOffThenOpensTheDoor()
        {
            var (ops, jet) = JetOnGate();
            var parkedAt = jet.StateStartedAt.ElapsedSeconds;
            Assert.That(AerobridgeTimeline.DockedFraction(jet, parkedAt + 30), Is.EqualTo(0f),
                "the bridge waits for the beacon");
            Assert.That(AerobridgeTimeline.DockedFraction(jet, parkedAt + AerobridgeTimeline.DockAfterParkSeconds + 30),
                Is.InRange(0.1f, 0.9f), "driving out");
            Assert.That(AerobridgeTimeline.DockedFraction(jet, parkedAt + 400), Is.EqualTo(1f));
            Assert.That(EngineStartSequence.For(jet, parkedAt + 100).DoorsOpen, Is.False, "door stays shut until docked");
            Assert.That(EngineStartSequence.For(jet, parkedAt + AerobridgeTimeline.DoorsOpenAfterParkSeconds + 1).DoorsOpen,
                Is.True);
        }

        [Test]
        public void Departure_DoorClosesThenTheBridgePullsBackBeforeTheBeacon()
        {
            var (ops, jet) = JetOnGate();
            DestinationCatalogue.TryFind("MEL", out var melbourne);
            var push = jet.StateStartedAt.Advance(3600);
            jet.Scheduled = new ScheduledDeparture(melbourne, push);
            var at = (double)push.ElapsedSeconds;

            Assert.That(AerobridgeTimeline.DockedFraction(jet, at - 600), Is.EqualTo(1f));
            Assert.That(EngineStartSequence.For(jet, at - AerobridgeTimeline.RetractBeforePushSeconds - 5).DoorsOpen,
                Is.False, "door shut before the bridge moves");
            Assert.That(AerobridgeTimeline.DockedFraction(jet, at - EngineStartSequence.BeaconOnBeforeSeconds), Is.EqualTo(0f),
                "clear of the aircraft before the beacon comes on");
            Assert.That(AerobridgeTimeline.DockedFraction(jet, at + 10), Is.EqualTo(0f));
        }

        [Test]
        public void NoBridgeAwayFromTheStandOrOnARemoteStand()
        {
            var (ops, jet) = JetOnGate();
            Assert.That(AerobridgeTimeline.DockedFraction(null, 0), Is.EqualTo(0f));
            var remote = ops.Fleet.FirstOrDefault(a => a.State == FleetState.AtStand && !AdelaideAerobridges.Serves(a.Stand));
            if (remote != null)
            {
                Assert.That(AerobridgeTimeline.DockedFraction(remote, remote.StateStartedAt.ElapsedSeconds + 1000), Is.EqualTo(0f));
                Assert.That(AerobridgeTimeline.DoorsOpen(remote, remote.StateStartedAt.ElapsedSeconds + 1000), Is.Null,
                    "a stand without a bridge keeps the stair door timing");
            }
        }

        private static (AirlineOperations, FleetAircraft) JetOnGate()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(3), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideStands);
            var qantas = Airline.Qantas();
            ops.AddAirline(qantas);
            var jet = ops.AddAircraft(qantas, "VH-VZX", AircraftType.Boeing737800, new StableId("GATE-21"));
            jet.Scheduled = null;
            return (ops, jet);
        }
    }
}
