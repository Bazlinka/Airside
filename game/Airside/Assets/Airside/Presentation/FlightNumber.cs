using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// A short, human-looking flight number (e.g. "QLK404") shown instead of a bare
    /// registration. Presentation only: deterministic from the airline, aircraft and
    /// route, never stored, never decides anything. Airside has no authored per-route
    /// flight numbers yet, so this is a stable stand-in — the same aircraft flying the
    /// same route always reads the same number.
    /// </summary>
    public static class FlightNumber
    {
        /// <summary>
        /// The 2-3 letter code shown before the number: the airline's own id for an AI
        /// operator (already short — REX, QLK, SIA…), or initials drawn from the
        /// player's chosen airline name.
        /// </summary>
        public static string AirlineCode(Airline airline)
        {
            if (airline == null)
                return "XX";
            return airline.IsPlayer ? CodeFromName(airline.Name) : airline.Id.Value;
        }

        private static string CodeFromName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "XX";

            var letters = new System.Text.StringBuilder(2);
            var atWordStart = true;
            foreach (var c in name)
            {
                if (char.IsWhiteSpace(c))
                {
                    atWordStart = true;
                    continue;
                }
                if (!atWordStart || !char.IsLetter(c))
                    continue;
                letters.Append(char.ToUpperInvariant(c));
                atWordStart = false;
                if (letters.Length == 2)
                    return letters.ToString();
            }

            if (letters.Length == 1)
            {
                // A one-word name: fall back to its second letter instead of stopping at one.
                foreach (var c in name.Trim())
                {
                    if (!char.IsLetter(c) || char.ToUpperInvariant(c) == letters[0])
                        continue;
                    return letters.Append(char.ToUpperInvariant(c)).ToString();
                }
            }

            return letters.Length > 0 ? letters.Append('X').ToString() : "XX";
        }

        /// <summary>
        /// Deterministic flight number for an airline flying a registration on a route to
        /// <paramref name="destinationCode"/> — the same inputs always give the same number.
        /// </summary>
        public static string For(Airline airline, string registration, string destinationCode)
        {
            // "|" keeps a registration/destination pair from hashing the same as a different
            // split of the same characters (e.g. "AB"+"C" vs "A"+"BC").
            var hash = StableHash.Of($"{registration}|{destinationCode}");
            var number = 100 + (int)(hash % 900);
            return $"{AirlineCode(airline)}{number}";
        }

        /// <summary>
        /// The flight number for an aircraft's current or scheduled route, or null when it
        /// has neither (parked with nothing planned) — callers fall back to the registration.
        /// </summary>
        public static string ForAircraft(FleetAircraft aircraft)
        {
            if (aircraft == null)
                return null;
            var code = aircraft.CurrentDestination?.Code ?? aircraft.Scheduled?.Destination.Code;
            return code == null ? null : For(aircraft.Airline, aircraft.Registration, code);
        }

        /// <summary>The flight number for an aircraft, or its registration when it has no route yet.</summary>
        public static string OrRegistration(FleetAircraft aircraft) =>
            ForAircraft(aircraft) ?? aircraft?.Registration;
    }
}
