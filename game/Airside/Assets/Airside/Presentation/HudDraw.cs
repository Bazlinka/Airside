using System;
using System.Collections.Generic;

namespace Airside.Presentation
{
    /// <summary>What one entry of a <see cref="HudDrawList"/> paints.</summary>
    public enum HudDrawKind
    {
        /// <summary>Opaque workspace surface with a hairline frame.</summary>
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
        Line
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
        /// <summary>Filled Coastal Blue — one per card.</summary>
        Primary,
        /// <summary>Outlined on the panel fill.</summary>
        Secondary,
        /// <summary>Signal Red outline, visually below the primary.</summary>
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

        public void Surface(HudBox box) => Add(HudDrawKind.Surface, box, value: 0.96f);

        public void Fill(HudBox box, HudTone tone, float alpha, string colourHex = null) =>
            Add(HudDrawKind.Fill, box, tone: tone, value: alpha, colourHex: colourHex);

        public void Hairline(HudBox box, HudTone tone = HudTone.Muted, float alpha = 0.35f) =>
            Add(HudDrawKind.Hairline, box, tone: tone, value: alpha);

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
        public const string FilterAvailable = "filter:available";
        public const string FilterLocked = "filter:locked";
        public const string Primary = "primary";
        public const string Cancel = "cancel";
        public const string Track = "track";
        public const string PlanFlight = "plan";
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

        public static string Select(string registration) => SelectPrefix + registration;
        public static string Buy(string typeId) => BuyPrefix + typeId;
        public static string Accept(string contractId) => AcceptPrefix + contractId;
        public static string Destination(string code) => DestinationPrefix + code;
        public static string Livery(string hex) => LiveryPrefix + hex;

        /// <summary>The payload of a prefixed action id, or empty when the prefix does not match.</summary>
        public static string Payload(string actionId, string prefix) =>
            !string.IsNullOrEmpty(actionId) && actionId.StartsWith(prefix, StringComparison.Ordinal)
                ? actionId.Substring(prefix.Length)
                : string.Empty;
    }
}
