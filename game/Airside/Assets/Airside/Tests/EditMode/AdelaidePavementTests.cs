using System;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AdelaidePavementTests
    {
        [Test]
        public void Pavement_MainStripMatchesBareFieldMetres()
        {
            Assert.That(AirsideAdelaidePavement.MainRunwayName, Is.EqualTo("Runway 05/23"));
            Assert.That(AirsideBareField.RunwayObjectName, Is.EqualTo("Runway 05/23"));
            Assert.That(AirsideAdelaidePavement.MainLengthMetres, Is.EqualTo(3100f));
            Assert.That(AirsideAdelaidePavement.MainWidthMetres, Is.EqualTo(45f));
        }

        [Test]
        public void Pavement_CrossStripIsPublishedTwelveThirty()
        {
            Assert.That(AirsideAdelaidePavement.CrossRunwayName, Is.EqualTo("Runway 12/30"));
            Assert.That(AirsideAdelaidePavement.CrossLengthMetres, Is.EqualTo(1652f));
            Assert.That(AirsideAdelaidePavement.CrossWidthMetres, Is.EqualTo(45f));
            // Not a surveyed figure — but it must stay inside the band the runway
            // designators allow (12 → 115°–124° M against 05 → 045°–054° M).
            Assert.That(AirsideAdelaidePavement.CrossYawDegrees, Is.InRange(61f, 79f));
        }

        [Test]
        public void Pavement_TaxiSkeletonHasParallelFAndDEExits()
        {
            Assert.That(AirsideAdelaidePavement.TaxiwayFName, Is.EqualTo("Taxiway F"));
            Assert.That(AirsideAdelaidePavement.TaxiwayDName, Is.EqualTo("Taxiway D"));
            Assert.That(AirsideAdelaidePavement.TaxiwayEName, Is.EqualTo("Taxiway E"));
            Assert.That(AirsideAdelaidePavement.TaxiwayWidthMetres, Is.EqualTo(23f));
            Assert.That(AirsideAdelaidePavement.TaxiwayFCenterZ, Is.GreaterThan(AirsideAdelaidePavement.MainHalfWidth));
            Assert.That(AirsideAdelaidePavement.TaxiLinkLengthZ, Is.GreaterThan(20f));
            Assert.That(AirsideAdelaidePavement.TaxiwayDCenterX, Is.GreaterThan(0f));
            Assert.That(AirsideAdelaidePavement.TaxiwayECenterX, Is.LessThan(0f));
        }

        [Test]
        public void Pavement_ParallelTaxiwaysMeetCodeESeparations()
        {
            // Runway centreline → parallel taxiway centreline, code 4E precision.
            Assert.That(AirsideAdelaidePavement.TaxiwayFCenterZ,
                Is.GreaterThanOrEqualTo(AirsideAdelaidePavement.CodeERunwayToTaxiwaySeparationMetres),
                "Taxiway F must clear the code 4E runway/taxiway separation");

            // And the whole sealed width must sit outside the runway strip.
            var fInnerEdge = AirsideAdelaidePavement.TaxiwayFCenterZ
                             - AirsideAdelaidePavement.TaxiwayHalfWidth
                             - AirsideAdelaidePavement.TaxiSealedShoulderMetres;
            Assert.That(fInnerEdge,
                Is.GreaterThan(AirsideAdelaidePavement.RunwayStripHalfWidthMetres),
                "Taxiway F pavement must not stand inside the 150 m runway strip");

            // Taxiway-to-taxiway, code E.
            var fToA = AirsideAdelaidePavement.TaxiwayACenterZ - AirsideAdelaidePavement.TaxiwayFCenterZ;
            Assert.That(fToA,
                Is.GreaterThanOrEqualTo(AirsideAdelaidePavement.CodeETaxiwayToTaxiwaySeparationMetres),
                "F→A separation must clear the code E taxiway/taxiway minimum");
        }

        [Test]
        public void Pavement_HoldShortSitsAtTheCodeEHoldingPosition()
        {
            var fromEdge = AirsideAdelaidePavement.HoldShortFromRunwayEdgeMetres;
            Assert.That(fromEdge + AirsideAdelaidePavement.MainHalfWidth,
                Is.EqualTo(AirsideAdelaidePavement.RunwayHoldingPositionFromCentrelineMetres),
                "holding position must land 90 m from the runway centreline");
            Assert.That(fromEdge, Is.GreaterThan(0f));
            Assert.That(fromEdge, Is.LessThan(AirsideAdelaidePavement.TaxiLinkLengthZ),
                "the exit link must be long enough to carry the holding position");
        }

        [Test]
        public void Pavement_DenserSilhouetteAddsTaxiAApronsAndInnerExits()
        {
            Assert.That(AirsideAdelaidePavement.TaxiwayAName, Is.EqualTo("Taxiway A"));
            Assert.That(AirsideAdelaidePavement.TaxiwayACenterZ, Is.GreaterThan(AirsideAdelaidePavement.TaxiwayFCenterZ));
            Assert.That(AirsideAdelaidePavement.RunwayExitCenterXs.Length, Is.EqualTo(4));
            Assert.That(AirsideAdelaidePavement.AfLinkCenterXs.Length, Is.EqualTo(4));
            Assert.That(AirsideAdelaidePavement.ApronEntryCenterXs.Length, Is.EqualTo(3));
            Assert.That(AirsideAdelaidePavement.ContainsTerminalApron(
                AirsideAdelaidePavement.TerminalApronCenterX,
                AirsideAdelaidePavement.TerminalApronCenterZ), Is.True);
            Assert.That(AirsideAdelaidePavement.DistanceToPavement(
                AirsideAdelaidePavement.RfdsApronCenterX,
                AirsideAdelaidePavement.RfdsApronCenterZ), Is.EqualTo(0f));
            // A sits on the ops plateau.
            Assert.That(AirsideAdelaideGround.IsOperationallyFlat(
                0f, AirsideAdelaidePavement.TaxiwayACenterZ), Is.True);
            Assert.That(AirsideAdelaideGround.IsOperationallyFlat(
                AirsideAdelaidePavement.TerminalApronCenterX,
                AirsideAdelaidePavement.TerminalApronCenterZ), Is.True);
            // Apron entries land under the terminal pad, not out in the grass.
            foreach (var x in AirsideAdelaidePavement.ApronEntryCenterXs)
            {
                Assert.That(AirsideAdelaidePavement.ContainsTerminalApron(
                    x, AirsideAdelaidePavement.TerminalApronCenterZ), Is.True,
                    $"apron entry X={x} must meet the terminal pad");
            }
        }

        [Test]
        public void Pavement_EveryStubJoinsTheSlabsItConnects()
        {
            // Each stub must span edge-to-edge with no gap and no overlap, or the
            // silhouette shows a floating rectangle.
            var linkSouth = AirsideAdelaidePavement.TaxiLinkCenterZ
                            - AirsideAdelaidePavement.TaxiLinkLengthZ * 0.5f;
            var linkNorth = AirsideAdelaidePavement.TaxiLinkCenterZ
                            + AirsideAdelaidePavement.TaxiLinkLengthZ * 0.5f;
            Assert.That(linkSouth, Is.EqualTo(AirsideAdelaidePavement.MainHalfWidth).Within(0.01f));
            Assert.That(linkNorth, Is.EqualTo(
                AirsideAdelaidePavement.TaxiwayFCenterZ - AirsideAdelaidePavement.TaxiwayHalfWidth).Within(0.01f));

            var afSouth = AirsideAdelaidePavement.AfLinkCenterZ
                          - AirsideAdelaidePavement.AfLinkLengthZ * 0.5f;
            var afNorth = AirsideAdelaidePavement.AfLinkCenterZ
                          + AirsideAdelaidePavement.AfLinkLengthZ * 0.5f;
            Assert.That(afSouth, Is.EqualTo(
                AirsideAdelaidePavement.TaxiwayFCenterZ + AirsideAdelaidePavement.TaxiwayHalfWidth).Within(0.01f));
            Assert.That(afNorth, Is.EqualTo(
                AirsideAdelaidePavement.TaxiwayACenterZ - AirsideAdelaidePavement.TaxiwayHalfWidth).Within(0.01f));

            var entrySouth = AirsideAdelaidePavement.ApronEntryCenterZ
                             - AirsideAdelaidePavement.ApronEntryLengthZ * 0.5f;
            var entryNorth = AirsideAdelaidePavement.ApronEntryCenterZ
                             + AirsideAdelaidePavement.ApronEntryLengthZ * 0.5f;
            Assert.That(AirsideAdelaidePavement.ApronEntryLengthZ, Is.GreaterThan(0f),
                "apron must sit north of Taxiway A or the entry stubs invert");
            Assert.That(entrySouth, Is.EqualTo(
                AirsideAdelaidePavement.TaxiwayACenterZ + AirsideAdelaidePavement.TaxiwayHalfWidth).Within(0.01f));
            Assert.That(entryNorth, Is.EqualTo(
                AirsideAdelaidePavement.TerminalApronCenterZ
                - AirsideAdelaidePavement.TerminalApronWidthZ * 0.5f).Within(0.01f));
        }

        [Test]
        public void Pavement_ApronsStayClearOfBothRunways()
        {
            var terminalClear = AirsideAdelaidePavement.TerminalApronClearanceFromRunways(5f);
            Assert.That(terminalClear, Is.GreaterThanOrEqualTo(
                AirsideAdelaidePavement.ApronRunwayClearanceMetres),
                "terminal apron must not intersect 05/23 or 12/30");

            var rfdsClear = AirsideAdelaidePavement.RfdsApronClearanceFromRunways(5f);
            Assert.That(rfdsClear, Is.GreaterThanOrEqualTo(
                AirsideAdelaidePavement.ApronRunwayClearanceMetres),
                "RFDS apron must not intersect 05/23 or 12/30");

            // Spot-check: no sampled apron point may lie on either strip.
            Assert.That(AirsideAdelaidePavement.ContainsAnyRunway(
                AirsideAdelaidePavement.TerminalApronCenterX,
                AirsideAdelaidePavement.TerminalApronCenterZ), Is.False);
            Assert.That(AirsideAdelaidePavement.ContainsCrossRunway(
                AirsideAdelaidePavement.TerminalApronCenterX + AirsideAdelaidePavement.TerminalApronLengthX * 0.5f,
                AirsideAdelaidePavement.TerminalApronCenterZ), Is.False);
        }

        [Test]
        public void Pavement_ApronsStayOutOfTheProtectedRunwayStrips()
        {
            Assert.That(AirsideAdelaidePavement.TerminalApronClearanceFromRunwayStrips(5f),
                Is.GreaterThan(0f),
                "terminal apron must stand outside the 150 m runway strip");
            Assert.That(AirsideAdelaidePavement.RfdsApronClearanceFromRunwayStrips(5f),
                Is.GreaterThan(0f),
                "RFDS apron must stand outside the 150 m runway strip");
        }

        [Test]
        public void Pavement_FilletsSmoothTJunctionsAtCodeCERadius()
        {
            Assert.That(AirsideAdelaidePavement.TaxiFilletRadiusMetres, Is.EqualTo(42f));
            Assert.That(AirsideAdelaidePavement.TaxiSealedShoulderMetres, Is.EqualTo(3.5f));
            var fillets = AirsideAdelaidePavement.AllFillets();
            // 4 runway exits × 4 + 4 A–F links × 4 + 3 apron entries × 4 + 4 end caps + crossing.
            Assert.That(fillets.Length, Is.EqualTo(49));

            var hw = AirsideAdelaidePavement.TaxiwayHalfWidth;
            var dX = AirsideAdelaidePavement.TaxiwayDCenterX;
            var fZ = AirsideAdelaidePavement.TaxiwayFCenterZ;

            // Just inside the corner: a real fillet fills this.
            var nearX = dX - hw - 5f;
            var nearZ = fZ - hw - 5f;
            Assert.That(AirsideAdelaidePavement.ContainsFillet(nearX, nearZ), Is.True,
                "fillet must fill the tight part of the re-entrant corner");
            Assert.That(AirsideAdelaidePavement.DistanceToPavement(nearX, nearZ), Is.EqualTo(0f));

            // The far diagonal of the corner square is *rounded away* by a real
            // fillet. A quarter-disk centred on the corner would wrongly cover it.
            var r = AirsideAdelaidePavement.TaxiFilletRadiusMetres;
            var farX = dX - hw - r * 0.5f;
            var farZ = fZ - hw - r * 0.5f;
            Assert.That(AirsideAdelaidePavement.ContainsFillet(farX, farZ), Is.False,
                "fillet must be concave — the corner diagonal is rounded away, not filled");

            Assert.That(AirsideAdelaidePavement.DistanceToPavement(0f, 1400f), Is.GreaterThan(50f));
        }

        [Test]
        public void Pavement_FilletsDoNotSwallowTheStubsTheySmooth()
        {
            // The bug this guards: quarter-disk "fillets" centred on each corner
            // widened every 23 m stub into an 80–107 m blob over its whole length.
            var nominal = AirsideAdelaidePavement.TaxiwayWidthMetres;
            var shoulders = nominal + 2f * AirsideAdelaidePavement.TaxiSealedShoulderMetres;

            void AssertStubStaysNarrow(string what, float stubX, float fromZ, float toZ)
            {
                var span = toZ - fromZ;

                // At mid-span a stub must be exactly its sealed width. The old
                // quarter-disk fillets made this 80.7 m on a 23 m taxiway.
                var mid = 2f * AirsideAdelaidePavement.PavementHalfWidthAcrossStub(
                    stubX, fromZ + span * 0.5f);
                Assert.That(mid, Is.LessThanOrEqualTo(shoulders + 1f),
                    $"{what} is {mid:0.0} m wide at mid-span; sealed width is {shoulders} m");

                // Across the middle 60% the flare must stay modest — the ends are
                // where a fillet is allowed to open out.
                var a = fromZ + span * 0.2f;
                var b = toZ - span * 0.2f;
                for (var z = a; z <= b; z += Math.Max(1f, span * 0.05f))
                {
                    var width = 2f * AirsideAdelaidePavement.PavementHalfWidthAcrossStub(stubX, z);
                    Assert.That(width, Is.LessThanOrEqualTo(shoulders + 16f),
                        $"{what} at Z={z:0.0} is {width:0.0} m wide; nominal is {nominal} m");
                }
            }

            var linkS = AirsideAdelaidePavement.TaxiLinkCenterZ
                        - AirsideAdelaidePavement.TaxiLinkLengthZ * 0.5f;
            var linkN = AirsideAdelaidePavement.TaxiLinkCenterZ
                        + AirsideAdelaidePavement.TaxiLinkLengthZ * 0.5f;
            AssertStubStaysNarrow("runway exit link", AirsideAdelaidePavement.TaxiwayDCenterX, linkS, linkN);

            var afS = AirsideAdelaidePavement.AfLinkCenterZ
                      - AirsideAdelaidePavement.AfLinkLengthZ * 0.5f;
            var afN = AirsideAdelaidePavement.AfLinkCenterZ
                      + AirsideAdelaidePavement.AfLinkLengthZ * 0.5f;
            AssertStubStaysNarrow("A–F link", AirsideAdelaidePavement.AfLinkCenterXs[0], afS, afN);

            var apS = AirsideAdelaidePavement.ApronEntryCenterZ
                      - AirsideAdelaidePavement.ApronEntryLengthZ * 0.5f;
            var apN = AirsideAdelaidePavement.ApronEntryCenterZ
                      + AirsideAdelaidePavement.ApronEntryLengthZ * 0.5f;
            AssertStubStaysNarrow("apron entry", AirsideAdelaidePavement.ApronEntryCenterXs[0], apS, apN);
        }

        [Test]
        public void Pavement_FilletRadiusNeverOutrunsTheStub()
        {
            Assert.That(
                AirsideAdelaidePavement.ClampFilletRadius(42f, 20f),
                Is.EqualTo(18f).Within(0.001f),
                "a fillet may not reach further than the stub is long");
            Assert.That(
                AirsideAdelaidePavement.ClampFilletRadius(42f, 1000f),
                Is.EqualTo(42f),
                "a stub with room keeps the nominal Code C/E radius");
        }

        [Test]
        public void Pavement_CornerFilletIsTangentToBothEdges()
        {
            var r = 40f;
            // Corner at the origin opening into the north-east quadrant.
            var f = new AirsideAdelaidePavement.FilletSpec(
                PavementArcKind.CornerFillet, 0f, 0f, r, 0f, (float)Math.PI * 0.5f);

            Assert.That(f.OutwardX, Is.EqualTo(1f));
            Assert.That(f.OutwardZ, Is.EqualTo(1f));
            Assert.That(f.ArcCenterX, Is.EqualTo(r));
            Assert.That(f.ArcCenterZ, Is.EqualTo(r));

            // The arc runs from (r, 0) to (0, r), touching each edge exactly once.
            // Along the edges themselves the fillet reaches the full radius...
            Assert.That(f.Contains(r - 0.05f, 0f), Is.True, "fillet reaches r along the X edge");
            Assert.That(f.Contains(0f, r - 0.05f), Is.True, "fillet reaches r along the Z edge");
            // ...but it pinches to nothing there, because the arc is tangent. A
            // quarter-disk centred on the corner would still be 40 m thick here.
            Assert.That(f.Contains(r - 0.05f, 0.05f), Is.False,
                "the fillet must be tangent to the X edge, not cross it");
            Assert.That(f.Contains(0.05f, r - 0.05f), Is.False,
                "the fillet must be tangent to the Z edge, not cross it");
            // Corner itself is filled.
            Assert.That(f.Contains(0.5f, 0.5f), Is.True);
            // The rounded-away diagonal is not.
            Assert.That(f.Contains(r * 0.5f, r * 0.5f), Is.False);
            // Nothing outside the quadrant square.
            Assert.That(f.Contains(r + 5f, 1f), Is.False);
            Assert.That(f.Contains(-5f, 1f), Is.False);

            // Distance is 0 inside and grows outside.
            Assert.That(f.DistanceTo(0.5f, 0.5f), Is.EqualTo(0f));
            Assert.That(f.DistanceTo(r * 0.5f, r * 0.5f), Is.GreaterThan(0f));
        }

        [Test]
        public void Pavement_AllFilletsIsCachedNotReallocated()
        {
            // Called once per ground-mesh vertex — must not allocate each time.
            Assert.That(AirsideAdelaidePavement.AllFillets(),
                Is.SameAs(AirsideAdelaidePavement.AllFillets()));
        }

        [Test]
        public void Pavement_CrossRunwayFootprintCoversIntersectionAndEnds()
        {
            Assert.That(AirsideAdelaidePavement.ContainsCrossRunway(0f, 0f), Is.True);
            var yaw = AirsideAdelaidePavement.CrossYawRadians;
            var along = 800f;
            var x = along * (float)Math.Cos(yaw);
            var z = along * (float)Math.Sin(yaw);
            Assert.That(AirsideAdelaidePavement.ContainsCrossRunway(x, z), Is.True);
            Assert.That(AirsideAdelaidePavement.ContainsCrossRunway(x * 1.2f, z * 1.2f), Is.False);
        }

        [Test]
        public void Pavement_PlateauCoversCrossStripTaxiAndAprons()
        {
            Assert.That(AirsideAdelaideGround.PlateauMaxZ,
                Is.EqualTo(AirsideAdelaidePavement.PlateauHalfZ));
            Assert.That(AirsideAdelaideGround.IsOperationallyFlat(0f, AirsideAdelaidePavement.TaxiwayFCenterZ),
                Is.True);
            var yaw = AirsideAdelaidePavement.CrossYawRadians;
            var tipX = AirsideAdelaidePavement.CrossHalfLength * (float)Math.Cos(yaw);
            var tipZ = AirsideAdelaidePavement.CrossHalfLength * (float)Math.Sin(yaw);
            Assert.That(AirsideAdelaideGround.IsOperationallyFlat(tipX, tipZ), Is.True,
                "12/30 tip must sit on the dead-level ops plateau");

            // Both apron pads, all four corners, must be dead level too.
            foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
            {
                Assert.That(AirsideAdelaideGround.IsOperationallyFlat(
                        AirsideAdelaidePavement.TerminalApronCenterX
                        + sx * AirsideAdelaidePavement.TerminalApronLengthX * 0.5f,
                        AirsideAdelaidePavement.TerminalApronCenterZ
                        + sz * AirsideAdelaidePavement.TerminalApronWidthZ * 0.5f),
                    Is.True, "terminal apron corner must sit on the ops plateau");
                Assert.That(AirsideAdelaideGround.IsOperationallyFlat(
                        AirsideAdelaidePavement.RfdsApronCenterX
                        + sx * AirsideAdelaidePavement.RfdsApronLengthX * 0.5f,
                        AirsideAdelaidePavement.RfdsApronCenterZ
                        + sz * AirsideAdelaidePavement.RfdsApronWidthZ * 0.5f),
                    Is.True, "RFDS apron corner must sit on the ops plateau");
            }
        }

        [Test]
        public void StripMarkings_CrossPaintStaysOnLocalPavement()
        {
            var halfL = AirsideAdelaidePavement.CrossHalfLength;
            var halfW = AirsideAdelaidePavement.CrossHalfWidth;
            var marks = AirsideStripMarkings.CrossRunwayAll();
            Assert.That(marks.Length, Is.GreaterThan(20));
            foreach (var mark in marks)
            {
                Assert.That(Math.Abs(mark.MinX), Is.LessThanOrEqualTo(halfL + 0.01f), mark.MinX.ToString());
                Assert.That(Math.Abs(mark.MaxX), Is.LessThanOrEqualTo(halfL + 0.01f));
                Assert.That(Math.Abs(mark.MinZ), Is.LessThanOrEqualTo(halfW + 0.01f));
                Assert.That(Math.Abs(mark.MaxZ), Is.LessThanOrEqualTo(halfW + 0.01f));
            }
        }

        [Test]
        public void StripMarkings_TaxiGuideIsSolidCentrelineAndDoubleEdges()
        {
            var length = 3000f;
            var width = 23f;
            var marks = AirsideStripMarkings.TaxiwayGuide(length, width);

            // 4 edge lines (double each side) + 1 continuous centreline.
            Assert.That(marks.Length, Is.EqualTo(5));

            var centre = Array.FindAll(marks, m => Math.Abs(m.CenterZ) < 0.01f);
            Assert.That(centre.Length, Is.EqualTo(1),
                "a taxiway centreline is one continuous line, not runway dashes");
            Assert.That(centre[0].LengthX, Is.EqualTo(length).Within(0.01f),
                "taxiway centreline must run the full length");

            foreach (var m in marks)
            {
                Assert.That(Math.Abs(m.MaxZ), Is.LessThanOrEqualTo(width * 0.5f + 0.01f),
                    "taxi paint must stay on the taxiway");
            }
        }

        [Test]
        public void StripMarkings_HoldShortIsIcaoPatternA()
        {
            var from = AirsideAdelaidePavement.HoldShortFromRunwayEdgeMetres;

            // ICAO pattern A is four bars across the taxiway.
            var lanes = AirsideStripMarkings.HoldShortBarLanes(23f, from);
            Assert.That(lanes.Length, Is.EqualTo(4));

            var hold = AirsideStripMarkings.HoldShortBars(23f, from);
            Assert.That(hold.Length, Is.GreaterThan(4),
                "the two far bars are dashed, so they break into segments");

            // Two of the four lanes are solid, and they are the pair nearest the runway.
            var solid = Array.FindAll(hold, m => m.LengthX > 20f);
            Assert.That(solid.Length, Is.EqualTo(2), "pattern A has two solid bars");
            foreach (var s in solid)
            {
                Assert.That(s.CenterZ, Is.LessThan(from),
                    "solid bars sit on the runway side of the pattern");
            }

            foreach (var m in hold)
            {
                Assert.That(m.LengthX, Is.LessThanOrEqualTo(23f),
                    "hold bars must not overhang the taxiway");
            }
        }

        [Test]
        public void Perimeter_MatchesPublishedSiteRectangleAndGatePattern()
        {
            Assert.That(AirsideAdelaidePerimeter.HalfX, Is.EqualTo(1700f));
            Assert.That(AirsideAdelaidePerimeter.HalfZ, Is.EqualTo(1154.5f));
            Assert.That(AirsideAdelaidePerimeter.FenceHeightMetres, Is.EqualTo(2.44f));
            Assert.That(AirsideAdelaidePerimeter.VehicleGates.Length, Is.EqualTo(3));
            Assert.That(AirsideAdelaidePerimeter.IsInsideFence(0f, 0f), Is.True);
            Assert.That(AirsideAdelaidePerimeter.IsInsideFence(5000f, 0f), Is.False);
            Assert.That(AirsideAdelaidePerimeter.IsInGateGap("N", 420f), Is.True);
            Assert.That(AirsideAdelaidePerimeter.IsInGateGap("N", 0f), Is.False);
            Assert.That(AirsideAdelaidePerimeter.PerimeterLengthMetres, Is.GreaterThan(10000f));
        }

        [Test]
        public void Perimeter_FenceFollowsTheGroundItStandsOn()
        {
            // The bug this guards: every panel was pinned to world Y = 0 while the
            // authored ground drops ~2.4 m into the boundary lip at the fence line,
            // leaving the whole 11 km fence hanging in the air.
            var hx = AirsideAdelaidePerimeter.FenceHalfX;
            var hz = AirsideAdelaidePerimeter.FenceHalfZ;

            var groundAtFence = AirsideAdelaideGround.WorldHeight(0f, hz);
            Assert.That(groundAtFence, Is.LessThan(-1f),
                "sanity: the authored boundary lip really does drop at the fence line");

            foreach (var x in new[] { -hx * 0.9f, 0f, hx * 0.9f })
            {
                foreach (var z in new[] { -hz, hz })
                {
                    var baseY = AirsideAdelaidePerimeter.FenceBaseY(x, z);
                    var ground = AirsideAdelaideGround.WorldHeight(x, z);
                    Assert.That(baseY, Is.LessThanOrEqualTo(ground + 0.01f),
                        $"fence base at ({x:0},{z:0}) must not float above the ground");
                    Assert.That(baseY, Is.GreaterThanOrEqualTo(ground - 1f),
                        $"fence base at ({x:0},{z:0}) must not be buried");
                }
            }
        }

        [Test]
        public void Perimeter_FenceBaseTracksTerrainNotAFixedHeight()
        {
            var hz = AirsideAdelaidePerimeter.FenceHalfZ;
            var a = AirsideAdelaidePerimeter.FenceBaseY(-1000f, hz);
            var b = AirsideAdelaidePerimeter.FenceBaseY(1000f, hz);
            Assert.That(Math.Abs(a - b), Is.GreaterThan(0.01f),
                "fence base must vary with the landform, not sit on one constant");
        }
    }
}
