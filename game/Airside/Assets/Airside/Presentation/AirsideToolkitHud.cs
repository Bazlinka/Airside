using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Airside.Presentation
{
    /// <summary>
    /// Decision 0025 item 6 — UI Toolkit surface. Owns the full gameplay HUD
    /// (left status, OPERATIONS, route offer, toasts, Saved chip) and
    /// full-screen overlays (briefing / pause / away / insolvency) via a
    /// runtime <see cref="UIDocument"/>. Canvas remains as a fallback.
    /// </summary>
    public sealed class AirsideToolkitHud : MonoBehaviour
    {
        private UIDocument _document;
        private VisualElement _root;

        private VisualElement _leftPanel;
        private Label _brandLabel;
        private VisualElement _brandImage;
        private Label _locationText;
        private Label _flightText;
        private Label _phaseText;
        private Label _clockText;
        private Label _cashText;
        private Label _financeText;
        private Label _warningText;
        private VisualElement _turnaroundBlock;
        private Label _turnaroundText;
        private Button _priorityButton;
        private Label _scheduleText;
        private Label _staffingText;
        private Label _earlyHintText;
        private VisualElement _crewRow;
        private Button _hireCrewButton;
        private Button _releaseCrewButton;
        private Label _standsText;
        private Button _buildStandButton;
        private Label _researchText;
        private VisualElement _researchTrack;
        private VisualElement _researchFill;
        private Button _researchButton;
        private Label _coachText;
        private Label _controlsText;
        private VisualElement _waitRow;
        private Label _waitLabel;
        private VisualElement _waitFill;

        private VisualElement _offerPanel;
        private VisualElement _offerAccent;
        private Label _offerTitle;
        private Label _offerBody;
        private Label _offerStatus;
        private Button _acceptButton;
        private Button _declineButton;
        private VisualElement _opsPanel;
        private Label _opsSummary;
        private Label _opsBody;
        private VisualElement _reportBlock;
        private Label _reportBody;
        private Label _opsToast;
        private Label _researchToast;
        private Label _saveChip;

        private VisualElement _briefingOverlay;
        private Label _briefingBody;
        private VisualElement _pauseOverlay;
        private VisualElement _awayOverlay;
        private Label _awayBody;
        private VisualElement _insolvencyOverlay;
        private Label _insolvencyBody;

        private bool _built;
        private bool _offerVisible;
        private bool _firstDecisionOffer;
        private bool _gameplayChromeVisible = true;

        private Action _onAccept;
        private Action _onDecline;
        private Action _onPriorityCrew;
        private Action _onHireCrew;
        private Action _onReleaseCrew;
        private Action _onBuildStand;
        private Action _onStartResearch;
        private Action _onBeginOperations;
        private Action _onResetAirport;
        private Action _onContinueAway;

        public bool IsActive => _built && _document != null && _document.rootVisualElement != null;

        public static AirsideToolkitHud Create(Transform host)
        {
            var go = new GameObject("Airside Toolkit HUD");
            go.transform.SetParent(host, false);
            var hud = go.AddComponent<AirsideToolkitHud>();
            hud.Build();
            return hud;
        }

        public void BindActions(
            Action onAccept,
            Action onDecline,
            Action onPriorityCrew = null,
            Action onHireCrew = null,
            Action onReleaseCrew = null,
            Action onBuildStand = null,
            Action onStartResearch = null,
            Action onBeginOperations = null,
            Action onResetAirport = null,
            Action onContinueAway = null)
        {
            _onAccept = onAccept;
            _onDecline = onDecline;
            _onPriorityCrew = onPriorityCrew;
            _onHireCrew = onHireCrew;
            _onReleaseCrew = onReleaseCrew;
            _onBuildStand = onBuildStand;
            _onStartResearch = onStartResearch;
            _onBeginOperations = onBeginOperations;
            _onResetAirport = onResetAirport;
            _onContinueAway = onContinueAway;
        }

        private void Build()
        {
            if (_built)
                return;

            _document = gameObject.AddComponent<UIDocument>();
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
            panelSettings.sortingOrder = 100;
            panelSettings.themeStyleSheet = ResolveRuntimeTheme();
            _document.panelSettings = panelSettings;

            _root = _document.rootVisualElement;
            _root.style.flexGrow = 1;
            _root.pickingMode = PickingMode.Position;

            BuildLeftPanel();
            BuildOfferPanel();
            BuildOpsPanel();
            BuildOverlays();

            _researchToast = MakeToastLabel("Research toast");
            _researchToast.style.top = 18;
            _researchToast.style.alignSelf = Align.Center;
            _root.Add(_researchToast);

            _opsToast = MakeToastLabel("Ops toast");
            _opsToast.style.bottom = 28;
            _opsToast.style.alignSelf = Align.Center;
            _root.Add(_opsToast);

            _saveChip = MakeToastLabel("Save chip");
            _saveChip.style.top = 18;
            _saveChip.style.right = 24;
            _saveChip.style.alignSelf = Align.FlexEnd;
            _saveChip.style.fontSize = 13;
            _saveChip.style.minWidth = 72;
            _saveChip.text = "Saved";
            _root.Add(_saveChip);

            SyncToast(string.Empty, false);
            SyncResearchToast(string.Empty, false);
            SyncSaveIndicator(false);
            SyncOffer(false, false, string.Empty, string.Empty, string.Empty, false, false, string.Empty);
            SyncOps(string.Empty, string.Empty, null);
            _built = true;
        }

        private void BuildLeftPanel()
        {
            _leftPanel = MakePanel("Status panel", 410f);
            _leftPanel.style.left = 22;
            _leftPanel.style.top = 22;
            _leftPanel.style.maxHeight = 620;
            _leftPanel.style.paddingLeft = 16;
            _leftPanel.style.paddingRight = 16;
            _leftPanel.style.paddingTop = 12;
            _leftPanel.style.paddingBottom = 12;

            var wordmark = AirsideTheme.WordmarkLight;
            if (wordmark != null)
            {
                _brandImage = new VisualElement { name = "Wordmark" };
                _brandImage.pickingMode = PickingMode.Ignore;
                _brandImage.style.height = 40;
                _brandImage.style.marginBottom = 6;
                _brandImage.style.backgroundImage = new StyleBackground(wordmark);
                _brandImage.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                _leftPanel.Add(_brandImage);
            }
            else
            {
                _brandLabel = MakePanelLabel("Brand", 22, FontStyle.Bold);
                _brandLabel.text = "AIRSIDE";
                _brandLabel.style.marginBottom = 4;
                _leftPanel.Add(_brandLabel);
            }

            _locationText = AddLeftLine(_leftPanel, "Location", 14, FontStyle.Normal);
            _flightText = AddLeftLine(_leftPanel, "Flight", 16, FontStyle.Bold);
            _phaseText = AddLeftLine(_leftPanel, "Phase", 15, FontStyle.Normal);
            _clockText = AddLeftLine(_leftPanel, "Clock", 13, FontStyle.Normal);
            _cashText = AddLeftLine(_leftPanel, "Cash", 13, FontStyle.Normal);
            _financeText = AddLeftLine(_leftPanel, "Finance", 13, FontStyle.Normal);
            _warningText = AddLeftLine(_leftPanel, "Warning", 13, FontStyle.Bold);
            _warningText.style.color = AirsideTheme.SafetyYellow;

            _turnaroundBlock = new VisualElement { name = "Turnaround" };
            _turnaroundBlock.style.marginTop = 4;
            _turnaroundBlock.style.marginBottom = 4;
            _turnaroundText = MakePanelLabel("Tasks", 12, FontStyle.Normal);
            _turnaroundText.style.whiteSpace = WhiteSpace.Normal;
            _turnaroundBlock.Add(_turnaroundText);
            _priorityButton = MakeButton("Hire priority crew", AirsideTheme.CoastalBlue, 190f);
            _priorityButton.style.marginTop = 6;
            _priorityButton.clicked += () => _onPriorityCrew?.Invoke();
            _turnaroundBlock.Add(_priorityButton);
            _leftPanel.Add(_turnaroundBlock);

            _scheduleText = AddLeftLine(_leftPanel, "Schedule", 13, FontStyle.Normal);
            _staffingText = AddLeftLine(_leftPanel, "Staffing", 13, FontStyle.Normal);
            _earlyHintText = AddLeftLine(_leftPanel, "Early hint", 13, FontStyle.Normal);

            _crewRow = new VisualElement { name = "Crew row" };
            _crewRow.style.flexDirection = FlexDirection.Row;
            _crewRow.style.marginTop = 4;
            _crewRow.style.marginBottom = 4;
            _hireCrewButton = MakeButton("Hire crew", AirsideTheme.CoastalBlue, 150f);
            _hireCrewButton.clicked += () => _onHireCrew?.Invoke();
            _crewRow.Add(_hireCrewButton);
            _releaseCrewButton = MakeButton("Release crew", AirsideTheme.Tarmac, 110f);
            _releaseCrewButton.style.marginLeft = 8;
            _releaseCrewButton.clicked += () => _onReleaseCrew?.Invoke();
            _crewRow.Add(_releaseCrewButton);
            _leftPanel.Add(_crewRow);

            _standsText = AddLeftLine(_leftPanel, "Stands", 13, FontStyle.Normal);
            _buildStandButton = MakeButton("Build stand 3", AirsideTheme.CoastalBlue, 220f);
            _buildStandButton.style.marginTop = 4;
            _buildStandButton.clicked += () => _onBuildStand?.Invoke();
            _leftPanel.Add(_buildStandButton);

            _researchText = AddLeftLine(_leftPanel, "Research", 13, FontStyle.Normal);
            _researchTrack = MakeProgressTrack("Research track");
            _researchFill = _researchTrack.Q<VisualElement>("Fill");
            _leftPanel.Add(_researchTrack);
            _researchButton = MakeButton("Start research", AirsideTheme.CoastalBlue, 260f);
            _researchButton.style.marginTop = 4;
            _researchButton.clicked += () => _onStartResearch?.Invoke();
            _leftPanel.Add(_researchButton);

            _coachText = AddLeftLine(_leftPanel, "Coach", 14, FontStyle.Bold);
            _coachText.style.color = AirsideTheme.SafetyYellow;
            _coachText.style.whiteSpace = WhiteSpace.Normal;
            _controlsText = AddLeftLine(_leftPanel, "Controls", 12, FontStyle.Normal);
            _controlsText.style.whiteSpace = WhiteSpace.Normal;

            _waitRow = new VisualElement { name = "Wait row" };
            _waitRow.style.marginTop = 6;
            _waitLabel = MakePanelLabel("Wait label", 12, FontStyle.Normal);
            _waitRow.Add(_waitLabel);
            var waitTrack = MakeProgressTrack("Wait track");
            _waitFill = waitTrack.Q<VisualElement>("Fill");
            _waitRow.Add(waitTrack);
            _leftPanel.Add(_waitRow);

            _root.Add(_leftPanel);
        }

        private void BuildOfferPanel()
        {
            _offerPanel = MakePanel("Offer panel", 340f);
            _offerPanel.style.top = 22;
            _offerPanel.style.right = 22;
            _offerPanel.style.minHeight = 156;

            _offerAccent = new VisualElement { name = "Offer accent" };
            _offerAccent.pickingMode = PickingMode.Ignore;
            _offerAccent.style.position = Position.Absolute;
            _offerAccent.style.left = 0;
            _offerAccent.style.top = 0;
            _offerAccent.style.bottom = 0;
            _offerAccent.style.width = 4;
            _offerAccent.style.backgroundColor = AirsideTheme.SafetyYellow;
            _offerPanel.Add(_offerAccent);

            _offerTitle = MakePanelLabel("Offer title", 16, FontStyle.Bold);
            _offerTitle.style.marginLeft = 14;
            _offerTitle.style.marginTop = 14;
            _offerTitle.style.marginRight = 14;
            _offerPanel.Add(_offerTitle);

            _offerBody = MakePanelLabel("Offer body", 14, FontStyle.Normal);
            _offerBody.style.marginLeft = 14;
            _offerBody.style.marginRight = 14;
            _offerBody.style.marginTop = 8;
            _offerBody.style.whiteSpace = WhiteSpace.Normal;
            _offerPanel.Add(_offerBody);

            _offerStatus = MakePanelLabel("Offer status", 13, FontStyle.Normal);
            _offerStatus.style.marginLeft = 14;
            _offerStatus.style.marginRight = 14;
            _offerStatus.style.marginTop = 8;
            _offerPanel.Add(_offerStatus);

            var row = new VisualElement { name = "Offer buttons" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginLeft = 14;
            row.style.marginRight = 14;
            row.style.marginTop = 12;
            row.style.marginBottom = 14;
            row.style.justifyContent = Justify.FlexStart;

            _acceptButton = MakeButton("Accept route", AirsideTheme.CoastalBlue, 190f);
            _acceptButton.clicked += () => _onAccept?.Invoke();
            row.Add(_acceptButton);

            _declineButton = MakeButton("Decline", AirsideTheme.Tarmac, 100f);
            _declineButton.style.marginLeft = 10;
            _declineButton.clicked += () => _onDecline?.Invoke();
            row.Add(_declineButton);

            _offerPanel.Add(row);
            _root.Add(_offerPanel);
        }

        private void BuildOpsPanel()
        {
            _opsPanel = MakePanel("Ops panel", 340f);
            _opsPanel.style.right = 22;
            _opsPanel.pickingMode = PickingMode.Ignore;

            var title = MakePanelLabel("Ops title", 16, FontStyle.Bold);
            title.text = "OPERATIONS";
            title.style.marginLeft = 14;
            title.style.marginTop = 12;
            title.style.marginRight = 14;
            _opsPanel.Add(title);

            _opsSummary = MakePanelLabel("Ops summary", 12, FontStyle.Normal);
            _opsSummary.style.marginLeft = 14;
            _opsSummary.style.marginRight = 14;
            _opsSummary.style.marginTop = 6;
            _opsSummary.style.whiteSpace = WhiteSpace.Normal;
            _opsSummary.style.color = new Color(AirsideTheme.Cloud.r, AirsideTheme.Cloud.g, AirsideTheme.Cloud.b, 0.92f);
            _opsPanel.Add(_opsSummary);

            _opsBody = MakePanelLabel("Ops body", 12, FontStyle.Normal);
            _opsBody.style.marginLeft = 14;
            _opsBody.style.marginRight = 14;
            _opsBody.style.marginTop = 8;
            _opsBody.style.marginBottom = 8;
            _opsBody.style.whiteSpace = WhiteSpace.Normal;
            _opsBody.style.color = new Color(AirsideTheme.Cloud.r, AirsideTheme.Cloud.g, AirsideTheme.Cloud.b, 0.88f);
            _opsPanel.Add(_opsBody);

            _reportBlock = new VisualElement { name = "Daily report" };
            _reportBlock.pickingMode = PickingMode.Ignore;
            _reportBlock.style.marginLeft = 10;
            _reportBlock.style.marginRight = 10;
            _reportBlock.style.marginBottom = 10;
            _reportBlock.style.paddingLeft = 10;
            _reportBlock.style.paddingRight = 10;
            _reportBlock.style.paddingTop = 8;
            _reportBlock.style.paddingBottom = 8;
            var reportInk = AirsideTheme.RunwayInk;
            reportInk.a = 0.55f;
            _reportBlock.style.backgroundColor = reportInk;
            _reportBlock.style.borderTopLeftRadius = 4;
            _reportBlock.style.borderTopRightRadius = 4;
            _reportBlock.style.borderBottomLeftRadius = 4;
            _reportBlock.style.borderBottomRightRadius = 4;

            var reportTitle = MakePanelLabel("Report title", 13, FontStyle.Bold);
            reportTitle.text = "DAILY REPORT";
            _reportBlock.Add(reportTitle);

            _reportBody = MakePanelLabel("Report body", 11, FontStyle.Normal);
            _reportBody.style.marginTop = 4;
            _reportBody.style.whiteSpace = WhiteSpace.Normal;
            _reportBlock.Add(_reportBody);

            _opsPanel.Add(_reportBlock);
            _root.Add(_opsPanel);
        }

        private void BuildOverlays()
        {
            _briefingOverlay = MakeOverlay("Briefing overlay", new Color(0.05f, 0.07f, 0.09f, 1f));
            var splash = AirsideTheme.SplashDawn;
            if (splash != null)
            {
                _briefingOverlay.style.backgroundImage = new StyleBackground(splash);
                _briefingOverlay.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
            }

            var briefingCard = MakeCenteredCard(500f);
            briefingCard.style.minHeight = 360;
            briefingCard.style.paddingLeft = 24;
            briefingCard.style.paddingRight = 24;
            briefingCard.style.paddingTop = 18;
            briefingCard.style.paddingBottom = 18;
            var wordmark = AirsideTheme.WordmarkLight;
            if (wordmark != null)
            {
                var wm = new VisualElement { name = "Briefing wordmark" };
                wm.pickingMode = PickingMode.Ignore;
                wm.style.height = 70;
                wm.style.marginBottom = 8;
                wm.style.backgroundImage = new StyleBackground(wordmark);
                wm.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                briefingCard.Add(wm);
            }
            else
            {
                var brand = MakePanelLabel("Briefing brand", 26, FontStyle.Bold);
                brand.text = "AIRSIDE";
                brand.style.marginBottom = 8;
                briefingCard.Add(brand);
            }

            _briefingBody = MakePanelLabel("Briefing body", 15, FontStyle.Normal);
            _briefingBody.style.whiteSpace = WhiteSpace.Normal;
            _briefingBody.style.flexGrow = 1;
            _briefingBody.style.marginBottom = 16;
            briefingCard.Add(_briefingBody);
            var begin = MakeButton("Begin operations", AirsideTheme.CoastalBlue, 220f);
            begin.style.alignSelf = Align.Center;
            begin.clicked += () => _onBeginOperations?.Invoke();
            briefingCard.Add(begin);
            _briefingOverlay.Add(briefingCard);
            _root.Add(_briefingOverlay);

            _pauseOverlay = MakeOverlay("Pause overlay", new Color(0.05f, 0.07f, 0.09f, 0.45f));
            var pauseCard = MakeCenteredCard(320f);
            pauseCard.style.paddingLeft = 24;
            pauseCard.style.paddingRight = 24;
            pauseCard.style.paddingTop = 18;
            pauseCard.style.paddingBottom = 18;
            var pauseTitle = MakePanelLabel("Pause title", 26, FontStyle.Bold);
            pauseTitle.text = "PAUSED";
            pauseCard.Add(pauseTitle);
            var pauseHint = MakePanelLabel("Pause hint", 15, FontStyle.Bold);
            pauseHint.text = "Space to resume";
            pauseHint.style.color = AirsideTheme.SafetyYellow;
            pauseHint.style.marginTop = 8;
            pauseHint.style.marginBottom = 14;
            pauseCard.Add(pauseHint);
            var resetPause = MakeButton("Start new airport", AirsideTheme.Tarmac, 220f);
            resetPause.style.alignSelf = Align.Center;
            resetPause.clicked += () => _onResetAirport?.Invoke();
            pauseCard.Add(resetPause);
            _pauseOverlay.Add(pauseCard);
            _root.Add(_pauseOverlay);

            _awayOverlay = MakeOverlay("Away overlay", new Color(0.05f, 0.07f, 0.09f, 0.55f));
            var awayCard = MakeCenteredCard(460f);
            awayCard.style.minHeight = 320;
            awayCard.style.paddingLeft = 24;
            awayCard.style.paddingRight = 24;
            awayCard.style.paddingTop = 18;
            awayCard.style.paddingBottom = 18;
            var awayTitle = MakePanelLabel("Away title", 26, FontStyle.Bold);
            awayTitle.text = "AIRSIDE";
            awayCard.Add(awayTitle);
            var awaySub = MakePanelLabel("Away subtitle", 16, FontStyle.Bold);
            awaySub.text = "Welcome back to operations";
            awaySub.style.marginTop = 6;
            awaySub.style.marginBottom = 10;
            awayCard.Add(awaySub);
            _awayBody = MakePanelLabel("Away body", 14, FontStyle.Normal);
            _awayBody.style.whiteSpace = WhiteSpace.Normal;
            _awayBody.style.flexGrow = 1;
            _awayBody.style.marginBottom = 16;
            awayCard.Add(_awayBody);
            var continueBtn = MakeButton("Continue operations", AirsideTheme.CoastalBlue, 220f);
            continueBtn.style.alignSelf = Align.Center;
            continueBtn.clicked += () => _onContinueAway?.Invoke();
            awayCard.Add(continueBtn);
            _awayOverlay.Add(awayCard);
            _root.Add(_awayOverlay);

            _insolvencyOverlay = MakeOverlay("Insolvency overlay", new Color(0.08f, 0.05f, 0.05f, 0.6f));
            var insolventCard = MakeCenteredCard(460f);
            insolventCard.style.minHeight = 280;
            insolventCard.style.paddingLeft = 24;
            insolventCard.style.paddingRight = 24;
            insolventCard.style.paddingTop = 18;
            insolventCard.style.paddingBottom = 18;
            var insolventTitle = MakePanelLabel("Insolvency title", 26, FontStyle.Bold);
            insolventTitle.text = "AIRSIDE";
            insolventCard.Add(insolventTitle);
            var insolventHead = MakePanelLabel("Insolvency head", 17, FontStyle.Bold);
            insolventHead.text = "Airport declared insolvent";
            insolventHead.style.color = AirsideTheme.SignalRed;
            insolventHead.style.marginTop = 8;
            insolventHead.style.marginBottom = 10;
            insolventCard.Add(insolventHead);
            _insolvencyBody = MakePanelLabel("Insolvency body", 14, FontStyle.Normal);
            _insolvencyBody.style.whiteSpace = WhiteSpace.Normal;
            _insolvencyBody.style.flexGrow = 1;
            _insolvencyBody.style.marginBottom = 16;
            insolventCard.Add(_insolvencyBody);
            var newAirport = MakeButton("Start a new airport", AirsideTheme.CoastalBlue, 260f);
            newAirport.style.alignSelf = Align.Center;
            newAirport.clicked += () => _onResetAirport?.Invoke();
            insolventCard.Add(newAirport);
            _insolvencyOverlay.Add(insolventCard);
            _root.Add(_insolvencyOverlay);

            HideAllOverlays();
        }

        private VisualElement MakeOverlay(string name, Color dim)
        {
            var overlay = new VisualElement { name = name };
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.top = 0;
            overlay.style.right = 0;
            overlay.style.bottom = 0;
            overlay.style.backgroundColor = dim;
            overlay.style.justifyContent = Justify.Center;
            overlay.style.alignItems = Align.Center;
            overlay.style.display = DisplayStyle.None;
            return overlay;
        }

        private VisualElement MakeCenteredCard(float width)
        {
            var card = new VisualElement { name = "Card" };
            card.style.width = width;
            card.style.backgroundColor = new Color(
                AirsideTheme.RunwayInk.r,
                AirsideTheme.RunwayInk.g,
                AirsideTheme.RunwayInk.b,
                0.96f);
            card.style.borderTopLeftRadius = 8;
            card.style.borderTopRightRadius = 8;
            card.style.borderBottomLeftRadius = 8;
            card.style.borderBottomRightRadius = 8;
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            var border = new Color(AirsideTheme.Tarmac.r, AirsideTheme.Tarmac.g, AirsideTheme.Tarmac.b, 0.85f);
            card.style.borderLeftColor = border;
            card.style.borderRightColor = border;
            card.style.borderTopColor = border;
            card.style.borderBottomColor = border;
            return card;
        }

        private void HideAllOverlays()
        {
            if (_briefingOverlay != null)
                _briefingOverlay.style.display = DisplayStyle.None;
            if (_pauseOverlay != null)
                _pauseOverlay.style.display = DisplayStyle.None;
            if (_awayOverlay != null)
                _awayOverlay.style.display = DisplayStyle.None;
            if (_insolvencyOverlay != null)
                _insolvencyOverlay.style.display = DisplayStyle.None;
        }

        public void SyncOverlays(
            bool showBriefing,
            bool showPause,
            bool showAway,
            bool showInsolvency,
            string locationName,
            int firstOfferAfterSeconds,
            string awayBody,
            string insolvencyBody)
        {
            if (_briefingOverlay == null)
                return;

            HideAllOverlays();

            if (showInsolvency)
            {
                _insolvencyOverlay.style.display = DisplayStyle.Flex;
                _insolvencyBody.text = insolvencyBody ?? string.Empty;
                SetGameplayChromeVisible(false);
                return;
            }

            if (showAway)
            {
                _awayOverlay.style.display = DisplayStyle.Flex;
                _awayBody.text = awayBody ?? string.Empty;
                SetGameplayChromeVisible(false);
                return;
            }

            if (showBriefing)
            {
                _briefingOverlay.style.display = DisplayStyle.Flex;
                _briefingBody.text =
                    "You run this regional airport\n\n" +
                    $"Aircraft move on their own. Your job is cash, reputation and capacity at {locationName}.\n\n" +
                    "First useful decision\n" +
                    $"In about {firstOfferAfterSeconds} seconds an airline will offer a scheduled route. Accept it to earn money on every completed flight.\n\n" +
                    "Watch OPERATIONS on the right. Watch cash and delays on the left. Press Enter to Accept the first offer.\n\n" +
                    "Space / Enter to begin  ·  Tab = 4× speed";
                SetGameplayChromeVisible(false);
                return;
            }

            if (showPause)
            {
                _pauseOverlay.style.display = DisplayStyle.Flex;
                // Pause keeps gameplay chrome visible underneath the dimmer.
                SetGameplayChromeVisible(true);
                return;
            }

            SetGameplayChromeVisible(true);
        }

        private static Label AddLeftLine(VisualElement parent, string name, int fontSize, FontStyle style)
        {
            var label = MakePanelLabel(name, fontSize, style);
            label.style.marginTop = 2;
            label.style.marginBottom = 2;
            label.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(label);
            return label;
        }

        private static VisualElement MakeProgressTrack(string name)
        {
            var track = new VisualElement { name = name };
            track.pickingMode = PickingMode.Ignore;
            track.style.height = 10;
            track.style.marginTop = 4;
            track.style.backgroundColor = new Color(
                AirsideTheme.Tarmac.r, AirsideTheme.Tarmac.g, AirsideTheme.Tarmac.b, 0.9f);
            track.style.borderTopLeftRadius = 3;
            track.style.borderTopRightRadius = 3;
            track.style.borderBottomLeftRadius = 3;
            track.style.borderBottomRightRadius = 3;
            var fill = new VisualElement { name = "Fill" };
            fill.pickingMode = PickingMode.Ignore;
            fill.style.height = Length.Percent(100);
            fill.style.width = Length.Percent(0);
            fill.style.backgroundColor = AirsideTheme.CoastalBlue;
            fill.style.borderTopLeftRadius = 3;
            fill.style.borderTopRightRadius = 3;
            fill.style.borderBottomLeftRadius = 3;
            fill.style.borderBottomRightRadius = 3;
            track.Add(fill);
            return track;
        }

        private VisualElement MakePanel(string name, float width)
        {
            var panel = new VisualElement { name = name };
            panel.style.position = Position.Absolute;
            panel.style.width = width;
            panel.style.backgroundColor = new Color(
                AirsideTheme.RunwayInk.r,
                AirsideTheme.RunwayInk.g,
                AirsideTheme.RunwayInk.b,
                0.94f);
            panel.style.borderTopLeftRadius = 6;
            panel.style.borderTopRightRadius = 6;
            panel.style.borderBottomLeftRadius = 6;
            panel.style.borderBottomRightRadius = 6;
            panel.style.borderLeftWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderTopWidth = 1;
            panel.style.borderBottomWidth = 1;
            var border = new Color(AirsideTheme.Tarmac.r, AirsideTheme.Tarmac.g, AirsideTheme.Tarmac.b, 0.85f);
            panel.style.borderLeftColor = border;
            panel.style.borderRightColor = border;
            panel.style.borderTopColor = border;
            panel.style.borderBottomColor = border;
            return panel;
        }

        private static Label MakePanelLabel(string name, int fontSize, FontStyle style)
        {
            var label = new Label
            {
                name = name,
                text = string.Empty
            };
            label.pickingMode = PickingMode.Ignore;
            label.style.color = AirsideTheme.Cloud;
            label.style.fontSize = fontSize;
            label.style.unityFontStyleAndWeight = style;
            label.style.unityTextAlign = TextAnchor.UpperLeft;
            return label;
        }

        private static Button MakeButton(string label, Color background, float width)
        {
            var button = new Button { text = label };
            button.style.width = width;
            button.style.height = 32;
            button.style.backgroundColor = background;
            button.style.color = AirsideTheme.Cloud;
            button.style.fontSize = 13;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.borderTopLeftRadius = 4;
            button.style.borderTopRightRadius = 4;
            button.style.borderBottomLeftRadius = 4;
            button.style.borderBottomRightRadius = 4;
            button.style.borderLeftWidth = 0;
            button.style.borderRightWidth = 0;
            button.style.borderTopWidth = 0;
            button.style.borderBottomWidth = 0;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            return button;
        }

        private static Label MakeToastLabel(string name)
        {
            var label = new Label
            {
                name = name,
                text = string.Empty
            };
            label.pickingMode = PickingMode.Ignore;
            label.style.position = Position.Absolute;
            label.style.backgroundColor = new Color(
                AirsideTheme.RunwayInk.r,
                AirsideTheme.RunwayInk.g,
                AirsideTheme.RunwayInk.b,
                0.92f);
            label.style.color = AirsideTheme.Cloud;
            label.style.fontSize = 15;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.paddingLeft = 18;
            label.style.paddingRight = 18;
            label.style.paddingTop = 10;
            label.style.paddingBottom = 10;
            label.style.borderTopLeftRadius = 6;
            label.style.borderTopRightRadius = 6;
            label.style.borderBottomLeftRadius = 6;
            label.style.borderBottomRightRadius = 6;
            label.style.maxWidth = 520;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.display = DisplayStyle.None;
            return label;
        }

        public void SetGameplayChromeVisible(bool visible)
        {
            _gameplayChromeVisible = visible;
            ApplyLeftVisibility();
            ApplyOfferVisibility();
            ApplyOpsVisibility();
            if (!visible)
            {
                SyncToast(string.Empty, false);
                SyncResearchToast(string.Empty, false);
                SyncSaveIndicator(false);
            }
        }

        public void SyncToast(string message, bool visible)
        {
            if (_opsToast == null)
                return;
            _opsToast.text = message ?? string.Empty;
            _opsToast.style.display = visible && !string.IsNullOrEmpty(message)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        public void SyncResearchToast(string message, bool visible)
        {
            if (_researchToast == null)
                return;
            _researchToast.text = message ?? string.Empty;
            _researchToast.style.display = visible && !string.IsNullOrEmpty(message)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        public void SyncSaveIndicator(bool visible)
        {
            if (_saveChip == null)
                return;
            _saveChip.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SyncLeftPanel(
            string locationLine,
            string flightLine,
            string phaseLine,
            string clockLine,
            Color clockColor,
            string cashLine,
            Color cashColor,
            string financeLine,
            Color financeColor,
            string warningLine,
            Color warningColor,
            bool showTurnaround,
            string turnaroundLines,
            bool priorityVisible,
            bool priorityInteractable,
            string priorityLabel,
            string scheduleLine,
            Color scheduleColor,
            string staffingLine,
            Color staffingColor,
            bool earlySession,
            string earlyHint,
            bool hireInteractable,
            string hireLabel,
            bool releaseInteractable,
            bool buildStandVisible,
            bool buildStandInteractable,
            string buildStandLabel,
            string standsLine,
            string researchLine,
            bool researchProgressVisible,
            float researchProgress01,
            bool researchButtonVisible,
            bool researchButtonInteractable,
            string researchButtonLabel,
            string coachLine,
            bool coachUrgent,
            string controlsLine,
            bool showWaitMeter,
            string waitLabel,
            float waitProgress01)
        {
            if (_leftPanel == null)
                return;

            _locationText.text = locationLine ?? string.Empty;
            _flightText.text = flightLine ?? string.Empty;
            _phaseText.text = phaseLine ?? string.Empty;
            _clockText.text = clockLine ?? string.Empty;
            _clockText.style.color = clockColor;
            _cashText.text = cashLine ?? string.Empty;
            _cashText.style.color = cashColor;
            _financeText.text = financeLine ?? string.Empty;
            _financeText.style.color = financeColor;

            var hasWarning = !string.IsNullOrEmpty(warningLine);
            _warningText.style.display = hasWarning ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasWarning)
            {
                _warningText.text = warningLine;
                _warningText.style.color = warningColor;
            }

            _turnaroundBlock.style.display = showTurnaround ? DisplayStyle.Flex : DisplayStyle.None;
            if (showTurnaround)
            {
                _turnaroundText.text = turnaroundLines ?? string.Empty;
                _priorityButton.style.display = priorityVisible ? DisplayStyle.Flex : DisplayStyle.None;
                if (priorityVisible)
                {
                    _priorityButton.SetEnabled(priorityInteractable);
                    _priorityButton.text = priorityLabel ?? string.Empty;
                }
            }

            _scheduleText.text = scheduleLine ?? string.Empty;
            _scheduleText.style.color = scheduleColor;
            _staffingText.text = staffingLine ?? string.Empty;
            _staffingText.style.color = staffingColor;

            _earlyHintText.style.display = earlySession ? DisplayStyle.Flex : DisplayStyle.None;
            if (earlySession)
                _earlyHintText.text = earlyHint ?? string.Empty;

            _crewRow.style.display = earlySession ? DisplayStyle.None : DisplayStyle.Flex;
            _standsText.style.display = earlySession ? DisplayStyle.None : DisplayStyle.Flex;
            _buildStandButton.style.display = !earlySession && buildStandVisible ? DisplayStyle.Flex : DisplayStyle.None;
            _researchText.style.display = earlySession ? DisplayStyle.None : DisplayStyle.Flex;
            _researchTrack.style.display = !earlySession && researchProgressVisible ? DisplayStyle.Flex : DisplayStyle.None;
            _researchButton.style.display = !earlySession && researchButtonVisible ? DisplayStyle.Flex : DisplayStyle.None;

            if (!earlySession)
            {
                _hireCrewButton.SetEnabled(hireInteractable);
                _hireCrewButton.text = hireLabel ?? string.Empty;
                _releaseCrewButton.SetEnabled(releaseInteractable);
                _standsText.text = standsLine ?? string.Empty;
                if (buildStandVisible)
                {
                    _buildStandButton.SetEnabled(buildStandInteractable);
                    _buildStandButton.text = buildStandLabel ?? string.Empty;
                }

                _researchText.text = researchLine ?? string.Empty;
                if (researchProgressVisible && _researchFill != null)
                    _researchFill.style.width = Length.Percent(Mathf.Clamp01(researchProgress01) * 100f);

                if (researchButtonVisible)
                {
                    _researchButton.SetEnabled(researchButtonInteractable);
                    _researchButton.text = researchButtonLabel ?? string.Empty;
                }
            }

            _coachText.text = coachLine ?? string.Empty;
            _coachText.style.color = coachUrgent ? AirsideTheme.SafetyYellow : AirsideTheme.Cloud;
            _controlsText.text = controlsLine ?? string.Empty;

            _waitRow.style.display = showWaitMeter ? DisplayStyle.Flex : DisplayStyle.None;
            if (showWaitMeter)
            {
                _waitLabel.text = waitLabel ?? string.Empty;
                if (_waitFill != null)
                    _waitFill.style.width = Length.Percent(Mathf.Clamp01(waitProgress01) * 100f);
            }

            ApplyLeftVisibility();
        }

        public void SyncOffer(
            bool visible,
            bool firstDecision,
            string title,
            string body,
            string status,
            bool statusCaution,
            bool canAccept,
            string acceptLabel)
        {
            _offerVisible = visible;
            _firstDecisionOffer = firstDecision;
            if (_offerPanel == null)
                return;

            if (!visible)
            {
                ApplyOfferVisibility();
                ApplyOpsVisibility();
                return;
            }

            _offerTitle.text = title ?? string.Empty;
            _offerTitle.style.color = firstDecision ? AirsideTheme.SafetyYellow : AirsideTheme.Cloud;
            _offerBody.text = body ?? string.Empty;
            _offerStatus.text = status ?? string.Empty;
            _offerStatus.style.color = statusCaution ? AirsideTheme.SafetyYellow : AirsideTheme.Cloud;
            _acceptButton.SetEnabled(canAccept);
            _acceptButton.text = string.IsNullOrEmpty(acceptLabel) ? "Accept route" : acceptLabel;
            _offerAccent.style.display = firstDecision ? DisplayStyle.Flex : DisplayStyle.None;
            if (firstDecision)
            {
                var pulse = 0.45f + 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f));
                var c = AirsideTheme.SafetyYellow;
                c.a = pulse;
                _offerAccent.style.backgroundColor = c;
            }

            _offerPanel.style.minHeight = firstDecision ? 196 : 156;
            ApplyOfferVisibility();
            ApplyOpsVisibility();
        }

        public void SyncOps(string summary, string body, string reportBodyOrNull)
        {
            if (_opsPanel == null)
                return;

            _opsSummary.text = summary ?? string.Empty;
            _opsBody.text = body ?? string.Empty;
            var hasReport = !string.IsNullOrEmpty(reportBodyOrNull);
            _reportBlock.style.display = hasReport ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasReport)
                _reportBody.text = reportBodyOrNull;

            ApplyOpsVisibility();
        }

        private void ApplyLeftVisibility()
        {
            if (_leftPanel == null)
                return;
            _leftPanel.style.display = _gameplayChromeVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ApplyOfferVisibility()
        {
            if (_offerPanel == null)
                return;
            _offerPanel.style.display = _gameplayChromeVisible && _offerVisible
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private void ApplyOpsVisibility()
        {
            if (_opsPanel == null)
                return;

            var show = _gameplayChromeVisible;
            _opsPanel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show)
                return;

            var offerHeight = _offerVisible ? (_firstDecisionOffer ? 196f : 156f) : 0f;
            var top = 22f + (_offerVisible ? offerHeight + 12f : 0f);
            _opsPanel.style.top = top;
        }

        /// <summary>
        /// Runtime-created <see cref="PanelSettings"/> need an explicit Theme Style
        /// Sheet or the player logs "No Theme Style Sheet set to PanelSettings".
        /// Ships as <c>Resources/Airside/UI/AirsideRuntimeTheme.tss</c> which imports
        /// Unity's built-in default theme.
        /// </summary>
        private static ThemeStyleSheet ResolveRuntimeTheme()
        {
            var theme = Resources.Load<ThemeStyleSheet>("Airside/UI/AirsideRuntimeTheme");
            if (theme != null)
                return theme;

#if UNITY_EDITOR
            theme = UnityEditor.AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(
                "Assets/Resources/Airside/UI/AirsideRuntimeTheme.tss");
            if (theme != null)
                return theme;

            theme = UnityEditor.AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(
                "Assets/UI Toolkit/UnityThemes/UnityDefaultTheme.tss");
            if (theme != null)
                return theme;
#endif
            return null;
        }
    }
}
