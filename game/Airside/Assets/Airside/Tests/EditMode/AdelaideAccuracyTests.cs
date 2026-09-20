using System.Linq;
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
        public void OperatingStands_AreTheLiveSubsetOfOsmParking()
        {
            Assert.That(AdelaideLayout.Bays.Select(b => b.Reference),
                Is.EquivalentTo(new[] { "50A", "50B", "50C", "50D", "50E", "50F" }));
            Assert.That(AdelaideLayout.TerminalGates.Select(g => g.Reference),
                Is.EquivalentTo(new[] { "13", "15", "18L", "20L" }));
            Assert.That(AdelaideLayout.TerminalGates.Length, Is.EqualTo(4),
                "live jet stands — OSM has many more parking lines; each needs a route and apron link");
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
                Assert.That(gate.NoseZ, Is.GreaterThan(facadeZ - 40f),
                    $"{gate.Reference} is a nose-in aerobridge stand, not a remote bay");
                Assert.That(System.Math.Abs(gate.HeadingDegrees), Is.LessThan(2f),
                    $"{gate.Reference} faces the terminal");
                Assert.That(AirsideAdelaidePavement.DistanceToPavement(gate.NoseX, gate.NoseZ),
                    Is.LessThan(6f), $"{gate.Reference} must stand on the rendered apron");
            }

            var ordered = AdelaideLayout.TerminalGates.OrderByDescending(g => g.NoseX).ToArray();
            Assert.That(ordered[0].Reference, Is.EqualTo("13"));
            Assert.That(ordered[1].Reference, Is.EqualTo("15"));
            Assert.That(ordered[2].Reference, Is.EqualTo("18L"));
            Assert.That(ordered[3].Reference, Is.EqualTo("20L"));
            Assert.That(ordered[0].NoseX - ordered[1].NoseX, Is.InRange(70f, 110f));
            Assert.That(ordered[2].NoseX - ordered[3].NoseX, Is.InRange(70f, 110f));
        }

        [Test]
        public void RegionalBays_SitWestOfTheJetStandsOnT4()
        {
            var westJet = AdelaideLayout.TerminalGates.Min(g => g.NoseX);
            foreach (var bay in AdelaideLayout.Bays)
            {
                Assert.That(bay.StopX, Is.LessThan(westJet - 80f), bay.Reference);
                Assert.That(bay.StopZ, Is.GreaterThan(460f), bay.Reference);
                Assert.That(AirsideAdelaidePavement.DistanceToPavement(bay.StopX, bay.StopZ),
                    Is.LessThan(6f), bay.Reference);
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
