using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// The Stats/career workspace (ADR 0066): funds, lifetime revenue, reliability, tier and
    /// exactly what the next one still needs, fleet size, milestones and contract history.
    /// </summary>
    public sealed class StatsWorkspaceTests
    {
        [Test]
        public void Stats_ShowsARealOverviewForAFreshCareer()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            var model = new StatsWorkspaceModel();
            model.Rebuild(ops, clock.Now);

            Assert.That(model.AirlineName, Is.EqualTo(ops.PlayerAirline.Name));
            Assert.That(model.FundsLine, Is.EqualTo($"${ops.CareerState.Funds:N0} on hand"));
            Assert.That(model.LifetimeRevenueLine, Is.EqualTo("$0 lifetime revenue"));
            Assert.That(model.ReliabilityLine, Is.EqualTo($"{ops.CareerState.Reliability}% reliability"));
            Assert.That(model.TierLine, Is.EqualTo("Provisional tier"));
            Assert.That(model.FleetLine, Is.EqualTo($"1 of {AircraftAcquisition.MaxPlayerAircraft} aircraft"));
            Assert.That(model.ContractHistory, Is.Empty);
            Assert.That(model.EmptyHistoryLine, Is.Not.Empty);
        }

        [Test]
        public void Stats_NextTierNamesExactlyWhatIsStillMissing()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            var model = new StatsWorkspaceModel();
            model.Rebuild(ops, clock.Now);

            Assert.That(model.HasNextTier, Is.True);
            Assert.That(model.NextTierTitle, Is.EqualTo("Next: Regional"));
            Assert.That(model.NextTierRequirementLine, Does.Contain(
                $"{AirlineCareerState.RegionalRotations} more rotation"));
            Assert.That(model.NextTierProgress01, Is.EqualTo(0f));
        }

        [Test]
        public void Stats_NextTierReportsMaxTierReachedAtInternational()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            ops.RestoreCareerState(ops.CareerState.Funds, 95, nameof(OperatingTier.International), null, 0, 0,
                System.Array.Empty<string>());
            var model = new StatsWorkspaceModel();
            model.Rebuild(ops, clock.Now);

            Assert.That(model.HasNextTier, Is.False);
            Assert.That(model.NextTierTitle, Is.EqualTo("International reached"));
        }

        [Test]
        public void Stats_MilestonesReflectRealProgress()
        {
            var (clock, ops, plane) = HudTestAirline.Create();
            var definition = RouteContractCatalogue.RegionalKingscoteIntro;
            Assert.That(ops.AcceptContract(definition).Accepted, Is.True);
            HudTestAirline.CompleteActiveContract(clock, ops, definition);

            var model = new StatsWorkspaceModel();
            model.Rebuild(ops, clock.Now);

            bool Reached(string title) => model.Milestones.Single(m => m.Title == title).Reached;
            Assert.That(Reached("First rotation completed"), Is.True);
            Assert.That(Reached("First contract fulfilled"), Is.True);
            Assert.That(Reached("Reached Regional"), Is.True);
            Assert.That(Reached("Reached Domestic"), Is.False);
            Assert.That(model.ContractHistory.Count, Is.EqualTo(1));
            Assert.That(model.ContractHistory[0].PaidText, Does.StartWith("$"));
        }

        [Test]
        public void Stats_LayoutKeepsBothColumnsInsideTheSurfaceAndNeverOverlapping()
        {
            foreach (var (width, height) in HudTestAirline.Viewports)
            {
                var surface = HudShell.WorkspaceSurface(width, height);
                var layout = StatsWorkspaceLayout.Create(surface);
                var label = $"{width}x{height}";

                Assert.That(layout.LeftColumn.Overlaps(layout.RightColumn), Is.False, label);
                Assert.That(layout.RightColumn.Right, Is.LessThanOrEqualTo(surface.Right + 0.01f), label);
                Assert.That(layout.LeftColumn.Overlaps(layout.Footer), Is.False, label);
                Assert.That(layout.RightColumn.Overlaps(layout.Footer), Is.False, label);
                Assert.That(layout.VisibleMilestones(11), Is.GreaterThanOrEqualTo(0), label);
            }
        }
    }
}
