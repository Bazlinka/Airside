using System;
using System.Collections.Generic;

namespace Airside.Presentation
{
    /// <summary>What one entry of a <see cref="HudDrawList"/> paints.</summary>
    public enum HudDrawKind
    {
        /// <summary>A floating graphite-glass panel: rounded, shadowed, with a 1 pt inner edge.</summary>
        Surface,
        /// <summary>Flat tinted rectangle. <see cref="HudDrawCommand.Value"/> is its alpha.</summary>
        Fill,
        /// <summary>A rule, one point thick on its short axis.</summary>
        Hairline,
        /// <summary>A frame drawn around the box rather than a fill.</summary>
        Outline,
        Text,
        /// <summary>Track plus fill; <see cref="HudDrawCommand.Value"/> is 0..1.</summary>
        Bar,
        Button,
        /// <summary>An invisible click target — a board row, a map dot, a list entry.</summary>
        Hotspot,
        /// <summary>A filled disc used for map destinations and status pips.</summary>
        Dot,
        /// <summary>A straight line from the box's top-left to its bottom-right corner.</summary>
        Line,
        /// <summary>A raised glass sub-card inside a surface. <see cref="HudDrawCommand.Value"/> is its alpha.</summary>
        Card,
        /// <summary>A fully rounded chip with a centred label: status, tier, phase.</summary>
        Pill,
        /// <summary>A circular progress gauge; <see cref="HudDrawCommand.Value"/> is 0..1.</summary>
        Ring,
        /// <summary>A tinted line icon; <see cref="HudDrawCommand.Text"/> is "category/name" (Art/UI/Icons).</summary>
        Icon,
        /// <summary>An approved art image, cropped to fill; <see cref="HudDrawCommand.Text"/> is its Art-relative path.</summary>
        Image,
        /// <summary>A left-to-right fade: <see cref="HudDrawCommand.Value"/> alpha at the left edge, clear at the right.</summary>
        Gradient
    }

    [Flags]
    public enum HudTextStyle
    {
        Regular = 0,
        Bold = 1,
        /// <summary>Wide-tracked small capitals — section captions and column headers.</summary>
        Caption = 2,
        Wrap = 4
    }

    public enum HudAlign
    {
        Left,
        Center,
        Right
    }

    /// <summary>Which of the three button treatments a <see cref="HudDrawKind.Button"/> uses.</summary>
    public enum HudButtonStyle
    {
        /// <summary>Filled avionics amber pill — one per card.</summary>
        Primary,
        /// <summary>Raised glass pill with an edge.</summary>
        Secondary,
        /// <summary>Red-outlined pill, visually below the primary.</summary>
        Destructive
    }

    /// <summary>
    /// One paint instruction in virtual HUD points. Deliberately a flat value type with no
    /// UnityEngine types in it: the runtime HUD rasterises a list of these through IMGUI and
    /// scripts/render-hud-mockups.py rasterises the very same list offline, so a mockup
    /// compared against the concept art is a picture of what the game actually draws.
    /// </summary>
    public readonly struct HudDrawCommand
    {
        public HudDrawCommand(HudDrawKind kind, HudBox box, string text, HudTone tone, float fontSize,
            HudTextStyle style, HudAlign align, float value, string colourHex, string actionId, bool enabled)
        {
            Kind = kind;
            Box = box;
            Text = text ?? string.Empty;
            Tone = tone;
            FontSize = fontSize;
            Style = style;
            Align = align;
            Value = value;
            ColourHex = colourHex;
            ActionId = actionId ?? string.Empty;
            Enabled = enabled;
        }

        public HudDrawKind Kind { get; }
        public HudBox Box { get; }
        public string Text { get; }
        public HudTone Tone { get; }
        public float FontSize { get; }
        public HudTextStyle Style { get; }
        public HudAlign Align { get; }

        /// <summary>Alpha for a fill, 0..1 progress for a bar, thickness for a line.</summary>
        public float Value { get; }

        /// <summary>An explicit colour (a livery), overriding <see cref="Tone"/>. Null when unused.</summary>
        public string ColourHex { get; }

        /// <summary>Non-empty on anything clickable; the workspace dispatches on this.</summary>
        public string ActionId { get; }

        public bool Enabled { get; }

        public HudButtonStyle ButtonStyle => (HudButtonStyle)(int)Value;
    }

    /// <summary>
    /// An ordered list of paint instructions for one HUD surface, reused between frames so
    /// drawing allocates nothing after the first pass.
    /// </summary>
    public sealed class HudDrawList
    {
        private readonly List<HudDrawCommand> _commands = new();

        public IReadOnlyList<HudDrawCommand> Commands => _commands;
        public int Count => _commands.Count;
        public HudDrawCommand this[int index] => _commands[index];

        public void Clear() => _commands.Clear();

        // Workspaces sit over a world the player is still operating. Graphite glass keeps the
        // miniature airport present behind it without sacrificing text contrast.
        public void Surface(HudBox box, float alpha = 0.90f) => Add(HudDrawKind.Surface, box, value: alpha);

        /// <summary>A raised glass sub-card — a section, a list well, an offer.</summary>
        public void Card(HudBox box, float alpha = 1f) => Add(HudDrawKind.Card, box, value: alpha);

        /// <summary>
        /// A rounded chip. Filled chips put dark text on the tone; unfilled ones tint the glass and
        /// colour the label, so several chips in a row never shout at once.
        /// </summary>
        public void Pill(HudBox box, string text, HudTone tone, bool filled = false, float fontSize = 10f,
            string colourHex = null) =>
            Add(HudDrawKind.Pill, box, text: text, tone: tone, fontSize: fontSize,
                style: HudTextStyle.Bold | HudTextStyle.Caption, align: HudAlign.Center,
                value: filled ? 1f : 0f, colourHex: colourHex);

        /// <summary>An approved UI line icon ("operation", "departure"), tinted by tone.</summary>
        public void Icon(HudBox box, string category, string name, HudTone tone = HudTone.Default, float alpha = 1f) =>
            Add(HudDrawKind.Icon, box, text: category + "/" + name, tone: tone, value: alpha);

        /// <summary>A circular progress gauge centred on (x, y).</summary>
        public void Ring(float centreX, float centreY, float diameter, float progress01, HudTone tone,
            float thickness = 6f) =>
            Add(HudDrawKind.Ring,
                new HudBox(centreX - diameter * 0.5f, centreY - diameter * 0.5f, diameter, diameter),
                tone: tone, fontSize: thickness, value: progress01 < 0f ? 0f : progress01 > 1f ? 1f : progress01);

        public void Fill(HudBox box, HudTone tone, float alpha, string colourHex = null) =>
            Add(HudDrawKind.Fill, box, tone: tone, value: alpha, colourHex: colourHex);

        /// <summary>A square-edged rule or band (never rounded, unlike <see cref="Fill"/>).</summary>
        public void Hairline(HudBox box, HudTone tone = HudTone.Muted, float alpha = 0.35f, string colourHex = null) =>
            Add(HudDrawKind.Hairline, box, tone: tone, value: alpha, colourHex: colourHex);

        /// <summary>A horizontal fade from <paramref name="alpha"/> at the left edge to clear at the right.</summary>
        public void Gradient(HudBox box, string colourHex, float alpha) =>
            Add(HudDrawKind.Gradient, box, value: alpha, colourHex: colourHex);

        /// <summary>An approved art image (Art-relative path), scaled to cover the box.</summary>
        public void Image(HudBox box, string artPath, float alpha = 1f) =>
            Add(HudDrawKind.Image, box, text: artPath, value: alpha);

        public void Outline(HudBox box, HudTone tone, float alpha = 1f) =>
            Add(HudDrawKind.Outline, box, tone: tone, value: alpha);

        /// <summary>
        /// <paramref name="alpha"/> is how loud the text is: other operators stay visible on
        /// the board but subordinate to your own airline (<see cref="Ownership"/>).
        /// </summary>
        public void Text(HudBox box, string text, float fontSize, HudTone tone = HudTone.Default,
            HudTextStyle style = HudTextStyle.Regular, HudAlign align = HudAlign.Left,
            string colourHex = null, float alpha = 1f)
        {
            if (string.IsNullOrEmpty(text) || box.IsEmpty)
                return;
            Add(HudDrawKind.Text, box, text: text, tone: tone, fontSize: fontSize, style: style,
                align: align, value: alpha, colourHex: colourHex);
        }

        /// <summary>A wide-tracked small-capital section caption — "NEEDS ATTENTION", "STATUS".</summary>
        public void Caption(HudBox box, string text, HudTone tone = HudTone.Muted,
            HudAlign align = HudAlign.Left, float fontSize = 11f) =>
            Text(box, text, fontSize, tone, HudTextStyle.Bold | HudTextStyle.Caption, align);

        public void Bar(HudBox box, float progress01, HudTone tone) =>
            Add(HudDrawKind.Bar, box, tone: tone, value: progress01);

        public void Button(HudBox box, string label, string actionId, HudButtonStyle style, bool enabled = true) =>
            Add(HudDrawKind.Button, box, text: label, fontSize: 13f, style: HudTextStyle.Bold,
                align: HudAlign.Center, value: (int)style, actionId: actionId, enabled: enabled);

        public void Hotspot(HudBox box, string actionId) =>
            Add(HudDrawKind.Hotspot, box, actionId: actionId);

        public void Dot(float centreX, float centreY, float diameter, HudTone tone, string colourHex = null) =>
            Add(HudDrawKind.Dot,
                new HudBox(centreX - diameter * 0.5f, centreY - diameter * 0.5f, diameter, diameter),
                tone: tone, colourHex: colourHex);

        public void Line(float x0, float y0, float x1, float y1, HudTone tone, float thickness = 1f,
            string colourHex = null) =>
            Add(HudDrawKind.Line, HudBox.Segment(x0, y0, x1, y1), tone: tone, value: thickness,
                colourHex: colourHex);

        private void Add(HudDrawKind kind, HudBox box, string text = null, HudTone tone = HudTone.Default,
            float fontSize = 0f, HudTextStyle style = HudTextStyle.Regular, HudAlign align = HudAlign.Left,
            float value = 0f, string colourHex = null, string actionId = null, bool enabled = true) =>
            _commands.Add(new HudDrawCommand(kind, box, text, tone, fontSize, style, align, value,
                colourHex, actionId, enabled));
    }

    /// <summary>Action ids shared between the workspace painters and the HUD that dispatches them.</summary>
    public static class HudAction
    {
        public const string SelectPrefix = "select:";
        public const string BuyPrefix = "buy:";
        public const string AcceptPrefix = "accept:";
        public const string DestinationPrefix = "destination:";
        public const string LiveryPrefix = "livery:";

        public const string Close = "close";
        public const string TabDepartures = "tab:departures";
        public const string TabArrivals = "tab:arrivals";
        public const string ToggleMovements = "operations:all-movements";
        public const string ToggleOtherOperators = "fleet:other-operators";
        public const string FilterAvailable = "filter:available";
        public const string FilterLocked = "filter:locked";
        public const string Primary = "primary";
        public const string Cancel = "cancel";
        public const string Track = "track";
        public const string PlanFlight = "plan";
        public const string RepeatFlight = "plan:repeat";
        public const string ResetMap = "reset-map";
        public const string NextAircraft = "aircraft:next";
        public const string PreviousAircraft = "aircraft:previous";
        public const string NextDeparture = "departure:next";
        public const string PreviousDeparture = "departure:previous";
        public const string ClearDestination = "destination:clear";
        public const string ViewEligibleAircraft = "contract:eligible";
        public const string ViewContracts = "contract:view";
        public const string CancelContract = "contract:cancel";
        public const string StartCheck = "check";
        public const string UpgradeBase = "base:upgrade";
        public const string CareerRoadmap = "career:roadmap";
        public const string ContractMarket = "career:offers";
        public const string PinGoalPrefix = "career:pin:";
        public const string StandPrefix = "stand:";

        public static string PinGoal(string id) => PinGoalPrefix + id;
        public static string Select(string registration) => SelectPrefix + registration;
        public static string Buy(string typeId) => BuyPrefix + typeId;
        public static string Accept(string contractId) => AcceptPrefix + contractId;
        public static string Destination(string code) => DestinationPrefix + code;
        public static string Livery(string hex) => LiveryPrefix + hex;
        public static string Stand(string standId) => StandPrefix + standId;

        /// <summary>The payload of a prefixed action id, or empty when the prefix does not match.</summary>
        public static string Payload(string actionId, string prefix) =>
            !string.IsNullOrEmpty(actionId) && actionId.StartsWith(prefix, StringComparison.Ordinal)
                ? actionId.Substring(prefix.Length)
                : string.Empty;
    }
}
