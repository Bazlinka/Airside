using System;
using UnityEngine;
using UnityEngine.UI;

namespace Airside.Presentation
{
    /// <summary>
    /// Decision 0025 item 6 foundation — runtime uGUI Canvas HUD for the primary
    /// first-session surfaces (status strip, route offer, ops toast). Built in code
    /// so packaged builds do not depend on editor-imported PanelSettings. Remaining
    /// dense IMGUI (operations log, research, briefing overlays) stays in
    /// <see cref="AirsidePrototype"/> until the full Toolkit/uGUI migration.
    /// </summary>
    public sealed class AirsideCanvasHud
    {
        private readonly RectTransform _root;
        private readonly RectTransform _leftPanel;
        private readonly RectTransform _offerPanel;
        private readonly RectTransform _toastPanel;
        private readonly Text _locationText;
        private readonly Text _flightText;
        private readonly Text _phaseText;
        private readonly Text _clockText;
        private readonly Text _cashText;
        private readonly Text _financeText;
        private readonly Text _coachText;
        private readonly Image _waitFill;
        private readonly GameObject _waitRow;
        private readonly Text _offerTitle;
        private readonly Text _offerBody;
        private readonly Text _offerStatus;
        private readonly Button _acceptButton;
        private readonly Button _declineButton;
        private readonly Text _acceptLabel;
        private readonly Text _toastText;
        private readonly Image _offerAccent;
        private Action _onAccept;
        private Action _onDecline;

        private AirsideCanvasHud(
            RectTransform root,
            RectTransform leftPanel,
            RectTransform offerPanel,
            RectTransform toastPanel,
            Text locationText,
            Text flightText,
            Text phaseText,
            Text clockText,
            Text cashText,
            Text financeText,
            Text coachText,
            Image waitFill,
            GameObject waitRow,
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
            _toastPanel = toastPanel;
            _locationText = locationText;
            _flightText = flightText;
            _phaseText = phaseText;
            _clockText = clockText;
            _cashText = cashText;
            _financeText = financeText;
            _coachText = coachText;
            _waitFill = waitFill;
            _waitRow = waitRow;
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

            var left = BuildPanel(root, "Status panel", new Vector2(22f, -22f), new Vector2(410f, 360f), anchorTopLeft: true);
            var location = AddText(left, "Location", 18, FontStyle.Normal, new Vector2(20f, -56f), new Vector2(370f, 22f));
            var flight = AddText(left, "Flight", 17, FontStyle.Bold, new Vector2(20f, -84f), new Vector2(370f, 24f));
            var phase = AddText(left, "Phase", 17, FontStyle.Normal, new Vector2(20f, -112f), new Vector2(370f, 24f));
            var clock = AddText(left, "Clock", 14, FontStyle.Normal, new Vector2(20f, -140f), new Vector2(370f, 22f));
            var cash = AddText(left, "Cash", 14, FontStyle.Normal, new Vector2(20f, -166f), new Vector2(370f, 22f));
            var finance = AddText(left, "Finance", 14, FontStyle.Normal, new Vector2(20f, -190f), new Vector2(370f, 22f));
            var coach = AddText(left, "Coach", 15, FontStyle.Bold, new Vector2(20f, -230f), new Vector2(370f, 44f));
            coach.color = AirsideTheme.SafetyYellow;

            var wordmarkLabel = AddText(left, "Brand", 22, FontStyle.Bold, new Vector2(20f, -18f), new Vector2(370f, 32f));
            wordmarkLabel.text = "AIRSIDE";
            wordmarkLabel.color = AirsideTheme.Cloud;
            var wordmark = AirsideTheme.WordmarkLight;
            if (wordmark != null)
            {
                wordmarkLabel.gameObject.SetActive(false);
                var imageGo = new GameObject("Wordmark", typeof(RectTransform));
                imageGo.transform.SetParent(left, false);
                var imageRt = imageGo.GetComponent<RectTransform>();
                imageRt.anchorMin = new Vector2(0f, 1f);
                imageRt.anchorMax = new Vector2(0f, 1f);
                imageRt.pivot = new Vector2(0f, 1f);
                imageRt.anchoredPosition = new Vector2(16f, -12f);
                imageRt.sizeDelta = new Vector2(240f, 40f);
                var image = imageGo.AddComponent<Image>();
                image.sprite = Sprite.Create(wordmark, new Rect(0, 0, wordmark.width, wordmark.height), new Vector2(0.5f, 0.5f));
                image.preserveAspect = true;
                image.raycastTarget = false;
            }

            var waitRow = new GameObject("Wait row", typeof(RectTransform));
            waitRow.transform.SetParent(left, false);
            var waitRt = waitRow.GetComponent<RectTransform>();
            waitRt.anchorMin = new Vector2(0f, 1f);
            waitRt.anchorMax = new Vector2(0f, 1f);
            waitRt.pivot = new Vector2(0f, 1f);
            waitRt.anchoredPosition = new Vector2(20f, -290f);
            waitRt.sizeDelta = new Vector2(370f, 36f);
            var waitLabel = AddText(waitRt, "Wait label", 13, FontStyle.Normal, new Vector2(0f, 0f), new Vector2(370f, 18f));
            waitLabel.name = "Wait label";
            waitLabel.text = "Waiting for first airline offer…";
            var track = new GameObject("Wait track", typeof(RectTransform));
            track.transform.SetParent(waitRt, false);
            var trackRt = track.GetComponent<RectTransform>();
            trackRt.anchorMin = new Vector2(0f, 1f);
            trackRt.anchorMax = new Vector2(0f, 1f);
            trackRt.pivot = new Vector2(0f, 1f);
            trackRt.anchoredPosition = new Vector2(0f, -22f);
            trackRt.sizeDelta = new Vector2(370f, 10f);
            var trackImage = track.AddComponent<Image>();
            trackImage.color = new Color(AirsideTheme.Tarmac.r, AirsideTheme.Tarmac.g, AirsideTheme.Tarmac.b, 0.9f);
            trackImage.raycastTarget = false;
            var fillGo = new GameObject("Wait fill", typeof(RectTransform));
            fillGo.transform.SetParent(trackRt, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.anchoredPosition = Vector2.zero;
            fillRt.sizeDelta = new Vector2(0f, 0f);
            var fillImage = fillGo.AddComponent<Image>();
            fillImage.color = AirsideTheme.CoastalBlue;
            fillImage.raycastTarget = false;

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

            var toast = BuildPanel(root, "Toast panel", new Vector2(0f, -18f), new Vector2(560f, 56f), anchorTopCenter: true);
            var toastText = AddText(toast, "Toast text", 16, FontStyle.Bold, new Vector2(18f, -14f), new Vector2(524f, 28f));
            toastText.alignment = TextAnchor.MiddleCenter;
            toastText.color = AirsideTheme.ClearGreen;
            toast.gameObject.SetActive(false);
            offer.gameObject.SetActive(false);
            // Phase-1 foundation: offer + toast on Canvas. Status/research stay on IMGUI
            // until the interactive left panel is migrated (hire/research buttons).
            left.gameObject.SetActive(false);

            var hud = new AirsideCanvasHud(
                root, left, offer, toast,
                location, flight, phase, clock, cash, finance, coach,
                fillImage, waitRow,
                offerTitle, offerBody, offerStatus,
                accept, decline, acceptLabel, toastText, offerAccent);

            accept.onClick.AddListener(() => hud._onAccept?.Invoke());
            decline.onClick.AddListener(() => hud._onDecline?.Invoke());

            if (UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var eventGo = new GameObject("EventSystem");
                eventGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            return hud;
        }

        public void BindActions(Action onAccept, Action onDecline)
        {
            _onAccept = onAccept;
            _onDecline = onDecline;
        }

        public void SyncStatus(
            string locationLine,
            string flightLine,
            string phaseLine,
            string clockLine,
            string cashLine,
            Color cashColor,
            string financeLine,
            Color financeColor,
            string coachLine,
            bool coachUrgent,
            bool showWaitMeter,
            string waitLabel,
            float waitProgress01,
            float leftPanelHeight)
        {
            _locationText.text = locationLine;
            _flightText.text = flightLine;
            _phaseText.text = phaseLine;
            _clockText.text = clockLine;
            _cashText.text = cashLine;
            _cashText.color = cashColor;
            _financeText.text = financeLine;
            _financeText.color = financeColor;
            _coachText.text = coachLine;
            _coachText.color = coachUrgent ? AirsideTheme.SafetyYellow : AirsideTheme.Cloud;
            _waitRow.SetActive(showWaitMeter);
            if (showWaitMeter)
            {
                var label = _waitRow.transform.Find("Wait label")?.GetComponent<Text>();
                if (label != null)
                    label.text = waitLabel;
                var fillRt = _waitFill.rectTransform;
                fillRt.sizeDelta = new Vector2(370f * Mathf.Clamp01(waitProgress01), 0f);
            }

            _leftPanel.sizeDelta = new Vector2(410f, leftPanelHeight);
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
                return;

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
        }

        public void SyncToast(string message, bool visible)
        {
            _toastPanel.gameObject.SetActive(visible);
            if (visible)
                _toastText.text = message ?? string.Empty;
        }

        public void SetVisible(bool visible) => _root.gameObject.SetActive(visible);

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

            // Thin Coastal Blue frame.
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
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
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
