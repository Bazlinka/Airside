using System.Text.Json;
using System.Text.Json.Serialization;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;

namespace Airside.Tools.HudMockup;

/// <summary>
/// Drives a scripted-but-real airline forward through the actual simulation, then writes
/// the shared HUD draw list for each page to JSON. Nothing here authors HUD content: it
/// plays the game headlessly and asks the same painters the runtime HUD uses what to draw.
/// </summary>
public static class Program
{
    private const float ViewportWidth = 1440f;
    private const float ViewportHeight = 900f;

    public static int Main(string[] args)
    {
        var output = args.Length > 0 ? args[0] : "work/hud-mockups/draw-lists.json";
        var width = args.Length > 1 ? float.Parse(args[1]) : ViewportWidth;
        var height = args.Length > 2 ? float.Parse(args[2]) : ViewportHeight;

        var scenario = Scenario.Play();
        var pages = new List<Page>
        {
            SplashPage(scenario, width, height, SplashStep.Menu),
            SplashPage(scenario, width, height, SplashStep.NewAirline),
            Overview(scenario, width, height),
            Operations(scenario, width, height),
            RouteMapPage(scenario, width, height),
            Fleet(scenario, width, height),
            Contracts(scenario, width, height),
            Career(scenario, width, height),
            Stats(scenario, width, height)
        };

        var document = new Document(new[] { width, height }, pages);
        var directory = Path.GetDirectoryName(Path.GetFullPath(output));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(output, JsonSerializer.Serialize(document, new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        }));

        var titles = Path.Combine(directory ?? ".", "title-metrics.json");
        File.WriteAllText(titles, JsonSerializer.Serialize(TitleMetrics(scenario),
            new JsonSerializerOptions { WriteIndented = false }));

        Console.WriteLine($"Wrote {pages.Count} pages to {output}");
        Console.WriteLine($"Wrote fuselage title metrics to {titles}");
        Console.WriteLine(scenario.Describe());
        return 0;
    }

    /// <summary>
    /// To-scale fuselage title measurements per type, so scripts/render-aircraft-titles.py
    /// can show what the paint actually comes out at against the real airframe length.
    /// </summary>
    private static List<TitleMetric> TitleMetrics(Scenario scenario)
    {
        var name = scenario.Operations.PlayerAirline.Name.ToUpperInvariant();
        var metrics = new List<TitleMetric>();
        foreach (var spec in AircraftCatalogue.All)
        {
            var layout = AircraftIdentityMarkings.For(spec.Type);
            var fitted = AircraftTitlePaint.OperatorCharacterSize(spec.Type, name,
                layout.OperatorCharacterSize);
            metrics.Add(new TitleMetric(
                spec.Name,
                (float)spec.LengthMetres,
                (float)spec.HeightMetres,
                name,
                layout.OperatorCharacterSize,
                fitted,
                AircraftTitlePaint.LineHeightMetres(fitted),
                AircraftTitlePaint.WidthMetres(name, fitted),
                AircraftTitlePaint.TitleLengthBudgetMetres(spec.Type),
                // What the removed backing plate measured: sized in character-size units
                // instead of metres, so it came out 6.4x smaller than the text it framed.
                Math.Max(0.6f, name.Length * layout.OperatorCharacterSize * 0.62f + 0.3f),
                layout.OperatorCharacterSize * 1.9f));
        }

        return metrics;
    }

    private sealed record TitleMetric(string Type, float LengthMetres, float HeightMetres,
        string Title, float AuthoredCharacterSize, float FittedCharacterSize, float TitleHeightMetres,
        float TitleWidthMetres, float BudgetMetres, float OldPlateWidthMetres, float OldPlateHeightMetres);

    // ---- Pages ---------------------------------------------------------------------

    private static Page Overview(Scenario scenario, float width, float height)
    {
        var list = new HudDrawList();
        PaintShell(list, scenario, HudWorkspace.None, width, height);
        var shell = HudShell.Layout(width, height);

        var operations = scenario.Operations;
        var objective = OperationsSummary.Objective(scenario.PlayerFleet, scenario.Now,
            operations.Clock, operations.CareerState, operations.MarketOffers(), operations.PinnedCareerGoal());
        var stage = operations.CareerStage();
        HudShellPainter.PaintCareer(list, shell.Career, objective, stage.Fraction, $"{stage.Done}/{stage.Total}");

        var rows = new List<OperationsRow>();
        OperationsSummary.FillPlayerRows(scenario.PlayerFleet, scenario.Now, rows);
        HudShellPainter.PaintOperations(list, shell.Operations, rows, scenario.SelectedRegistration,
            $"{OperationsSummary.AvailableCount(scenario.PlayerFleet, scenario.Now)} available");

        // The runtime words these from the live aircraft; the mockup uses the scenario's own.
        var selected = scenario.Selected;
        var card = new SelectionCardData
        {
            Registration = selected.Registration,
            TypeName = selected.Type.Name,
            LiveryHex = operations.PlayerAirline.LiveryHex,
            RouteLine = selected.Scheduled.HasValue
                ? $"Adelaide → {selected.Scheduled.Value.Destination.Name} · departs {operations.Clock.TimeText(selected.Scheduled.Value.DepartAt)}"
                : "Adelaide",
            LiveLine = "Parked · 0 kt",
            PhaseLabel = "Turnaround",
            IsPlayer = true,
            PrimaryLabel = "View plan",
            CanCancel = selected.Scheduled.HasValue
        };
        if (selected.Scheduled.HasValue)
        {
            var prep = DeparturePrep.For(selected, scenario.Now, operations.CareerState.BaseLevel);
            card.Prep.Add(new SelectionPrepStage("Fuel", (float)prep.FuelProgress, prep.Stage == DeparturePrepStage.Fuel));
            card.Prep.Add(new SelectionPrepStage("Catering", (float)prep.CateringProgress, prep.Stage == DeparturePrepStage.Catering));
            card.Prep.Add(new SelectionPrepStage("Baggage", (float)prep.BaggageProgress, prep.Stage == DeparturePrepStage.Baggage));
            card.Prep.Add(new SelectionPrepStage("Boarding", (float)prep.BoardingProgress, prep.Stage == DeparturePrepStage.Boarding));
        }
        var cardHeight = Math.Min(shell.SelectedCard.Height, SelectionCardPainter.HeightFor(card));
        SelectionCardPainter.Paint(list, new HudBox(shell.SelectedCard.X, shell.SelectedCard.Bottom - cardHeight,
            shell.SelectedCard.Width, cardHeight), card);

        ToastPainter.Paint(list, shell.Toast, "Soak Air is open for business. Plan a flight for VH-PAX.",
            HudTone.Accent, 1f);
        if (!shell.MiniMap.IsEmpty)
            MiniMapFrame.Paint(list, shell.MiniMap, "ADELAIDE · 23 / 05");
        return new Page("overview", Serialise(list));
    }

    private static Page SplashPage(Scenario scenario, float width, float height, SplashStep step)
    {
        var operations = scenario.Operations;
        var model = new SplashModel
        {
            Step = step,
            HasSave = true,
            SaveName = operations.PlayerAirline.Name,
            SaveLiveryHex = operations.PlayerAirline.LiveryHex,
            SaveTier = operations.CareerState.Tier.ToString(),
            SaveSummary = $"{operations.PlayerFleetCount()} aircraft · ${operations.CareerState.Funds:N0} · {operations.CareerState.Reliability}% reliability",
            SavedWhen = "Saved 26 Sep 12:06 · 4 services flown",
            ClockText = operations.Clock.TimeText(scenario.Now),
            StartingFunds = AirlineCareerState.StartingFunds,
            SelectedLivery = 1
        };
        foreach (var livery in StatsWorkspaceModel.LiveryPalette)
            model.Liveries.Add(livery);
        var list = new HudDrawList();
        SplashPainter.Paint(list, SplashLayout.Create(width, height, step, model.HasSave), model, 0.3f);
        return new Page(step == SplashStep.Menu ? "splash" : "splash-new", Serialise(list));
    }

    private static Page Career(Scenario scenario, float width, float height)
    {
        var list = new HudDrawList();
        PaintShell(list, scenario, HudWorkspace.Stats, width, height);
        var model = new CareerTrackModel();
        model.Rebuild(scenario.Operations);
        var layout = CareerTrackLayout.Create(HudShell.WorkspaceSurface(width, height), model.CurrentGoals.Count);
        var page = new HudDrawList();
        CareerTrackPainter.Paint(page, model, layout);
        return new Page("career", Serialise(list, page));
    }

    private static Page Operations(Scenario scenario, float width, float height)
    {
        var list = new HudDrawList();
        PaintShell(list, scenario, HudWorkspace.Operations, width, height);

        var model = new OperationsWorkspaceModel();
        model.Rebuild(scenario.Operations, scenario.Now, OperationsBoardTab.Departures,
            scenario.SelectedRegistration, scenario.EventHistory);
        var layout = OperationsWorkspaceLayout.Create(HudShell.WorkspaceSurface(width, height),
            model.Attention.Count);

        var page = new HudDrawList();
        OperationsWorkspacePainter.Paint(page, model, layout, scenario.SelectedRegistration,
            model.FirstActiveRowIndex);
        return new Page("operations", Serialise(list, page));
    }

    private static Page RouteMapPage(Scenario scenario, float width, float height)
    {
        var list = new HudDrawList();
        PaintShell(list, scenario, HudWorkspace.Map, width, height);

        DestinationCatalogue.TryFind("PLO", out var portLincoln);
        var model = new RouteMapWorkspaceModel();
        model.Rebuild(scenario.Operations, scenario.Selected, portLincoln, 15 * 60, scenario.Now,
            RouteMapFilter.Available);
        var layout = RouteMapWorkspaceLayout.Create(HudShell.WorkspaceSurface(width, height));

        var page = new HudDrawList();
        RouteMapWorkspacePainter.Paint(page, model, layout);

        var network = new HudDrawList();
        var lens = new AustraliaMapLens();
        lens.SetZoom(1.9f);
        lens.CenterOn(layout.Map.Width, layout.Map.Height, 140.5, -32.0);
        RouteMapWorkspacePainter.PaintNetwork(network, layout.Map, lens, model.AllDestinations,
            scenario.Operations.Home, portLincoln, scenario.Operations.PlayerAirline.LiveryHex,
            model.AircraftRangeKm, model.AircraftRangeLabel);

        var filters = new HudDrawList();
        RouteMapWorkspacePainter.PaintFilters(filters, model, layout);

        // Surface and map well, then the network inside it, then the pills floating over it —
        // the same order the runtime map paints in.
        return new Page("map", Serialise(list, page, network, filters));
    }

    private static Page Fleet(Scenario scenario, float width, float height)
    {
        var list = new HudDrawList();
        PaintShell(list, scenario, HudWorkspace.Fleet, width, height);

        var model = new FleetWorkspaceModel();
        model.Rebuild(scenario.Operations, scenario.Now, scenario.SelectedRegistration);
        var layout = FleetWorkspaceLayout.Create(HudShell.WorkspaceSurface(width, height), model.Market.Count);

        var page = new HudDrawList();
        FleetWorkspacePainter.Paint(page, model, layout, scenario.SelectedRegistration, 0);
        return new Page("fleet", Serialise(list, page));
    }

    private static Page Contracts(Scenario scenario, float width, float height)
    {
        var list = new HudDrawList();
        PaintShell(list, scenario, HudWorkspace.Contracts, width, height);

        var model = new ContractsWorkspaceModel();
        model.Rebuild(scenario.Operations, scenario.Now);
        var layout = ContractsWorkspaceLayout.Create(HudShell.WorkspaceSurface(width, height),
            model.ActiveTerms.Count);

        var highlighted = string.Empty;
        foreach (var offer in model.Offers)
        {
            if (!offer.CanAccept)
                continue;
            highlighted = offer.Definition.Id;
            break;
        }

        var page = new HudDrawList();
        ContractsWorkspacePainter.Paint(page, model, layout, highlighted);
        return new Page("contracts", Serialise(list, page));
    }

    private static Page Stats(Scenario scenario, float width, float height)
    {
        var list = new HudDrawList();
        PaintShell(list, scenario, HudWorkspace.Stats, width, height);

        var model = new StatsWorkspaceModel();
        model.Rebuild(scenario.Operations, scenario.Now);
        var layout = StatsWorkspaceLayout.Create(HudShell.WorkspaceSurface(width, height));

        var page = new HudDrawList();
        StatsWorkspacePainter.Paint(page, model, layout);
        return new Page("stats", Serialise(list, page));
    }

    // ---- Shared shell --------------------------------------------------------------

    private static void PaintShell(HudDrawList list, Scenario scenario, HudWorkspace active,
        float width, float height)
    {
        var shell = HudShell.Layout(width, height, workspaceOpen: active != HudWorkspace.None);
        var airline = scenario.Operations.PlayerAirline;
        var tabs = new List<HudNavTab>();
        HudShell.FillTabs(shell.Rail, height, active, tabs);
        HudShellPainter.PaintRail(list, shell.Rail, airline.LiveryHex, tabs);

        var values = new List<HudCapsuleValue>();
        HudShellPainter.CapsuleValues(scenario.Operations, scenario.Operations.Clock.TimeText(scenario.Now), values);
        var segments = new List<HudCapsuleSegment>();
        HudShell.FillCapsule(shell.Capsule, values, segments);
        HudShellPainter.PaintCapsule(list, shell.Capsule, segments);
    }

    // ---- Serialisation -------------------------------------------------------------

    private static List<Command> Serialise(params HudDrawList[] lists)
    {
        var commands = new List<Command>();
        foreach (var list in lists)
        foreach (var c in list.Commands)
            commands.Add(new Command(
                c.Kind.ToString(),
                new[] { c.Box.X, c.Box.Y, c.Box.Width, c.Box.Height },
                string.IsNullOrEmpty(c.Text) ? null : c.Text,
                c.Tone.ToString(),
                c.FontSize,
                (int)c.Style,
                c.Align.ToString(),
                c.Value,
                c.ColourHex,
                string.IsNullOrEmpty(c.ActionId) ? null : c.ActionId,
                c.Enabled));
        return commands;
    }

    private sealed record Document(float[] Viewport, List<Page> Pages);

    private sealed record Page(string Name, List<Command> Commands);

    private sealed record Command(string Kind, float[] Box, string Text, string Tone, float FontSize,
        int Style, string Align, float Value, string Colour, string Action, bool Enabled);
}
