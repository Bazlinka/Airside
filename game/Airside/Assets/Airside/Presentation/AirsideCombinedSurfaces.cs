namespace Airside.Presentation
{
    /// <summary>
    /// The generated tile airfield is retired. While <see cref="AirsideBareField.Enabled"/>
    /// the runtime builds one runway slab on empty Adelaide ground. The old combined
    /// pads remain as a last-resort fallback behind these flags.
    /// </summary>
    public static class AirsideCombinedSurfaces
    {
        // Retired paths stay available only for explicit regression captures. These
        // must not be compile-time constants: that turned their call sites into
        // unreachable code and hid warnings in the actual build.
        public static readonly bool UseTileOperational = AirsideBareField.HasLaunchFlag("-airsideLegacyTiles");
        public static readonly bool UseTilePaddock = AirsideBareField.HasLaunchFlag("-airsideLegacyTiles");

        /// <summary>Bare field: one runway slab. The retired miniature used six pads.</summary>
        public const int CombinedPadCount = 1;
    }
}
