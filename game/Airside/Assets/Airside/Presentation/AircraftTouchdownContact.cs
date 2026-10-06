using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>Presentation contact events follow the same performance profile as the landing path.</summary>
    public static class AircraftTouchdownContact
    {
        public static bool HasContact(AircraftType type, float progress)
        {
            type = type ?? AircraftType.Atr42;
            return !type.IsRotorcraft && progress >= AircraftPerformance.For(type).TouchdownProgress;
        }

        public static bool TryRecord(ISet<string> fired, string id, AircraftPhase phase,
            AircraftType type, float progress, bool hasView)
        {
            if (phase != AircraftPhase.Landing)
            {
                fired.Remove(id);
                return false;
            }
            return hasView && HasContact(type, progress) && fired.Add(id);
        }
    }
}
