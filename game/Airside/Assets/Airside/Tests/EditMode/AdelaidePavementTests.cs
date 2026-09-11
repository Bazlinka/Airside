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
            Assert.That(AirsideAdelaidePavement.CrossYawDegrees, Is.EqualTo(73f));
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
        }

        [Test]
        public void Pavement_FilletsSmoothTJunctionsAtCodeCERadius()
        {
            Assert.That(AirsideAdelaidePavement.TaxiFilletRadiusMetres, Is.EqualTo(42f));
            Assert.That(AirsideAdelaidePavement.TaxiSealedShoulderMetres, Is.EqualTo(3.5f));
            var fillets = AirsideAdelaidePavement.AllFillets();
            // 4 runway exits × 4 + 4 A–F links × 4 + 3 apron entries × 4 + 4 end caps + crossing.
            Assert.That(fillets.Length, Is.EqualTo(49));
            var r = AirsideAdelaidePavement.TaxiFilletRadiusMetres;
            var hw = AirsideAdelaidePavement.TaxiwayHalfWidth;
            var dX = AirsideAdelaidePavement.TaxiwayDCenterX;
            var fZ = AirsideAdelaidePavement.TaxiwayFCenterZ;
            var sampleX = dX - hw - r * 0.5f;
            var sampleZ = fZ - hw - r * 0.5f;
            Assert.That(AirsideAdelaidePavement.ContainsFillet(sampleX, sampleZ), Is.True);
            Assert.That(AirsideAdelaidePavement.DistanceToPavement(sampleX, sampleZ), Is.EqualTo(0f));
            Assert.That(AirsideAdelaidePavement.DistanceToPavement(0f, 900f), Is.GreaterThan(50f));
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
        public void Pavement_PlateauCoversCrossStripAndTaxiF()
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
        public void StripMarkings_TaxiGuideHasEdgesAndDashes()
        {
            var marks = AirsideStripMarkings.TaxiwayGuide(3000f, 23f);
            Assert.That(marks.Length, Is.GreaterThan(4));
            var hold = AirsideStripMarkings.HoldShortBars(23f, 12f);
            Assert.That(hold.Length, Is.EqualTo(2));
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
    }
}
