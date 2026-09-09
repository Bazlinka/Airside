namespace Airside.Presentation
{
    /// <summary>
    /// P0 — the generated tile airfield is retired. Combined runway / taxi / apron
    /// slabs plus the WLD-004 terrain kit cover the same ground with a handful of
    /// meshes. The old CreateBlock dumps remain in <c>AirsidePrototype</c> as a
    /// last-resort fallback behind these flags. Runtime construction uses
    /// <c>BuildCombinedOperationalSurfaces</c> and the WLD-004 terrain kit.
    /// </summary>
    public static class AirsideCombinedSurfaces
    {
        public const bool UseTileOperational = false;
        public const bool UseTilePaddock = false;

        public const int CombinedPadCount = 6;
    }
}
