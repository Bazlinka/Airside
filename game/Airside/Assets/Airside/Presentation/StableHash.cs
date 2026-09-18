namespace Airside.Presentation
{
    /// <summary>
    /// Deterministic 32-bit FNV-1a over the UTF-16 code units of a string.
    /// <c>string.GetHashCode</c> is not stable across runtimes, so anything that must read
    /// the same in the editor, a player build and the headless harness — flight numbers,
    /// apron-figure variants — hashes through here instead.
    /// </summary>
    public static class StableHash
    {
        public static uint Of(string text)
        {
            var hash = 2166136261u;
            if (text == null)
                return hash;
            for (var i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619u;
            }

            return hash;
        }
    }
}
