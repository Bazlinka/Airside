using System;
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
            Assert.That(model.BaseCapabilityLine, Does.StartWith("Regional starter base"));
            Assert.That(model.BaseCapabilityLine, Does.Contain("Regional apron"));
            Assert.That(model.AdelaideRankLine, Is.EqualTo("#1 of 1 at Adelaide"));
            Assert.That(model.ContractHistory, Is.Empty);
            Assert.That(model.EmptyHistoryLine, Is.Not.Empty);
            Assert.That(model.CurrentLiveryHex, Is.EqualTo(ops.PlayerAirline.LiveryHex));
        }

        [Test]
        public void Stats_RepaintingTheLiveryIsReflectedOnTheNextRebuild()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            var target = StatsWorkspaceModel.LiveryPalette.First(l => l.Hex != ops.PlayerAirline.LiveryHex);
            Assert.That(ops.SetLivery(target.Hex).Accepted, Is.True);

            var model = new StatsWorkspaceModel();
            model.Rebuild(ops, clock.Now);
            Assert.That(model.CurrentLiveryHex, Is.EqualTo(target.Hex));
        }

        [Test]
        public void Stats_AdelaideStandingUsesRealCompletedRotationsAndNamesTheNextRival()
        {
            var (clock, ops, player) = HudTestAirline.Create("Southern Cross");
            player.CompletedTrips = 3;

            var rex = Airline.Rex();
            ops.AddAirline(rex);
            var rexAircraft = ops.AddAircraft(rex, "VH-REX", AircraftType.Saab340,
                AirlineOperations.AdelaideRegionalBays[1]);
            rexAircraft.CompletedTrips = 6;

            var qantasLink = Airline.QantasLink();
            ops.AddAirline(qantasLink);
            var qantasAircraft = ops.AddAircraft(qantasLink, "VH-QLK", AircraftType.Dash8Q400,
                AirlineOperations.AdelaideRegionalBays[2]);
            qantasAircraft.CompletedTrips = 2;

            var model = new StatsWorkspaceModel();
            model.Rebuild(ops, clock.Now);

            Assert.That(model.AdelaideStandings.Select(row => row.AirlineName),
                Is.EqualTo(new[] { "Rex", "Southern Cross", "QantasLink" }));
            Assert.That(model.AdelaideStandings.Select(row => row.CompletedRotations),
                Is.EqualTo(new[] { 6, 3, 2 }));
            Assert.That(model.AdelaideRankLine, Is.EqualTo("#2 of 3 at Adelaide"));
            Assert.That(model.CompetitiveTargetLine, Is.EqualTo("Pass Rex: 4 more rotations."));
        }

        [Test]
        public void Stats_TiedFreshAirlinesNeedARealRotationToTakeTheLead()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            ops.AddAirline(Airline.Rex());

            var model = new StatsWorkspaceModel();
            model.Rebuild(ops, clock.Now);

            Assert.That(model.AdelaideRankLine, Is.EqualTo("Tied #1 of 2 at Adelaide"));
            Assert.That(model.CompetitiveTargetLine, Does.Contain("outright lead"));
        }

        [Test]
        public void Stats_CompactStandingAlwaysKeepsThePlayerVisible()
        {
            var (clock, ops, player) = HudTestAirline.Create();
            player.CompletedTrips = 0;
            foreach (var (airline, registration, trips, bay) in new[]
                     {
                         (Airline.Rex(), "VH-RXA", 8, 1),
                         (Airline.QantasLink(), "VH-QXA", 7, 2),
                         (Airline.VirginAustralia(), "VH-VXA", 6, 3),
                         (Airline.Jetstar(), "VH-JXA", 5, 4)
                     })
            {
                ops.AddAirline(airline);
                var aircraft = ops.AddAircraft(airline, registration, AircraftType.Saab340,
                    AirlineOperations.AdelaideRegionalBays[bay]);
                aircraft.CompletedTrips = trips;
            }

            var model = new StatsWorkspaceModel();
            model.Rebuild(ops, clock.Now);
            var compact = model.VisibleStandings(3);

            Assert.That(compact, Has.Count.EqualTo(3));
            Assert.That(compact.Count(row => row.IsPlayer), Is.EqualTo(1));
            Assert.That(compact[0].AirlineName, Is.EqualTo("Rex"));
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
            Assert.That(model.NextTierRequirementLine, Does.Contain("expanded regional base"));
            Assert.That(model.NextTierProgress01, Is.EqualTo(0f));
        }

        [Test]
        public void BaseCapability_RoadmapTracksExistingOperatingTiers()
        {
            Assert.That(CareerProgress.BaseCapabilityFor(OperatingTier.Provisional).Title,
                Is.EqualTo("Regional starter base"));
            Assert.That(CareerProgress.BaseCapabilityFor(OperatingTier.Regional).Title,
                Is.EqualTo("Expanded regional base"));
            Assert.That(CareerProgress.BaseCapabilityFor(OperatingTier.Domestic).Title,
                Is.EqualTo("Jet-gate operation"));
            Assert.That(CareerProgress.BaseCapabilityFor(OperatingTier.International).Title,
                Is.EqualTo("International base"));
            Assert.That(CareerProgress.BaseCapabilityFor(OperatingTier.International).NextUnlock, Is.Empty);
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
            Assert.That(model.ContractsFulfilledLine, Is.EqualTo("1 contracts fulfilled all-time"));
            Assert.That(model.MilestonesReachedLine, Does.Match(@"^\d+ of \d+ milestones reached$"));
        }

        [Test]
        public void Stats_ContractsFulfilledLineIsTheLifetimeCountNotTheCappedHistory()
        {
            // completedContractIds is the true lifetime record (never trimmed); contractHistory
            // is the capped-at-MaxHistoryShown recent list — seeded directly rather than
            // simulated so this tests the Stats model's own counting, not flight simulation.
            var toFulfil = StatsWorkspaceModel.MaxHistoryShown + 2;
            var completedIds = Enumerable.Range(0, toFulfil).Select(i => $"STATS-TEST-{i}").ToList();
            var history = Enumerable.Range(0, StatsWorkspaceModel.MaxHistoryShown)
                .Select(i => new CompletedContractRecord($"STATS-TEST-{i}", "ADL", "KGC", 100, new SimulationTime(i)))
                .ToList();

            var (clock, ops, _) = HudTestAirline.Create();
            ops.RestoreCareerState(ops.CareerState.Funds, ops.CareerState.Reliability,
                ops.CareerState.Tier.ToString(), null, 0, 0, Array.Empty<string>(),
                completedIds, ops.CareerState.CompletedPlayerRotations, null,
                ops.CareerState.LifetimeRevenue, history);

            var model = new StatsWorkspaceModel();
            model.Rebuild(ops, clock.Now);
            Assert.That(model.ContractHistory.Count, Is.EqualTo(StatsWorkspaceModel.MaxHistoryShown));
            Assert.That(model.ContractsFulfilledLine, Is.EqualTo($"{toFulfil} contracts fulfilled all-time"));
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

                // The livery swatch row (six 28px squares) must fit inside the column that
                // holds it and clear of the workspace footer, at every viewport this runs.
                var lastSwatch = layout.LiverySwatch(StatsWorkspaceModel.LiveryPalette.Length - 1);
                Assert.That(lastSwatch.Right, Is.LessThanOrEqualTo(layout.LeftColumn.Right + 0.01f), label);
                Assert.That(lastSwatch.Bottom, Is.LessThanOrEqualTo(layout.Footer.Y + 0.01f), label);

                var visibleStandings = layout.VisibleStandingRows(12);
                if (visibleStandings > 0)
                    Assert.That(layout.CompetitionTarget(visibleStandings).Bottom,
                        Is.LessThanOrEqualTo(layout.LeftColumn.Bottom + 0.01f), label);

                // The rename field + button sit in the header, right-aligned before CLOSE —
                // must clear the title on the left and CLOSE on the right at every width.
                Assert.That(layout.RenameFieldBox.Overlaps(layout.TitleBox), Is.False, label);
                Assert.That(layout.RenameFieldBox.Overlaps(layout.RenameButtonBox), Is.False, label);
                Assert.That(layout.RenameButtonBox.Right,
                    Is.LessThanOrEqualTo(OperationsWorkspacePainter.CloseBox(surface).X + 0.01f), label);
                Assert.That(layout.RenameFieldBox.X, Is.GreaterThanOrEqualTo(layout.TitleBox.Right), label);
            }
        }

        [Test]
        public void Stats_PaintedMilestonesHistoryAndFulfilledLineNeverRunPastTheFooter()
        {
            // A fully populated model (all 11 milestones, a full history list) is the worst
            // case for vertical space — including on a cramped stacked layout, where a
            // straight-line reservation once let the lifetime line run 24px past the footer
            // (caught by this test after the fact; StatsWorkspacePainter now stops drawing
            // rather than overflow, the same defensive pattern ContractsWorkspacePainter's
            // PaintActive already uses for its own terms list).
            var (clock, ops, plane) = HudTestAirline.Create();
            var definition = RouteContractCatalogue.RegionalKingscoteIntro;
            Assert.That(ops.AcceptContract(definition).Accepted, Is.True);
            HudTestAirline.CompleteActiveContract(clock, ops, definition);
            var history = Enumerable.Range(0, StatsWorkspaceModel.MaxHistoryShown)
                .Select(i => new CompletedContractRecord($"FULL-{i}", "ADL", "KGC", 100, new SimulationTime(i)))
                .ToList();
            ops.RestoreCareerState(50_000, 100, nameof(OperatingTier.International), null, 0, 0,
                Array.Empty<string>(), history.Select(h => h.DefinitionId).ToList(), 40, null, 12_345, history);
            Assert.That(ops.BuyAircraft(AircraftType.Boeing7378).Accepted, Is.True, "for the jet-operator milestone");

            var model = new StatsWorkspaceModel();
            model.Rebuild(ops, clock.Now);
            // 11 milestones plus the current campaign chapter's header and goals (ADR 0083).
            var chapter = Campaign.Current(ops.CampaignChapters());
            Assert.That(model.Milestones.Count, Is.EqualTo(11 + 1 + chapter.Goals.Count),
                "worst case assumes the full milestone list");
            Assert.That(model.ContractHistory.Count, Is.EqualTo(StatsWorkspaceModel.MaxHistoryShown));

            foreach (var (width, height) in HudTestAirline.Viewports)
            {
                var surface = HudShell.WorkspaceSurface(width, height);
                var layout = StatsWorkspaceLayout.Create(surface);
                var into = new HudDrawList();
                StatsWorkspacePainter.Paint(into, model, layout);

                var floor = layout.RightColumn.Bottom;
                foreach (var command in into.Commands)
                {
                    // Only the Right column's own content (Milestones/history/fulfilled line):
                    // exclude the footer's own row (the airline-name attribution, deliberately
                    // below RightColumn.Bottom) and anything left of the column entirely.
                    if (command.Kind != HudDrawKind.Text)
                        continue;
                    if (command.Box.X < layout.RightColumn.X - 0.01f)
                        continue;
                    if (command.Box.Y >= layout.Footer.Y - 0.01f)
                        continue;
                    Assert.That(command.Box.Bottom, Is.LessThanOrEqualTo(floor + 0.5f),
                        $"{width}x{height}: '{command.Text}' runs past the Right column's own bottom");
                }
            }
        }
    }
}
