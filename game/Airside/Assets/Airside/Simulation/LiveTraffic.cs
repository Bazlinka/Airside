using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>One real aircraft from the adsb.lol feed, as last reported.</summary>
    public readonly struct LiveAircraft
    {
        public LiveAircraft(string hex, string callsign, string registration, string typeCode,
            double latitude, double longitude, double? altitudeFeet, bool onGround,
            double groundSpeedKnots, double trackDegrees, double verticalRateFpm, double positionAgeSeconds)
        {
            Hex = hex;
            Callsign = callsign;
            Registration = registration;
            TypeCode = typeCode;
            Latitude = latitude;
            Longitude = longitude;
            AltitudeFeet = altitudeFeet;
            OnGround = onGround;
            GroundSpeedKnots = groundSpeedKnots;
            TrackDegrees = trackDegrees;
            VerticalRateFpm = verticalRateFpm;
            PositionAgeSeconds = positionAgeSeconds;
        }

        /// <summary>ICAO 24-bit address: the stable key while callsigns change.</summary>
        public string Hex { get; }
        public string Callsign { get; }
        public string Registration { get; }
        public string TypeCode { get; }
        public double Latitude { get; }
        public double Longitude { get; }
        public double? AltitudeFeet { get; }
        public bool OnGround { get; }
        public double GroundSpeedKnots { get; }
        public double TrackDegrees { get; }
        public double VerticalRateFpm { get; }
        public double PositionAgeSeconds { get; }

        public string Label => !string.IsNullOrEmpty(Callsign) ? Callsign
            : !string.IsNullOrEmpty(Registration) ? Registration : Hex;
    }

    /// <summary>Where and how to draw one live aircraft this frame.</summary>
    public readonly struct LiveTrafficPose
    {
        public LiveTrafficPose(double x, double y, double z, float yawDegrees, float pitchDegrees)
        {
            X = x;
            Y = y;
            Z = z;
            YawDegrees = yawDegrees;
            PitchDegrees = pitchDegrees;
        }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public float YawDegrees { get; }
        public float PitchDegrees { get; }
    }

    /// <summary>
    /// Real Adelaide traffic from adsb.lol (ODbL 1.0), drawn in the sky only (ADR 0081).
    /// Nothing here feeds the simulation: it never books a runway, stand or slot, is never
    /// saved, and the game plays identically without it. Aircraft on the ground or low over
    /// the field are left out so they never drive through the player's own fleet.
    /// </summary>
    public static class LiveTraffic
    {
        public const string Credit = "Live traffic: adsb.lol (ODbL)";

        /// <summary>Drawn in the 3D sky out to here.</summary>
        public const double RadiusNauticalMiles = 60.0;

        /// <summary>Fetched out to here (adsb.lol's maximum) so the Route Map shows the region.</summary>
        public const double FeedRadiusNauticalMiles = 250.0;

        /// <summary>YPAD elevation: ground-relative height for traffic low over the field.</summary>
        public const double FieldElevationFeet = 20.0;

        /// <summary>On the ground positions are only dead-reckoned briefly: taxiways turn.</summary>
        public const double GroundDeadReckonSeconds = 5.0;
        public const float PollSeconds = 10f;

        /// <summary>Reports older than this are dropped rather than dead-reckoned.</summary>
        public const double StaleSeconds = 45.0;

        /// <summary>Real distances inside this are drawn 1:1, so a real final lines up with the runway.</summary>
        public const double NearFieldMetres = 5_000.0;

        /// <summary>Everything out to the feed radius is squeezed into the rest of the draw distance.</summary>
        public const double DrawRadiusMetres = 7_500.0;

        /// <summary>Below this over the field a real aircraft is landing, rolling or taxiing — not drawn.</summary>
        public const double FieldFloorFeet = 500.0;
        public const double FieldRadiusMetres = 6_000.0;

        private const double FeetPerMetre = 3.28084;
        private const double KnotsToMetresPerSecond = 0.514444;

        public static string RequestUrl()
        {
            var adl = DestinationCatalogue.Adelaide;
            return string.Format(CultureInfo.InvariantCulture, "https://api.adsb.lol/v2/point/{0:0.###}/{1:0.###}/{2:0}",
                adl.Latitude, adl.Longitude, FeedRadiusNauticalMiles);
        }

        /// <summary>
        /// The catalogue model closest to a real ICAO type, or null for types the game has no
        /// honest stand-in for (light aircraft, helicopters, freighters it would misdraw).
        /// </summary>
        public static AircraftType ModelFor(string typeCode)
        {
            switch ((typeCode ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "SF34":
                case "SB20":
                    return AircraftType.Saab340;
                case "AT43":
                case "AT45":
                case "AT46":
                case "AT72":
                case "AT75":
                case "AT76":
                    return AircraftType.Atr42;
                case "DH8A":
                case "DH8B":
                case "DH8C":
                case "DH8D":
                    return AircraftType.Dash8Q400;
                case "A319":
                case "A320":
                case "A20N":
                case "A321":
                case "A21N":
                case "BCS3":
                    return AircraftType.AirbusA321Neo;
                case "B737":
                case "B738":
                case "B739":
                case "B37M":
                case "B38M":
                case "B39M":
                case "B712":
                case "E190":
                case "E195":
                case "E290":
                case "E295":
                case "F100":
                    return AircraftType.Boeing7378;
                case "A332":
                case "A333":
                case "A338":
                case "A339":
                case "A359":
                case "A35K":
                case "A388":
                    return AircraftType.AirbusA350900;
                case "B772":
                case "B773":
                case "B77L":
                case "B77W":
                case "B788":
                case "B789":
                case "B78X":
                case "B744":
                case "B748":
                    return AircraftType.Boeing78710;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Where to draw <paramref name="aircraft"/> <paramref name="secondsSinceReport"/> after the
        /// feed reported it, dead-reckoned along its track. False when it should not be drawn.
        /// </summary>
        public static bool TryPose(LiveAircraft aircraft, double secondsSinceReport, out LiveTrafficPose pose)
        {
            pose = default;
            if (aircraft.OnGround || !aircraft.AltitudeFeet.HasValue || ModelFor(aircraft.TypeCode) == null)
                return false;
            var age = aircraft.PositionAgeSeconds + Math.Max(0.0, secondsSinceReport);
            if (age > StaleSeconds)
                return false;

            Advance(aircraft, age, out var x, out var z, out var feet);
            var range = Math.Sqrt(x * x + z * z);
            if (range < FieldRadiusMetres && feet < FieldFloorFeet)
                return false;
            if (range > RadiusNauticalMiles * 1852.0)
                return false;

            Display(x, z, feet, out var dx, out var dy, out var dz);

            // Face along the drawn path, not the true track: past the near field the squeeze
            // bends straight lines, and a true heading would crab against the motion.
            Advance(aircraft, age + 4.0, out var ax, out var az, out var aheadFeet);
            Display(ax, az, aheadFeet, out var adx, out _, out var adz);
            var yaw = (adx - dx) * (adx - dx) + (adz - dz) * (adz - dz) > 0.01
                ? (float)(Math.Atan2(adx - dx, adz - dz) * 180.0 / Math.PI)
                : YpadFrame.UnityYawFromTrue(aircraft.TrackDegrees);

            // Attitude, not flight path: a wing flies a few degrees nose-up of where it is
            // going, so a 3° descent still shows the nose slightly up. Positive is nose up.
            var speed = Math.Max(30.0, aircraft.GroundSpeedKnots) * KnotsToMetresPerSecond;
            var climb = aircraft.VerticalRateFpm / FeetPerMetre / 60.0;
            var flightPath = Math.Atan2(climb, speed) * 180.0 / Math.PI;
            var pitch = (float)Math.Max(-3.0, Math.Min(12.0, flightPath + 3.0));
            pose = new LiveTrafficPose(dx, dy, dz, yaw, pitch);
            return true;
        }

        /// <summary>
        /// Where to draw a real aircraft that is on the ground, or low over the field landing or
        /// taking off — the traffic <see cref="TryPose"/> leaves out. 1:1 on the drawn pavement.
        /// Whether it is actually drawn is the caller's call: it must stand aside for the
        /// player's fleet (<see cref="LiveGroundClearance"/>).
        /// </summary>
        public static bool TryFieldPose(LiveAircraft aircraft, double secondsSinceReport, out LiveTrafficPose pose,
            out bool onGround)
        {
            pose = default;
            onGround = aircraft.OnGround;
            if (ModelFor(aircraft.TypeCode) == null)
                return false;
            var age = aircraft.PositionAgeSeconds + Math.Max(0.0, secondsSinceReport);
            if (age > StaleSeconds)
                return false;

            var moving = aircraft.GroundSpeedKnots >= 2.0;
            var reckon = onGround ? Math.Min(age, moving ? GroundDeadReckonSeconds : 0.0) : age;
            Advance(aircraft, reckon, out var x, out var z, out var feet);
            if (Math.Sqrt(x * x + z * z) > FieldRadiusMetres)
                return false;
            if (!onGround && (!aircraft.AltitudeFeet.HasValue || feet >= FieldFloorFeet))
                return false;

            var height = onGround ? 0.0 : Math.Max(0.0, (feet - FieldElevationFeet) / FeetPerMetre);
            var speed = Math.Max(30.0, aircraft.GroundSpeedKnots) * KnotsToMetresPerSecond;
            var climb = aircraft.VerticalRateFpm / FeetPerMetre / 60.0;
            var pitch = onGround ? 0f
                : (float)Math.Max(-3.0, Math.Min(12.0, Math.Atan2(climb, speed) * 180.0 / Math.PI + 3.0));
            pose = new LiveTrafficPose(x, height, z, YpadFrame.UnityYawFromTrue(aircraft.TrackDegrees), pitch);
            return true;
        }

        /// <summary>Latitude/longitude <paramref name="seconds"/> along the reported track, for maps.</summary>
        public static void PositionAfter(LiveAircraft aircraft, double seconds, out double latitude, out double longitude)
        {
            var metres = aircraft.OnGround && aircraft.GroundSpeedKnots < 2.0
                ? 0.0
                : aircraft.GroundSpeedKnots * KnotsToMetresPerSecond * Math.Max(0.0, Math.Min(seconds, StaleSeconds));
            var track = aircraft.TrackDegrees * Math.PI / 180.0;
            latitude = aircraft.Latitude + Math.Cos(track) * metres / 110_574.0;
            longitude = aircraft.Longitude
                        + Math.Sin(track) * metres / (111_320.0 * Math.Cos(aircraft.Latitude * Math.PI / 180.0));
        }

        private static void Advance(LiveAircraft aircraft, double seconds, out double x, out double z, out double feet)
        {
            YpadFrame.ToWorld(aircraft.Latitude, aircraft.Longitude, out x, out z);
            var track = aircraft.TrackDegrees * Math.PI / 180.0;
            var metres = aircraft.GroundSpeedKnots * KnotsToMetresPerSecond * seconds;
            YpadFrame.FromEastNorth(Math.Sin(track) * metres, Math.Cos(track) * metres, out var mx, out var mz);
            x += mx;
            z += mz;
            feet = Math.Max(0.0, aircraft.AltitudeFeet.GetValueOrDefault() + aircraft.VerticalRateFpm * seconds / 60.0);
        }

        /// <summary>
        /// 1:1 inside the near field so approaches meet the runway; beyond it the rest of the
        /// feed radius is squeezed into the last few kilometres of draw distance, and height
        /// is squeezed with it so a cruising jet does not tower over a model-scale horizon.
        /// </summary>
        public static void Display(double x, double z, double feet, out double dx, out double dy, out double dz)
        {
            var range = Math.Sqrt(x * x + z * z);
            var metresUp = feet / FeetPerMetre;
            if (range <= NearFieldMetres || range < 1e-6)
            {
                dx = x;
                dz = z;
                dy = metresUp;
                return;
            }

            var outer = RadiusNauticalMiles * 1852.0;
            var t = Math.Min(1.0, (range - NearFieldMetres) / (outer - NearFieldMetres));
            var drawn = NearFieldMetres + t * (DrawRadiusMetres - NearFieldMetres);
            var scale = drawn / range;
            dx = x * scale;
            dz = z * scale;
            var squeeze = Math.Min(1.0, (range - NearFieldMetres) / 20_000.0);
            dy = metresUp * (1.0 - squeeze * 0.72);
        }

        /// <summary>
        /// Reads an adsb.lol v2 response. Tolerant by design: a field of the wrong type or
        /// a missing position skips that aircraft, never the whole feed.
        /// </summary>
        public static List<LiveAircraft> Parse(string json)
        {
            var result = new List<LiveAircraft>();
            if (string.IsNullOrEmpty(json))
                return result;
            object root;
            try
            {
                root = new JsonReader(json).ReadValue();
            }
            catch (FormatException)
            {
                return result;
            }

            if (root is not Dictionary<string, object> document
                || !document.TryGetValue("ac", out var list) || list is not List<object> aircraft)
                return result;

            foreach (var item in aircraft)
            {
                if (item is not Dictionary<string, object> a)
                    continue;
                var lat = Number(a, "lat");
                var lon = Number(a, "lon");
                var hex = Text(a, "hex");
                if (!lat.HasValue || !lon.HasValue || string.IsNullOrEmpty(hex))
                    continue;

                a.TryGetValue("alt_baro", out var altRaw);
                var onGround = altRaw is string s && s.Equals("ground", StringComparison.OrdinalIgnoreCase);
                double? altitude = altRaw is double feet ? feet : Number(a, "alt_geom");
                result.Add(new LiveAircraft(
                    hex, Text(a, "flight")?.Trim(), Text(a, "r"), Text(a, "t"),
                    lat.Value, lon.Value, onGround ? 0.0 : altitude, onGround,
                    Number(a, "gs") ?? 0.0, Number(a, "track") ?? Number(a, "true_heading") ?? 0.0,
                    Number(a, "baro_rate") ?? Number(a, "geom_rate") ?? 0.0,
                    Number(a, "seen_pos") ?? Number(a, "seen") ?? 0.0));
            }

            return result;
        }

        private static double? Number(Dictionary<string, object> item, string key) =>
            item.TryGetValue(key, out var value) && value is double number ? number : null;

        private static string Text(Dictionary<string, object> item, string key) =>
            item.TryGetValue(key, out var value) ? value as string : null;

        /// <summary>Minimal JSON reader: objects, arrays, strings, numbers, true/false/null.</summary>
        private sealed class JsonReader
        {
            private readonly string _text;
            private int _at;

            public JsonReader(string text) => _text = text;

            public object ReadValue()
            {
                SkipSpace();
                if (_at >= _text.Length)
                    throw new FormatException("unexpected end");
                var c = _text[_at];
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default: return ReadNumber();
                }
            }

            private Dictionary<string, object> ReadObject()
            {
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                _at++;
                SkipSpace();
                if (Peek('}'))
                {
                    _at++;
                    return result;
                }

                while (true)
                {
                    SkipSpace();
                    var key = ReadString();
                    SkipSpace();
                    Require(':');
                    result[key] = ReadValue();
                    SkipSpace();
                    if (Peek(','))
                    {
                        _at++;
                        continue;
                    }

                    Require('}');
                    return result;
                }
            }

            private List<object> ReadArray()
            {
                var result = new List<object>();
                _at++;
                SkipSpace();
                if (Peek(']'))
                {
                    _at++;
                    return result;
                }

                while (true)
                {
                    result.Add(ReadValue());
                    SkipSpace();
                    if (Peek(','))
                    {
                        _at++;
                        continue;
                    }

                    Require(']');
                    return result;
                }
            }

            private string ReadString()
            {
                Require('"');
                var builder = new StringBuilder();
                while (_at < _text.Length)
                {
                    var c = _text[_at++];
                    if (c == '"')
                        return builder.ToString();
                    if (c != '\\')
                    {
                        builder.Append(c);
                        continue;
                    }

                    if (_at >= _text.Length)
                        break;
                    var e = _text[_at++];
                    switch (e)
                    {
                        case 'n': builder.Append('\n'); break;
                        case 't': builder.Append('\t'); break;
                        case 'r': builder.Append('\r'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'u':
                            if (_at + 4 > _text.Length)
                                throw new FormatException("bad escape");
                            builder.Append((char)int.Parse(_text.Substring(_at, 4), NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture));
                            _at += 4;
                            break;
                        default: builder.Append(e); break;
                    }
                }

                throw new FormatException("unterminated string");
            }

            private double ReadNumber()
            {
                var start = _at;
                while (_at < _text.Length && "+-0123456789.eE".IndexOf(_text[_at]) >= 0)
                    _at++;
                if (_at == start
                    || !double.TryParse(_text.Substring(start, _at - start), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var number))
                    throw new FormatException("bad number");
                return number;
            }

            private void Expect(string word)
            {
                if (string.CompareOrdinal(_text, _at, word, 0, word.Length) != 0)
                    throw new FormatException("bad literal");
                _at += word.Length;
            }

            private void Require(char c)
            {
                if (!Peek(c))
                    throw new FormatException($"expected {c}");
                _at++;
            }

            private bool Peek(char c) => _at < _text.Length && _text[_at] == c;

            private void SkipSpace()
            {
                while (_at < _text.Length && char.IsWhiteSpace(_text[_at]))
                    _at++;
            }
        }
    }
}
