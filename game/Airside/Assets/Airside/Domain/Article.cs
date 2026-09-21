namespace Airside.Domain
{
    /// <summary>
    /// "a" or "an" in front of a type or place name, so the HUD reads "an ATR 42-600" and
    /// "an Alice Springs contract" rather than "a ATR 42-600". Goes by the first letter:
    /// every name in the catalogues is pronounced the way it is spelt at the start.
    /// </summary>
    public static class Article
    {
        public static string A(string noun) => (StartsWithVowel(noun) ? "an " : "a ") + noun;

        /// <summary>Sentence-initial form: "A Saab 340B …", "An Airbus A321neo …".</summary>
        public static string CapitalA(string noun) => (StartsWithVowel(noun) ? "An " : "A ") + noun;

        private static bool StartsWithVowel(string noun) =>
            !string.IsNullOrEmpty(noun) && "AEIOUaeiou".IndexOf(noun[0]) >= 0;
    }
}
