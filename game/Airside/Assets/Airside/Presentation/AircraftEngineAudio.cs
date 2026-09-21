using Airside.Domain;

namespace Airside.Presentation
{
    /// <summary>
    /// CC0 engine beds keyed by type family (ADR 0087). Recorded Dash 8 / jet loops
    /// replace the procedural sine; pitch and volume still follow spool and takeoff.
    /// </summary>
    public static class AircraftEngineAudio
    {
        public const string Dash8Q400 = "Airside/Audio/eng_dash8_q400_pw100";
        public const string TwinTurboprop = "Airside/Audio/eng_dash8_300_twin";
        public const string JetTurbine = "Airside/Audio/eng_jet_turbine";

        public static string ResourceName(AircraftType type)
        {
            if (type != null && type.Id == AircraftType.Dash8Q400.Id)
                return Dash8Q400;
            if (type != null && AircraftCatalogue.TryFor(type, out var spec)
                && spec.StandClass == StandClass.TerminalGate)
                return JetTurbine;
            return TwinTurboprop;
        }
    }
}
