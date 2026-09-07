using System;
using Airside.Simulation;
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
        private VisualElement _phaseRow;
        private VisualElement _phaseIcon;
        private Label _phaseText;
        private VisualElement _clockRow;
        private VisualElement _weatherIcon;
        private Label _clockText;
        private VisualElement _economyStrip;
        private VisualElement _cashIcon;
        private VisualElement _incomeIcon;
        private Label _cashText;
        private VisualElement _repIcon;
        private VisualElement _researchIcon;
        private Label _financeText;
        private Label _warningText;
        private VisualElement _speedChip;
        private Label _speedText;
        private Button _pauseButton;
        private Button _speed1Button;
        private Button _speed4Button;
        private VisualElement _stripResearchTrack;
        private VisualElement _stripResearchFill;
        private Label _stripResearchText;
        private VisualElement _repTrack;
        private VisualElement _repFill;
        private Label _metarText;
        private VisualElement _turnaroundBlock;
        private Label _turnaroundText;
        private VisualElement _turnaroundBars;
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
        private VisualElement _routeIcon;
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
        private Action _onTogglePause;
        private Action _onSpeed1;
        private Action _onSpeed4;
        private Action _onFollow;
        private Action _onOverview;
        private Action _onToggleMute;

        private Button _followButton;
        private Button _overviewButton;
        private Button _muteButton;
        private VisualElement _saveIcon;

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
            Action onContinueAway = null,
            Action onTogglePause = null,
            Action onSpeed1 = null,
            Action onSpeed4 = null,
            Action onFollow = null,
            Action onOverview = null,
            Action onToggleMute = null)
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
            _onTogglePause = onTogglePause;
            _onSpeed1 = onSpeed1;
            _onSpeed4 = onSpeed4;
            _onFollow = onFollow;
            _onOverview = onOverview;
            _onToggleMute = onToggleMute;
        }

        private void Build()
        {
            if (_built)
                return;

            _document = gameObject.AddComponent<UIDocument>();
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(2560, 1440);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
            panelSettings.sortingOrder = 100;
            panelSettings.themeStyleSheet = ResolveRuntimeTheme();
            _document.panelSettings = panelSettings;

            _root = _document.rootVisualElement;
            _root.style.flexGrow = 1;
            _root.pickingMode = PickingMode.Position;

            BuildEconomyStrip();
            BuildLeftPanel();
            BuildOfferPanel();
            BuildOpsPanel();
            BuildSpeedChip();
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
            _saveChip.style.minWidth = 88;
            _saveChip.style.borderLeftWidth = 3;
            _saveChip.style.borderLeftColor = AirsideTheme.CoastalBlue;
            _saveChip.style.flexDirection = FlexDirection.Row;
            _saveChip.style.alignItems = Align.Center;
            _saveIcon = MakeIconSlot("Save icon", 16);
            _saveIcon.style.marginRight = 6;
            _saveChip.Add(_saveIcon);
            var saveText = new Label("Saved") { name = "Save text" };
            saveText.pickingMode = PickingMode.Ignore;
            saveText.style.color = AirsideTheme.Cloud;
            saveText.style.fontSize = 13;
            saveText.style.unityFontStyleAndWeight = FontStyle.Bold;
            _saveChip.Add(saveText);
            _saveChip.text = string.Empty;
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
            // REF-004: turnaround / flight card sits bottom-left; ops owns top-left.
            _leftPanel = MakePanel("Status panel", 360f);
            _leftPanel.style.left = 22;
            _leftPanel.style.bottom = 22;
            _leftPanel.style.maxHeight = 460;
            _leftPanel.style.paddingLeft = 14;
            _leftPanel.style.paddingRight = 14;
            _leftPanel.style.paddingTop = 10;
            _leftPanel.style.paddingBottom = 12;
            // Coastal accent bar so the status panel reads as brand chrome, not a debug box.
            _leftPanel.style.borderLeftWidth = 3;
            _leftPanel.style.borderLeftColor = AirsideTheme.CoastalBlue;
            _leftPanel.style.borderTopWidth = 2;
            _leftPanel.style.borderTopColor = new Color(
                AirsideTheme.OpenSky.r, AirsideTheme.OpenSky.g, AirsideTheme.OpenSky.b, 0.55f);

            var wordmark = AirsideTheme.WordmarkLight;
            if (wordmark != null)
            {
                _brandImage = new VisualElement { name = "Wordmark" };
                _brandImage.pickingMode = PickingMode.Ignore;
                _brandImage.style.height = 36;
                _brandImage.style.marginBottom = 4;
                _brandImage.style.backgroundImage = new StyleBackground(wordmark);
                _brandImage.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                _leftPanel.Add(_brandImage);
            }
            else
            {
                _brandLabel = MakePanelLabel("Brand", 20, FontStyle.Bold);
                _brandLabel.text = "AIRSIDE";
                _brandLabel.style.marginBottom = 4;
                _leftPanel.Add(_brandLabel);
            }

            _locationText = AddLeftLine(_leftPanel, "Location", 13, FontStyle.Normal);
            _locationText.style.color = AirsideTheme.OpenSky;
            _flightText = AddLeftLine(_leftPanel, "Flight", 16, FontStyle.Bold);

            _phaseRow = new VisualElement { name = "Phase row" };
            _phaseRow.style.flexDirection = FlexDirection.Row;
            _phaseRow.style.alignItems = Align.Center;
            _phaseRow.style.marginTop = 2;
            _phaseRow.style.marginBottom = 2;
            _phaseIcon = MakeIconSlot("Phase icon", 20);
            _phaseRow.Add(_phaseIcon);
            _phaseText = MakePanelLabel("Phase", 14, FontStyle.Normal);
            _phaseText.style.color = AirsideTheme.OpenSky;
            _phaseText.style.marginLeft = 8;
            _phaseRow.Add(_phaseText);
            _leftPanel.Add(_phaseRow);

            _clockRow = new VisualElement { name = "Clock row" };
            _clockRow.style.flexDirection = FlexDirection.Row;
            _clockRow.style.alignItems = Align.Center;
            _clockRow.style.marginTop = 2;
            _clockRow.style.marginBottom = 2;
            _weatherIcon = MakeIconSlot("Weather icon", 18);
            _clockRow.Add(_weatherIcon);
            _clockText = MakePanelLabel("Clock", 12, FontStyle.Normal);
            _clockText.style.marginLeft = 8;
            _clockText.style.whiteSpace = WhiteSpace.Normal;
            _clockRow.Add(_clockText);
            _leftPanel.Add(_clockRow);

            _warningText = AddLeftLine(_leftPanel, "Warning", 13, FontStyle.Bold);
            _warningText.style.color = AirsideTheme.SafetyYellow;

            _turnaroundBlock = new VisualElement { name = "Turnaround" };
            _turnaroundBlock.style.marginTop = 4;
            _turnaroundBlock.style.marginBottom = 4;
            _turnaroundText = MakePanelLabel("Tasks", 11, FontStyle.Normal);
            _turnaroundText.style.whiteSpace = WhiteSpace.Normal;
            _turnaroundText.style.display = DisplayStyle.None;
            _turnaroundBlock.Add(_turnaroundText);
            _turnaroundBars = new VisualElement { name = "Turnaround bars" };
            _turnaroundBars.style.marginTop = 2;
            _turnaroundBlock.Add(_turnaroundBars);
            _priorityButton = MakeButton("Hire priority crew", AirsideTheme.CoastalBlue, 180f);
            _priorityButton.style.marginTop = 6;
            _priorityButton.clicked += () => _onPriorityCrew?.Invoke();
            _turnaroundBlock.Add(_priorityButton);
            _leftPanel.Add(_turnaroundBlock);

            _scheduleText = AddLeftLine(_leftPanel, "Schedule", 12, FontStyle.Normal);
            _staffingText = AddLeftLine(_leftPanel, "Staffing", 12, FontStyle.Normal);
            _earlyHintText = AddLeftLine(_leftPanel, "Early hint", 12, FontStyle.Normal);

            _crewRow = new VisualElement { name = "Crew row" };
            _crewRow.style.flexDirection = FlexDirection.Row;
            _crewRow.style.marginTop = 2;
            _crewRow.style.marginBottom = 2;
            _hireCrewButton = MakeButton("Hire", AirsideTheme.CoastalBlue, 100f);
            _hireCrewButton.clicked += () => _onHireCrew?.Invoke();
            _crewRow.Add(_hireCrewButton);
            _releaseCrewButton = MakeButton("Release", AirsideTheme.Tarmac, 90f);
            _releaseCrewButton.style.marginLeft = 6;
            _releaseCrewButton.clicked += () => _onReleaseCrew?.Invoke();
            _crewRow.Add(_releaseCrewButton);
            _leftPanel.Add(_crewRow);

            _standsText = AddLeftLine(_leftPanel, "Stands", 12, FontStyle.Normal);
            _buildStandButton = MakeButton("Build stand 3", AirsideTheme.CoastalBlue, 180f);
            _buildStandButton.style.marginTop = 2;
            _buildStandButton.clicked += () => _onBuildStand?.Invoke();
            _leftPanel.Add(_buildStandButton);

            _researchText = AddLeftLine(_leftPanel, "Research", 12, FontStyle.Normal);
            _researchTrack = MakeProgressTrack("Research track");
            _researchFill = _researchTrack.Q<VisualElement>("Fill");
            _leftPanel.Add(_researchTrack);
            _researchButton = MakeButton("Start research", AirsideTheme.CoastalBlue, 200f);
            _researchButton.style.marginTop = 2;
            _researchButton.clicked += () => _onStartResearch?.Invoke();
            _leftPanel.Add(_researchButton);

            _coachText = AddLeftLine(_leftPanel, "Coach", 14, FontStyle.Bold);
            _coachText.style.color = AirsideTheme.OpenSky;
            _coachText.style.whiteSpace = WhiteSpace.Normal;
            _controlsText = AddLeftLine(_leftPanel, "Controls", 11, FontStyle.Normal);
            _controlsText.style.color = new Color(AirsideTheme.Cloud.r, AirsideTheme.Cloud.g, AirsideTheme.Cloud.b, 0.7f);
            _controlsText.style.whiteSpace = WhiteSpace.Normal;
            _controlsText.style.display = DisplayStyle.None;

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

        private void BuildEconomyStrip()
        {
            // REF-004 top-centre cash / finance / research strip (0025 item 6).
            _economyStrip = MakePanel("Economy strip", 520f);
            _economyStrip.style.top = 18;
            _economyStrip.style.left = Length.Percent(50);
            _economyStrip.style.translate = new Translate(Length.Percent(-50), 0);
            _economyStrip.style.height = StyleKeyword.Auto;
            _economyStrip.style.paddingLeft = 12;
            _economyStrip.style.paddingRight = 12;
            _economyStrip.style.paddingTop = 6;
            _economyStrip.style.paddingBottom = 6;
            _economyStrip.style.flexDirection = FlexDirection.Row;
            _economyStrip.style.alignItems = Align.Center;
            _economyStrip.style.justifyContent = Justify.Center;
            _economyStrip.pickingMode = PickingMode.Ignore;
            _economyStrip.style.borderTopWidth = 2;
            _economyStrip.style.borderTopColor = new Color(
                AirsideTheme.OpenSky.r, AirsideTheme.OpenSky.g, AirsideTheme.OpenSky.b, 0.45f);

            _cashIcon = MakeIconSlot("Cash icon", 18);
            _economyStrip.Add(_cashIcon);
            _cashText = MakePanelLabel("Cash", 13, FontStyle.Bold);
            _cashText.style.marginLeft = 6;
            _cashText.style.marginRight = 8;
            _economyStrip.Add(_cashText);

            _incomeIcon = MakeIconSlot("Income icon", 16);
            _incomeIcon.style.marginRight = 4;
            _economyStrip.Add(_incomeIcon);

            _economyStrip.Add(MakeStripDivider());

            _repIcon = MakeIconSlot("Rep icon", 18);
            _economyStrip.Add(_repIcon);
            _financeText = MakePanelLabel("Finance", 12, FontStyle.Normal);
            _financeText.style.marginLeft = 6;
            _financeText.style.marginRight = 6;
            _financeText.style.whiteSpace = WhiteSpace.NoWrap;
            _economyStrip.Add(_financeText);
            _repTrack = MakeProgressTrack("Rep track");
            _repTrack.style.width = 72;
            _repTrack.style.marginTop = 0;
            _repTrack.style.marginRight = 10;
            _repFill = _repTrack.Q<VisualElement>("Fill");
            if (_repFill != null)
                _repFill.style.backgroundColor = AirsideTheme.ClearGreen;
            _economyStrip.Add(_repTrack);

            _economyStrip.Add(MakeStripDivider());

            _researchIcon = MakeIconSlot("Research icon", 16);
            _researchIcon.style.marginRight = 4;
            _economyStrip.Add(_researchIcon);
            _stripResearchText = MakePanelLabel("Strip research", 11, FontStyle.Normal);
            _stripResearchText.style.marginRight = 6;
            _stripResearchText.style.color = new Color(AirsideTheme.Cloud.r, AirsideTheme.Cloud.g, AirsideTheme.Cloud.b, 0.85f);
            _economyStrip.Add(_stripResearchText);
            _stripResearchTrack = MakeProgressTrack("Strip research track");
            _stripResearchTrack.style.width = 90;
            _stripResearchTrack.style.marginTop = 0;
            _stripResearchFill = _stripResearchTrack.Q<VisualElement>("Fill");
            _economyStrip.Add(_stripResearchTrack);

            _root.Add(_economyStrip);
        }

        private static VisualElement MakeStripDivider()
        {
            var div = new VisualElement { name = "Strip divider" };
            div.pickingMode = PickingMode.Ignore;
            div.style.width = 1;
            div.style.height = 18;
            div.style.marginLeft = 4;
            div.style.marginRight = 8;
            div.style.backgroundColor = new Color(
                AirsideTheme.Cloud.r, AirsideTheme.Cloud.g, AirsideTheme.Cloud.b, 0.22f);
            return div;
        }

        private void BuildSpeedChip()
        {
            // REF-004 + Batch F4 UI-ICO-005 — pause/speed/camera/audio chrome.
            _speedChip = MakePanel("Speed chip", 420f);
            _speedChip.style.bottom = 22;
            _speedChip.style.left = Length.Percent(50);
            _speedChip.style.translate = new Translate(Length.Percent(-50), 0);
            _speedChip.style.paddingLeft = 10;
            _speedChip.style.paddingRight = 10;
            _speedChip.style.paddingTop = 8;
            _speedChip.style.paddingBottom = 8;
            _speedChip.style.flexDirection = FlexDirection.Row;
            _speedChip.style.alignItems = Align.Center;
            _speedChip.style.justifyContent = Justify.Center;
            _speedChip.pickingMode = PickingMode.Position;
            _speedChip.style.borderTopWidth = 2;
            _speedChip.style.borderTopColor = new Color(
                AirsideTheme.CoastalBlue.r, AirsideTheme.CoastalBlue.g, AirsideTheme.CoastalBlue.b, 0.7f);

            _pauseButton = MakeIconChromeButton("Pause", AirsideTheme.Tarmac, 56f);
            _pauseButton.clicked += () => _onTogglePause?.Invoke();
            _speedChip.Add(_pauseButton);

            _speed1Button = MakeIconChromeButton("1×", AirsideTheme.CoastalBlue, 48f);
            _speed1Button.style.marginLeft = 6;
            _speed1Button.clicked += () => _onSpeed1?.Invoke();
            _speedChip.Add(_speed1Button);

            _speed4Button = MakeIconChromeButton("4×", AirsideTheme.CoastalBlue, 48f);
            _speed4Button.style.marginLeft = 6;
            _speed4Button.clicked += () => _onSpeed4?.Invoke();
            _speedChip.Add(_speed4Button);

            _followButton = MakeIconChromeButton("Follow", AirsideTheme.Tarmac, 56f);
            _followButton.style.marginLeft = 10;
            _followButton.clicked += () => _onFollow?.Invoke();
            _speedChip.Add(_followButton);

            _overviewButton = MakeIconChromeButton("Overview", AirsideTheme.Tarmac, 64f);
            _overviewButton.style.marginLeft = 6;
            _overviewButton.clicked += () => _onOverview?.Invoke();
            _speedChip.Add(_overviewButton);

            _muteButton = MakeIconChromeButton("Audio", AirsideTheme.Tarmac, 52f);
            _muteButton.style.marginLeft = 6;
            _muteButton.clicked += () => _onToggleMute?.Invoke();
            _speedChip.Add(_muteButton);

            _speedText = MakePanelLabel("Speed", 11, FontStyle.Normal);
            _speedText.style.marginLeft = 10;
            _speedText.style.color = new Color(AirsideTheme.Cloud.r, AirsideTheme.Cloud.g, AirsideTheme.Cloud.b, 0.75f);
            _speedChip.Add(_speedText);
            _root.Add(_speedChip);
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

            var offerHeader = new VisualElement { name = "Offer header" };
            offerHeader.style.flexDirection = FlexDirection.Row;
            offerHeader.style.alignItems = Align.Center;
            offerHeader.style.marginLeft = 14;
            offerHeader.style.marginTop = 14;
            offerHeader.style.marginRight = 14;
            _routeIcon = MakeIconSlot("Route icon", 18);
            _routeIcon.style.marginRight = 8;
            offerHeader.Add(_routeIcon);
            _offerTitle = MakePanelLabel("Offer title", 16, FontStyle.Bold);
            _offerTitle.style.flexGrow = 1;
            offerHeader.Add(_offerTitle);
            _offerPanel.Add(offerHeader);

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
            // REF-004: OPERATIONS card owns top-left (status/turnaround sits bottom-left).
            _opsPanel = MakePanel("Ops panel", 340f);
            _opsPanel.style.left = 22;
            _opsPanel.style.top = 22;
            _opsPanel.pickingMode = PickingMode.Ignore;
            // Match status panel brand chrome (0025 item 6).
            _opsPanel.style.borderLeftWidth = 3;
            _opsPanel.style.borderLeftColor = AirsideTheme.CoastalBlue;
            _opsPanel.style.borderTopWidth = 2;
            _opsPanel.style.borderTopColor = new Color(
                AirsideTheme.OpenSky.r, AirsideTheme.OpenSky.g, AirsideTheme.OpenSky.b, 0.55f);

            var title = MakePanelLabel("Ops title", 16, FontStyle.Bold);
            title.text = "OPERATIONS";
            title.style.color = AirsideTheme.OpenSky;
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

            _metarText = MakePanelLabel("Metar", 11, FontStyle.Normal);
            _metarText.style.marginLeft = 14;
            _metarText.style.marginRight = 14;
            _metarText.style.marginTop = 4;
            _metarText.style.color = new Color(AirsideTheme.OpenSky.r, AirsideTheme.OpenSky.g, AirsideTheme.OpenSky.b, 0.9f);
            _opsPanel.Add(_metarText);

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
            AddBrandHeader(pauseCard, "PAUSED");
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
            AddBrandHeader(awayCard, null);
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
            AddBrandHeader(insolventCard, null);
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

        /// <summary>
        /// Decision 0025 item 8 — wordmark first on overlays; optional subtitle when
        /// wordmark art is missing falls back to AIRSIDE text, then the subtitle.
        /// </summary>
        private void AddBrandHeader(VisualElement card, string subtitleOrNull)
        {
            var wordmark = AirsideTheme.WordmarkLight;
            if (wordmark != null)
            {
                var wm = new VisualElement { name = "Overlay wordmark" };
                wm.pickingMode = PickingMode.Ignore;
                wm.style.height = 36;
                wm.style.marginBottom = string.IsNullOrEmpty(subtitleOrNull) ? 8 : 4;
                wm.style.backgroundImage = new StyleBackground(wordmark);
                wm.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                card.Add(wm);
            }
            else
            {
                var brand = MakePanelLabel("Overlay brand", 26, FontStyle.Bold);
                brand.text = "AIRSIDE";
                brand.style.marginBottom = 4;
                card.Add(brand);
            }

            if (!string.IsNullOrEmpty(subtitleOrNull))
            {
                var title = MakePanelLabel("Overlay title", 22, FontStyle.Bold);
                title.text = subtitleOrNull;
                title.style.marginBottom = 4;
                card.Add(title);
            }
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
            // REF-004 chrome: denser ink + coastal hairline so panels read as brand glass,
            // not translucent IMGUI debug boxes.
            var ink = AirsideTheme.RunwayInk;
            ink.a = 0.86f;
            panel.style.backgroundColor = ink;
            var panelTex = AirsideTheme.PanelBackground;
            if (panelTex != null)
            {
                panel.style.backgroundImage = new StyleBackground(panelTex);
                panel.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
            }
            panel.style.borderTopLeftRadius = 4;
            panel.style.borderTopRightRadius = 4;
            panel.style.borderBottomLeftRadius = 4;
            panel.style.borderBottomRightRadius = 4;
            panel.style.borderLeftWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderTopWidth = 2;
            panel.style.borderBottomWidth = 1;
            var border = new Color(AirsideTheme.Tarmac.r, AirsideTheme.Tarmac.g, AirsideTheme.Tarmac.b, 0.4f);
            var top = new Color(AirsideTheme.CoastalBlue.r, AirsideTheme.CoastalBlue.g, AirsideTheme.CoastalBlue.b, 0.7f);
            panel.style.borderLeftColor = border;
            panel.style.borderRightColor = border;
            panel.style.borderTopColor = top;
            panel.style.borderBottomColor = border;
            panel.style.paddingLeft = 2;
            panel.style.paddingRight = 2;
            return panel;
        }

        private static VisualElement MakeIconSlot(string name, float size)
        {
            var icon = new VisualElement { name = name };
            icon.pickingMode = PickingMode.Ignore;
            icon.style.width = size;
            icon.style.height = size;
            icon.style.flexShrink = 0;
            icon.style.display = DisplayStyle.None;
            return icon;
        }

        private static void ApplyIcon(VisualElement slot, Texture2D texture)
        {
            if (slot == null)
                return;
            if (texture == null)
            {
                slot.style.display = DisplayStyle.None;
                slot.style.backgroundImage = StyleKeyword.None;
                return;
            }

            slot.style.display = DisplayStyle.Flex;
            slot.style.backgroundImage = new StyleBackground(texture);
            slot.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
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
            button.style.height = 30;
            button.style.backgroundColor = background;
            button.style.color = AirsideTheme.Cloud;
            button.style.fontSize = 12;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.borderTopLeftRadius = 3;
            button.style.borderTopRightRadius = 3;
            button.style.borderBottomLeftRadius = 3;
            button.style.borderBottomRightRadius = 3;
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderTopWidth = 1;
            button.style.borderBottomWidth = 1;
            var edge = new Color(AirsideTheme.OpenSky.r, AirsideTheme.OpenSky.g, AirsideTheme.OpenSky.b, 0.35f);
            button.style.borderLeftColor = edge;
            button.style.borderRightColor = edge;
            button.style.borderTopColor = edge;
            button.style.borderBottomColor = edge;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.letterSpacing = 0.4f;
            return button;
        }

        /// <summary>
        /// Chrome control that can show a Batch F4 system icon above a short label.
        /// Missing icons keep the text label so the HUD stays usable.
        /// </summary>
        private static Button MakeIconChromeButton(string label, Color background, float width)
        {
            var button = MakeButton(label, background, width);
            button.style.height = 36;
            button.style.flexDirection = FlexDirection.Column;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            button.style.paddingTop = 2;
            button.style.paddingBottom = 2;
            button.style.fontSize = 10;
            var icon = MakeIconSlot($"{label} icon", 16);
            icon.name = "chrome-icon";
            icon.style.marginBottom = 1;
            button.Insert(0, icon);
            return button;
        }

        private static void ApplyChromeButtonIcon(Button button, Texture2D texture, string fallbackText)
        {
            if (button == null)
                return;
            var icon = button.Q<VisualElement>("chrome-icon");
            ApplyIcon(icon, texture);
            // Keep a short caption under the icon; hide text only when the icon is present
            // and the caption would crowd a narrow chip.
            button.text = texture != null ? string.Empty : fallbackText;
            if (texture != null && icon != null)
            {
                // Icon-only chrome reads cleaner at 24 px; tooltip via tooltip attribute.
                button.tooltip = fallbackText;
            }
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
                0.94f);
            label.style.color = AirsideTheme.Cloud;
            label.style.fontSize = 14;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.paddingLeft = 16;
            label.style.paddingRight = 16;
            label.style.paddingTop = 9;
            label.style.paddingBottom = 9;
            label.style.borderTopLeftRadius = 3;
            label.style.borderTopRightRadius = 3;
            label.style.borderBottomLeftRadius = 3;
            label.style.borderBottomRightRadius = 3;
            label.style.borderLeftWidth = 3;
            label.style.borderLeftColor = AirsideTheme.CoastalBlue;
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
            ApplyEconomyVisibility();
            ApplySpeedVisibility();
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
            if (_speedText != null)
            {
                // Prefer a calm speed readout; fall back to the controls hint string.
                _speedText.text = string.IsNullOrEmpty(controlsLine) ? "1× time" : controlsLine.Split('\n')[0];
            }

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
                // Bars are driven by SyncTurnaroundBars; keep text for delay callouts only.
                var delayOnly = ExtractDelayLine(turnaroundLines);
                if (!string.IsNullOrEmpty(delayOnly))
                {
                    _turnaroundText.text = delayOnly;
                    _turnaroundText.style.display = DisplayStyle.Flex;
                    _turnaroundText.style.color = AirsideTheme.SignalRed;
                }
                else
                {
                    _turnaroundText.text = string.Empty;
                    _turnaroundText.style.display = DisplayStyle.None;
                }

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
            // Research progress lives on the top economy strip; left keeps the start button only.
            _researchText.style.display = DisplayStyle.None;
            _researchTrack.style.display = DisplayStyle.None;
            _researchButton.style.display = !earlySession && researchButtonVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_stripResearchText != null)
                _stripResearchText.style.display = earlySession ? DisplayStyle.None : DisplayStyle.Flex;
            if (_stripResearchTrack != null && earlySession)
                _stripResearchTrack.style.display = DisplayStyle.None;

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
                if (_stripResearchText != null)
                    _stripResearchText.text = string.IsNullOrEmpty(researchLine) ? "Research" : researchLine;
                if (_stripResearchFill != null)
                    _stripResearchFill.style.width = Length.Percent(
                        researchProgressVisible ? Mathf.Clamp01(researchProgress01) * 100f : 0f);
                if (_stripResearchTrack != null)
                    _stripResearchTrack.style.display = researchProgressVisible ? DisplayStyle.Flex : DisplayStyle.None;

                if (researchButtonVisible)
                {
                    _researchButton.SetEnabled(researchButtonInteractable);
                    _researchButton.text = researchButtonLabel ?? string.Empty;
                }
            }

            _coachText.text = coachLine ?? string.Empty;
            _coachText.style.color = coachUrgent ? AirsideTheme.SafetyYellow : AirsideTheme.OpenSky;
            // REF-004 calm chrome: coach only when urgent; otherwise flight card stays sparse.
            _coachText.style.display = coachUrgent && !string.IsNullOrEmpty(coachLine)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _controlsText.text = controlsLine ?? string.Empty;

            _waitRow.style.display = showWaitMeter ? DisplayStyle.Flex : DisplayStyle.None;
            if (showWaitMeter)
            {
                _waitLabel.text = waitLabel ?? string.Empty;
                if (_waitFill != null)
                    _waitFill.style.width = Length.Percent(Mathf.Clamp01(waitProgress01) * 100f);
            }

            ApplyLeftVisibility();
            ApplyEconomyVisibility();
            ApplySpeedVisibility();
        }

        /// <summary>
        /// Turnaround task progress bars (REF-004) — one row per ground task with
        /// optional service icons matching the IMGUI turnaround list.
        /// </summary>
        public void SyncTurnaroundBars(
            bool visible,
            string[] names,
            float[] progress01,
            Texture2D[] icons = null)
        {
            if (_turnaroundBars == null)
                return;

            _turnaroundBars.Clear();
            if (!visible || names == null || progress01 == null)
                return;

            var count = Math.Min(names.Length, progress01.Length);
            for (var i = 0; i < count; i++)
            {
                var row = new VisualElement { name = $"Task row {i}" };
                row.style.marginTop = 3;

                var header = new VisualElement { name = $"Task header {i}" };
                header.style.flexDirection = FlexDirection.Row;
                header.style.alignItems = Align.Center;
                header.style.marginBottom = 1;

                var icon = MakeIconSlot($"Task icon {i}", 14);
                icon.style.marginRight = 4;
                Texture2D tex = null;
                if (icons != null && i < icons.Length)
                    tex = icons[i];
                ApplyIcon(icon, tex);
                header.Add(icon);

                var label = MakePanelLabel($"Task {i}", 11, FontStyle.Normal);
                label.text = names[i] ?? string.Empty;
                label.style.flexGrow = 1;
                header.Add(label);
                row.Add(header);

                var track = MakeProgressTrack($"Task track {i}");
                track.style.marginTop = 1;
                track.style.height = 7;
                var fill = track.Q<VisualElement>("Fill");
                if (fill != null)
                {
                    var p = Mathf.Clamp01(progress01[i]);
                    fill.style.width = Length.Percent(p * 100f);
                    fill.style.backgroundColor = p >= 0.999f
                        ? AirsideTheme.ClearGreen
                        : p > 0.01f
                            ? AirsideTheme.CoastalBlue
                            : new Color(AirsideTheme.Tarmac.r, AirsideTheme.Tarmac.g, AirsideTheme.Tarmac.b, 0.5f);
                }

                row.Add(track);
                _turnaroundBars.Add(row);
            }
        }

        private static string ExtractDelayLine(string turnaroundLines)
        {
            if (string.IsNullOrEmpty(turnaroundLines))
                return null;
            foreach (var line in turnaroundLines.Split('\n'))
            {
                if (line.IndexOf("DELAY", StringComparison.OrdinalIgnoreCase) >= 0)
                    return line.Trim();
            }

            return null;
        }

        /// <summary>
        /// Batch E panel icons + Batch F4 UI-ICO-005 system chrome.
        /// </summary>
        public void SyncChromeIcons(
            AircraftPhase? phase,
            WeatherKind weather,
            int speed,
            bool paused,
            bool audioMuted = false,
            bool following = false)
        {
            ApplyIcon(_phaseIcon, phase.HasValue ? AirsideTheme.OperationIcon(phase.Value) : null);
            ApplyIcon(_weatherIcon, AirsideTheme.WeatherIcon(weather));
            ApplyIcon(_cashIcon, AirsideTheme.Icon("economy", "cash"));
            ApplyIcon(_incomeIcon, AirsideTheme.Icon("economy", "income"));
            ApplyIcon(_repIcon, AirsideTheme.Icon("economy", "reputation"));
            ApplyIcon(_researchIcon, AirsideTheme.Icon("economy", "research"));
            ApplyIcon(_routeIcon, AirsideTheme.Icon("economy", "route"));
            ApplyIcon(_saveIcon, AirsideTheme.SystemIcon("save"));

            if (_speedText != null)
                _speedText.text = paused ? "PAUSED" : (following ? "Follow" : "Tab cycles");

            if (_pauseButton != null)
            {
                var pauseIcon = paused
                    ? AirsideTheme.SystemIcon("play")
                    : AirsideTheme.SystemIcon("pause");
                ApplyChromeButtonIcon(_pauseButton, pauseIcon, paused ? "Resume" : "Pause");
                _pauseButton.style.backgroundColor = paused ? AirsideTheme.CoastalBlue : AirsideTheme.Tarmac;
                _pauseButton.style.opacity = paused ? 1f : 0.9f;
            }

            ApplyChromeButtonIcon(_speed1Button, AirsideTheme.SystemIcon("speed"), "1×");
            ApplyChromeButtonIcon(_speed4Button, AirsideTheme.SystemIcon("speed"), "4×");
            // Keep numeric captions on speed even when icons load — 1× vs 4× must stay distinct.
            if (_speed1Button != null)
                _speed1Button.text = "1×";
            if (_speed4Button != null)
                _speed4Button.text = "4×";

            ApplyChromeButtonIcon(_followButton, AirsideTheme.SystemIcon("follow"), "Follow");
            ApplyChromeButtonIcon(_overviewButton, AirsideTheme.SystemIcon("overview"), "Overview");
            ApplyChromeButtonIcon(
                _muteButton,
                audioMuted ? AirsideTheme.SystemIcon("audio_off") : AirsideTheme.SystemIcon("audio_on"),
                audioMuted ? "Unmute" : "Mute");

            if (_followButton != null)
            {
                _followButton.style.backgroundColor = following ? AirsideTheme.CoastalBlue : AirsideTheme.Tarmac;
                _followButton.style.opacity = following ? 1f : 0.9f;
            }

            if (_muteButton != null)
            {
                _muteButton.style.backgroundColor = audioMuted ? AirsideTheme.SignalRed : AirsideTheme.Tarmac;
                _muteButton.style.opacity = audioMuted ? 1f : 0.9f;
            }

            HighlightSpeedButton(_speed1Button, !paused && speed <= 1);
            HighlightSpeedButton(_speed4Button, !paused && speed >= 4);
        }

        private static void HighlightSpeedButton(Button button, bool active)
        {
            if (button == null)
                return;
            button.style.backgroundColor = active ? AirsideTheme.OpenSky : AirsideTheme.CoastalBlue;
            button.style.opacity = active ? 1f : 0.72f;
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

        public void SyncOps(string summary, string body, string reportBodyOrNull, string metarLine = null)
        {
            if (_opsPanel == null)
                return;

            _opsSummary.text = summary ?? string.Empty;
            _opsBody.text = body ?? string.Empty;
            if (_metarText != null)
            {
                _metarText.text = metarLine ?? string.Empty;
                _metarText.style.display = string.IsNullOrEmpty(metarLine) ? DisplayStyle.None : DisplayStyle.Flex;
            }

            var hasReport = !string.IsNullOrEmpty(reportBodyOrNull);
            _reportBlock.style.display = hasReport ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasReport)
                _reportBody.text = reportBodyOrNull;

            ApplyOpsVisibility();
        }

        public void SyncReputationBar(float score01)
        {
            if (_repFill == null)
                return;
            _repFill.style.width = Length.Percent(Mathf.Clamp01(score01) * 100f);
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

            // Top-left ops card — no longer stacks under the right-side offer.
            _opsPanel.style.top = 22;
            _opsPanel.style.left = 22;
        }

        private void ApplyEconomyVisibility()
        {
            if (_economyStrip == null)
                return;
            _economyStrip.style.display = _gameplayChromeVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ApplySpeedVisibility()
        {
            if (_speedChip == null)
                return;
            _speedChip.style.display = _gameplayChromeVisible ? DisplayStyle.Flex : DisplayStyle.None;
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
