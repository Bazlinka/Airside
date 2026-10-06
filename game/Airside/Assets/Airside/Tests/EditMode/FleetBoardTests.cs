using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// The unified Fleet workspace (ADR 0239): every base in one roster, filtered and sorted, the outstation
    /// aircraft's own profile, a market that delivers to the chosen base, and a painter that stays inside its sheet.
    /// </summary>
    public sealed class FleetBoardTests
    {
        /// <summary>The player's Saab at Adelaide plus a Dash 8-400 (VH-O01) based at Melbourne.</summary>
        private static (ManualSimulationClock Clock, AirlineOperations Ops) WithMelbourne()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            ops.RestoreCareerState(500_000, 95, nameof(OperatingTier.Domestic), null, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), 100,
                baseLevel: PlayerBaseLevel.ExpandedRegional, manualRotations: 12);
            Assert.That(ops.OpenOutstationBase("MEL").Accepted, Is.True);
            var bought = ops.BuyAircraftAtOutstation(AircraftType.Dash8Q400, "MEL");
            Assert.That(bought.Accepted, Is.True, bought.Reason);
            return (clock, ops);
        }

        private static FleetWorkspaceModel Build(AirlineOperations ops, SimulationTime now, string selected,
            FleetBoardState board = null)
        {
            var model = new FleetWorkspaceModel();
            model.Rebuild(ops, now, selected, board);
            return model;
        }

        [Test]
        public void Roster_ListsEveryBaseGroupedWithHeadings()
        {
            var (clock, ops) = WithMelbourne();

            var model = Build(ops, clock.Now, null);

            Assert.That(model.Mine.Select(r => r.Registration), Is.EqualTo(new[] { "VH-PAX", "VH-O01" }));
            Assert.That(model.Mine[1].IsOutstation, Is.True);
            Assert.That(model.Mine[1].BaseCode, Is.EqualTo("MEL"));
            Assert.That(model.MineSlots.Count, Is.EqualTo(4), "two base headings and two aircraft");
            Assert.That(model.MineSlots[0].IsHeader, Is.True);
            Assert.That(model.MineSlots[0].Text, Does.StartWith("ADELAIDE"));
            Assert.That(model.MineSlots[2].IsHeader, Is.True);
            Assert.That(model.MineSlots[2].Text, Does.StartWith("MELBOURNE"));
            Assert.That(model.Subtitle, Does.StartWith("1 of "));
            Assert.That(model.Subtitle, Does.Contain("1 based away"));
            Assert.That(model.OwnedCount, Is.EqualTo(ops.PlayerFleetCount()));
        }

        [Test]
        public void Roster_HasNoHeadingsWhileEveryAircraftIsAtOneBase()
        {
            var (clock, ops, _) = HudTestAirline.Create();

            var model = Build(ops, clock.Now, null);

            Assert.That(model.MineSlots.Count, Is.EqualTo(model.Mine.Count));
            Assert.That(model.MineSlots.All(s => !s.IsHeader), Is.True);
        }

        [Test]
        public void BaseFilter_ListsOnlyThatBasesAircraft()
        {
            var (clock, ops) = WithMelbourne();
            var board = new FleetBoardState { BaseFilter = "MEL" };

            var model = Build(ops, clock.Now, null, board);

            Assert.That(model.Mine.Select(r => r.Registration), Is.EqualTo(new[] { "VH-O01" }));
            Assert.That(model.Bases.Single(b => b.Code == "MEL").Selected, Is.True);
            Assert.That(model.Bases.Single(b => b.Code == "ADL").Selected, Is.False);
            Assert.That(model.MineSlots.All(s => !s.IsHeader), Is.True, "one base needs no heading");

            board.ToggleBase("MEL");
            Assert.That(board.BaseFilter, Is.Empty, "clicking the chosen base again shows every base");
        }

        [Test]
        public void StatusFilter_SeparatesParkedFromFlyingAndChecks()
        {
            var (clock, ops) = WithMelbourne();
            Assert.That(ops.ScheduleOutstationService("VH-O01", "SYD", new SimulationTime(1800)).Accepted, Is.True);
            clock.Set(new SimulationTime(2400));

            var flying = Build(ops, clock.Now, null, new FleetBoardState { Status = FleetStatusFilter.Flying });
            Assert.That(flying.Mine.Select(r => r.Registration), Is.EqualTo(new[] { "VH-O01" }));

            var parked = Build(ops, clock.Now, null, new FleetBoardState { Status = FleetStatusFilter.Parked });
            Assert.That(parked.Mine.Select(r => r.Registration), Is.EqualTo(new[] { "VH-PAX" }));

            var checks = Build(ops, clock.Now, null, new FleetBoardState { Status = FleetStatusFilter.Check });
            Assert.That(checks.Mine, Is.Empty);
            Assert.That(checks.StatusFilterLabel, Is.EqualTo("CHECKS"));
        }

        [Test]
        public void Sort_OrdersByTypeInsideEachBase()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            ops.RestoreCareerState(20_000, 90, nameof(OperatingTier.Provisional), null, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), 6, baseLevel: PlayerBaseLevel.ExpandedRegional);
            ops.AddAircraft(ops.PlayerAirline, "VH-AAA", AircraftType.Atr42, AirlineOperations.AdelaideRegionalBays[2]);

            var byFleet = Build(ops, clock.Now, null, new FleetBoardState { Sort = FleetSortMode.Fleet });
            Assert.That(byFleet.Mine.Select(r => r.Registration), Is.EqualTo(new[] { "VH-PAX", "VH-AAA" }));

            var byType = Build(ops, clock.Now, null, new FleetBoardState { Sort = FleetSortMode.Type });
            var expected = byType.Mine.OrderBy(r => r.TypeName, StringComparer.Ordinal).Select(r => r.Registration);
            Assert.That(byType.Mine.Select(r => r.Registration), Is.EqualTo(expected));
            Assert.That(byType.SortLabel, Is.EqualTo("TYPE"));
        }

        [Test]
        public void BoardState_CyclesThroughEveryFilterAndSortThenWrapsRound()
        {
            var board = new FleetBoardState();
            for (var i = 0; i < 4; i++)
                board.CycleStatus();
            Assert.That(board.Status, Is.EqualTo(FleetStatusFilter.All));
            for (var i = 0; i < 4; i++)
                board.CycleSort();
            Assert.That(board.Sort, Is.EqualTo(FleetSortMode.Fleet));
        }

        [Test]
        public void SelectingAnOutstationAircraft_ShowsItsProfileAndRoutes()
        {
            var (clock, ops) = WithMelbourne();

            var model = Build(ops, clock.Now, "VH-O01");

            Assert.That(model.HasSelection, Is.True);
            Assert.That(model.SelectedIsOutstation, Is.True);
            Assert.That(model.SelectedBaseCode, Is.EqualTo("MEL"));
            Assert.That(model.SelectedCapability.Any(line => line.Contains("Based at Melbourne")), Is.True);
            Assert.That(model.OutstationRoutes, Is.Not.Empty);
            Assert.That(model.OutstationRoutes.Select(r => r.Code), Does.Not.Contain("ADL"),
                "Adelaide movements belong to the live airport");
            Assert.That(model.OutstationRoutes.Select(r => r.Code), Does.Not.Contain("MEL"));
            Assert.That(model.CanSell, Is.True);
            Assert.That(model.SellLabel, Does.StartWith("SELL $"));
            Assert.That(model.HasMoveAction, Is.True);
            Assert.That(model.CanMoveBase, Is.True);
            Assert.That(model.MoveBaseLabel, Does.StartWith("TO ADELAIDE $"));
            Assert.That(model.AssignmentLine, Is.EqualTo("No flight planned"));
            Assert.That(model.Camera.Visible, Is.False, "an outstation aircraft has no 3D view");
        }

        [Test]
        public void ARouteOnAnOutstationProfile_CanBeSentOnlyWhenTheSimulationWouldAcceptIt()
        {
            var (clock, ops) = WithMelbourne();
            Assert.That(ops.ScheduleOutstationService("VH-O01", "SYD", new SimulationTime(1800)).Accepted, Is.True);

            var model = Build(ops, clock.Now, "VH-O01");

            Assert.That(model.OutstationRoutes, Is.Empty, "a planned flight has to finish first");
            Assert.That(model.RouteBlockedNote, Is.Not.Empty);
            Assert.That(model.CanSell, Is.False);
            Assert.That(model.CanMoveBase, Is.False);
            Assert.That(model.MoveHint, Does.Contain("flight planned"));
        }

        [Test]
        public void Market_DeliversToTheChosenBaseAndNeverOffersARefusedPurchase()
        {
            var (clock, ops) = WithMelbourne();
            var board = new FleetBoardState { BaseFilter = "MEL" };

            var model = Build(ops, clock.Now, null, board);

            Assert.That(model.BuyBase, Is.EqualTo("MEL"));
            Assert.That(model.MarketCaption, Does.Contain("DELIVERED TO MELBOURNE"));
            foreach (var offer in model.Market)
            {
                var refusal = ops.PurchaseRefusal(offer.Type, "MEL");
                Assert.That(offer.CanBuy, Is.EqualTo(refusal == null), offer.TypeName + ": " + refusal);
                if (offer.CanBuy)
                    Assert.That(offer.StandLine, Does.Contain("Melbourne"));
            }

            Assert.That(Build(ops, clock.Now, null).BuyBase, Is.EqualTo("ADL"), "with no base chosen it buys for Adelaide");
        }

        [Test]
        public void Bases_ShowOpenBasesWithTheirAircraftAndTheNextOneToOpen()
        {
            var (clock, ops) = WithMelbourne();

            var model = Build(ops, clock.Now, null);

            Assert.That(model.Bases.Select(b => b.Code), Is.EqualTo(new[] { "ADL", "MEL", "SYD", "BNE", "PER" }));
            var melbourne = model.Bases.Single(b => b.Code == "MEL");
            Assert.That(melbourne.IsOpen, Is.True);
            Assert.That(melbourne.Aircraft, Is.EqualTo(1));
            Assert.That(melbourne.Capacity, Is.EqualTo(AirlineOperations.OutstationCapacity));
            var sydney = model.Bases.Single(b => b.Code == "SYD");
            Assert.That(sydney.IsOpen, Is.False);
            Assert.That(sydney.OpenCost, Is.EqualTo(ops.NextOutstationCost));
            Assert.That(sydney.CanOpen, Is.EqualTo(sydney.OpenReason.Length == 0 && ops.CareerState.CanAfford(ops.NextOutstationCost)));
        }

        [Test]
        public void StatusText_ReadsTheSameForEveryKindOfAircraft()
        {
            var (clock, ops) = WithMelbourne();
            var entries = new List<PlayerFleetEntry>();
            PlayerFleet.Entries(ops, clock.Now, entries);
            var live = entries.Single(e => !e.IsOutstation);
            var away = entries.Single(e => e.IsOutstation);
            var level = ops.CareerState.BaseLevel;

            Assert.That(FleetStatusText.For(live, clock.Now, ops.Clock, level), Is.EqualTo("Available"));
            Assert.That(FleetStatusText.For(away, clock.Now, ops.Clock, level), Is.EqualTo("Available"));

            Assert.That(ops.ScheduleOutstationService("VH-O01", "SYD", new SimulationTime(1800)).Accepted, Is.True);
            PlayerFleet.Entries(ops, new SimulationTime(600), entries);
            Assert.That(FleetStatusText.For(entries.Single(e => e.IsOutstation), new SimulationTime(600), ops.Clock, level),
                Does.Contain("Sydney"));

            var depart = ops.OutstationFleet.Single().DepartAtSeconds;
            PlayerFleet.Entries(ops, new SimulationTime(depart + 600), entries);
            Assert.That(FleetStatusText.For(entries.Single(e => e.IsOutstation), new SimulationTime(depart + 600),
                ops.Clock, level), Is.EqualTo("Flying to Sydney"));
        }

        [Test]
        public void AFerryReadsAsAFerryOnTheRoster()
        {
            var (clock, ops) = WithMelbourne();
            Assert.That(ops.RelocateToAdelaide("VH-O01").Accepted, Is.True);

            var model = Build(ops, clock.Now, "VH-O01");

            var row = model.Mine.Single(r => r.Registration == "VH-O01");
            Assert.That(row.Status, Is.EqualTo("Ferry in from Melbourne"));
            Assert.That(row.IsOutstation, Is.False, "it is an Adelaide aircraft from the moment it leaves");
            Assert.That(model.AssignmentLine, Does.Contain("ferry"));
        }

        [Test]
        public void Layout_KeepsTheBasesStripClearOfTheRosterAndMarket()
        {
            foreach (var (width, height) in HudTestAirline.Viewports)
            {
                var surface = HudShell.WorkspaceSurface(width, height);
                var layout = FleetWorkspaceLayout.Create(surface, AircraftAcquisition.All.Count);
                var label = $"{width}x{height}";

                Assert.That(layout.Bases.Overlaps(layout.Roster), Is.False, label);
                Assert.That(layout.Bases.Overlaps(layout.Detail), Is.False, label);
                Assert.That(layout.Bases.Overlaps(layout.Market), Is.False, label);
                if (!layout.Bases.IsEmpty)
                {
                    Assert.That(layout.Bases.Bottom, Is.LessThanOrEqualTo(surface.Bottom + 0.01f), label);
                    for (var i = 0; i < 5; i++)
                    {
                        var chip = layout.BaseChip(i, 5);
                        Assert.That(chip.X, Is.GreaterThanOrEqualTo(surface.X - 0.01f), label);
                        Assert.That(chip.Right, Is.LessThanOrEqualTo(surface.Right + 0.01f), label);
                        if (i > 0)
                            Assert.That(chip.Overlaps(layout.BaseChip(i - 1, 5)), Is.False, label);
                    }
                }
            }
        }

        [Test]
        public void Painter_StaysInsideTheSheetForEveryViewportAndSelection()
        {
            var (clock, ops) = WithMelbourne();
            Assert.That(ops.SetRepeatSchedule("VH-O01", "SYD", 12).Accepted, Is.True);
            var selections = new[] { "VH-PAX", "VH-O01" };
            var boards = new[]
            {
                new FleetBoardState(),
                new FleetBoardState { BaseFilter = "MEL", Sort = FleetSortMode.Earnings },
                new FleetBoardState { Status = FleetStatusFilter.Flying }
            };

            foreach (var (width, height) in HudTestAirline.Viewports)
            foreach (var selected in selections)
            foreach (var board in boards)
            {
                var surface = HudShell.WorkspaceSurface(width, height);
                var model = Build(ops, clock.Now, selected, board);
                var list = new HudDrawList();
                var layout = FleetWorkspaceLayout.Create(surface, model.Market.Count);

                FleetWorkspacePainter.Paint(list, model, layout, selected, 0, showOtherOperators: true);

                AssertInside(list, surface, $"fleet {selected} {width}x{height}");
            }
        }

        [Test]
        public void Painter_GivesEveryControlAnActionTheHudKnowsHowToRun()
        {
            var (clock, ops) = WithMelbourne();
            var surface = HudShell.WorkspaceSurface(1440f, 900f);
            var model = Build(ops, clock.Now, "VH-O01");
            var list = new HudDrawList();
            FleetWorkspacePainter.Paint(list, model, FleetWorkspaceLayout.Create(surface, model.Market.Count), "VH-O01", 0);

            foreach (var command in list.Commands)
            {
                if (command.Kind != HudDrawKind.Button && command.Kind != HudDrawKind.Hotspot)
                    continue;
                Assert.That(command.ActionId, Is.Not.Empty, command.Text);
                Assert.That(IsKnownFleetAction(command.ActionId), Is.True, command.ActionId);
            }

            Assert.That(list.Commands.Any(c => c.ActionId == FleetActions.Sell), Is.True);
            Assert.That(list.Commands.Any(c => c.ActionId == FleetActions.MoveBase), Is.True);
            Assert.That(list.Commands.Any(c => c.ActionId == FleetActions.CycleStatus), Is.True);
            Assert.That(list.Commands.Any(c => c.ActionId == FleetActions.CycleSort), Is.True);
            Assert.That(list.Commands.Any(c => c.ActionId == FleetActions.Base("MEL")), Is.True);
            Assert.That(list.Commands.Any(c => c.ActionId.StartsWith(FleetActions.RoutePrefix, StringComparison.Ordinal)),
                Is.True, "an idle outstation aircraft lists routes it can be sent on");
            Assert.That(list.Commands.Any(c => c.ActionId == HudAction.Select("VH-O01")), Is.True);
        }

        [Test]
        public void Painter_OffersCamerasOnlyOnceTheHudHasSaidWhichAreAvailable()
        {
            var (clock, ops) = WithMelbourne();
            var surface = HudShell.WorkspaceSurface(1440f, 900f);
            var model = Build(ops, clock.Now, "VH-PAX");
            var layout = FleetWorkspaceLayout.Create(surface, model.Market.Count);
            var list = new HudDrawList();

            FleetWorkspacePainter.Paint(list, model, layout, "VH-PAX", 0);
            Assert.That(list.Commands.Any(c => c.ActionId == HudAction.CameraCockpit), Is.False);

            model.Camera.Visible = true;
            model.Camera.Exterior = true;
            FleetWorkspacePainter.Paint(list, model, layout, "VH-PAX", 0);
            var cockpit = list.Commands.Single(c => c.ActionId == HudAction.CameraCockpit);
            var exterior = list.Commands.Single(c => c.ActionId == HudAction.CameraExterior);
            Assert.That(cockpit.Enabled, Is.False);
            Assert.That(exterior.Enabled, Is.True);
        }

        [Test]
        public void PurchaseRefusal_AgreesWithTheBuyCommandsForEveryTypeAtEveryBase()
        {
            var setups = new (long Funds, PlayerBaseLevel Level)[]
            {
                (60_000, PlayerBaseLevel.ExpandedRegional),
                (300_000, PlayerBaseLevel.JetGate),
                (8_000, PlayerBaseLevel.Starter)
            };
            foreach (var (funds, level) in setups)
            foreach (var offer in AircraftAcquisition.All)
            foreach (var baseCode in new[] { "ADL", "MEL" })
            {
                var (_, ops, _) = HudTestAirline.Create();
                // The base is restored open rather than bought, so a low-cash setup still has one to buy at.
                ops.RestoreCareerState(funds, 95, nameof(OperatingTier.Domestic), null, 0, 0,
                    Array.Empty<string>(), Array.Empty<string>(), 100, baseLevel: level,
                    outstationBases: baseCode == "MEL" ? new[] { "MEL" } : null);

                var refusal = ops.PurchaseRefusal(offer.Type, baseCode);
                var result = baseCode == "ADL"
                    ? ops.BuyAircraft(offer.Type)
                    : ops.BuyAircraftAtOutstation(offer.Type, "MEL");

                var label = $"{offer.Type.Name} at {baseCode}, ${funds:N0}, {level}";
                Assert.That(result.Accepted, Is.EqualTo(refusal == null), label + " / " + refusal);
                if (!result.Accepted)
                    Assert.That(refusal, Is.EqualTo(result.Reason), label);
            }
        }

        [Test]
        public void PurchaseRefusal_UnknownBaseAndTypeAreRefusedWithTheCommandsWords()
        {
            var (_, ops, _) = HudTestAirline.Create();

            Assert.That(ops.PurchaseRefusal(AircraftType.Saab340, "SYD"), Is.EqualTo("Open that base first."));
            Assert.That(ops.PurchaseRefusal(null, "ADL"), Is.EqualTo("That type is not for sale."));
        }

        private static bool IsKnownFleetAction(string id)
        {
            string[] exact =
            {
                HudAction.Close, HudAction.ToggleOtherOperators, HudAction.Primary, HudAction.StartCheck, HudAction.Track,
                HudAction.ToggleFreighter, HudAction.CameraCockpit, HudAction.CameraPassenger,
                HudAction.CameraExterior, FleetWorkspacePainter.MarketPrevious, FleetWorkspacePainter.MarketNext,
                FleetActions.CycleStatus, FleetActions.CycleSort, FleetActions.RoutesPrevious,
                FleetActions.RoutesNext, FleetActions.Sell, FleetActions.MoveBase, FleetActions.OutstationCheck,
                FleetActions.RemoveRepeat
            };
            if (exact.Contains(id))
                return true;
            string[] prefixes =
            {
                HudAction.SelectPrefix, HudAction.BuyPrefix, FleetActions.BasePrefix, FleetActions.OpenBasePrefix,
                FleetActions.RoutePrefix, FleetActions.RepeatPrefix
            };
            return prefixes.Any(p => id.StartsWith(p, StringComparison.Ordinal));
        }

        private static void AssertInside(HudDrawList list, HudBox surface, string label)
        {
            foreach (var command in list.Commands)
            {
                if (command.Kind == HudDrawKind.Line || command.Box.IsEmpty)
                    continue;
                Assert.That(command.Box.X, Is.GreaterThanOrEqualTo(surface.X - 0.01f),
                    $"{label}: {command.Kind} '{command.Text}' left of the surface");
                Assert.That(command.Box.Right, Is.LessThanOrEqualTo(surface.Right + 0.01f),
                    $"{label}: {command.Kind} '{command.Text}' right of the surface");
                Assert.That(command.Box.Y, Is.GreaterThanOrEqualTo(surface.Y - 0.01f),
                    $"{label}: {command.Kind} '{command.Text}' above the surface");
                Assert.That(command.Box.Bottom, Is.LessThanOrEqualTo(surface.Bottom + 0.01f),
                    $"{label}: {command.Kind} '{command.Text}' below the surface");
            }
        }
    }
}
