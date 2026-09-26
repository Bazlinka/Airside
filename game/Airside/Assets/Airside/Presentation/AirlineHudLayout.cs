using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Unity-facing placement of the Glass Cockpit airline HUD (ADR 0122): the navigation rail,
    /// status capsule, career ring card, live flight tiles, workspace sheet, toasts, radar and
    /// the selected-aircraft card. Every rectangle comes from <see cref="HudShell.Layout"/>, which
    /// is UnityEngine-free so the fits-and-never-overlaps contract is tested headlessly; this type
    /// only converts to <see cref="Rect"/> for IMGUI.
    /// </summary>
    public readonly struct AirlineHudLayout
    {
        public const float Margin = HudShell.Margin;
        public const float SetupWidth = HudShell.SetupWidth;
        public const float SelectedCardWidth = HudShell.SelectedCardWidth;
        public const float SelectedCardHeight = HudShell.SelectedCardHeight;
        public const float MiniMapWidth = HudShell.MiniMapWidth;
        public const float MiniMapHeight = HudShell.MiniMapHeight;
        public const float ToastWidth = HudShell.ToastWidth;
        public const float ToastHeight = HudShell.ToastHeight;

        private AirlineHudLayout(HudShellLayout shell)
        {
            Shell = shell;
        }

        /// <summary>The pure layout this was made from.</summary>
        public HudShellLayout Shell { get; }

        public Rect Rail => ToRect(Shell.Rail);
        public Rect Capsule => ToRect(Shell.Capsule);

        /// <summary>Bottom-left career ring card, or the first-flight guide while it runs.</summary>
        public Rect Objective => ToRect(Shell.Career);

        /// <summary>Top-right live flight tiles. Zero-sized when there is no room.</summary>
        public Rect Operations => ToRect(Shell.Operations);

        /// <summary>The one open workspace sheet.</summary>
        public Rect Workspace => ToRect(Shell.Workspace);

        public Rect Toast => ToRect(Shell.Toast);

        /// <summary>Bottom-right airfield radar. Zero-sized when it would not fit.</summary>
        public Rect MiniMap => ToRect(Shell.MiniMap);

        /// <summary>Bottom-centre selected-aircraft reservation. Zero-sized on very small windows.</summary>
        public Rect SelectedCard => ToRect(Shell.SelectedCard);

        /// <summary>The region modal cards (setup, away summary, help) are centred in.</summary>
        public Rect SetupArea => ToRect(Shell.SetupArea);

        public static AirlineHudLayout Create(HudLayout hud, bool showGuide = false, bool workspaceOpen = false) =>
            new(HudShell.Layout(hud.Viewport.x, hud.Viewport.y, showGuide, workspaceOpen));

        /// <summary>A modal card of the preferred height, centred in <see cref="SetupArea"/>.</summary>
        public Rect SetupPanel(float preferredHeight) =>
            ToRect(HudShell.CentredPanel(Shell.SetupArea, SetupWidth, preferredHeight));

        public static Rect ToRect(HudBox box) => new(box.X, box.Y, box.Width, box.Height);
    }
}
