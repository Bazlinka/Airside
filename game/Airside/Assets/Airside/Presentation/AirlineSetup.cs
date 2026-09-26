using System;
using System.Collections.Generic;
using System.Globalization;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>The four cards of first-time airline setup (ADR 0123).</summary>
    public enum SetupStep
    {
        Identity,
        Livery,
        Difficulty,
        Briefing
    }

    /// <summary>
    /// Everything the player chooses when founding an airline: name, flight code, livery,
    /// difficulty and whether the first flight is coached. Pure state with its own validation, so
    /// the runtime only forwards clicks and text into it.
    /// </summary>
    public sealed class AirlineSetupModel
    {
        public const int NameLimit = 24;
        public const int HueSteps = 36;
        public const int ShadeSteps = 8;

        /// <summary>Curated livery colours; the hue and shade strips make any other.</summary>
        public static readonly (string Label, string Hex)[] Palette =
        {
            ("Crimson", "#C8102E"), ("Navy", "#1F3A93"), ("Forest", "#2E7D32"), ("Sunset", "#E8772E"),
            ("Violet", "#6A3FA0"), ("Gold", "#D4A017"), ("Teal", "#0F8B8D"), ("Sky", "#3A8DDE"),
            ("Magenta", "#B5267A"), ("Charcoal", "#33393F"), ("Ochre", "#B8742A"), ("Eucalypt", "#4F7F68")
        };

        public SetupStep Step = SetupStep.Identity;
        public string Name = "Southern Cross Regional";
        public string Code = string.Empty;
        public bool CodeEdited;

        /// <summary>A palette index, or -1 when the hue/shade strips chose a custom colour.</summary>
        public int PaletteIndex = 1;
        public int Hue = 22;
        public int Shade = 5;
        public CareerDifficulty Difficulty = CareerDifficulty.Standard;
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

        public DifficultyProfile DifficultyProfile => Simulation.Difficulty.For(Difficulty);

        public string StepError => Step switch
        {
            SetupStep.Identity when !NameValid => $"Give your airline a name ({NameLimit} characters at most).",
            SetupStep.Identity when !CodeValid => IsTakenCode(EffectiveCode)
                ? $"{EffectiveCode} belongs to a real airline at Adelaide — choose another code."
                : "The flight code is two or three letters.",
            _ => string.Empty
        };

        public bool CanAdvance => string.IsNullOrEmpty(StepError);

        /// <summary>Codes the AI operators already fly under, so flight numbers never collide.</summary>
        public static bool IsTakenCode(string code) => code switch
        {
            "REX" or "QLK" or "VOZ" or "QFA" or "JST" or "ANZ" or "SIA" or "CPA" or "MAS" or "UAE" or "QTR"
                or "FJI" or "RFDS" => true,
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

            if (TryIndex(action, AirlineSetupPainter.StepPrefix, 4, out var step))
            {
                // Jumping ahead is only allowed past valid cards.
                if (step <= (int)Step || CanAdvance)
                    Step = (SetupStep)Math.Min(step, CanAdvance ? 3 : (int)Step);
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
            var difficulty = HudAction.Payload(action, AirlineSetupPainter.DifficultyPrefix);
            if (difficulty.Length > 0 && Enum.TryParse(difficulty, out CareerDifficulty parsed))
            {
                Difficulty = parsed;
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
    /// Back / Next (or Start) footer, and a live preview of the airline's livery, flight code and
    /// difficulty on the aircraft it will start with.
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
        public const string DifficultyPrefix = "setup:difficulty:";

        private static readonly string[] StepNames = { "IDENTITY", "LIVERY", "DIFFICULTY", "BRIEFING" };

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
                case SetupStep.Difficulty:
                    PaintDifficulty(into, layout.Body, model);
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
            var width = (card.Width - 48f - 3f * 6f) / 4f;
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
            into.Text(example.WithHeight(16f).Offset(0f, 21f), model.CodeEdited ? "Your own code" : "Suggested from the name — type to change it",
                10f, HudTone.Muted);
            into.Text(new HudBox(body.X, layout.CodeField.Bottom + 22f, body.Width, 60f),
                "Your airline is based at Adelaide Airport with one Saab 340B on the regional bays. The name is painted on "
                + "every aircraft you fly; you can rename it later from the Career page.",
                11f, HudTone.Muted, HudTextStyle.Wrap);
        }

        private static void PaintLivery(HudDrawList into, HudBox body, AirlineSetupModel model)
        {
            into.Caption(body.WithHeight(12f), "LIVERY COLOUR", HudTone.Muted, HudAlign.Left, 10f);
            var y = body.Y + 22f;
            const int columns = 6;
            var cell = Math.Min(46f, (body.Width - (columns - 1) * 12f) / columns);
            for (var i = 0; i < AirlineSetupModel.Palette.Length; i++)
            {
                var cx = body.X + (i % columns) * (cell + 12f) + cell * 0.5f;
                var cy = y + (i / columns) * (cell + 12f) + cell * 0.5f;
                if (i == model.PaletteIndex)
                    into.Ring(cx, cy, cell + 8f, 1f, HudTone.Caution, 3f);
                into.Dot(cx, cy, cell - 4f, HudTone.Default, AirlineSetupModel.Palette[i].Hex);
                into.Hotspot(new HudBox(cx - cell * 0.5f, cy - cell * 0.5f, cell, cell), PalettePrefix + i);
            }
            y += 2f * (cell + 12f) + 8f;

            into.Caption(new HudBox(body.X, y, body.Width, 12f), "OR MIX YOUR OWN", HudTone.Muted, HudAlign.Left, 10f);
            y += 20f;
            var hueWidth = body.Width / AirlineSetupModel.HueSteps;
            for (var i = 0; i < AirlineSetupModel.HueSteps; i++)
            {
                var segment = new HudBox(body.X + i * hueWidth, y, hueWidth + 0.5f, 22f);
                into.Hairline(segment, HudTone.Default, 1f,
                    AirlineSetupModel.FromHueShade(i / (float)AirlineSetupModel.HueSteps, 0.8f));
                into.Hotspot(segment, HuePrefix + i);
            }
            if (model.PaletteIndex < 0)
                into.Outline(new HudBox(body.X + model.Hue * hueWidth - 2f, y - 3f, hueWidth + 4f, 28f), HudTone.Default, 1f);
            y += 32f;
            var shadeWidth = body.Width / AirlineSetupModel.ShadeSteps;
            for (var i = 0; i < AirlineSetupModel.ShadeSteps; i++)
            {
                var segment = new HudBox(body.X + i * shadeWidth + 2f, y, shadeWidth - 4f, 22f);
                into.Fill(segment, HudTone.Default, 1f,
                    AirlineSetupModel.FromHueShade(model.Hue / (float)AirlineSetupModel.HueSteps,
                        i / (float)(AirlineSetupModel.ShadeSteps - 1)));
                if (model.PaletteIndex < 0 && i == model.Shade)
                    into.Outline(segment.Inset(-3f), HudTone.Default, 1f);
                into.Hotspot(segment, ShadePrefix + i);
            }
            y += 34f;
            into.Text(new HudBox(body.X, y, body.Width, 16f), $"{model.LiveryLabel}  ·  {model.LiveryHex}", 12f,
                HudTone.Default, HudTextStyle.Bold);
        }

        private static void PaintDifficulty(HudDrawList into, HudBox body, AirlineSetupModel model)
        {
            into.Caption(body.WithHeight(12f), "HOW FORGIVING IS THE BUSINESS?", HudTone.Muted, HudAlign.Left, 10f);
            var y = body.Y + 20f;
            var height = Math.Min(92f, (body.Height - 20f - 2f * 8f) / 3f);
            foreach (var profile in Difficulty.All)
            {
                var box = new HudBox(body.X, y, body.Width, height);
                var chosen = profile.Difficulty == model.Difficulty;
                into.Card(box, chosen ? 1f : 0.7f);
                if (chosen)
                    into.Outline(box, HudTone.Caution, 0.95f);
                into.Dot(box.X + 18f, box.Y + 20f, 12f, chosen ? HudTone.Caution : HudTone.Muted);
                into.Text(new HudBox(box.X + 34f, box.Y + 10f, box.Width - 48f, 20f), profile.Title, 15f,
                    HudTone.Default, HudTextStyle.Bold);
                into.Text(new HudBox(box.X + 34f, box.Y + 30f, box.Width - 48f, 16f), profile.Summary, 11f, HudTone.Muted);
                var effects = profile.Effects;
                into.Text(new HudBox(box.X + 34f, box.Y + 50f, box.Width - 48f, 32f),
                    string.Join("  ·  ", effects), 10f, chosen ? HudTone.Caution : HudTone.Muted, HudTextStyle.Wrap);
                into.Hotspot(box, DifficultyPrefix + profile.Difficulty);
                y += height + 8f;
            }
        }

        private static void PaintBriefing(HudDrawList into, HudBox body, AirlineSetupModel model, bool replacesSave)
        {
            into.Caption(body.WithHeight(12f), "HOW AIRSIDE WORKS", HudTone.Muted, HudAlign.Left, 10f);
            var y = body.Y + 20f;
            var points = new[]
            {
                ("1", "Plan a flight", "Select your Saab, open Map (Tab) and pick a destination and time."),
                ("2", "Every flight pays", "Revenue minus dispatch cost. Contracts add a bonus on top."),
                ("3", "Stay on time", "Punctual pushbacks build reliability; late ones and overdue checks cost it."),
                ("4", "Grow", "Finish each tier's goals on the Career ring to unlock bigger aircraft and routes.")
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
            into.Pill(new HudBox(preview.Right - 20f - 110f, stage.Bottom + 42f, 110f, 22f),
                model.DifficultyProfile.Title.ToUpperInvariant(), HudTone.Accent, filled: true, fontSize: 9f);
            into.Text(new HudBox(preview.X + 20f, stage.Bottom + 74f, preview.Width - 40f, 16f),
                $"One Saab 340B  ·  ${model.DifficultyProfile.StartingFunds:N0} float  ·  Adelaide", 11f, HudTone.Muted);
        }
    }
}
