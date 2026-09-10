namespace Airside.Presentation
{
    /// <summary>
    /// The generated tile airfield is retired. While <see cref="AirsideBareField.Enabled"/>
    /// the runtime builds one runway slab on empty Adelaide ground. The old combined
    /// pads remain as a last-resort fallback behind these flags.
    /// </summary>
    public static class AirsideCombinedSurfaces
    {
        public const bool UseTileOperational = false;
        public const bool UseTilePaddock = false;

        /// <summary>Bare field: one runway slab. The retired miniature used six pads.</summary>
        public const int CombinedPadCount = 1;
    }
}
