using System.Collections.Generic;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class FlightNumberTests
    {
        [Test]
        public void EveryPlayerAircraft_ShowsItsOwnFlightNumber()
        {
            // The hashed spare band had 40 slots: with a growing fleet two of the player's aircraft
            // regularly showed the same flight on the board, tags and toasts.
            var player = Airline.Player("Southern Cross Regional", "#1F3A93");
            var fleet = new List<FleetAircraft>();
            for (var i = 0; i < AircraftAcquisition.MaxPlayerAircraft; i++)
                fleet.Add(new FleetAircraft(AirlineOperations.NextPlayerRegistration(fleet), player,
                    AircraftType.Saab340, default, new SimulationTime(0)));

            // Two aircraft on different cities still may not share a number, out or home.
            var seen = new HashSet<string>();
            for (var i = 0; i < fleet.Count; i++)
            {
                var city = i % 2 == 0 ? "KGC" : "MEL";
                Assert.That(seen.Add(FlightNumber.For(player, fleet[i].Registration, city)), Is.True,
                    fleet[i].Registration + " out");
                Assert.That(seen.Add(FlightNumber.For(player, fleet[i].Registration, city, returningHome: true)),
                    Is.True, fleet[i].Registration + " home");
            }
        }

        [Test]
        public void PlayerNumbers_FollowTheMarkAndLeaveRoomForTheReturn()
        {
            Assert.That(FlightNumber.TryPlayerNumber("VH-PAA", out var first), Is.True);
            Assert.That(first, Is.EqualTo(100));
            Assert.That(FlightNumber.TryPlayerNumber("VH-PAB", out var second), Is.True);
            Assert.That(second, Is.EqualTo(102));
            Assert.That(FlightNumber.TryPlayerNumber("vh-pzz", out var last), Is.True);
            Assert.That(last, Is.InRange(100, 998));
            Assert.That(FlightNumber.TryPlayerNumber("VH-ZRC", out _), Is.False, "not a player mark");
            Assert.That(FlightNumber.TryPlayerNumber(null, out _), Is.False);
            var player = Airline.Player("Southern Cross Regional", "#1F3A93");
            Assert.That(FlightNumber.For(player, "VH-PAB", "KGC"), Is.EqualTo("SC102"));
            Assert.That(FlightNumber.For(player, "VH-PAB", "KGC", returningHome: true), Is.EqualTo("SC103"));
        }
    }
}
