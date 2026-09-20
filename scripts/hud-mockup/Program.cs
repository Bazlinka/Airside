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
            Overview(scenario, width, height),
            Operations(scenario, width, height),
            RouteMapPage(scenario, width, height),
            Fleet(scenario, width, height),
            Contracts(scenario, width, height),
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

        var objective = OperationsSummary.Objective(scenario.PlayerFleet, scenario.Now,
            scenario.Operations.Clock, scenario.Operations.CareerState, scenario.Operations.MarketOffers());
        HudShellPainter.PaintObjective(list, HudShell.Objective(width, height, showGuide: false), objective);
        return new Page("overview", Serialise(list));
    }

    private static Page Operations(Scenario scenario, float width, float height)
    {
        var list = new HudDrawList();
        PaintShell(list, scenario, HudWorkspace.Operations, width, height);
        PaintObjective(list, scenario, width, height);

        var model = new OperationsWorkspaceModel();
        model.Rebuild(scenario.Operations, scenario.Now, OperationsBoardTab.Departures,
            scenario.SelectedRegistration, scenario.EventHistory);
        var layout = OperationsWorkspaceLayout.Create(HudShell.WorkspaceSurface(width, height),
            model.Attention.Count);

        var page = new HudDrawList();
        OperationsWorkspacePainter.Paint(page, model, layout, scenario.SelectedRegistration, 0);
        return new Page("operations", Serialise(list, page));
    }

    private static Page RouteMapPage(Scenario scenario, float width, float height)
    {
        var list = new HudDrawList();
        PaintShell(list, scenario, HudWorkspace.Map, width, height);
        PaintObjective(list, scenario, width, height);

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
            scenario.Operations.Home, portLincoln, scenario.Operations.PlayerAirline.LiveryHex);

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
        PaintObjective(list, scenario, width, height);

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
        PaintObjective(list, scenario, width, height);

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
        PaintObjective(list, scenario, width, height);

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
        var bar = HudShell.TopBar(width, height);
        var nav = HudShell.NavStrip(width, height);
        var airline = scenario.Operations.PlayerAirline;
        var career = scenario.Operations.CareerState;
        var clock = scenario.Operations.Clock;

        var segments = new List<HudTopBarSegment>();
        HudShell.FillSegments(bar, airline.Name, nav, new[]
        {
            $"ADELAIDE  {clock.TimeText(scenario.Now)}",
            $"${career.Funds:N0}",
            $"RELIABILITY {career.Reliability}%",
            career.Tier.ToString().ToUpperInvariant()
        }, segments);

        var tabs = new List<HudNavTab>();
        HudShell.FillTabs(nav, active, tabs);
        HudShellPainter.PaintTopBar(list, bar, nav, airline.Name, airline.LiveryHex, segments, tabs);
    }

    private static void PaintObjective(HudDrawList list, Scenario scenario, float width, float height)
    {
        if (!HudShell.ObjectiveSurvivesWorkspace(width, height))
            return;
        var objective = OperationsSummary.Objective(scenario.PlayerFleet, scenario.Now,
            scenario.Operations.Clock, scenario.Operations.CareerState, scenario.Operations.MarketOffers());
        HudShellPainter.PaintObjective(list, HudShell.Objective(width, height, showGuide: false), objective);
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
