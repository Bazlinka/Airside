using System;
using System.Linq;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Terminal doors and background people: pure geometry and the simulation-clock walk.</summary>
    public sealed class TerminalPeopleTests
    {
        [Test]
        public void EveryRegionalBayHasADoorThatFacesIt()
        {
            Assert.That(AdelaideTerminalDoors.Doors.Count, Is.InRange(2, AdelaideLayout.Bays.Length));
            foreach (var bay in AdelaideLayout.Bays)
            {
                Assert.That(AdelaideTerminalDoors.TryForBay(bay.Id, out var door), Is.True, bay.Id);
                Assert.That(door.NormalX * door.NormalX + door.NormalZ * door.NormalZ, Is.EqualTo(1f).Within(0.01f), bay.Id);
                var toBay = door.NormalX * (bay.StopX - door.X) + door.NormalZ * (bay.StopZ - door.Z);
                Assert.That(toBay, Is.GreaterThan(0f), bay.Id + " is behind its door");
            }

            Assert.That(AdelaideTerminalDoors.TryForBay("nowhere", out _), Is.False);
        }

        [Test]
        public void DoorsSitOnTheTerminalWallAwayFromCorners()
        {
            var outline = AdelaideLayout.Terminals.First(t => t.Name == AdelaideTerminalDoors.TerminalName).Xz;
            foreach (var door in AdelaideTerminalDoors.Doors)
            {
                var best = float.MaxValue;
                var corner = float.MaxValue;
                var n = outline.Length / 2;
                for (var i = 0; i < n; i++)
                {
                    var j = (i + 1) % n;
                    var ax = outline[i * 2];
                    var az = outline[i * 2 + 1];
                    var bx = outline[j * 2] - ax;
                    var bz = outline[j * 2 + 1] - az;
                    var l2 = bx * bx + bz * bz;
                    var t = l2 < 1e-6f ? 0f : Math.Max(0f, Math.Min(1f, ((door.X - ax) * bx + (door.Z - az) * bz) / l2));
                    var d = (float)Math.Sqrt(Math.Pow(door.X - (ax + bx * t), 2) + Math.Pow(door.Z - (az + bz * t), 2));
                    best = Math.Min(best, d);
                    corner = Math.Min(corner, (float)Math.Sqrt((door.X - ax) * (door.X - ax) + (door.Z - az) * (door.Z - az)));
                }

                Assert.That(best, Is.LessThan(1.5f), door.Id + " is not on the wall");
                Assert.That(corner, Is.GreaterThan(4f), door.Id + " is at a corner");
            }
        }

        [Test]
        public void WalkwayCorridorsStartInFrontOfTheirDoor()
        {
            foreach (var pair in AdelaideWalkwayGeometry.Corridors)
            {
                Assert.That(AdelaideTerminalDoors.TryForBay(pair.Key, out var door), Is.True, pair.Key);
                Assert.That(pair.Value[0], Is.EqualTo(door.ThresholdX).Within(0.01f), pair.Key);
                Assert.That(pair.Value[1], Is.EqualTo(door.ThresholdZ).Within(0.01f), pair.Key);
            }
        }

        [Test]
        public void BackgroundPeopleWalkTheirLineAndPassengersVanishAtTheEnds()
        {
            var walkers = AdelaideAmbientPeople.Walkers;
            Assert.That(walkers.Count(w => !w.Staff), Is.GreaterThanOrEqualTo(12));
            Assert.That(walkers.Count(w => w.Staff), Is.GreaterThanOrEqualTo(2));
            foreach (var walker in walkers)
            {
                Assert.That(walker.Length, Is.GreaterThan(5f));
                var seen = 0;
                var hidden = 0;
                for (var t = 0.0; t < walker.PeriodSeconds; t += 1.0)
                {
                    if (!AdelaideAmbientPeople.TryLocate(walker, t, out var x, out var z, out _, out _, out _))
                    {
                        hidden++;
                        continue;
                    }

                    seen++;
                    var along = Math.Sqrt((x - walker.X0) * (x - walker.X0) + (z - walker.Z0) * (z - walker.Z0));
                    Assert.That(along, Is.LessThanOrEqualTo(walker.Length + 0.01f));
                }

                Assert.That(seen, Is.GreaterThan(0));
                if (walker.Staff)
                    Assert.That(hidden, Is.EqualTo(0), "staff stay in view while they wait");
                else
                    Assert.That(hidden, Is.GreaterThan(0), "a passenger is out of sight at the door or car");
            }
        }

        [Test]
        public void TheWalkIsAPureFunctionOfTime()
        {
            var walker = AdelaideAmbientPeople.Walkers[0];
            AdelaideAmbientPeople.TryLocate(walker, 100.0, out var x1, out var z1, out _, out _, out _);
            AdelaideAmbientPeople.TryLocate(walker, 100.0, out var x2, out var z2, out _, out _, out _);
            Assert.That(x1, Is.EqualTo(x2));
            Assert.That(z1, Is.EqualTo(z2));
        }
    }
}
