using System;

namespace Airside.Presentation
{
    /// <summary>
    /// Named camera bookmarks for the visual-overhaul Phase 0 baseline
    /// (<c>docs/plans/visual-overhaul-plan.md</c>). No UnityEngine types — the
    /// capture script, camera controller and headless tests share one table.
    /// Individual <c>-airsideOverview*</c> flags still override a resolved view.
    /// </summary>
    public static class AirsideVisualBaselineViews
    {
        public const string ReviewViewFlag = "-airsideReviewView";

        public readonly struct View
        {
            public View(string id, string label, float centerX, float centerZ,
                float distance, float pitch, float yaw)
            {
                Id = id;
                Label = label;
                CenterX = centerX;
                CenterZ = centerZ;
                Distance = distance;
                Pitch = pitch;
                Yaw = yaw;
            }

            public string Id { get; }
            public string Label { get; }
            public float CenterX { get; }
            public float CenterZ { get; }
            public float Distance { get; }
            public float Pitch { get; }
            public float Yaw { get; }
        }

        /// <summary>Default field overview — matches <see cref="AirsideBareField"/> framing.</summary>
        public static readonly View Overview = new(
            "overview", "Field overview",
            150f, 350f,
            AirsideBareField.OverviewDistance,
            AirsideBareField.OverviewPitch,
            AirsideBareField.OverviewYaw);

        /// <summary>Mid Terminal 1 airside face, looking at the aerobridge line.</summary>
        public static readonly View TerminalAirside = new(
            "terminal-airside", "Terminal 1 airside",
            1400f, 380f,
            520f, 28f, 210f);

        /// <summary>Landside kerb / forecourt, looking south onto Terminal 1.</summary>
        public static readonly View TerminalKerb = new(
            "terminal-kerb", "Terminal 1 landside kerb",
            1250f, 620f,
            380f, 22f, 160f);

        /// <summary>Eastern hangar row (Cobham / Rex / Sharp).</summary>
        public static readonly View HangarRow = new(
            "hangar-row", "Eastern hangar row",
            1000f, 940f,
            420f, 26f, 200f);

        /// <summary>Suburb edge south-east of the field.</summary>
        public static readonly View SuburbEdge = new(
            "suburb-edge", "Suburb edge",
            2200f, -900f,
            900f, 32f, 320f);

        /// <summary>West toward Gulf St Vincent / West Beach.</summary>
        public static readonly View Coast = new(
            "coast", "Coast / West Beach",
            -1800f, 400f,
            1600f, 18f, 270f);

        public static readonly View[] All =
        {
            Overview, TerminalAirside, TerminalKerb, HangarRow, SuburbEdge, Coast
        };

        /// <summary>Lighting times captured for every bookmark (HH:mm Adelaide local).</summary>
        public static readonly string[] LightingTimes = { "12:00", "18:30", "23:30" };

        /// <summary>High-tier overview budget from the visual overhaul plan.</summary>
        public const int OverviewBudgetFps = 60;
        public const int CaptureWidth = 1600;
        public const int CaptureHeight = 900;

        public static bool TryResolve(string id, out View view)
        {
            view = default;
            if (string.IsNullOrWhiteSpace(id))
                return false;
            foreach (var candidate in All)
            {
                if (string.Equals(candidate.Id, id.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    view = candidate;
                    return true;
                }
            }
            return false;
        }

        public static View ResolveOrOverview(string id) =>
            TryResolve(id, out var view) ? view : Overview;

        /// <summary>Read <c>-airsideReviewView</c> from a command line, or overview when absent.</summary>
        public static View FromCommandLine(string[] args)
        {
            if (args == null)
                return Overview;
            var index = Array.IndexOf(args, ReviewViewFlag);
            if (index < 0 || index + 1 >= args.Length)
                return Overview;
            return ResolveOrOverview(args[index + 1]);
        }
    }
}
