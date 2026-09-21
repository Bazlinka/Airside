using System;
using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>One rotating market offer, with the real reason it cannot be taken yet.</summary>
    public readonly struct ContractOfferRow
    {
        public ContractOfferRow(RouteContractDefinition definition, string title, string terms,
            string lockReason, bool canAccept)
        {
            Definition = definition;
            Title = title ?? string.Empty;
            Terms = terms ?? string.Empty;
            LockReason = lockReason ?? string.Empty;
            CanAccept = canAccept;
        }

        public RouteContractDefinition Definition { get; }
        public string Title { get; }

        /// <summary>"4 rotations · ATR 42-600 · $2,800 total" — all from the definition.</summary>
        public string Terms { get; }

        /// <summary>The actual missing capability, or empty when nothing is missing.</summary>
        public string LockReason { get; }

        /// <summary>
        /// True only when <see cref="AirlineOperations.AcceptContract"/> would accept it. A
        /// completed contract is never offered at all, so it can never appear actionable.
        /// </summary>
        public bool CanAccept { get; }
    }

    /// <summary>
    /// The Contracts workspace as data (ADR 0057): the one active commitment shown apart
    /// from the rotating market, with real progress, payments, penalties and refresh time.
    /// </summary>
    public sealed class ContractsWorkspaceModel
    {
        private readonly List<ContractOfferRow> _offers = new();
        private readonly List<string> _activeTerms = new();

        public string Title => "CONTRACTS";

        /// <summary>"New offers in 4 h 12 min", from <see cref="ContractMarket.WindowEnd"/>.</summary>
        public string RefreshLine { get; private set; } = string.Empty;

        public bool HasActive { get; private set; }
        public string ActiveTitle { get; private set; } = string.Empty;
        public string ActiveRoute { get; private set; } = string.Empty;
        public string ActiveProgressText { get; private set; } = string.Empty;
        public float ActiveProgress01 { get; private set; }
        public string ActiveProgressPercent { get; private set; } = string.Empty;
        public IReadOnlyList<string> ActiveTerms => _activeTerms;

        /// <summary>The registrations that can actually progress the active contract.</summary>
        public string EligibleAircraftLine { get; private set; } = string.Empty;

        public bool HasEligibleAircraft { get; private set; }

        public IReadOnlyList<ContractOfferRow> Offers => _offers;

        /// <summary>Shown in place of the offer list when the market has nothing for this window.</summary>
        public string EmptyOffersLine { get; private set; } = string.Empty;

        public string FooterLine =>
            "Accepting a contract is a service commitment. Cancellation can reduce reliability.";

        public void Rebuild(AirlineOperations operations, SimulationTime now)
        {
            _offers.Clear();
            _activeTerms.Clear();
            HasActive = false;
            HasEligibleAircraft = false;
            ActiveTitle = string.Empty;
            ActiveRoute = string.Empty;
            ActiveProgressText = string.Empty;
            ActiveProgress01 = 0f;
            ActiveProgressPercent = string.Empty;
            EligibleAircraftLine = string.Empty;
            EmptyOffersLine = string.Empty;
            if (operations == null)
                return;

            var career = operations.CareerState;
            var windowEnd = ContractMarket.WindowEnd(now);
            var remaining = windowEnd.ElapsedSeconds - now.ElapsedSeconds;
            RefreshLine = $"New offers in {RouteMapWorkspaceModel.Duration(remaining)}";

            if (career.ActiveContract != null
                && career.TryFindDefinition(career.ActiveContract.DefinitionId, out var active))
                FillActive(operations, career, active);

            FillOffers(operations, career);
        }

        private void FillActive(AirlineOperations operations, AirlineCareerState career,
            RouteContractDefinition definition)
        {
            HasActive = true;
            ActiveTitle = OperationsSummary.ProveTitle(definition);
            ActiveRoute = $"{OperationsSummary.PlaceName(definition.OriginCode)} ↔ "
                          + OperationsSummary.PlaceName(definition.DestinationCode);

            var done = career.ActiveContract.CompletedRotations;
            var required = definition.RequiredRotations;
            ActiveProgressText = $"{done} of {required} rotations complete";
            ActiveProgress01 = required <= 0 ? 0f : Clamp01(done / (float)required);
            ActiveProgressPercent = $"{(int)(ActiveProgress01 * 100f)}%";

            _activeTerms.Add($"Eligible: {definition.EligibleType.Name}");
            _activeTerms.Add($"${definition.PaymentPerRotation:N0} per rotation"
                             + $"  ·  ${definition.CompletionReward:N0} completion bonus");
            _activeTerms.Add(definition.ReliabilityLossOnCancel > 0
                ? $"Cancellation: −{definition.ReliabilityLossOnCancel} reliability"
                : "Cancellation: no reliability penalty");
            if (definition.ReliabilityGainPerRotation > 0)
                _activeTerms.Add($"+{definition.ReliabilityGainPerRotation} reliability per rotation");


            EligibleAircraftLine = EligibleRegistrations(operations, definition.EligibleType, out var any);
            HasEligibleAircraft = any;
        }

        private void FillOffers(AirlineOperations operations, AirlineCareerState career)
        {
            var offers = operations.MarketOffers();
            foreach (var definition in offers)
            {
                // An already-fulfilled contract is not an offer at all: it is never drawn, so
                // it can never be clicked (AcceptContract would refuse it anyway).
                if (career.HasCompleted(definition.Id))
                    continue;

                var total = definition.PaymentPerRotation * definition.RequiredRotations
                            + definition.CompletionReward;
                var title = $"{OperationsSummary.PlaceName(definition.DestinationCode)} "
                            + $"{RouteMapWorkspaceModel.BandLabel(RouteAccess.BandOf(definition.DestinationCode)).ToLowerInvariant()} service";
                var terms = $"{definition.RequiredRotations} rotations"
                            + $"  ·  {definition.EligibleType.Name}"
                            + $"  ·  ${total:N0} total";

                string lockReason;
                if (career.ActiveContract != null)
                    lockReason = "One contract at a time — finish or cancel the active one";
                else if (career.Tier < definition.RequiredTier)
                {
                    var requiredBase = CareerProgress.BaseCapabilityFor(definition.RequiredTier);
                    lockReason = $"Requires {requiredBase.Title} ({definition.RequiredTier} tier)";
                }
                else if (!OwnsType(operations, definition.EligibleType))
                    lockReason = $"Requires {Article.A(definition.EligibleType.Name)} in your fleet";
                else
                    lockReason = string.Empty;

                _offers.Add(new ContractOfferRow(definition, title, terms, lockReason,
                    lockReason.Length == 0));
            }

            if (_offers.Count == 0)
                EmptyOffersLine = offers.Count == 0
                    ? "No offers this window — fly, raise reliability, or buy a type that opens longer routes."
                    : "Every offer this window is already complete. New offers are on the way.";
        }

        private static bool OwnsType(AirlineOperations operations, AircraftType type)
        {
            foreach (var owned in operations.PlayerOwnedTypes())
                if (owned.Id == type.Id)
                    return true;
            return false;
        }

        private static string EligibleRegistrations(AirlineOperations operations, AircraftType type, out bool any)
        {
            any = false;
            var player = operations.PlayerAirline;
            if (player == null)
                return $"No {type.Name} in your fleet yet";

            var list = string.Empty;
            foreach (var aircraft in operations.FleetOf(player))
            {
                if (aircraft.Type.Id != type.Id)
                    continue;
                any = true;
                list = list.Length == 0 ? aircraft.Registration : $"{list}  ·  {aircraft.Registration}";
            }

            return any ? list : $"No {type.Name} in your fleet yet";
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }

    /// <summary>Where the Contracts workspace draws its two columns.</summary>
    public readonly struct ContractsWorkspaceLayout
    {
        public const float ColumnGap = 28f;
        public const float CaptionHeight = 20f;
        public const float OfferHeight = 86f;
        // The market only ever runs three offers at a time (ADR 0056), so the common case is
        // always fewer than a tall column could actually fit at OfferHeight — cards used to
        // stay pinned to that minimum height regardless, leaving most of the column empty
        // below them. MaxOfferHeight caps how far a card grows to fill the gap instead.
        public const float MaxOfferHeight = 172f;
        public const float OfferGap = 10f;
        public const float MinColumnWidth = 300f;

        public const float ActiveTermHeight = 19f;

        /// <summary>Everything in the active card except its list of terms.</summary>
        public const float ActiveCardChrome = 162f;

        private ContractsWorkspaceLayout(HudBox surface, HudBox header, HudBox activeColumn,
            HudBox offersColumn, HudBox divider, HudBox footer)
        {
            Surface = surface;
            Header = header;
            ActiveColumn = activeColumn;
            OffersColumn = offersColumn;
            Divider = divider;
            Footer = footer;
        }

        /// <summary>Height an active card needs to show <paramref name="terms"/> terms in full.</summary>
        public static float ActiveCardHeightFor(int terms) =>
            ActiveCardChrome + (terms < 0 ? 0 : terms) * ActiveTermHeight;

        public HudBox Surface { get; }
        public HudBox Header { get; }
        public HudBox ActiveColumn { get; }
        public HudBox OffersColumn { get; }
        public HudBox Divider { get; }
        public HudBox Footer { get; }

        public HudBox TitleBox => new(Header.X + HudShell.SurfacePadding, Header.Y + 16f, 220f, 30f);

        public HudBox RefreshBox => new(Header.X + HudShell.SurfacePadding + 232f, Header.Y + 24f,
            Header.Width - HudShell.SurfacePadding * 2f - 232f, 18f);

        public HudBox ActiveCaption => ActiveColumn.WithHeight(CaptionHeight);
        public HudBox OffersCaption => OffersColumn.WithHeight(CaptionHeight);

        public HudBox ActiveCard => new(ActiveColumn.X, ActiveColumn.Y + CaptionHeight + 8f,
            ActiveColumn.Width, ActiveColumn.Height - CaptionHeight - 8f);


        /// <summary>
        /// Per-card height when laying out <paramref name="shown"/> offers: fills whatever
        /// room OfferHeight would otherwise have left empty, up to MaxOfferHeight, instead of
        /// always sitting at the size that fits the most possible offers regardless of how
        /// many the market actually has open right now.
        /// </summary>
        public float OfferCardHeight(int shown)
        {
            if (shown <= 0)
                return OfferHeight;
            var available = OffersColumn.Height - CaptionHeight - 8f;
            var natural = (available - (shown - 1) * OfferGap) / shown;
            return natural < OfferHeight ? OfferHeight : natural > MaxOfferHeight ? MaxOfferHeight : natural;
        }

        public HudBox OfferCard(int index, int shown)
        {
            var height = OfferCardHeight(shown);
            return new HudBox(OffersColumn.X, OffersColumn.Y + CaptionHeight + 8f + index * (height + OfferGap),
                OffersColumn.Width, height);
        }

        /// <summary>How many offers fit at the compact OfferHeight — the cap on how many this
        /// column can ever show, independent of how tall each one grows to fill space.</summary>
        public int VisibleOffers
        {
            get
            {
                var available = OffersColumn.Height - CaptionHeight - 8f;
                return available <= 0f ? 0 : (int)((available + OfferGap) / (OfferHeight + OfferGap));
            }
        }

        /// <summary>
        /// <paramref name="activeTerms"/> is how many lines the active contract's terms take;
        /// a stacked narrow window sizes the top half around them instead of guessing, which
        /// is what used to push the card's own button down over the first offer.
        /// </summary>
        public static ContractsWorkspaceLayout Create(HudBox surface, int activeTerms = 4)
        {
            var header = HudShell.Header(surface);
            var footer = HudShell.Footer(surface);
            var body = HudShell.Body(surface, hasFooter: true);

            var columnWidth = (body.Width - ColumnGap) * 0.42f;
            if (columnWidth < MinColumnWidth)
                columnWidth = MinColumnWidth;
            if (columnWidth > body.Width - ColumnGap - MinColumnWidth)
                columnWidth = body.Width - ColumnGap - MinColumnWidth;
            if (columnWidth < 0f || body.Width < MinColumnWidth * 2f + ColumnGap)
            {
                // Too narrow for two columns: stack the active contract above the offers.
                var wanted = CaptionHeight + 8f + ActiveCardHeightFor(activeTerms);
                var top = wanted > body.Height * 0.62f ? body.Height * 0.62f : wanted;
                var stackedActive = new HudBox(body.X, body.Y, body.Width, top);
                var stackedOffers = new HudBox(body.X, body.Y + top + 12f, body.Width,
                    body.Height - top - 12f);
                return new ContractsWorkspaceLayout(surface, header, stackedActive, stackedOffers,
                    HudBox.Empty, footer);
            }

            var active = new HudBox(body.X, body.Y, columnWidth, body.Height);
            var offers = new HudBox(body.X + columnWidth + ColumnGap, body.Y,
                body.Width - columnWidth - ColumnGap, body.Height);
            var divider = new HudBox(body.X + columnWidth + ColumnGap * 0.5f, body.Y, 1f, body.Height);

            return new ContractsWorkspaceLayout(surface, header, active, offers, divider, footer);
        }
    }

    /// <summary>Paints the Contracts workspace into the shared draw list.</summary>
    public static class ContractsWorkspacePainter
    {
        public static void Paint(HudDrawList into, ContractsWorkspaceModel model,
            ContractsWorkspaceLayout layout, string highlightedContractId)
        {
            if (into == null || model == null)
                return;

            into.Clear();
            into.Surface(layout.Surface);
            into.Text(layout.TitleBox, model.Title, 26f, HudTone.Default, HudTextStyle.Bold | HudTextStyle.Caption);
            into.Text(layout.RefreshBox, model.RefreshLine, 12f, HudTone.Muted);
            into.Button(OperationsWorkspacePainter.CloseBox(layout.Surface), "CLOSE", HudAction.Close,
                HudButtonStyle.Secondary);
            into.Hairline(HudShell.HeaderRule(layout.Surface));

            PaintActive(into, model, layout);
            if (!layout.Divider.IsEmpty)
                into.Hairline(layout.Divider);
            PaintOffers(into, model, layout, highlightedContractId);

            into.Hairline(HudShell.FooterRule(layout.Surface));
            into.Text(layout.Footer.Inset(HudShell.SurfacePadding, 10f, HudShell.SurfacePadding, 0f)
                    .WithHeight(16f), model.FooterLine, 11f, HudTone.Muted);
        }

        private static void PaintActive(HudDrawList into, ContractsWorkspaceModel model,
            ContractsWorkspaceLayout layout)
        {
            into.Caption(layout.ActiveCaption, "ACTIVE CONTRACT");
            var card = layout.ActiveCard;

            if (!model.HasActive)
            {
                into.Fill(card.WithHeight(96f), HudTone.Default, 0.03f);
                into.Text(card.Inset(16f, 18f, 16f, 0f).WithHeight(44f),
                    "No contract accepted. Take one from the market to add a completion bonus on top of "
                    + "the per-flight pay.", 13f, HudTone.Muted, HudTextStyle.Wrap);
                return;
            }

            var wanted = ContractsWorkspaceLayout.ActiveCardHeightFor(model.ActiveTerms.Count);
            var body = card.WithHeight(Math.Min(card.Height, wanted));
            into.Fill(body, HudTone.Default, 0.05f);
            into.Outline(body, HudTone.Caution, 0.7f);

            var x = body.X + 16f;
            var width = body.Width - 32f;
            var y = body.Y + 14f;
            into.Text(new HudBox(x, y, width, 24f), model.ActiveTitle, 17f, HudTone.Default, HudTextStyle.Bold);
            y += 26f;
            into.Text(new HudBox(x, y, width, 18f), model.ActiveRoute, 13f, HudTone.Muted);
            y += 24f;
            into.Text(new HudBox(x, y, width, 18f), model.ActiveProgressText, 12f, HudTone.Muted);
            y += 24f;
            into.Bar(new HudBox(x, y, width - 46f, 10f), model.ActiveProgress01, HudTone.Caution);
            into.Text(new HudBox(x + width - 40f, y - 4f, 40f, 18f), model.ActiveProgressPercent, 12f,
                HudTone.Muted, HudTextStyle.Regular, HudAlign.Right);
            y += 22f;

            // A short card drops the terms it cannot show rather than painting them over
            // whatever is underneath it.
            var termsFloor = body.Bottom - 52f;
            foreach (var term in model.ActiveTerms)
            {
                if (y + ContractsWorkspaceLayout.ActiveTermHeight > termsFloor)
                    break;
                into.Text(new HudBox(x, y, width, 18f), term, 12f);
                y += ContractsWorkspaceLayout.ActiveTermHeight;
            }

            y = body.Bottom - 44f;
            into.Button(new HudBox(x, y, width, 30f),
                model.HasEligibleAircraft ? $"ELIGIBLE: {model.EligibleAircraftLine}" : "NO ELIGIBLE AIRCRAFT",
                HudAction.ViewEligibleAircraft, HudButtonStyle.Secondary, model.HasEligibleAircraft);
        }

        private static void PaintOffers(HudDrawList into, ContractsWorkspaceModel model,
            ContractsWorkspaceLayout layout, string highlightedContractId)
        {
            into.Caption(layout.OffersCaption, "AVAILABLE OFFERS");

            if (model.Offers.Count == 0)
            {
                into.Text(new HudBox(layout.OffersColumn.X, layout.OffersColumn.Y + 32f,
                    layout.OffersColumn.Width, 40f), model.EmptyOffersLine, 13f, HudTone.Muted,
                    HudTextStyle.Wrap);
                return;
            }

            var shown = Math.Min(model.Offers.Count, layout.VisibleOffers);
            // The offer market only ever runs three at a time (ADR 0056), so this column is
            // almost always taller than three compact cards need. OfferCardHeight grows each
            // card to use the room instead of leaving it blank below them; the text/button
            // block itself stays its natural size and centres in whatever extra height that
            // card ends up with, so a bigger card reads as "more breathing room", not
            // "content stretched thin".
            const float contentHeight = 78f;
            for (var i = 0; i < shown; i++)
            {
                var offer = model.Offers[i];
                var card = layout.OfferCard(i, shown);
                var highlighted = offer.CanAccept && offer.Definition.Id == highlightedContractId;
                into.Fill(card, highlighted ? HudTone.Accent : HudTone.Default, highlighted ? 0.22f : 0.04f);
                into.Outline(card, highlighted ? HudTone.Accent : HudTone.Muted, highlighted ? 0.9f : 0.25f);

                var contentY = card.Y + (card.Height - contentHeight) * 0.5f;
                var buttonWidth = 168f;
                var textWidth = card.Width - buttonWidth - 40f;
                into.Text(new HudBox(card.X + 16f, contentY, textWidth, 22f), offer.Title, 15f,
                    HudTone.Default, HudTextStyle.Bold);
                into.Text(new HudBox(card.X + 16f, contentY + 24f, textWidth, 18f), offer.Terms, 12f,
                    HudTone.Muted);
                if (offer.LockReason.Length > 0)
                    into.Text(new HudBox(card.X + 16f, contentY + 44f, textWidth, 18f), offer.LockReason, 11f,
                        HudTone.Caution);

                into.Button(new HudBox(card.Right - buttonWidth - 16f, contentY + 12f, buttonWidth, 32f),
                    "ACCEPT CONTRACT", HudAction.Accept(offer.Definition.Id),
                    highlighted ? HudButtonStyle.Primary : HudButtonStyle.Secondary, offer.CanAccept);
                into.Hotspot(card, HudAction.Accept(offer.Definition.Id));
            }

            if (shown < model.Offers.Count)
                into.Text(new HudBox(layout.OffersColumn.X, layout.OffersColumn.Bottom - 16f,
                    layout.OffersColumn.Width, 16f), $"{model.Offers.Count - shown} more this window", 11f,
                    HudTone.Muted, HudTextStyle.Caption);
        }
    }
}
