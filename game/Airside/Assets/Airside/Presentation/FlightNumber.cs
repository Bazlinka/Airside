using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// The flight a board would print: a callsign and number (ZL3482, QF680) and the
    /// city it is flying. Presentation only — deterministic from the airline, aircraft
    /// and route, never stored, never decides a schedule.
    /// Published Adelaide services use a representative number for that airline and city.
    /// Extra aircraft on the same city, and any route that is not a published service,
    /// take a spare number so the airport still looks busy.
    /// </summary>
    public static class FlightNumber
    {
        /// <summary>
        /// The 2-3 letter code stored for the airline: the operator id for AI (REX, QFA…),
        /// or the player's chosen code. This is the code setup refuses, not the callsign
        /// printed on a board.
        /// </summary>
        public static string AirlineCode(Airline airline)
        {
            if (airline == null)
                return "XX";
            if (!airline.IsPlayer)
                return airline.Id.Value;
            return string.IsNullOrEmpty(airline.Code) ? CodeFromName(airline.Name) : airline.Code;
        }

        /// <summary>
        /// The letters a departures board prints in front of the number. Qantas and
        /// QantasLink are both QF, Rex is ZL, Virgin is VA.
        /// </summary>
        public static string Callsign(Airline airline)
        {
            if (airline == null)
                return "XX";
            if (airline.IsPlayer)
                return AirlineCode(airline);
            return airline.Id.Value switch
            {
                "QFA" or "QLK" => "QF",
                "VOZ" => "VA",
                "JST" => "JQ",
                "ANZ" => "NZ",
                "SIA" => "SQ",
                "CPA" => "CX",
                "MAS" => "MH",
                "UAE" => "EK",
                "QTR" => "QR",
                "FJI" => "FJ",
                "REX" => "ZL",
                "RFDS" => "FD",
                _ => airline.Id.Value
            };
        }

        /// <summary>The code a new airline is offered before the player types their own.</summary>
        public static string SuggestCode(string name) => CodeFromName(name);

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
        /// Flight number for an airline, registration and destination. The same inputs
        /// always give the same number. A return to Adelaide is the next number.
        /// </summary>
        public static string For(Airline airline, string registration, string destinationCode, bool returningHome = false)
        {
            if (string.IsNullOrEmpty(destinationCode))
                return null;
            var callsign = Callsign(airline);
            var published = PublishedDeparture(airline != null && !airline.IsPlayer ? airline.Id.Value : null, destinationCode);
            int number;
            if (published.HasValue)
            {
                // Four slots on the city (3482, 3484, 3486, 3488) so two Rex Saabs to
                // Kingscote are not the same flight. Cities are ten apart, so they do not meet.
                var slot = (int)(StableHash.Of(registration ?? string.Empty) % 4) * 2;
                number = published.Value + slot;
            }
            else if (airline != null && airline.IsPlayer && TryPlayerNumber(registration, out var own))
            {
                // Each player aircraft keeps its own number (ADR 0165 follow-up). The hashed spare
                // band had 40 slots, so two of the player's aircraft often showed the same flight.
                number = own;
            }
            else
            {
                // Spare band for routes that are not a published Adelaide service. Still stable
                // for the same aircraft and city.
                var hash = StableHash.Of($"{registration}|{destinationCode}");
                number = 900 + (int)(hash % 40) * 2;
            }

            if (returningHome)
                number += 1;
            return $"{callsign}{number}";
        }

        /// <summary>
        /// A player aircraft's own outbound number from its <c>VH-P??</c> mark: VH-PAA is 100,
        /// VH-PAB 102 and so on, even so the return (+1) never meets another aircraft. Marks are
        /// issued in order and never reissued, so every aircraft the player owns reads differently.
        /// </summary>
        public static bool TryPlayerNumber(string registration, out int number)
        {
            number = 0;
            if (registration == null || registration.Length != 6
                || !registration.StartsWith("VH-P", System.StringComparison.OrdinalIgnoreCase))
                return false;
            var a = char.ToUpperInvariant(registration[4]) - 'A';
            var b = char.ToUpperInvariant(registration[5]) - 'A';
            if (a < 0 || a > 25 || b < 0 || b > 25)
                return false;
            // 450 marks fit 100–998; a career that has retired more than that wraps.
            number = 100 + (a * 26 + b) % 450 * 2;
            return true;
        }

        /// <summary>City this aircraft is flying, or null when it has no route.</summary>
        public static string PlaceName(FleetAircraft aircraft)
        {
            var destination = aircraft?.CurrentDestination ?? aircraft?.Scheduled?.Destination;
            return destination?.Name;
        }

        /// <summary>The aircraft is on the leg back to Adelaide, including the turn at the outstation.</summary>
        public static bool IsReturning(FleetAircraft aircraft) =>
            aircraft != null && aircraft.State is FleetState.AtDestination
                or FleetState.Inbound or FleetState.HoldingForLanding or FleetState.Landing
                or FleetState.GoAround or FleetState.AwaitingStand or FleetState.TaxiIn;

        /// <summary>
        /// The flight number for an aircraft's current or scheduled route, or null when it
        /// has neither — callers fall back to the registration.
        /// </summary>
        public static string ForAircraft(FleetAircraft aircraft)
        {
            if (aircraft == null)
                return null;
            var code = aircraft.CurrentDestination?.Code ?? aircraft.Scheduled?.Destination.Code;
            return code == null ? null : For(aircraft.Airline, aircraft.Registration, code, IsReturning(aircraft));
        }

        /// <summary>The flight number for an aircraft, or its registration when it has no route yet.</summary>
        public static string OrRegistration(FleetAircraft aircraft) =>
            ForAircraft(aircraft) ?? aircraft?.Registration;

        /// <summary>"ZL3482 Kingscote", or the registration when nothing is booked.</summary>
        public static string Title(FleetAircraft aircraft)
        {
            var number = ForAircraft(aircraft);
            if (number == null)
                return aircraft?.Registration ?? string.Empty;
            var place = PlaceName(aircraft);
            return string.IsNullOrEmpty(place) ? number : $"{number} {place}";
        }

        /// <summary>
        /// Representative Adelaide departure numbers. Not a copy of today's timetable:
        /// each pair is a real city that operator flies, in the number band it actually uses.
        /// The return is this number plus one.
        /// </summary>
        private static int? PublishedDeparture(string airlineId, string destinationCode) =>
            (airlineId, destinationCode) switch
            {
                ("REX", "KGC") => 3482,
                ("REX", "PLO") => 3472,
                ("REX", "WYA") => 3462,
                ("REX", "MGB") => 3492,
                ("REX", "CED") => 3452,
                ("REX", "BHQ") => 3432,
                ("REX", "CPD") => 3442,
                ("REX", "MQL") => 3422,
                ("QLK", "PLO") => 2262,
                ("QLK", "ASP") => 2282,
                ("QFA", "MEL") => 670,
                ("QFA", "SYD") => 680,
                ("QFA", "BNE") => 710,
                ("QFA", "PER") => 880,
                ("QFA", "CBR") => 690,
                ("QFA", "AKL") => 170,
                ("VOZ", "MEL") => 230,
                ("VOZ", "SYD") => 410,
                ("VOZ", "BNE") => 440,
                ("VOZ", "PER") => 720,
                ("VOZ", "CBR") => 250,
                ("JST", "MEL") => 770,
                ("JST", "SYD") => 780,
                ("JST", "BNE") => 790,
                ("JST", "OOL") => 760,
                ("JST", "PER") => 820,
                ("JST", "DPS") => 110,
                ("ANZ", "AKL") => 120,
                ("ANZ", "CHC") => 140,
                ("SIA", "SIN") => 270,
                ("CPA", "HKG") => 170,
                ("MAS", "KUL") => 130,
                ("UAE", "DXB") => 440,
                ("QTR", "DOH") => 840,
                ("FJI", "NAN") => 350,
                ("RFDS", "PLO") => 200,
                ("RFDS", "MGB") => 210,
                ("RFDS", "CED") => 220,
                ("RFDS", "CPD") => 230,
                ("RFDS", "WYA") => 240,
                ("RFDS", "KGC") => 250,
                ("RFDS", "BHQ") => 260,
                _ => null
            };
    }
}
