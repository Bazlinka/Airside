using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// The playable field is Adelaide Airport: OSM footprints, published strip
    /// lengths, and the operating stand set. Extra OSM parking lines are mapped
    /// but not live — each one still needs a taxi route and apron link.
    /// </summary>
    public sealed class AdelaideAccuracyTests
    {
        [Test]
        public void Runways_MatchPublishedAdelaideLengths()
        {
            Assert.That(AirsideAdelaidePavement.MainLengthMetres, Is.EqualTo(3100f));
            Assert.That(AdelaideLayout.MainRunwayLengthMetres, Is.EqualTo(3100f).Within(40f));
            Assert.That(AirsideAdelaidePavement.CrossLengthMetres, Is.EqualTo(1652f));
            Assert.That(AdelaideLayout.CrossRunwayLengthMetres, Is.InRange(1600f, 1750f));
        }

        [Test]
        public void Terminal_SitsNorthOfTheMainStripAtAdelaideScale()
        {
            var terminal = AdelaideLayout.Terminals.Single(t => t.Name.Contains("Domestic"));
            var (cx, cz, width, depth) = OutlineExtents(terminal.Xz);
            Assert.That(cx, Is.InRange(1200f, 1400f), "the main terminal sits on the 23 half");
            Assert.That(cz, Is.InRange(450f, 530f), "and north-west of 05/23");
            Assert.That(width, Is.InRange(550f, 700f), "OSM domestic/international bar is ~640 m");
            Assert.That(depth, Is.InRange(90f, 140f), "airside-to-landside depth of the real shell");
            Assert.That(AdelaideTerminalArchitecture.ShellHeightMetres, Is.EqualTo(14f));
        }

        [Test]
        public void Rfds_SitsWestOfTheTerminalAtHangarScale()
        {
            var rfds = AdelaideLayout.Terminals.Single(t => t.Name.Contains("Flying Doctor"));
            var terminal = AdelaideLayout.Terminals.Single(t => t.Name.Contains("Domestic"));
            var (rx, rz, width, depth) = OutlineExtents(rfds.Xz);
            var (tx, _, _, _) = OutlineExtents(terminal.Xz);
            Assert.That(rx, Is.LessThan(tx - 1200f), "RFDS is on the 05 half, west of T1");
            Assert.That(rz, Is.GreaterThan(450f));
            Assert.That(width, Is.InRange(80f, 140f));
            Assert.That(depth, Is.InRange(50f, 90f));
            Assert.That(AdelaideTerminalArchitecture.RfdsHangarHeightMetres, Is.EqualTo(8f));
            Assert.That(AdelaideTerminalArchitecture.RfdsHangarHeightMetres,
                Is.LessThan(AdelaideTerminalArchitecture.ShellHeightMetres));
        }

        [Test]
        public void OperatingStands_MatchTheCurrentAdelaideApronChart()
        {
            Assert.That(AdelaideLayout.Bays.Select(b => b.Reference),
                Is.EquivalentTo(new[] { "50A", "50B", "50C", "50D", "50E", "50F", "50G",
                    "10A", "10B", "10C", "10D", "2A" }));
            Assert.That(AdelaideLayout.TerminalGates.Select(g => g.Reference),
                Is.EquivalentTo(new[]
                {
                    "12L", "13", "14L", "15", "16L", "16R", "17", "18L", "18R", "19",
                    "20L", "20R", "21", "22L", "22R", "23", "24", "25", "26L", "27",
                    "28L", "28R", "29"
                }));
            Assert.That(AdelaideLayout.TerminalGates[0].Id, Is.EqualTo("GATE-13"));
            Assert.That(AdelaideLayout.TerminalGates[1].Id, Is.EqualTo("GATE-15"));
            Assert.That(AdelaideLayout.TerminalGates[2].Id, Is.EqualTo("GATE-18"));
            Assert.That(AdelaideLayout.TerminalGates[3].Id, Is.EqualTo("GATE-20"));
        }

        [Test]
        public void GateNoses_SitOnTheApronJustSouthOfTheTerminalFacade()
        {
            var terminal = AdelaideLayout.Terminals.Single(t => t.Name.Contains("Domestic"));
            var facadeZ = float.MaxValue;
            for (var i = 1; i < terminal.Xz.Length; i += 2)
                facadeZ = System.Math.Min(facadeZ, terminal.Xz[i]);

            foreach (var gate in AdelaideLayout.TerminalGates)
            {
                Assert.That(gate.NoseZ, Is.LessThan(facadeZ),
                    $"{gate.Reference} must stop on the apron, not inside the shell");
                Assert.That(gate.NoseZ, Is.GreaterThan(340f),
                    $"{gate.Reference} is a T1 stand, not a remote cargo bay");
                Assert.That(System.Math.Abs(gate.HeadingDegrees), Is.LessThan(15f),
                    $"{gate.Reference} faces the terminal");
                Assert.That(AirsideAdelaidePavement.DistanceToPavement(gate.NoseX, gate.NoseZ),
                    Is.LessThan(6f), $"{gate.Reference} must stand on the rendered apron");
            }

            var east = AdelaideLayout.TerminalGates.Single(g => g.Reference == "13");
            var mid = AdelaideLayout.TerminalGates.Single(g => g.Reference == "18L");
            var west = AdelaideLayout.TerminalGates.Single(g => g.Reference == "28L");
            Assert.That(east.NoseX, Is.GreaterThan(mid.NoseX));
            Assert.That(mid.NoseX, Is.GreaterThan(west.NoseX));
        }

        [Test]
        public void SharedPiers_CannotParkBothLeftAndRight()
        {
            Assert.That(AirlineOperations.SharedPierPairs.Count, Is.EqualTo(5));
            Assert.That(AirlineOperations.StandFits(AircraftType.Saab340, new StableId("BAY-10A")), Is.True);
            Assert.That(AirlineOperations.StandFits(AircraftType.Dash8Q400, new StableId("BAY-10A")), Is.False);
            Assert.That(AirlineOperations.StandFits(AircraftType.Boeing7378, new StableId("GATE-22L")), Is.True);
            Assert.That(AirlineOperations.StandFits(AircraftType.Atr42, new StableId("GATE-22L")), Is.False);
        }

        [Test]
        public void RegionalBays_SitOnT4_WalkOutsSitEastOfThePiers()
        {
            foreach (var bay in AdelaideLayout.Bays)
            {
                Assert.That(AirsideAdelaidePavement.DistanceToPavement(bay.StopX, bay.StopZ),
                    Is.LessThan(6f), bay.Reference);
                if (bay.Reference.StartsWith("50"))
                {
                    Assert.That(bay.StopZ, Is.GreaterThan(460f), bay.Reference);
                    Assert.That(bay.StopX, Is.LessThan(1150f), bay.Reference);
                }
                else
                {
                    Assert.That(bay.StopX, Is.GreaterThan(1600f), bay.Reference);
                }
            }
        }

        private static (float cx, float cz, float width, float depth) OutlineExtents(float[] xz)
        {
            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minZ = float.MaxValue;
            var maxZ = float.MinValue;
            for (var i = 0; i + 1 < xz.Length; i += 2)
            {
                minX = System.Math.Min(minX, xz[i]);
                maxX = System.Math.Max(maxX, xz[i]);
                minZ = System.Math.Min(minZ, xz[i + 1]);
                maxZ = System.Math.Max(maxZ, xz[i + 1]);
            }

            return ((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f, maxX - minX, maxZ - minZ);
        }
    }
}
