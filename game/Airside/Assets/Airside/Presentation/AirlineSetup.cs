using System;
using System.Collections.Generic;
using System.Globalization;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>The three cards of first-time airline setup (ADR 0123; difficulty removed by ADR 0127).</summary>
    public enum SetupStep
    {
        Identity,
        Livery,
        Briefing
    }

    /// <summary>
    /// Everything the player chooses when founding an airline: name, flight code, livery and
    /// whether the first flight is coached. There is one career balance (ADR 0127). Pure state with its own validation, so
    /// the runtime only forwards clicks and text into it.
    /// </summary>
    public sealed class AirlineSetupModel
    {
        public const int NameLimit = 24;
        public const int StepCount = 3;
        public const int HueSteps = 36;
        public const int ShadeSteps = 8;

        /// <summary>Five fleet paint presets, shared with the in-game Airline page.</summary>
        public static readonly (string Label, string Hex)[] Palette =
        {
            ("Coastline", "#0F8B8D"), ("Southern Cross", "#1F3A93"),
            ("Outback", "#B8742A"), ("Gulf", "#3A8DDE"), ("Redgum", "#70415C")
        };

        public static readonly string[] PaletteAccents =
        {
            "#C8B286", "#A7C9D9", "#293F4F", "#17242A", "#B7C9AD"
        };

        public SetupStep Step = SetupStep.Identity;
        public string Name = "Southern Cross Regional";
        public string Code = string.Empty;
        public bool CodeEdited;

        /// <summary>A palette index, or -1 when the hue/shade strips chose a custom colour.</summary>
        public int PaletteIndex = 1;
        public int Hue = 22;
        public int Shade = 5;
        public bool Coaching = true;

        public string TrimmedName => (Name ?? string.Empty).Trim();
        public bool NameValid => TrimmedName.Length > 0 && TrimmedName.Length <= NameLimit;

        /// <summary>The code in use: the player's own once typed, otherwise one suggested from the name.</summary>
        public string EffectiveCode => CodeEdited && !string.IsNullOrWhiteSpace(Code)
            ? Code.Trim().ToUpperInvariant()
            : FlightNumber.SuggestCode(TrimmedName);

        public bool CodeValid => Airline.IsValidCode(EffectiveCode) && !IsTakenCode(EffectiveCode);

        public bool CanStart => NameValid && CodeValid;

        public string LiveryHex => PaletteIndex >= 0 && PaletteIndex < Palette.Length
            ? Palette[PaletteIndex].Hex
            : FromHueShade(Hue / (float)HueSteps, Shade / (float)(ShadeSteps - 1));

        public string LiveryLabel => PaletteIndex >= 0 && PaletteIndex < Palette.Length ? Palette[PaletteIndex].Label : "Custom";

        public string StepError => Step switch
        {
            SetupStep.Identity when !NameValid => $"Give your airline a name ({NameLimit} characters at most).",
            SetupStep.Identity when !CodeValid => IsTakenCode(EffectiveCode)
                ? $"{EffectiveCode} belongs to a real airline at Adelaide. Choose another code."
                : "The flight code is two or three letters.",
            _ => string.Empty
        };

        public bool CanAdvance => string.IsNullOrEmpty(StepError);

        /// <summary>Codes the AI operators already fly under, so flight numbers never collide.</summary>
        public static bool IsTakenCode(string code) => code switch
        {
            "REX" or "QLK" or "VOZ" or "QFA" or "JST" or "ANZ" or "SIA" or "CPA" or "MAS" or "UAE" or "QTR"
                or "FJI" or "RFDS"
                or "QF" or "VA" or "JQ" or "NZ" or "SQ" or "CX" or "MH" or "EK" or "QR" or "FJ" or "ZL" or "FD" => true,
            _ => false
        };

        /// <summary>
        /// A livery colour from the hue strip (0..1 around the wheel) and the shade strip (0 deep … 1 bright),
        /// kept saturated and dark enough to read on a white fuselage.
        /// </summary>
        public static string FromHueShade(float hue01, float shade01)
        {
            var h = (hue01 % 1f + 1f) % 1f * 6f;
            var shade = Math.Clamp(shade01, 0f, 1f);
            var s = 0.78f - 0.18f * shade;
            var v = 0.38f + 0.52f * shade;
            var i = (int)Math.Floor(h);
            var f = h - i;
            var p = v * (1f - s);
            var q = v * (1f - s * f);
            var t = v * (1f - s * (1f - f));
            var (r, g, b) = (i % 6) switch
            {
                0 => (v, t, p),
                1 => (q, v, p),
                2 => (p, v, t),
                3 => (p, q, v),
                4 => (t, p, v),
                _ => (v, p, q)
            };
            return "#" + ToByte(r).ToString("X2", CultureInfo.InvariantCulture)
                       + ToByte(g).ToString("X2", CultureInfo.InvariantCulture)
                       + ToByte(b).ToString("X2", CultureInfo.InvariantCulture);
        }

        private static int ToByte(float value) => Math.Clamp((int)Math.Round(value * 255f), 0, 255);

        /// <summary>
        /// Applies a setup action id. Returns true when it was one of ours. Start and the manual are
        /// the runtime's to act on and are reported through <paramref name="outcome"/>.
        /// </summary>
        public bool Apply(string action, out SetupOutcome outcome)
        {
            outcome = SetupOutcome.None;
            if (string.IsNullOrEmpty(action) || !action.StartsWith(AirlineSetupPainter.Prefix, StringComparison.Ordinal))
                return false;
            switch (action)
            {
                case AirlineSetupPainter.Next:
                    if (CanAdvance && Step < SetupStep.Briefing)
                        Step++;
                    return true;
                case AirlineSetupPainter.Back:
                    if (Step == SetupStep.Identity)
                        outcome = SetupOutcome.Leave;
                    else
                        Step--;
                    return true;
                case AirlineSetupPainter.Start:
                    outcome = CanStart ? SetupOutcome.Start : SetupOutcome.None;
                    return true;
                case AirlineSetupPainter.Manual:
                    outcome = SetupOutcome.OpenManual;
                    return true;
                case AirlineSetupPainter.ToggleCoaching:
                    Coaching = !Coaching;
                    return true;
            }

            if (TryIndex(action, AirlineSetupPainter.StepPrefix, StepCount, out var step))
            {
                // Jumping ahead is only allowed past valid cards.
                if (step <= (int)Step || CanAdvance)
                    Step = (SetupStep)Math.Min(step, CanAdvance ? StepCount - 1 : (int)Step);
                return true;
            }
            if (TryIndex(action, AirlineSetupPainter.PalettePrefix, Palette.Length, out var palette))
            {
                PaletteIndex = palette;
                return true;
            }
            if (TryIndex(action, AirlineSetupPainter.HuePrefix, HueSteps, out var hue))
            {
                Hue = hue;
                PaletteIndex = -1;
                return true;
            }
            if (TryIndex(action, AirlineSetupPainter.ShadePrefix, ShadeSteps, out var shade))
            {
                Shade = shade;
                PaletteIndex = -1;
                return true;
            }
            return true;
        }

        private static bool TryIndex(string action, string prefix, int count, out int index)
        {
            index = -1;
            var payload = HudAction.Payload(action, prefix);
            return payload.Length > 0 && int.TryParse(payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out index)
                   && index >= 0 && index < count;
        }
    }

    public enum SetupOutcome
    {
        None,
        Start,
        Leave,
        OpenManual
    }

    /// <summary>Placement of the setup card and its live preview beside the title art.</summary>
    public readonly struct AirlineSetupLayout
    {
        public const float CardHeight = 500f;
        public const float PreviewWidth = 380f;

        public AirlineSetupLayout(HudBox card, HudBox preview)
        {
            Card = card;
            Preview = preview;
        }

        public HudBox Card { get; }

        /// <summary>The live preview card; empty when the window is too narrow for it.</summary>
        public HudBox Preview { get; }

        public HudBox Body => new(Card.X + 24f, Card.Y + 96f, Card.Width - 48f, Card.Height - 96f - 76f);
        public HudBox NameField => new(Body.X, Body.Y + 26f, Body.Width, 38f);
        public HudBox CodeField => new(Body.X, Body.Y + 108f, 96f, 38f);

        public static AirlineSetupLayout Create(HudBox card, float viewportWidth)
        {
            var preview = new HudBox(card.Right + 18f, card.Y, PreviewWidth, Math.Min(282f, card.Height));
            if (preview.Right > viewportWidth - 24f)
                preview = new HudBox(preview.X, preview.Y, 0f, 0f);
            return new AirlineSetupLayout(card, preview);
        }
    }

    /// <summary>
    /// Paints the setup card (ADR 0123): a four-step header, the current step's controls, a
    /// Back / Next (or Start) footer, and a live preview of the airline's livery and flight code on
    /// the aircraft it will start with.
    /// </summary>
    public static class AirlineSetupPainter
    {
        public const string Prefix = "setup:";
        public const string Next = "setup:next";
        public const string Back = "setup:back";
        public const string Start = "setup:start";
        public const string Manual = "setup:manual";
        public const string ToggleCoaching = "setup:coaching";
        public const string StepPrefix = "setup:step:";
        public const string PalettePrefix = "setup:palette:";
        public const string HuePrefix = "setup:hue:";
        public const string ShadePrefix = "setup:shade:";
        private static readonly string[] StepNames = { "IDENTITY", "LIVERY", "BRIEFING" };

        public static void Paint(HudDrawList into, AirlineSetupLayout layout, AirlineSetupModel model, bool replacesSave)
        {
            if (into == null || model == null || layout.Card.IsEmpty)
                return;
            var card = layout.Card;
            into.Surface(card, 0.92f);
            PaintSteps(into, card, model);

            switch (model.Step)
            {
                case SetupStep.Identity:
                    PaintIdentity(into, layout, model);
                    break;
                case SetupStep.Livery:
                    PaintLivery(into, layout.Body, model);
                    break;
                default:
                    PaintBriefing(into, layout.Body, model, replacesSave);
                    break;
            }

            var error = model.StepError;
            var footerY = card.Bottom - 22f - 42f;
            if (!string.IsNullOrEmpty(error))
                into.Text(new HudBox(card.X + 24f, footerY - 24f, card.Width - 48f, 18f), error, 11f, HudTone.Negative);
            into.Button(new HudBox(card.X + 24f, footerY, 110f, 42f), model.Step == SetupStep.Identity ? "CANCEL" : "BACK",
                Back, HudButtonStyle.Secondary);
            if (model.Step == SetupStep.Briefing)
                into.Button(new HudBox(card.X + 144f, footerY, card.Width - 168f, 42f), "START AIRLINE", Start,
                    HudButtonStyle.Primary, model.CanStart);
            else
                into.Button(new HudBox(card.X + 144f, footerY, card.Width - 168f, 42f), "NEXT", Next,
                    HudButtonStyle.Primary, model.CanAdvance);

            PaintPreview(into, layout.Preview, model);
        }

        private static void PaintSteps(HudDrawList into, HudBox card, AirlineSetupModel model)
        {
            into.Caption(new HudBox(card.X + 24f, card.Y + 20f, card.Width - 48f, 12f), "FOUND YOUR AIRLINE  ·  ADELAIDE",
                HudTone.Accent, HudAlign.Left, 10f);
            var width = (card.Width - 48f - (StepNames.Length - 1) * 6f) / StepNames.Length;
            for (var i = 0; i < StepNames.Length; i++)
            {
                var box = new HudBox(card.X + 24f + i * (width + 6f), card.Y + 42f, width, 34f);
                var current = (int)model.Step == i;
                var done = (int)model.Step > i;
                into.Fill(new HudBox(box.X, box.Y, box.Width, 3f), current ? HudTone.Caution : done ? HudTone.Accent : HudTone.Muted,
                    current || done ? 1f : 0.3f);
                into.Text(new HudBox(box.X, box.Y + 9f, box.Width, 12f), $"{i + 1}  {StepNames[i]}", 8.5f,
                    current ? HudTone.Default : done ? HudTone.Accent : HudTone.Muted, HudTextStyle.Bold | HudTextStyle.Caption);
                into.Hotspot(box, StepPrefix + i);
            }
        }

        private static void PaintIdentity(HudDrawList into, AirlineSetupLayout layout, AirlineSetupModel model)
        {
            var body = layout.Body;
            into.Caption(body.WithHeight(12f), "AIRLINE NAME", HudTone.Muted, HudAlign.Left, 10f);
            into.Card(layout.NameField);
            if (!model.NameValid)
                into.Outline(layout.NameField, HudTone.Negative, 0.8f);
            into.Caption(new HudBox(body.X, layout.CodeField.Y - 20f, body.Width, 12f), "FLIGHT CODE", HudTone.Muted,
                HudAlign.Left, 10f);
            into.Card(layout.CodeField);
            if (!model.CodeValid)
                into.Outline(layout.CodeField, HudTone.Negative, 0.8f);
            var example = new HudBox(layout.CodeField.Right + 14f, layout.CodeField.Y, body.Right - layout.CodeField.Right - 14f, 38f);
            into.Text(example.WithHeight(18f).Offset(0f, 2f), $"Flights read {model.EffectiveCode} 101, {model.EffectiveCode} 204 …",
                12f, HudTone.Default, HudTextStyle.Bold);
            into.Text(example.WithHeight(16f).Offset(0f, 21f), model.CodeEdited ? "Your own code" : "Made from the name. Type to change it",
                10f, HudTone.Muted);
            into.Text(new HudBox(body.X, layout.CodeField.Bottom + 22f, body.Width, 60f),
                "You start at Adelaide Airport with one Saab 340B on the regional bays, and $"
                + FlightEconomics.StartingFunds.ToString("N0") + " in cash. A second Saab is $"
                + AircraftAcquisition.Saab340.Price.ToString("N0") + " in Fleet. The name goes on "
                + "every aircraft you fly. You can change it later on the Career page.",
                11f, HudTone.Muted, HudTextStyle.Wrap);
        }

        private static void PaintLivery(HudDrawList into, HudBox body, AirlineSetupModel model)
        {
            into.Caption(body.WithHeight(12f), "CHOOSE YOUR FLEET LIVERY", HudTone.Muted, HudAlign.Left, 10f);
            var y = body.Y + 24f;
            for (var i = 0; i < AirlineSetupModel.Palette.Length; i++)
            {
                var (label, hex) = AirlineSetupModel.Palette[i];
                var row = new HudBox(body.X, y + i * 42f, body.Width, 36f);
                into.Fill(row, HudTone.Default, 0.65f, AirsidePalette.GlassHex);
                into.Fill(new HudBox(row.X + 8f, row.Y + 7f, 30f, 22f), HudTone.Default, 1f, hex);
                into.Fill(new HudBox(row.X + 8f, row.Y + 23f, 30f, 4f), HudTone.Default, 1f,
                    AirlineSetupModel.PaletteAccents[i]);
                into.Text(new HudBox(row.X + 50f, row.Y + 9f, row.Width - 60f, 18f), label, 13f,
                    HudTone.Default, HudTextStyle.Bold);
                if (i == model.PaletteIndex)
                    into.Outline(row, HudTone.Caution, 1f);
                into.Hotspot(row, PalettePrefix + i);
            }
            into.Text(new HudBox(body.X, y + 216f, body.Width, 32f),
                "Each aircraft wears its own fitted design in your fleet colours.", 12f, HudTone.Muted);
        }

        private static void PaintBriefing(HudDrawList into, HudBox body, AirlineSetupModel model, bool replacesSave)
        {
            into.Caption(body.WithHeight(12f), "HOW AIRSIDE WORKS", HudTone.Muted, HudAlign.Left, 10f);
            var y = body.Y + 20f;
            var points = new[]
            {
                ("1", "Plan a flight", "Select your Saab, open Map (Tab) and pick a destination and time."),
                ("2", "Every flight pays", "You earn the fares minus the cost of the flight. Contracts add a bonus."),
                ("3", "Stay on time", "Leaving on time builds reliability. Late flights and overdue checks cost it."),
                ("4", "Grow", "Finish each tier's goals to unlock bigger aircraft and longer routes.")
            };
            foreach (var (number, title, text) in points)
            {
                into.Dot(body.X + 11f, y + 11f, 22f, HudTone.Accent);
                into.Text(new HudBox(body.X, y + 4f, 22f, 16f), number, 11f, HudTone.Default, HudTextStyle.Bold,
                    HudAlign.Center, AirsidePalette.OnAccentHex);
                into.Text(new HudBox(body.X + 34f, y, body.Width - 34f, 18f), title, 13f, HudTone.Default, HudTextStyle.Bold);
                into.Text(new HudBox(body.X + 34f, y + 18f, body.Width - 34f, 30f), text, 11f, HudTone.Muted, HudTextStyle.Wrap);
                y += 54f;
            }
            var toggle = new HudBox(body.X, y + 4f, body.Width, 30f);
            into.Card(toggle, 0.8f);
            var knob = new HudBox(toggle.Right - 52f, toggle.Y + 6f, 40f, 18f);
            into.Fill(knob, model.Coaching ? HudTone.Accent : HudTone.Muted, model.Coaching ? 1f : 0.35f);
            into.Dot(model.Coaching ? knob.Right - 9f : knob.X + 9f, knob.Y + 9f, 14f, HudTone.Default);
            into.Text(new HudBox(toggle.X + 12f, toggle.Y + 7f, toggle.Width - 80f, 16f), "Coach my first flight step by step", 11f,
                HudTone.Default, HudTextStyle.Bold);
            into.Hotspot(toggle, ToggleCoaching);
            y += 40f;
            into.Button(new HudBox(body.X, y, body.Width, 30f), "READ THE FLIGHT MANUAL", Manual, HudButtonStyle.Secondary);
            if (replacesSave)
                into.Text(new HudBox(body.X, y + 34f, body.Width, 16f), "Starting a new airline replaces your saved one.",
                    10f, HudTone.Caution);
        }

        private static void PaintPreview(HudDrawList into, HudBox preview, AirlineSetupModel model)
        {
            if (preview.IsEmpty)
                return;
            into.Surface(preview, 0.86f);
            into.Caption(new HudBox(preview.X + 20f, preview.Y + 18f, preview.Width - 40f, 12f), "PREVIEW", HudTone.Accent,
                HudAlign.Left, 10f);
            // A livery-coloured horizon behind the starting Saab, as its tail and cheatline would read.
            var stage = new HudBox(preview.X + 16f, preview.Y + 40f, preview.Width - 32f, 150f);
            into.Fill(stage, HudTone.Default, 0.9f, AirsidePalette.GlassRaisedHex);
            into.Fill(new HudBox(stage.X, stage.Y + stage.Height * 0.58f, stage.Width, stage.Height * 0.42f),
                HudTone.Default, 0.85f, model.LiveryHex);
            into.Image(stage.Inset(10f, 4f, 10f, 4f), "UI/Aircraft/thb_air_sf34_v01.png");
            into.Fill(new HudBox(preview.X + 20f, stage.Bottom + 16f, 5f, 18f), HudTone.Default, 1f, model.LiveryHex);
            into.Text(new HudBox(preview.X + 32f, stage.Bottom + 14f, preview.Width - 52f, 22f),
                Airline.Player(model.TrimmedName.Length > 0 ? model.TrimmedName : "Your airline", model.LiveryHex).FuselageTitle, 16f,
                HudTone.Default, HudTextStyle.Bold | HudTextStyle.Caption);
            into.Pill(new HudBox(preview.X + 20f, stage.Bottom + 42f, 150f, 22f),
                $"{model.EffectiveCode} 101  ADL › KGC", HudTone.Route, fontSize: 9f);
            into.Text(new HudBox(preview.X + 20f, stage.Bottom + 74f, preview.Width - 40f, 16f),
                $"One Saab 340B  ·  ${FlightEconomics.StartingFunds:N0}  ·  Adelaide", 11f, HudTone.Muted);
        }
    }
}
