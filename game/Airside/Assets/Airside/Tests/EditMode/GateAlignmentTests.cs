using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0141 — terminal gates lined up along T1, with boxes sized by code.</summary>
    public sealed class GateAlignmentTests
    {
        private static IEnumerable<AdelaideTerminalGate> ContactGates =>
            AdelaideGateAlignment.Gates.Where(g => !AdelaideGateAlignment.IsRemote(g));

        [Test]
        public void EveryContactGate_StopsAtItsCodesSetbackFromTheWall()
        {
            Assert.That(ContactGates.Count(), Is.EqualTo(AdelaideGateAlignment.Gates.Length - 2), "20R and 22R are the remote stands");
            foreach (var gate in ContactGates)
            {
                var setback = AdelaideGateAlignment.WallZAt(gate.NoseX) - gate.NoseZ;
                Assert.That(setback, Is.EqualTo(AdelaideGateAlignment.SetbackFor(gate.Id)).Within(0.5f), gate.Reference);
                Assert.That(gate.HeadingDegrees, Is.EqualTo(AdelaideGateAlignment.SquareHeadingAt(gate.NoseX)).Within(0.5f));
                Assert.That(Math.Abs(gate.HeadingDegrees), Is.LessThan(1f), $"{gate.Reference} is square to the wall");
            }

            var noses = ContactGates.Select(g => g.NoseZ).ToList();
            Assert.That(noses.Max() - noses.Min(), Is.LessThan(5f), "one straight row, not a ragged one");
        }

        [Test]
        public void TheRemoteStands_KeepTheirPlaceButAreSquared()
        {
            foreach (var id in new[] { "GATE-20R", "GATE-22R" })
            {
                var raw = AdelaideLayout.TerminalGates.Single(g => g.Id == id);
                var aligned = AdelaideGateAlignment.Gates.Single(g => g.Id == id);
                Assert.That(aligned.NoseX, Is.EqualTo(raw.NoseX));
                Assert.That(aligned.NoseZ, Is.EqualTo(raw.NoseZ));
                Assert.That(Math.Abs(aligned.HeadingDegrees), Is.LessThan(1f));
            }
        }

        [Test]
        public void CodeEGates_MatchTheOperationsList()
        {
            Assert.That(AdelaideGateAlignment.CodeEGateIds,
                Is.EquivalentTo(AirlineOperations.CodeEGates.Select(g => g.Value)));
        }

        [Test]
        public void TaxiInAndPushback_MeetTheNewStopSmoothly()
        {
            foreach (var gate in AdelaideGateAlignment.Gates)
            {
                Assert.That(gate.TaxiIn[^2], Is.EqualTo(gate.NoseX).Within(0.01f), gate.Reference);
                Assert.That(gate.TaxiIn[^1], Is.EqualTo(gate.NoseZ).Within(0.01f), gate.Reference);
                Assert.That(gate.Pushback[0], Is.EqualTo(gate.NoseX).Within(0.01f), gate.Reference);
                Assert.That(gate.Pushback[1], Is.EqualTo(gate.NoseZ).Within(0.01f), gate.Reference);
                foreach (var path in new[] { gate.TaxiIn, gate.Pushback })
                    for (var i = 2; i < path.Length; i += 2)
                    {
                        var step = Math.Sqrt(Math.Pow(path[i] - path[i - 2], 2) + Math.Pow(path[i + 1] - path[i - 1], 2));
                        Assert.That(step, Is.LessThan(8.0), $"{gate.Reference}: no jump in the rebuilt path");
                    }

                // The last metres of the lead-in are square to the wall.
                var n = gate.TaxiIn.Length;
                var dx = gate.TaxiIn[n - 2] - gate.TaxiIn[n - 6];
                var dz = gate.TaxiIn[n - 1] - gate.TaxiIn[n - 5];
                var approach = Math.Atan2(dx, dz) * 180.0 / Math.PI;
                Assert.That(approach, Is.EqualTo(0.0).Within(2.0), gate.Reference);
            }
        }

        [Test]
        public void TheTaxiInLeg_EndsOnTheStandPose()
        {
            foreach (var gate in AdelaideGateAlignment.Gates)
            {
                var stand = new StableId(gate.Id);
                var type = AirlineOperations.StandCodeLetter(stand) == 'E' ? AircraftType.AirbusA350900 : AircraftType.Boeing7378;
                var leg = AdelaideGround.TaxiIn(stand, type);
                var end = leg.PoseAt(leg.Seconds);
                var pose = AdelaideGround.StandPose(stand);
                Assert.That(Math.Abs(end.X - pose.X) + Math.Abs(end.Z - pose.Z), Is.LessThan(1.5f), gate.Reference);
            }
        }

        [Test]
        public void PaintedBoxes_NeverCrossExceptOnOnePier()
        {
            var boxes = AdelaideStandMarkings.All().Where(m => m.StandId.StartsWith("GATE-") && m.Envelope.Length == 8).ToList();
            var pairs = AirlineOperations.SharedPierPairs.Select(p => (p.A.Value, p.B.Value)).ToList();
            bool Paired(string a, string b) => pairs.Contains((a, b)) || pairs.Contains((b, a));
            for (var i = 0; i < boxes.Count; i++)
            for (var j = i + 1; j < boxes.Count; j++)
            {
                if (Paired(boxes[i].StandId, boxes[j].StandId))
                    continue;
                var a = Bounds(boxes[i].Envelope);
                var b = Bounds(boxes[j].Envelope);
                var overlap = Math.Min(a.MaxX, b.MaxX) - Math.Max(a.MinX, b.MinX);
                var depth = Math.Min(a.MaxZ, b.MaxZ) - Math.Max(a.MinZ, b.MinZ);
                Assert.That(overlap <= 0.5f || depth <= 0.5f, Is.True, $"{boxes[i].StandId} and {boxes[j].StandId} cross");
            }

            // 16L/16R, 18/18R and 28L/28R paint one shared box between them.
            foreach (var id in new[] { "GATE-16R", "GATE-18R", "GATE-28R" })
            {
                Assert.That(AdelaideStandMarkings.SharesPartnerBox(id), Is.True, id);
                Assert.That(AdelaideStandMarkings.All().Single(m => m.StandId == id).Envelope, Is.Empty);
            }

            var wide = Bounds(AdelaideStandMarkings.All().Single(m => m.StandId == "GATE-25").Envelope);
            Assert.That(wide.MaxX - wide.MinX, Is.EqualTo(AdelaideStandMarkings.CodeEEnvelopeWidthMetres).Within(0.5f),
                "a widebody gate paints a widebody box");
        }

        private static (float MinX, float MaxX, float MinZ, float MaxZ) Bounds(float[] xz)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (var i = 0; i + 1 < xz.Length; i += 2)
            {
                minX = Math.Min(minX, xz[i]); maxX = Math.Max(maxX, xz[i]);
                minZ = Math.Min(minZ, xz[i + 1]); maxZ = Math.Max(maxZ, xz[i + 1]);
            }

            return (minX, maxX, minZ, maxZ);
        }
    }
}
