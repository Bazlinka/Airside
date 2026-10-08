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
            "A contract is a promise. Abandon it and your reliability drops.";

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
                FillActive(operations, career, active, now);

            FillOffers(operations, career);
        }

        private void FillActive(AirlineOperations operations, AirlineCareerState career,
            RouteContractDefinition definition, SimulationTime now)
        {
            HasActive = true;
            ActiveTitle = OperationsSummary.ProveTitle(definition);
            ActiveRoute = $"{OperationsSummary.PlaceName(definition.OriginCode)} ↔ "
                          + OperationsSummary.PlaceName(definition.DestinationCode);

            var done = career.ActiveContract.CompletedRotations;
            var required = definition.RequiredRotations;
            ActiveProgressText = $"{done} of {required} flight{(required == 1 ? "" : "s")} done";
            ActiveProgress01 = required <= 0 ? 0f : Clamp01(done / (float)required);
            ActiveProgressPercent = $"{(int)(ActiveProgress01 * 100f)}%";

            _activeTerms.Add($"Aircraft: {definition.EligibleType.Name}" + (definition.RequiresFreighter ? " · freighter refit required" : ""));
            _activeTerms.Add($"${definition.PaymentPerRotation:N0} a flight"
                             + $"  ·  ${definition.CompletionReward:N0} when done");
            _activeTerms.Add(definition.ReliabilityLossOnCancel > 0
                ? $"Abandoning costs {definition.ReliabilityLossOnCancel} reliability"
                : "Abandoning costs no reliability");
            if (definition.ReliabilityGainPerRotation > 0)
                _activeTerms.Add($"+{definition.ReliabilityGainPerRotation} reliability a flight");
            // ADR 0127: a deadline turns the contract into a commitment with a clock on it.
            if (operations.ContractExpiresAt() is { } due)
            {
                var left = Math.Max(0, due.ElapsedSeconds - now.ElapsedSeconds);
                _activeTerms.Add($"Due in {RouteMapWorkspaceModel.Duration(left)}."
                                 + (definition.ReliabilityLossOnCancel > 0
                                     ? $" Miss it and lose {definition.ReliabilityLossOnCancel} reliability" : string.Empty));
            }


            EligibleAircraftLine = EligibleRegistrations(operations, definition, out var any);
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
                var title = OfferTitle(definition);
                var terms = $"{definition.RequiredRotations} {(definition.RequiredRotations == 1 ? "flight" : "flights")}"
                            + $"  ·  {definition.EligibleType.Name}"
                            + (definition.RequiresFreighter ? " freighter" : "")
                            + $"  ·  ${total:N0} total"
                            + (definition.HasDeadline ? $"  ·  within {RouteMapWorkspaceModel.Duration(definition.DeadlineSeconds)}" : string.Empty);

                string lockReason;
                if (career.ActiveContract != null)
                    lockReason = "One contract at a time. Finish or abandon yours first";
                else if (career.Tier < definition.RequiredTier)
                    lockReason = $"Needs the {definition.RequiredTier} tier";
                else if (!operations.HasContractAircraft(definition))
                    lockReason = definition.RequiresFreighter
                        ? $"Refit {Article.A(definition.EligibleType.Name)} as a freighter at Adelaide"
                        : $"Needs {Article.A(definition.EligibleType.Name)} at Adelaide";
                else if (definition.HasDeadline && !operations.CanStillFinish(definition))
                    lockReason = "Your eligible aircraft cannot finish before the deadline";
                else
                    lockReason = string.Empty;

                _offers.Add(new ContractOfferRow(definition, title, terms, lockReason,
                    lockReason.Length == 0));
            }

            if (_offers.Count == 0)
                EmptyOffersLine = offers.Count == 0
                    ? "No offers right now. Fly more, raise your reliability or buy an aircraft that opens longer routes."
                    : "You have done every offer on the board. New ones are coming.";
        }

        /// <summary>"Kingscote charter", "Ceduna medical flight", "Mildura freight run", "Melbourne domestic service".</summary>
        public static string OfferTitle(RouteContractDefinition definition)
        {
            var place = OperationsSummary.PlaceName(definition.DestinationCode);
            return definition.Kind switch
            {
                ContractKind.Charter => $"{place} charter",
                ContractKind.Medical => $"{place} medical flight",
                ContractKind.Freight => $"{place} freight run",
                _ => $"{place} {RouteMapWorkspaceModel.BandLabel(RouteAccess.BandOf(definition.DestinationCode)).ToLowerInvariant()} route"
            };
        }

        private static string EligibleRegistrations(AirlineOperations operations, RouteContractDefinition definition, out bool any)
        {
            var type = definition.EligibleType;
            any = false;
            var player = operations.PlayerAirline;
            if (player == null)
                return $"No {type.Name} in your fleet yet";

            var list = string.Empty;
            foreach (var aircraft in operations.FleetOf(player))
            {
                if (!definition.MatchesAircraft(aircraft.Type, aircraft.IsFreighter))
                    continue;
                any = true;
                list = list.Length == 0 ? aircraft.Registration : $"{list}  ·  {aircraft.Registration}";
            }

            return any ? list : definition.RequiresFreighter
                ? $"Refit {Article.A(type.Name)} as a freighter at Adelaide"
                : $"No {type.Name} at Adelaide yet";
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
                // Always leave room for one offer card, or compact windows show no offers at all.
                var offerRoom = body.Height - 12f - (CaptionHeight + 8f + OfferHeight);
                if (top > offerRoom)
                    top = offerRoom > 0f ? offerRoom : 0f;
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
            HudShellPainter.PaintSheetHeader(into, layout.Surface, model.Title, model.RefreshLine,
                layout.TitleBox, layout.RefreshBox);

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
                    "No contract yet. Take one from the offers below. It pays a bonus on top of each flight's pay.", 13f, HudTone.Muted, HudTextStyle.Wrap);
                return;
            }

            var wanted = ContractsWorkspaceLayout.ActiveCardHeightFor(model.ActiveTerms.Count);
            var body = card.WithHeight(Math.Min(card.Height, wanted));
            into.Card(body, 1f);
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
            // Abandon is the only way out of a contract the airline can no longer fly (ADR 0121).
            const float abandonWidth = 112f;
            into.Button(new HudBox(x, y, width - abandonWidth - 8f, 30f),
                model.HasEligibleAircraft ? $"FLY WITH {model.EligibleAircraftLine}" : "NO AIRCRAFT FOR THIS",
                HudAction.ViewEligibleAircraft, HudButtonStyle.Secondary, model.HasEligibleAircraft);
            into.Button(new HudBox(x + width - abandonWidth, y, abandonWidth, 30f), "ABANDON",
                HudAction.CancelContract, HudButtonStyle.Destructive);
        }

        /// <summary>"4 flights", "ATR 42-600", "within 30 h": an offer's terms as chips (ADR 0130).</summary>
        public static IReadOnlyList<string> Chips(RouteContractDefinition definition)
        {
            var chips = new List<string>
            {
                definition.RequiredRotations == 1 ? "1 flight" : $"{definition.RequiredRotations} flights",
                definition.EligibleType.Name
            };
            if (definition.HasDeadline)
                chips.Add($"within {RouteMapWorkspaceModel.Duration(definition.DeadlineSeconds)}");
            return chips;
        }

        /// <summary>The lock reason every shown offer shares, or null when they differ or any is open.</summary>
        public static string SharedLockReason(IReadOnlyList<ContractOfferRow> offers, int shown)
        {
            string shared = null;
            var count = 0;
            for (var i = 0; i < offers.Count && i < shown; i++)
            {
                var reason = offers[i].LockReason;
                if (reason.Length == 0)
                    return null;
                if (shared == null)
                    shared = reason;
                else if (shared != reason)
                    return null;
                count++;
            }

            return count >= 2 ? shared : null;
        }

        private static ((string Category, string Name) Icon, HudTone Tone) KindBadge(ContractKind kind) => kind switch
        {
            ContractKind.Charter => (("economy", "income"), HudTone.Caution),
            ContractKind.Medical => (("service", "priority"), HudTone.Negative),
            ContractKind.Freight => (("service", "baggage"), HudTone.Muted),
            _ => (("economy", "route"), HudTone.Accent)
        };

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
            // ADR 0130: a reason every card shares (one contract at a time) is said once, by the caption.
            var shared = SharedLockReason(model.Offers, shown);
            if (shared != null)
            {
                var room = layout.OffersColumn.Width - 170f;
                // A side sheet (ADR 0135) is narrower: the short form keeps the point.
                var notice = HudShell.Measure(shared, 11f) <= room ? shared : shared.Split('.')[0];
                into.Text(new HudBox(layout.OffersCaption.X + 170f, layout.OffersCaption.Y + 1f, room, 16f), notice, 11f,
                    HudTone.Caution, HudTextStyle.Bold);
            }

            // The offer market only ever runs three at a time (ADR 0056), so cards grow to use the
            // column (OfferCardHeight) and the content centres in whatever height each card gets.
            for (var i = 0; i < shown; i++)
            {
                var offer = model.Offers[i];
                var definition = offer.Definition;
                var card = layout.OfferCard(i, shown);
                var highlighted = offer.CanAccept && definition.Id == highlightedContractId;
                into.Card(card, offer.CanAccept ? 0.95f : 0.6f);
                if (highlighted)
                    into.Fill(card, HudTone.Accent, 0.14f);
                into.Outline(card, highlighted ? HudTone.Accent : HudTone.Muted, highlighted ? 0.9f : 0.18f);

                var reason = shared == null ? offer.LockReason : string.Empty;
                var contentHeight = reason.Length > 0 ? 70f : 52f;
                var contentY = card.Y + (card.Height - contentHeight) * 0.5f;

                // Kind badge: a disc with the kind's icon.
                var (kindIcon, kindTone) = KindBadge(definition.Kind);
                into.Dot(card.X + 34f, contentY + 22f, 36f, kindTone);
                into.Icon(new HudBox(card.X + 24f, contentY + 12f, 20f, 20f), kindIcon.Category, kindIcon.Name,
                    HudTone.Default);

                var rightWidth = card.Width < 640f ? 118f : 150f;
                var picture = card.Width >= 560f && card.Height >= 96f ? 96f : 0f;
                var textX = card.X + 64f;
                var textWidth = card.Width - 64f - rightWidth - picture - 24f;
                var titleSize = 15f;
                var titleWidth = HudShell.Measure(offer.Title, titleSize);
                if (titleWidth > textWidth && titleWidth > 0f)
                    titleSize = Math.Max(12f, titleSize * textWidth / titleWidth);
                into.Text(new HudBox(textX, contentY, textWidth, 22f), offer.Title, titleSize, HudTone.Default,
                    HudTextStyle.Bold);

                // Chips: how many flights, which aircraft, how long you have.
                var chipX = textX;
                var chipY = contentY + 27f;
                var rows = 1;
                foreach (var chip in Chips(definition))
                {
                    var chipWidth = HudShell.Measure(chip, 10f, 0.3f) + 16f;
                    if (chipX + chipWidth > textX + textWidth)
                    {
                        // A narrow card (side sheet, ADR 0135) takes a second row rather than drop a term.
                        if (rows == 2 || chipX == textX || chipY + 48f > card.Bottom - 6f)
                            break;
                        rows++;
                        chipX = textX;
                        chipY += 24f;
                    }
                    into.Pill(new HudBox(chipX, chipY, chipWidth, 20f), chip, HudTone.Muted);
                    chipX += chipWidth + 6f;
                }

                if (reason.Length > 0)
                    into.Text(new HudBox(textX, chipY + 26f, textWidth, 16f), reason, 11f, HudTone.Caution);

                if (picture > 0f)
                {
                    var thumb = FleetWorkspacePainter.Thumbnail(definition.EligibleType);
                    if (thumb.Length > 0)
                        into.Image(new HudBox(card.Right - rightWidth - 16f - picture, card.Y + (card.Height - picture / 1.5f) * 0.5f,
                            picture, picture / 1.5f), thumb, offer.CanAccept ? 1f : 0.6f);
                }

                // The money, big, over the button.
                var total = definition.PaymentPerRotation * definition.RequiredRotations + definition.CompletionReward;
                var right = new HudBox(card.Right - rightWidth - 16f, contentY - 4f, rightWidth, 26f);
                into.Text(right, $"${total:N0}", 22f, offer.CanAccept ? HudTone.Positive : HudTone.Muted,
                    HudTextStyle.Bold, HudAlign.Right);
                into.Button(new HudBox(right.X, right.Y + 30f, rightWidth, 30f), "ACCEPT",
                    HudAction.Accept(definition.Id),
                    highlighted ? HudButtonStyle.Primary : HudButtonStyle.Secondary, offer.CanAccept);
                into.Hotspot(card, HudAction.Accept(definition.Id));
            }

            if (shown < model.Offers.Count)
                into.Text(new HudBox(layout.OffersColumn.X, layout.OffersColumn.Bottom - 16f,
                    layout.OffersColumn.Width, 16f), $"{model.Offers.Count - shown} more on offer", 11f,
                    HudTone.Muted, HudTextStyle.Caption);
        }
    }
}
