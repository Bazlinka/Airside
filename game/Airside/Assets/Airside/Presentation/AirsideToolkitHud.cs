using UnityEngine;
using UnityEngine.UIElements;

namespace Airside.Presentation
{
    /// <summary>
    /// Decision 0025 item 6 — first UI Toolkit surface. Owns ops/research toasts
    /// via a runtime <see cref="UIDocument"/> so Bailey can grow Toolkit coverage
    /// without waiting for editor-authored UXML. Canvas keeps panels/overlays.
    /// </summary>
    public sealed class AirsideToolkitHud : MonoBehaviour
    {
        private UIDocument _document;
        private Label _opsToast;
        private Label _researchToast;
        private Label _saveChip;
        private bool _built;

        public bool IsActive => _built && _document != null && _document.rootVisualElement != null;

        public static AirsideToolkitHud Create(Transform host)
        {
            var go = new GameObject("Airside Toolkit HUD");
            go.transform.SetParent(host, false);
            var hud = go.AddComponent<AirsideToolkitHud>();
            hud.Build();
            return hud;
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

            var root = _document.rootVisualElement;
            root.style.flexGrow = 1;
            root.pickingMode = PickingMode.Ignore;

            _researchToast = MakeToastLabel("Research toast");
            _researchToast.style.top = 18;
            _researchToast.style.alignSelf = Align.Center;
            root.Add(_researchToast);

            _opsToast = MakeToastLabel("Ops toast");
            _opsToast.style.bottom = 28;
            _opsToast.style.alignSelf = Align.Center;
            root.Add(_opsToast);

            _saveChip = MakeToastLabel("Save chip");
            _saveChip.style.top = 18;
            _saveChip.style.right = 24;
            _saveChip.style.alignSelf = Align.FlexEnd;
            _saveChip.style.fontSize = 13;
            _saveChip.style.minWidth = 72;
            _saveChip.text = "Saved";
            root.Add(_saveChip);

            SyncToast(string.Empty, false);
            SyncResearchToast(string.Empty, false);
            SyncSaveIndicator(false);
            _built = true;
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
    }
}
