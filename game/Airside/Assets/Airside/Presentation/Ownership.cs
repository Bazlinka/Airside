using Airside.Domain;

namespace Airside.Presentation
{
    /// <summary>
    /// The one rule for telling the player's aircraft from everyone else's (ADR 0048), shared by
    /// the Hangar, fleet panel, Flights board, route map, field tags and selection card: the same
    /// section names, the player's livery accent and ownership badge, and quieter AI traffic.
    /// </summary>
    public static class Ownership
    {
        public const string PlayerSection = "YOUR AIRLINE";
        public const string OtherSection = "OTHER OPERATORS";
        public const string PlayerBadge = "YOURS";

        /// <summary>AI rows, tags and map icons are drawn at this opacity so the player's stand out.</summary>
        public const float OtherAlpha = 0.62f;

        public static string SectionFor(Airline airline) => airline != null && airline.IsPlayer ? PlayerSection : OtherSection;

        /// <summary>Badge text for a player-owned aircraft; null for other operators.</summary>
        public static string BadgeFor(Airline airline) => airline != null && airline.IsPlayer ? PlayerBadge : null;

        public static float AlphaFor(Airline airline) => airline != null && airline.IsPlayer ? 1f : OtherAlpha;
    }
}
