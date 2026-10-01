using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>Local spectator access; never changes aircraft schedules or saves.</summary>
    public static class CockpitAvailability
    {
        public static bool Supported(AircraftType type) => type != null && (type.Id == AircraftType.Saab340.Id || type.Id == AircraftType.Atr42.Id
            || type.Id == AircraftType.Dash8Q400.Id || JetCockpitProfile.TryFor(type.Id, out _));
        public static string Reason(AircraftType type, bool visible, EngineState engines)
        {
            if (type != null && type.IsRotorcraft) return "No helicopter cockpit yet";
            if (!Supported(type)) return "Cockpit coming later for this type";
            if (!visible) return "Aircraft outside the local area";
            // First non-zero spool, rather than AnyRunning's 2% threshold.
            if (engines.Left <= 0f && engines.Right <= 0f) return "Available after engine start";
            return string.Empty;
        }
    }
}
