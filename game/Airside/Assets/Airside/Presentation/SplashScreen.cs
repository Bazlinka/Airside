using System;
using System.Collections.Generic;

namespace Airside.Presentation
{
    /// <summary>Which card the title screen shows.</summary>
    public enum SplashStep
    {
        Menu,
        NewAirline
    }

    /// <summary>What the title screen shows, worded by the runtime from the save file and live clock.</summary>
    public sealed class SplashModel
    {
        public SplashStep Step = SplashStep.Menu;
        public bool HasSave;
        public string SaveName = string.Empty;
        public string SaveLiveryHex;
        public string SaveTier = string.Empty;
        public string SaveSummary = string.Empty;
        public string SavedWhen = string.Empty;
        public string SaveError = string.Empty;
        public string ClockText = string.Empty;
        /// <summary>The first-time setup wizard's choices (ADR 0123).</summary>
        public readonly AirlineSetupModel Setup = new();
    }

    /// <summary>
    /// The opening title screen (ADR 0122): the approved dawn illustration full-bleed with a
    /// graphite fade on the left, the wordmark and live Adelaide clock, and one glass card —
    /// Continue / New airline / Options / Quit, or the two-field new-airline form.
    /// Pure layout and painting; the runtime draws the editable name field at <see cref="NameField"/>.
    /// </summary>
    public readonly struct SplashLayout
    {
        public const string SplashArt = "UI/Illustrations/ui_splash_airport_dawn_v01.png";
        public const string WordmarkArt = "Brand/airside_wordmark_light_v01.png";
        public const float CardWidth = 420f;

        private SplashLayout(HudBox viewport, HudBox title, HudBox card, HudBox footer)
        {
            Viewport = viewport;
            Title = title;
            Card = card;
            Footer = footer;
        }

        public HudBox Viewport { get; }
        public HudBox Title { get; }
        public HudBox Card { get; }
        public HudBox Footer { get; }

        /// <summary>The setup wizard's card and live preview (ADR 0123).</summary>
        public AirlineSetupLayout Setup => AirlineSetupLayout.Create(Card, Viewport.Width);

        public static SplashLayout Create(float width, float height, SplashStep step, bool hasSave)
        {
            var left = Math.Max(24f, Math.Min(96f, width * 0.06f));
            var cardWidth = Math.Min(CardWidth, width - left * 2f);
            var cardHeight = step == SplashStep.NewAirline ? AirlineSetupLayout.CardHeight : hasSave ? 324f : 304f;
            var titleHeight = step == SplashStep.NewAirline && height < 820f ? 0f : 176f;
            var total = titleHeight + 18f + cardHeight;
            var top = Math.Max(24f, (height - total) * 0.5f - 10f);
            var title = new HudBox(left, top, Math.Min(560f, width - left * 2f), titleHeight);
            var card = new HudBox(left, titleHeight > 0f ? title.Bottom + 18f : top, cardWidth, Math.Min(cardHeight, Math.Max(0f, height - title.Bottom - 60f)));
            var footer = new HudBox(left, height - 40f, Math.Max(0f, width - left * 2f - 440f), 18f);
            return new SplashLayout(new HudBox(0f, 0f, width, height), title, card, footer);
        }
    }

    public static class SplashPainter
    {
        public const string Continue = "splash:continue";
        public const string NewAirline = "splash:new";
        public const string Options = "splash:options";
        public const string Quit = "splash:quit";
        public const string HowToPlay = "splash:manual";

        /// <summary>
        /// Paints the whole title screen: art, fade, title block and the current card.
        /// <paramref name="kenBurns01"/> slowly pushes the illustration in (0 at launch).
        /// </summary>
        public static void Paint(HudDrawList into, SplashLayout layout, SplashModel model, float kenBurns01 = 0f)
        {
            if (into == null || model == null)
                return;
            into.Clear();
            var view = layout.Viewport;
            var push = 1f + 0.06f * Math.Clamp(kenBurns01, 0f, 1f);
            var artWidth = view.Width * push;
            var artHeight = view.Height * push;
            into.Image(new HudBox(view.X - (artWidth - view.Width) * 0.35f, view.Y - (artHeight - view.Height) * 0.5f,
                artWidth, artHeight), SplashLayout.SplashArt);

            // Graphite fade from the left so the title and card read over the bright dawn sky.
            var fadeWidth = Math.Min(view.Width, Math.Max(layout.Card.Right + 260f, view.Width * 0.58f));
            into.Gradient(new HudBox(view.X, view.Y, fadeWidth, view.Height), AirsidePalette.GlassHex, 0.88f);
            into.Hairline(new HudBox(view.X, view.Bottom - 90f, view.Width, 90f), HudTone.Default, 0.35f,
                AirsidePalette.GlassHex);

            if (layout.Title.Height > 0f)
                PaintTitle(into, layout.Title, model);
            if (model.Step == SplashStep.NewAirline)
                AirlineSetupPainter.Paint(into, layout.Setup, model.Setup, model.HasSave);
            else
                PaintMenu(into, layout.Card, model);
            into.Text(layout.Footer, model.Step == SplashStep.NewAirline
                    ? "Enter  next      Esc  back"
                    : model.HasSave ? "Enter  continue      Esc  menu" : "Enter  new airline      Esc  menu",
                11f, HudTone.Muted, HudTextStyle.Bold | HudTextStyle.Caption);
        }

        private static void PaintTitle(HudDrawList into, HudBox title, SplashModel model)
        {
            var wordmarkWidth = Math.Min(360f, title.Width);
            into.Image(new HudBox(title.X - 6f, title.Y, wordmarkWidth, wordmarkWidth * 0.25f), SplashLayout.WordmarkArt);
            var y = title.Y + wordmarkWidth * 0.25f + 10f;
            into.Text(new HudBox(title.X, y, title.Width, 30f), "Build an airline from one Saab at Adelaide.", 20f,
                HudTone.Default, HudTextStyle.Bold);
            y += 36f;
            var pill = new HudBox(title.X, y, 236f, 26f);
            into.Fill(pill, HudTone.Default, 0.72f, AirsidePalette.GlassHex);
            into.Dot(pill.X + 16f, pill.Y + 13f, 8f, HudTone.Positive);
            into.Caption(new HudBox(pill.X + 28f, pill.Y + 7f, pill.Width - 34f, 14f),
                "LIVE  ·  ADELAIDE  " + model.ClockText, HudTone.Default, HudAlign.Left, 10f);
        }

        private static void PaintMenu(HudDrawList into, HudBox card, SplashModel model)
        {
            if (card.IsEmpty)
                return;
            into.Surface(card, 0.9f);
            var x = card.X + 24f;
            var inner = card.Width - 48f;
            var y = card.Y + 22f;
            if (model.HasSave)
            {
                into.Caption(new HudBox(x, y, inner, 12f), "YOUR AIRLINE", HudTone.Accent, HudAlign.Left, 10f);
                y += 22f;
                into.Dot(x + 7f, y + 11f, 14f, HudTone.Default, model.SaveLiveryHex);
                into.Text(new HudBox(x + 22f, y, inner - 130f, 24f), model.SaveName, 20f, HudTone.Default, HudTextStyle.Bold);
                if (!string.IsNullOrEmpty(model.SaveTier))
                    into.Pill(new HudBox(card.Right - 24f - 104f, y + 2f, 104f, 20f), model.SaveTier.ToUpperInvariant(),
                        HudTone.Accent, filled: true, fontSize: 9f);
                y += 30f;
                into.Text(new HudBox(x, y, inner, 16f), model.SaveSummary, 12f, HudTone.Muted);
                y += 18f;
                into.Text(new HudBox(x, y, inner, 16f), model.SavedWhen, 11f, HudTone.Muted);
                y += 30f;
                into.Button(new HudBox(x, y, inner, 44f), "CONTINUE", Continue, HudButtonStyle.Primary);
                y += 54f;
                into.Button(new HudBox(x, y, inner, 36f), "NEW AIRLINE", NewAirline, HudButtonStyle.Secondary);
                y += 46f;
            }
            else
            {
                into.Caption(new HudBox(x, y, inner, 12f), "ADELAIDE  ·  PROVISIONAL OPERATOR", HudTone.Accent, HudAlign.Left, 10f);
                y += 22f;
                into.Text(new HudBox(x, y, inner, 26f), "Start small. Grow the airline.", 20f, HudTone.Default, HudTextStyle.Bold);
                y += 32f;
                var message = string.IsNullOrEmpty(model.SaveError)
                    ? "One Saab on the regional bays. Fly contracts, earn the next aircraft, and grow from Provisional to an established international airline."
                    : model.SaveError;
                into.Text(new HudBox(x, y, inner, 52f), message, 12f,
                    string.IsNullOrEmpty(model.SaveError) ? HudTone.Muted : HudTone.Negative, HudTextStyle.Wrap);
                y += 64f;
                into.Button(new HudBox(x, y, inner, 44f), "NEW AIRLINE", NewAirline, HudButtonStyle.Primary);
                y += 54f;
            }
            into.Button(new HudBox(x, y, inner, 32f), "HOW TO PLAY", HowToPlay, HudButtonStyle.Secondary);
            y += 42f;
            var half = (inner - 10f) * 0.5f;
            into.Button(new HudBox(x, y, half, 32f), "OPTIONS", Options, HudButtonStyle.Secondary);
            into.Button(new HudBox(x + half + 10f, y, half, 32f), "QUIT", Quit, HudButtonStyle.Secondary);
        }
    }
}
