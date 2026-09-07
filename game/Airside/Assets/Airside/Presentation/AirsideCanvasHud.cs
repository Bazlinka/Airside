using System;
using UnityEngine;
using UnityEngine.UI;

namespace Airside.Presentation
{
    /// <summary>
    /// Decision 0025 item 6 — runtime uGUI Canvas HUD for primary first-session
    /// surfaces: left status, route offer, operations log, ops/research toasts,
    /// save indicator, and full-screen overlays (briefing / pause / away /
    /// insolvency). Built in code so packaged builds do not depend on
    /// editor-imported PanelSettings. Residual IMGUI remains only when Canvas
    /// is inactive.
    /// </summary>
    public sealed class AirsideCanvasHud
    {
        private readonly RectTransform _root;
        private readonly RectTransform _leftPanel;
        private readonly RectTransform _offerPanel;
        private readonly RectTransform _opsPanel;
        private readonly RectTransform _toastPanel;
        private readonly Text _opsSummary;
        private readonly Text _opsBody;
        private readonly Text _reportTitle;
        private readonly Text _reportBody;
        private readonly GameObject _reportBlock;
        private readonly Text _locationText;
        private readonly Text _flightText;
        private readonly Text _phaseText;
        private readonly Text _clockText;
        private readonly Text _cashText;
        private readonly Text _financeText;
        private readonly Text _warningText;
        private readonly Text _turnaroundText;
        private readonly GameObject _turnaroundBlock;
        private readonly Button _priorityButton;
        private readonly Text _priorityLabel;
        private readonly Text _scheduleText;
        private readonly Text _staffingText;
        private readonly Text _earlyHintText;
        private readonly GameObject _crewRow;
        private readonly Button _hireCrewButton;
        private readonly Button _releaseCrewButton;
        private readonly Text _standsText;
        private readonly Button _buildStandButton;
        private readonly Text _buildStandLabel;
        private readonly Text _researchText;
        private readonly Image _researchFill;
        private readonly GameObject _researchTrack;
        private readonly Button _researchButton;
        private readonly Text _researchButtonLabel;
        private readonly Text _coachText;
        private readonly Text _controlsText;
        private readonly Image _waitFill;
        private readonly GameObject _waitRow;
        private readonly Text _waitLabel;
        private readonly Text _offerTitle;
        private readonly Text _offerBody;
        private readonly Text _offerStatus;
        private readonly Button _acceptButton;
        private readonly Button _declineButton;
        private readonly Text _acceptLabel;
        private readonly Text _toastText;
        private readonly Image _offerAccent;
        private RectTransform _researchToastPanel;
        private Text _researchToastText;
        private RectTransform _savePanel;
        private Text _saveText;
        private RectTransform _pausePanel;
        private RectTransform _briefingPanel;
        private Image _briefingSplash;
        private Text _briefingBody;
        private RectTransform _awayPanel;
        private Text _awayBody;
        private RectTransform _insolvencyPanel;
        private Text _insolvencyBody;
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

        private AirsideCanvasHud(
            RectTransform root,
            RectTransform leftPanel,
            RectTransform offerPanel,
            RectTransform opsPanel,
            Text opsSummary,
            Text opsBody,
            Text reportTitle,
            Text reportBody,
            GameObject reportBlock,
            RectTransform toastPanel,
            Text locationText,
            Text flightText,
            Text phaseText,
            Text clockText,
            Text cashText,
            Text financeText,
            Text warningText,
            Text turnaroundText,
            GameObject turnaroundBlock,
            Button priorityButton,
            Text priorityLabel,
            Text scheduleText,
            Text staffingText,
            Text earlyHintText,
            GameObject crewRow,
            Button hireCrewButton,
            Button releaseCrewButton,
            Text standsText,
            Button buildStandButton,
            Text buildStandLabel,
            Text researchText,
            Image researchFill,
            GameObject researchTrack,
            Button researchButton,
            Text researchButtonLabel,
            Text coachText,
            Text controlsText,
            Image waitFill,
            GameObject waitRow,
            Text waitLabel,
            Text offerTitle,
            Text offerBody,
            Text offerStatus,
            Button acceptButton,
            Button declineButton,
            Text acceptLabel,
            Text toastText,
            Image offerAccent)
        {
            _root = root;
            _leftPanel = leftPanel;
            _offerPanel = offerPanel;
            _opsPanel = opsPanel;
            _opsSummary = opsSummary;
            _opsBody = opsBody;
            _reportTitle = reportTitle;
            _reportBody = reportBody;
            _reportBlock = reportBlock;
            _toastPanel = toastPanel;
            _locationText = locationText;
            _flightText = flightText;
            _phaseText = phaseText;
            _clockText = clockText;
            _cashText = cashText;
            _financeText = financeText;
            _warningText = warningText;
            _turnaroundText = turnaroundText;
            _turnaroundBlock = turnaroundBlock;
            _priorityButton = priorityButton;
            _priorityLabel = priorityLabel;
            _scheduleText = scheduleText;
            _staffingText = staffingText;
            _earlyHintText = earlyHintText;
            _crewRow = crewRow;
            _hireCrewButton = hireCrewButton;
            _releaseCrewButton = releaseCrewButton;
            _standsText = standsText;
            _buildStandButton = buildStandButton;
            _buildStandLabel = buildStandLabel;
            _researchText = researchText;
            _researchFill = researchFill;
            _researchTrack = researchTrack;
            _researchButton = researchButton;
            _researchButtonLabel = researchButtonLabel;
            _coachText = coachText;
            _controlsText = controlsText;
            _waitFill = waitFill;
            _waitRow = waitRow;
            _waitLabel = waitLabel;
            _offerTitle = offerTitle;
            _offerBody = offerBody;
            _offerStatus = offerStatus;
            _acceptButton = acceptButton;
            _declineButton = declineButton;
            _acceptLabel = acceptLabel;
            _toastText = toastText;
            _offerAccent = offerAccent;
        }

        public bool IsActive => _root != null && _root.gameObject.activeInHierarchy;

        public static AirsideCanvasHud Create(Transform host)
        {
            var canvasGo = new GameObject("Airside Canvas HUD", typeof(RectTransform));
            canvasGo.transform.SetParent(host, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var root = canvasGo.GetComponent<RectTransform>();
            Stretch(root);

            var left = BuildPanel(root, "Status panel", new Vector2(22f, -22f), new Vector2(410f, 420f), anchorTopLeft: true);
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(left, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 0f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.offsetMin = new Vector2(16f, 12f);
            contentRt.offsetMax = new Vector2(-16f, -12f);
            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 4f;
            layout.padding = new RectOffset(4, 4, 0, 0);
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            AddWordmark(contentRt);

            var location = AddLayoutText(contentRt, "Location", 14, FontStyle.Normal, 18f);
            var flight = AddLayoutText(contentRt, "Flight", 16, FontStyle.Bold, 22f);
            var phase = AddLayoutText(contentRt, "Phase", 15, FontStyle.Normal, 22f);
            var clock = AddLayoutText(contentRt, "Clock", 13, FontStyle.Normal, 20f);
            var cash = AddLayoutText(contentRt, "Cash", 13, FontStyle.Normal, 20f);
            var finance = AddLayoutText(contentRt, "Finance", 13, FontStyle.Normal, 20f);
            var warning = AddLayoutText(contentRt, "Warning", 13, FontStyle.Bold, 20f);
            warning.color = AirsideTheme.SafetyYellow;

            var turnaroundBlock = new GameObject("Turnaround", typeof(RectTransform));
            turnaroundBlock.transform.SetParent(contentRt, false);
            turnaroundBlock.AddComponent<LayoutElement>().preferredHeight = 90f;
            var turnaroundLayout = turnaroundBlock.AddComponent<VerticalLayoutGroup>();
            turnaroundLayout.spacing = 4f;
            turnaroundLayout.childControlHeight = true;
            turnaroundLayout.childControlWidth = true;
            turnaroundLayout.childForceExpandHeight = false;
            turnaroundLayout.childForceExpandWidth = true;
            var turnaround = AddLayoutText(turnaroundBlock.GetComponent<RectTransform>(), "Tasks", 12, FontStyle.Normal, 72f);
            var priority = AddLayoutButton(turnaroundBlock.GetComponent<RectTransform>(), "Hire priority crew", 190f, 28f, AirsideTheme.CoastalBlue);
            var priorityLabel = priority.GetComponentInChildren<Text>();

            var schedule = AddLayoutText(contentRt, "Schedule", 13, FontStyle.Normal, 20f);
            var staffing = AddLayoutText(contentRt, "Staffing", 13, FontStyle.Normal, 20f);
            var earlyHint = AddLayoutText(contentRt, "Early hint", 13, FontStyle.Normal, 22f);

            var crewRow = new GameObject("Crew row", typeof(RectTransform));
            crewRow.transform.SetParent(contentRt, false);
            crewRow.AddComponent<LayoutElement>().preferredHeight = 28f;
            var crewLayout = crewRow.AddComponent<HorizontalLayoutGroup>();
            crewLayout.spacing = 8f;
            crewLayout.childControlHeight = true;
            crewLayout.childControlWidth = false;
            crewLayout.childForceExpandHeight = true;
            crewLayout.childForceExpandWidth = false;
            var hireCrew = AddLayoutButton(crewRow.GetComponent<RectTransform>(), "Hire crew", 150f, 28f, AirsideTheme.CoastalBlue, flexibleWidth: false);
            var releaseCrew = AddLayoutButton(crewRow.GetComponent<RectTransform>(), "Release crew", 110f, 28f, AirsideTheme.Tarmac, flexibleWidth: false);

            var stands = AddLayoutText(contentRt, "Stands", 13, FontStyle.Normal, 20f);
            var buildStand = AddLayoutButton(contentRt, "Build stand 3", 220f, 28f, AirsideTheme.CoastalBlue);
            var buildStandLabel = buildStand.GetComponentInChildren<Text>();

            var research = AddLayoutText(contentRt, "Research", 13, FontStyle.Normal, 20f);
            var researchTrackGo = new GameObject("Research track", typeof(RectTransform));
            researchTrackGo.transform.SetParent(contentRt, false);
            researchTrackGo.AddComponent<LayoutElement>().preferredHeight = 10f;
            var trackImage = researchTrackGo.AddComponent<Image>();
            trackImage.color = new Color(AirsideTheme.Tarmac.r, AirsideTheme.Tarmac.g, AirsideTheme.Tarmac.b, 0.9f);
            trackImage.raycastTarget = false;
            var researchFillGo = new GameObject("Research fill", typeof(RectTransform));
            researchFillGo.transform.SetParent(researchTrackGo.transform, false);
            var researchFillRt = researchFillGo.GetComponent<RectTransform>();
            researchFillRt.anchorMin = new Vector2(0f, 0f);
            researchFillRt.anchorMax = new Vector2(0f, 1f);
            researchFillRt.pivot = new Vector2(0f, 0.5f);
            researchFillRt.anchoredPosition = Vector2.zero;
            researchFillRt.sizeDelta = new Vector2(0f, 0f);
            var researchFill = researchFillGo.AddComponent<Image>();
            researchFill.color = AirsideTheme.CoastalBlue;
            researchFill.raycastTarget = false;
            var researchButton = AddLayoutButton(contentRt, "Start research", 260f, 28f, AirsideTheme.CoastalBlue);
            var researchButtonLabel = researchButton.GetComponentInChildren<Text>();

            var coach = AddLayoutText(contentRt, "Coach", 14, FontStyle.Bold, 28f);
            coach.color = AirsideTheme.SafetyYellow;
            var controls = AddLayoutText(contentRt, "Controls", 12, FontStyle.Normal, 20f);

            var waitRow = new GameObject("Wait row", typeof(RectTransform));
            waitRow.transform.SetParent(contentRt, false);
            waitRow.AddComponent<LayoutElement>().preferredHeight = 36f;
            var waitLayout = waitRow.AddComponent<VerticalLayoutGroup>();
            waitLayout.spacing = 4f;
            waitLayout.childControlHeight = true;
            waitLayout.childControlWidth = true;
            waitLayout.childForceExpandHeight = false;
            waitLayout.childForceExpandWidth = true;
            var waitLabel = AddLayoutText(waitRow.GetComponent<RectTransform>(), "Wait label", 12, FontStyle.Normal, 16f);
            waitLabel.text = "Waiting for first airline offer…";
            var waitTrackGo = new GameObject("Wait track", typeof(RectTransform));
            waitTrackGo.transform.SetParent(waitRow.transform, false);
            waitTrackGo.AddComponent<LayoutElement>().preferredHeight = 10f;
            var waitTrackImage = waitTrackGo.AddComponent<Image>();
            waitTrackImage.color = new Color(AirsideTheme.Tarmac.r, AirsideTheme.Tarmac.g, AirsideTheme.Tarmac.b, 0.9f);
            waitTrackImage.raycastTarget = false;
            var waitFillGo = new GameObject("Wait fill", typeof(RectTransform));
            waitFillGo.transform.SetParent(waitTrackGo.transform, false);
            var waitFillRt = waitFillGo.GetComponent<RectTransform>();
            waitFillRt.anchorMin = new Vector2(0f, 0f);
            waitFillRt.anchorMax = new Vector2(0f, 1f);
            waitFillRt.pivot = new Vector2(0f, 0.5f);
            waitFillRt.anchoredPosition = Vector2.zero;
            waitFillRt.sizeDelta = new Vector2(0f, 0f);
            var waitFill = waitFillGo.AddComponent<Image>();
            waitFill.color = AirsideTheme.CoastalBlue;
            waitFill.raycastTarget = false;

            var offer = BuildPanel(root, "Offer panel", new Vector2(-22f, -22f), new Vector2(340f, 196f), anchorTopRight: true);
            var offerAccentGo = new GameObject("Offer accent", typeof(RectTransform));
            offerAccentGo.transform.SetParent(offer, false);
            var accentRt = offerAccentGo.GetComponent<RectTransform>();
            accentRt.anchorMin = new Vector2(0f, 0f);
            accentRt.anchorMax = new Vector2(0f, 1f);
            accentRt.pivot = new Vector2(0f, 0.5f);
            accentRt.anchoredPosition = Vector2.zero;
            accentRt.sizeDelta = new Vector2(4f, 0f);
            var offerAccent = offerAccentGo.AddComponent<Image>();
            offerAccent.color = AirsideTheme.SafetyYellow;
            offerAccent.raycastTarget = false;
            var offerTitle = AddText(offer, "Offer title", 16, FontStyle.Bold, new Vector2(18f, -16f), new Vector2(300f, 24f));
            offerTitle.color = AirsideTheme.SafetyYellow;
            var offerBody = AddText(offer, "Offer body", 14, FontStyle.Normal, new Vector2(18f, -48f), new Vector2(300f, 72f));
            var offerStatus = AddText(offer, "Offer status", 13, FontStyle.Normal, new Vector2(18f, -124f), new Vector2(300f, 20f));
            var accept = AddButton(offer, "Accept", new Vector2(18f, -152f), new Vector2(190f, 32f), AirsideTheme.CoastalBlue);
            var decline = AddButton(offer, "Decline", new Vector2(220f, -152f), new Vector2(100f, 32f), AirsideTheme.Tarmac);
            var acceptLabel = accept.GetComponentInChildren<Text>();

            var toast = BuildPanel(root, "Ops toast", new Vector2(0f, 28f), new Vector2(440f, 52f));
            toast.anchorMin = new Vector2(0.5f, 0f);
            toast.anchorMax = new Vector2(0.5f, 0f);
            toast.pivot = new Vector2(0.5f, 0f);
            toast.anchoredPosition = new Vector2(0f, 28f);
            var toastText = AddText(toast, "Toast text", 15, FontStyle.Bold, new Vector2(18f, -14f), new Vector2(404f, 26f));
            toastText.alignment = TextAnchor.MiddleCenter;
            toastText.color = AirsideTheme.ClearGreen;
            toast.gameObject.SetActive(false);

            var researchToast = BuildPanel(root, "Research toast", new Vector2(0f, -18f), new Vector2(520f, 64f), anchorTopCenter: true);
            var researchToastText = AddText(researchToast, "Research toast text", 16, FontStyle.Bold, new Vector2(20f, -18f), new Vector2(480f, 28f));
            researchToastText.alignment = TextAnchor.MiddleCenter;
            researchToastText.color = AirsideTheme.ClearGreen;
            researchToast.gameObject.SetActive(false);

            var savePanel = BuildPanel(root, "Save indicator", new Vector2(-24f, 24f), new Vector2(110f, 36f));
            savePanel.anchorMin = new Vector2(1f, 0f);
            savePanel.anchorMax = new Vector2(1f, 0f);
            savePanel.pivot = new Vector2(1f, 0f);
            savePanel.anchoredPosition = new Vector2(-24f, 24f);
            var saveText = AddText(savePanel, "Saved", 14, FontStyle.Bold, new Vector2(16f, -8f), new Vector2(78f, 22f));
            saveText.text = "Saved";
            saveText.color = AirsideTheme.ClearGreen;
            savePanel.gameObject.SetActive(false);

            offer.gameObject.SetActive(false);

            var ops = BuildPanel(root, "Ops panel", new Vector2(-22f, -22f), new Vector2(340f, 260f), anchorTopRight: true);
            var opsTitle = AddText(ops, "Ops title", 16, FontStyle.Bold, new Vector2(18f, -14f), new Vector2(300f, 24f));
            opsTitle.text = "OPERATIONS";
            var opsSummary = AddText(ops, "Ops summary", 12, FontStyle.Normal, new Vector2(18f, -40f), new Vector2(304f, 36f));
            var opsBody = AddText(ops, "Ops body", 12, FontStyle.Normal, new Vector2(18f, -78f), new Vector2(304f, 140f));
            var reportBlock = new GameObject("Daily report", typeof(RectTransform));
            reportBlock.transform.SetParent(ops, false);
            var reportRt = reportBlock.GetComponent<RectTransform>();
            reportRt.anchorMin = new Vector2(0f, 0f);
            reportRt.anchorMax = new Vector2(1f, 0f);
            reportRt.pivot = new Vector2(0.5f, 0f);
            reportRt.anchoredPosition = new Vector2(0f, 8f);
            reportRt.sizeDelta = new Vector2(-16f, 96f);
            var reportBg = reportBlock.AddComponent<Image>();
            var reportInk = AirsideTheme.RunwayInk;
            reportInk.a = 0.55f;
            reportBg.color = reportInk;
            reportBg.raycastTarget = false;
            var reportTitle = AddText(reportRt, "Report title", 13, FontStyle.Bold, new Vector2(10f, -8f), new Vector2(280f, 18f));
            reportTitle.text = "DAILY REPORT";
            var reportBody = AddText(reportRt, "Report body", 11, FontStyle.Normal, new Vector2(10f, -28f), new Vector2(290f, 60f));
            reportBlock.SetActive(false);
            left.gameObject.SetActive(true);

            var hud = new AirsideCanvasHud(
                root, left, offer, ops, opsSummary, opsBody, reportTitle, reportBody, reportBlock, toast,
                location, flight, phase, clock, cash, finance, warning,
                turnaround, turnaroundBlock, priority, priorityLabel,
                schedule, staffing, earlyHint,
                crewRow, hireCrew, releaseCrew,
                stands, buildStand, buildStandLabel,
                research, researchFill, researchTrackGo, researchButton, researchButtonLabel,
                coach, controls,
                waitFill, waitRow, waitLabel,
                offerTitle, offerBody, offerStatus,
                accept, decline, acceptLabel, toastText, offerAccent);
            hud._researchToastPanel = researchToast;
            hud._researchToastText = researchToastText;
            hud._savePanel = savePanel;
            hud._saveText = saveText;

            accept.onClick.AddListener(() => hud._onAccept?.Invoke());
            decline.onClick.AddListener(() => hud._onDecline?.Invoke());
            priority.onClick.AddListener(() => hud._onPriorityCrew?.Invoke());
            hireCrew.onClick.AddListener(() => hud._onHireCrew?.Invoke());
            releaseCrew.onClick.AddListener(() => hud._onReleaseCrew?.Invoke());
            buildStand.onClick.AddListener(() => hud._onBuildStand?.Invoke());
            researchButton.onClick.AddListener(() => hud._onStartResearch?.Invoke());
            hud.EnsureOverlays(root);

            if (UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var eventGo = new GameObject("EventSystem");
                eventGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            return hud;
        }

        private void EnsureOverlays(RectTransform root)
        {
            // Full-screen briefing splash + card.
            var splashGo = new GameObject("Briefing splash", typeof(RectTransform));
            splashGo.transform.SetParent(root, false);
            var splashRt = splashGo.GetComponent<RectTransform>();
            Stretch(splashRt);
            _briefingSplash = splashGo.AddComponent<Image>();
            _briefingSplash.color = new Color(0.05f, 0.07f, 0.09f, 1f);
            _briefingSplash.raycastTarget = true;
            var splashTex = AirsideTheme.SplashDawn;
            if (splashTex != null)
            {
                _briefingSplash.sprite = Sprite.Create(
                    splashTex, new Rect(0, 0, splashTex.width, splashTex.height), new Vector2(0.5f, 0.5f));
                _briefingSplash.preserveAspect = false;
                _briefingSplash.type = Image.Type.Simple;
            }

            var briefingCard = BuildPanel(splashRt, "Briefing card", Vector2.zero, new Vector2(500f, 360f), anchorTopCenter: true);
            briefingCard.anchorMin = new Vector2(0.5f, 0.5f);
            briefingCard.anchorMax = new Vector2(0.5f, 0.5f);
            briefingCard.pivot = new Vector2(0.5f, 0.5f);
            briefingCard.anchoredPosition = Vector2.zero;
            var wordmark = AirsideTheme.WordmarkLight;
            if (wordmark != null)
            {
                var wmGo = new GameObject("Briefing wordmark", typeof(RectTransform));
                wmGo.transform.SetParent(briefingCard, false);
                var wmRt = wmGo.GetComponent<RectTransform>();
                wmRt.anchorMin = new Vector2(0f, 1f);
                wmRt.anchorMax = new Vector2(0f, 1f);
                wmRt.pivot = new Vector2(0f, 1f);
                wmRt.anchoredPosition = new Vector2(24f, -14f);
                wmRt.sizeDelta = new Vector2(280f, 70f);
                var wm = wmGo.AddComponent<Image>();
                wm.sprite = Sprite.Create(wordmark, new Rect(0, 0, wordmark.width, wordmark.height), new Vector2(0.5f, 0.5f));
                wm.preserveAspect = true;
                wm.raycastTarget = false;
            }
            else
            {
                var brand = AddText(briefingCard, "Briefing brand", 26, FontStyle.Bold, new Vector2(24f, -18f), new Vector2(450f, 34f));
                brand.text = "AIRSIDE";
            }

            _briefingBody = AddText(briefingCard, "Briefing body", 15, FontStyle.Normal, new Vector2(24f, -96f), new Vector2(450f, 200f));
            var begin = AddButton(briefingCard, "Begin operations", new Vector2(140f, -318f), new Vector2(220f, 30f), AirsideTheme.CoastalBlue);
            begin.onClick.AddListener(() => _onBeginOperations?.Invoke());
            _briefingPanel = splashRt;
            splashGo.SetActive(false);

            // Pause dimmer + card.
            var pauseGo = new GameObject("Pause overlay", typeof(RectTransform));
            pauseGo.transform.SetParent(root, false);
            var pauseRt = pauseGo.GetComponent<RectTransform>();
            Stretch(pauseRt);
            var dim = pauseGo.AddComponent<Image>();
            dim.color = new Color(0.05f, 0.07f, 0.09f, 0.45f);
            dim.raycastTarget = true;
            var pauseCard = BuildPanel(pauseRt, "Pause card", Vector2.zero, new Vector2(320f, 140f));
            pauseCard.anchorMin = new Vector2(0.5f, 0.5f);
            pauseCard.anchorMax = new Vector2(0.5f, 0.5f);
            pauseCard.pivot = new Vector2(0.5f, 0.5f);
            pauseCard.anchoredPosition = Vector2.zero;
            var pauseTitle = AddText(pauseCard, "Pause title", 26, FontStyle.Bold, new Vector2(24f, -18f), new Vector2(270f, 36f));
            pauseTitle.text = "PAUSED";
            var pauseHint = AddText(pauseCard, "Pause hint", 15, FontStyle.Bold, new Vector2(24f, -52f), new Vector2(270f, 22f));
            pauseHint.text = "Space to resume";
            pauseHint.color = AirsideTheme.SafetyYellow;
            var reset = AddButton(pauseCard, "Start new airport", new Vector2(50f, -88f), new Vector2(220f, 30f), AirsideTheme.Tarmac);
            reset.onClick.AddListener(() => _onResetAirport?.Invoke());
            _pausePanel = pauseRt;
            pauseGo.SetActive(false);

            // Away summary card.
            var awayGo = new GameObject("Away overlay", typeof(RectTransform));
            awayGo.transform.SetParent(root, false);
            var awayRt = awayGo.GetComponent<RectTransform>();
            Stretch(awayRt);
            var awayDim = awayGo.AddComponent<Image>();
            awayDim.color = new Color(0.05f, 0.07f, 0.09f, 0.55f);
            awayDim.raycastTarget = true;
            var awayCard = BuildPanel(awayRt, "Away card", Vector2.zero, new Vector2(460f, 348f));
            awayCard.anchorMin = new Vector2(0.5f, 0.5f);
            awayCard.anchorMax = new Vector2(0.5f, 0.5f);
            awayCard.pivot = new Vector2(0.5f, 0.5f);
            awayCard.anchoredPosition = Vector2.zero;
            var awayTitle = AddText(awayCard, "Away title", 26, FontStyle.Bold, new Vector2(24f, -18f), new Vector2(410f, 34f));
            awayTitle.text = "AIRSIDE";
            var awaySub = AddText(awayCard, "Away subtitle", 16, FontStyle.Bold, new Vector2(24f, -52f), new Vector2(410f, 22f));
            awaySub.text = "Welcome back to operations";
            _awayBody = AddText(awayCard, "Away body", 14, FontStyle.Normal, new Vector2(24f, -86f), new Vector2(410f, 200f));
            var continueBtn = AddButton(awayCard, "Continue operations", new Vector2(130f, -292f), new Vector2(200f, 32f), AirsideTheme.CoastalBlue);
            continueBtn.onClick.AddListener(() => _onContinueAway?.Invoke());
            _awayPanel = awayRt;
            awayGo.SetActive(false);

            // Insolvency card.
            var insolventGo = new GameObject("Insolvency overlay", typeof(RectTransform));
            insolventGo.transform.SetParent(root, false);
            var insolventRt = insolventGo.GetComponent<RectTransform>();
            Stretch(insolventRt);
            var insolventDim = insolventGo.AddComponent<Image>();
            insolventDim.color = new Color(0.08f, 0.05f, 0.05f, 0.6f);
            insolventDim.raycastTarget = true;
            var insolventCard = BuildPanel(insolventRt, "Insolvency card", Vector2.zero, new Vector2(460f, 290f));
            insolventCard.anchorMin = new Vector2(0.5f, 0.5f);
            insolventCard.anchorMax = new Vector2(0.5f, 0.5f);
            insolventCard.pivot = new Vector2(0.5f, 0.5f);
            insolventCard.anchoredPosition = Vector2.zero;
            var insolventTitle = AddText(insolventCard, "Insolvency title", 26, FontStyle.Bold, new Vector2(24f, -22f), new Vector2(410f, 34f));
            insolventTitle.text = "AIRSIDE";
            var insolventHead = AddText(insolventCard, "Insolvency head", 17, FontStyle.Bold, new Vector2(24f, -58f), new Vector2(410f, 28f));
            insolventHead.text = "Airport declared insolvent";
            insolventHead.color = AirsideTheme.SignalRed;
            _insolvencyBody = AddText(insolventCard, "Insolvency body", 14, FontStyle.Normal, new Vector2(24f, -96f), new Vector2(410f, 110f));
            var newAirport = AddButton(insolventCard, "Start a new airport", new Vector2(100f, -220f), new Vector2(260f, 36f), AirsideTheme.CoastalBlue);
            newAirport.onClick.AddListener(() => _onResetAirport?.Invoke());
            _insolvencyPanel = insolventRt;
            insolventGo.SetActive(false);
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
            if (_briefingPanel != null)
            {
                _briefingPanel.gameObject.SetActive(showBriefing && !showAway && !showInsolvency);
                if (showBriefing && _briefingBody != null)
                {
                    _briefingBody.text =
                        "You run this regional airport\n\n" +
                        $"Aircraft move on their own. Your job is cash, reputation and capacity at {locationName}.\n\n" +
                        "First useful decision\n" +
                        $"In about {firstOfferAfterSeconds} seconds an airline will offer a scheduled route. Accept it to earn money on every completed flight.\n\n" +
                        "Watch OPERATIONS on the right. Watch cash and delays on the left. Press Enter to Accept the first offer.\n\n" +
                        "Space / Enter to begin  ·  Tab = 4× speed";
                }
            }

            if (_awayPanel != null)
            {
                _awayPanel.gameObject.SetActive(showAway && !showInsolvency);
                if (showAway && _awayBody != null)
                    _awayBody.text = awayBody ?? string.Empty;
            }

            if (_insolvencyPanel != null)
            {
                _insolvencyPanel.gameObject.SetActive(showInsolvency);
                if (showInsolvency && _insolvencyBody != null)
                    _insolvencyBody.text = insolvencyBody ?? string.Empty;
            }

            if (_pausePanel != null)
                _pausePanel.gameObject.SetActive(showPause && !showBriefing && !showAway && !showInsolvency);

            // Hide gameplay panels while any full-screen overlay owns the screen.
            var gameplay = !showBriefing && !showAway && !showInsolvency;
            if (_leftPanel != null)
                _leftPanel.gameObject.SetActive(gameplay);
            if (_opsPanel != null)
                _opsPanel.gameObject.SetActive(gameplay);
            if (!gameplay)
            {
                _offerPanel.gameObject.SetActive(false);
                _toastPanel.gameObject.SetActive(false);
                if (_researchToastPanel != null)
                    _researchToastPanel.gameObject.SetActive(false);
                if (_savePanel != null)
                    _savePanel.gameObject.SetActive(false);
            }
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
            _locationText.text = locationLine;
            _flightText.text = flightLine;
            _phaseText.text = phaseLine;
            _clockText.text = clockLine;
            _clockText.color = clockColor;
            _cashText.text = cashLine;
            _cashText.color = cashColor;
            _financeText.text = financeLine;
            _financeText.color = financeColor;

            var hasWarning = !string.IsNullOrEmpty(warningLine);
            _warningText.gameObject.SetActive(hasWarning);
            if (hasWarning)
            {
                _warningText.text = warningLine;
                _warningText.color = warningColor;
            }

            _turnaroundBlock.SetActive(showTurnaround);
            if (showTurnaround)
            {
                _turnaroundText.text = turnaroundLines ?? string.Empty;
                var lines = string.IsNullOrEmpty(turnaroundLines) ? 1 : turnaroundLines.Split('\n').Length;
                _turnaroundBlock.GetComponent<LayoutElement>().preferredHeight = Mathf.Clamp(18f + lines * 16f + (priorityVisible ? 34f : 0f), 40f, 160f);
                _priorityButton.gameObject.SetActive(priorityVisible);
                if (priorityVisible)
                {
                    _priorityButton.interactable = priorityInteractable;
                    _priorityLabel.text = priorityLabel;
                }
            }

            _scheduleText.text = scheduleLine;
            _scheduleText.color = scheduleColor;
            _staffingText.text = staffingLine;
            _staffingText.color = staffingColor;

            _earlyHintText.gameObject.SetActive(earlySession);
            if (earlySession)
                _earlyHintText.text = earlyHint;

            _crewRow.SetActive(!earlySession);
            _standsText.gameObject.SetActive(!earlySession);
            _buildStandButton.gameObject.SetActive(!earlySession && buildStandVisible);
            _researchText.gameObject.SetActive(!earlySession);
            _researchTrack.SetActive(!earlySession && researchProgressVisible);
            _researchButton.gameObject.SetActive(!earlySession && researchButtonVisible);

            if (!earlySession)
            {
                _hireCrewButton.interactable = hireInteractable;
                _hireCrewButton.GetComponentInChildren<Text>().text = hireLabel;
                _releaseCrewButton.interactable = releaseInteractable;
                _standsText.text = standsLine;
                if (buildStandVisible)
                {
                    _buildStandButton.interactable = buildStandInteractable;
                    _buildStandLabel.text = buildStandLabel;
                }

                _researchText.text = researchLine;
                if (researchProgressVisible)
                {
                    var trackRt = _researchTrack.GetComponent<RectTransform>();
                    var trackWidth = Mathf.Max(40f, trackRt.rect.width);
                    if (trackWidth < 40f)
                        trackWidth = 370f;
                    _researchFill.rectTransform.sizeDelta = new Vector2(trackWidth * Mathf.Clamp01(researchProgress01), 0f);
                }

                if (researchButtonVisible)
                {
                    _researchButton.interactable = researchButtonInteractable;
                    _researchButtonLabel.text = researchButtonLabel;
                }
            }

            _coachText.text = coachLine;
            _coachText.color = coachUrgent ? AirsideTheme.SafetyYellow : AirsideTheme.Cloud;
            _controlsText.text = controlsLine;

            _waitRow.SetActive(showWaitMeter);
            if (showWaitMeter)
            {
                _waitLabel.text = waitLabel;
                _waitFill.rectTransform.sizeDelta = new Vector2(370f * Mathf.Clamp01(waitProgress01), 0f);
            }

            // Prefer content height; clamp so the world stays readable.
            LayoutRebuilder.ForceRebuildLayoutImmediate(_leftPanel);
            var preferred = LayoutUtility.GetPreferredHeight(_leftPanel.Find("Content") as RectTransform);
            _leftPanel.sizeDelta = new Vector2(410f, Mathf.Clamp(preferred + 24f, 360f, 620f));
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
            _offerPanel.gameObject.SetActive(visible);
            if (!visible)
            {
                RepositionOpsBelowOffer(false, firstDecision: false);
                return;
            }

            _offerTitle.text = title;
            _offerTitle.color = firstDecision ? AirsideTheme.SafetyYellow : AirsideTheme.Cloud;
            _offerBody.text = body;
            _offerStatus.text = status;
            _offerStatus.color = statusCaution ? AirsideTheme.SafetyYellow : AirsideTheme.Cloud;
            _acceptButton.interactable = canAccept;
            _acceptLabel.text = acceptLabel;
            _offerAccent.gameObject.SetActive(firstDecision);
            if (firstDecision)
            {
                var pulse = 0.45f + 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f));
                var c = AirsideTheme.SafetyYellow;
                c.a = pulse;
                _offerAccent.color = c;
            }

            RepositionOpsBelowOffer(true, firstDecision);
        }

        public void SyncOps(string summary, string body, string reportBodyOrNull)
        {
            _opsSummary.text = summary ?? string.Empty;
            _opsBody.text = body ?? string.Empty;
            var hasReport = !string.IsNullOrEmpty(reportBodyOrNull);
            _reportBlock.SetActive(hasReport);
            if (hasReport)
                _reportBody.text = reportBodyOrNull;

            var lineCount = 1;
            if (!string.IsNullOrEmpty(body))
                lineCount = body.Split('\n').Length;
            var bodyHeight = Mathf.Clamp(18f + lineCount * 15f, 60f, 180f);
            _opsBody.rectTransform.sizeDelta = new Vector2(304f, bodyHeight);
            var reportExtra = hasReport ? 108f : 12f;
            _opsPanel.sizeDelta = new Vector2(340f, 78f + bodyHeight + reportExtra);
        }

        private void RepositionOpsBelowOffer(bool offerVisible, bool firstDecision)
        {
            var offerHeight = offerVisible ? (firstDecision ? 196f : 156f) : 0f;
            var top = -22f - (offerVisible ? offerHeight + 12f : 0f);
            _opsPanel.anchoredPosition = new Vector2(-22f, top);
        }

        public void SyncToast(string message, bool visible)
        {
            _toastPanel.gameObject.SetActive(visible);
            if (visible)
                _toastText.text = message ?? string.Empty;
        }

        public void SyncResearchToast(string message, bool visible)
        {
            if (_researchToastPanel == null)
                return;
            _researchToastPanel.gameObject.SetActive(visible);
            if (visible && _researchToastText != null)
                _researchToastText.text = message ?? string.Empty;
        }

        public void SyncSaveIndicator(bool visible)
        {
            if (_savePanel == null)
                return;
            _savePanel.gameObject.SetActive(visible);
            if (visible && _saveText != null)
                _saveText.text = "Saved";
        }

        public void SetVisible(bool visible) => _root.gameObject.SetActive(visible);

        /// <summary>
        /// When UI Toolkit owns OPERATIONS + route offer, hide the Canvas copies
        /// so the two systems do not double-draw the right column.
        /// </summary>
        public void SetRightPanelsVisible(bool visible)
        {
            if (_opsPanel != null)
                _opsPanel.gameObject.SetActive(visible);
            if (_offerPanel != null && !visible)
                _offerPanel.gameObject.SetActive(false);
        }

        /// <summary>
        /// When UI Toolkit owns the left status panel, hide the Canvas copy.
        /// </summary>
        public void SetLeftPanelVisible(bool visible)
        {
            if (_leftPanel != null)
                _leftPanel.gameObject.SetActive(visible);
        }

        /// <summary>
        /// When UI Toolkit owns full-screen overlays, hide the Canvas copies.
        /// </summary>
        public void SetOverlaysVisible(bool visible)
        {
            if (visible)
                return;
            if (_briefingPanel != null)
                _briefingPanel.gameObject.SetActive(false);
            if (_pausePanel != null)
                _pausePanel.gameObject.SetActive(false);
            if (_awayPanel != null)
                _awayPanel.gameObject.SetActive(false);
            if (_insolvencyPanel != null)
                _insolvencyPanel.gameObject.SetActive(false);
        }

        private static void AddWordmark(RectTransform parent)
        {
            var wordmark = AirsideTheme.WordmarkLight;
            if (wordmark != null)
            {
                var imageGo = new GameObject("Wordmark", typeof(RectTransform));
                imageGo.transform.SetParent(parent, false);
                imageGo.AddComponent<LayoutElement>().preferredHeight = 40f;
                var image = imageGo.AddComponent<Image>();
                image.sprite = Sprite.Create(wordmark, new Rect(0, 0, wordmark.width, wordmark.height), new Vector2(0.5f, 0.5f));
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.color = Color.white;
            }
            else
            {
                var label = AddLayoutText(parent, "Brand", 22, FontStyle.Bold, 32f);
                label.text = "AIRSIDE";
                label.color = AirsideTheme.Cloud;
            }
        }

        private static RectTransform BuildPanel(
            RectTransform parent,
            string name,
            Vector2 anchoredPos,
            Vector2 size,
            bool anchorTopLeft = false,
            bool anchorTopRight = false,
            bool anchorTopCenter = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            if (anchorTopLeft)
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
            }
            else if (anchorTopRight)
            {
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
            }
            else if (anchorTopCenter)
            {
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
            }

            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var image = go.AddComponent<Image>();
            var ink = AirsideTheme.RunwayInk;
            ink.a = 0.94f;
            image.color = ink;
            image.raycastTarget = true;

            AddEdge(rt, "Edge top", anchoredTop: true, horizontal: true);
            AddEdge(rt, "Edge bottom", anchoredTop: false, horizontal: true);
            AddEdge(rt, "Edge left", anchoredLeft: true, horizontal: false);
            AddEdge(rt, "Edge right", anchoredLeft: false, horizontal: false);
            return rt;
        }

        private static void AddEdge(RectTransform parent, string name, bool horizontal, bool anchoredTop = false, bool anchoredLeft = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            if (horizontal)
            {
                rt.anchorMin = new Vector2(0f, anchoredTop ? 1f : 0f);
                rt.anchorMax = new Vector2(1f, anchoredTop ? 1f : 0f);
                rt.pivot = new Vector2(0.5f, anchoredTop ? 1f : 0f);
                rt.sizeDelta = new Vector2(0f, 2f);
                rt.anchoredPosition = Vector2.zero;
            }
            else
            {
                rt.anchorMin = new Vector2(anchoredLeft ? 0f : 1f, 0f);
                rt.anchorMax = new Vector2(anchoredLeft ? 0f : 1f, 1f);
                rt.pivot = new Vector2(anchoredLeft ? 0f : 1f, 0.5f);
                rt.sizeDelta = new Vector2(2f, 0f);
                rt.anchoredPosition = Vector2.zero;
            }

            var image = go.AddComponent<Image>();
            image.color = new Color(AirsideTheme.CoastalBlue.r, AirsideTheme.CoastalBlue.g, AirsideTheme.CoastalBlue.b, 0.55f);
            image.raycastTarget = false;
        }

        private static Text AddLayoutText(RectTransform parent, string name, int fontSize, FontStyle style, float preferredHeight)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().preferredHeight = preferredHeight;
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = AirsideTheme.Cloud;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static Button AddLayoutButton(
            RectTransform parent,
            string label,
            float width,
            float height,
            Color color,
            bool flexibleWidth = true)
        {
            var go = new GameObject(label + " button", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.preferredWidth = width;
            le.flexibleWidth = flexibleWidth ? 0f : 0f;
            var image = go.AddComponent<Image>();
            image.color = color;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            Stretch(textRt);
            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 13;
            text.fontStyle = FontStyle.Bold;
            text.color = AirsideTheme.Cloud;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;
            text.raycastTarget = false;
            return button;
        }

        private static Text AddText(RectTransform parent, string name, int fontSize, FontStyle style, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = AirsideTheme.Cloud;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static Button AddButton(RectTransform parent, string label, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(label + " button", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var image = go.AddComponent<Image>();
            image.color = color;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var text = AddText(rt, "Label", 14, FontStyle.Bold, new Vector2(8f, -4f), new Vector2(size.x - 16f, size.y - 8f));
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return button;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
