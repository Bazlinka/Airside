using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Airside.Presentation
{
    /// <summary>
    /// Decision 0025 item 6 — UI Toolkit surface. Owns the left status panel,
    /// OPERATIONS, route offer, toasts and Saved chip via a runtime
    /// <see cref="UIDocument"/>. Canvas keeps full-screen overlays
    /// (briefing / away / insolvency / pause) until those migrate.
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
            Action onStartResearch = null)
        {
            _onAccept = onAccept;
            _onDecline = onDecline;
            _onPriorityCrew = onPriorityCrew;
            _onHireCrew = onHireCrew;
            _onReleaseCrew = onReleaseCrew;
            _onBuildStand = onBuildStand;
            _onStartResearch = onStartResearch;
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
            panelSettings.sortingOrder = 50;
            _document.panelSettings = panelSettings;

            _root = _document.rootVisualElement;
            _root.style.flexGrow = 1;
            _root.pickingMode = PickingMode.Position;

            BuildLeftPanel();
            BuildOfferPanel();
            BuildOpsPanel();

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
    }
}
