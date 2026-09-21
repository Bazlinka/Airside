using System;

namespace Airside.Domain
{
    /// <summary>
    /// Sun and moon as seen from a point on Earth at a UTC instant. Azimuth is compass
    /// degrees (0 north, 90 east, clockwise); elevation is degrees above the true horizon.
    /// Presentation places the discs and the key light from this; it never feeds the sim.
    /// </summary>
    public readonly struct CelestialBody
    {
        public CelestialBody(double azimuthDegrees, double elevationDegrees)
        {
            AzimuthDegrees = azimuthDegrees;
            ElevationDegrees = elevationDegrees;
        }

        /// <summary>0 = north, 90 = east, clockwise.</summary>
        public double AzimuthDegrees { get; }

        /// <summary>Positive above the horizon; negative below.</summary>
        public double ElevationDegrees { get; }

        /// <summary>Unit east / north / up for this direction.</summary>
        public void Horizontal(out double east, out double north, out double up)
        {
            var azimuth = AzimuthDegrees * CelestialSky.Deg2Rad;
            var elevation = ElevationDegrees * CelestialSky.Deg2Rad;
            var cosEl = Math.Cos(elevation);
            east = Math.Sin(azimuth) * cosEl;
            north = Math.Cos(azimuth) * cosEl;
            up = Math.Sin(elevation);
        }
    }

    /// <summary>
    /// Low-precision (about a degree) solar and lunar position for Adelaide lighting
    /// and sky discs. Paul Schlyter's geocentric formulae, with NOAA-style GMST so
    /// southern-hemisphere noon sits due north and the sun walks east → north → west.
    /// </summary>
    public readonly struct CelestialSky
    {
        public const double Deg2Rad = Math.PI / 180.0;
        private const double Rad2Deg = 180.0 / Math.PI;

        /// <summary>YPAD reference used by the live-traffic feed and the layout generator.</summary>
        public const double AdelaideLatitudeDegrees = -34.945;
        public const double AdelaideLongitudeDegrees = 138.531;

        public CelestialSky(CelestialBody sun, CelestialBody moon, double moonPhase)
        {
            Sun = sun;
            Moon = moon;
            MoonPhase = moonPhase;
        }

        public CelestialBody Sun { get; }
        public CelestialBody Moon { get; }

        /// <summary>0 at new moon, 0.5 at full, wrapping at 1.</summary>
        public double MoonPhase { get; }

        /// <summary>Lit fraction of the moon's disc, 0 new to 1 full.</summary>
        public double MoonIllumination
        {
            get
            {
                var elongation = MoonPhase * 2.0 * Math.PI;
                return 0.5 * (1.0 - Math.Cos(elongation));
            }
        }

        /// <summary>
        /// 0 below civil twilight (−6°), 1 once the sun is 25° up. Winter noon at
        /// Adelaide still reaches full daylight (~32°).
        /// </summary>
        public double Daylight => DaylightFromSunElevation(Sun.ElevationDegrees);

        public static CelestialSky AtAdelaide(DateTime utc) =>
            At(utc, AdelaideLatitudeDegrees, AdelaideLongitudeDegrees);

        public static CelestialSky At(DateTime utc, double latitudeDegrees, double longitudeDegrees)
        {
            utc = utc.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(utc, DateTimeKind.Utc)
                : utc.ToUniversalTime();

            var julian = JulianDate(utc);
            var d = julian - 2451545.0;
            var ecliptic = 23.4393 - 3.563e-7 * d;

            SunEquatorial(d, ecliptic, out var sunRa, out var sunDec, out var sunLon);
            MoonEquatorial(d, ecliptic, out var moonRa, out var moonDec, out var moonLon);

            var gmst = Rev(280.46061837 + 360.98564736629 * d);
            var lst = Rev(gmst + longitudeDegrees) * Deg2Rad;
            var lat = latitudeDegrees * Deg2Rad;

            Horizontal(lst, lat, sunRa, sunDec, out var sunAz, out var sunEl);
            Horizontal(lst, lat, moonRa, moonDec, out var moonAz, out var moonEl);

            var phase = Rev(moonLon - sunLon) / 360.0;
            return new CelestialSky(
                new CelestialBody(sunAz, sunEl),
                new CelestialBody(moonAz, moonEl),
                phase);
        }

        public static double DaylightFromSunElevation(double elevationDegrees)
        {
            if (elevationDegrees <= -6.0)
                return 0.0;
            if (elevationDegrees >= 25.0)
                return 1.0;
            var t = (elevationDegrees + 6.0) / 31.0;
            return t * t * (3.0 - 2.0 * t);
        }

        /// <summary>Warmth 0..1, peaking when the sun is a few degrees above the horizon.</summary>
        public static double GoldenHour(double elevationDegrees)
        {
            if (elevationDegrees <= -4.0 || elevationDegrees >= 22.0)
                return 0.0;
            var rise = Clamp01((elevationDegrees + 4.0) / 10.0);
            var fall = Clamp01((22.0 - elevationDegrees) / 16.0);
            return rise * fall;
        }

        private static void SunEquatorial(double d, double ecliptic, out double ra, out double dec,
            out double trueLongitude)
        {
            var w = 282.9404 + 4.70935e-5 * d;
            var e = 0.016709 - 1.151e-9 * d;
            var m = Rev(356.0470 + 0.9856002585 * d);
            var eAnom = m + Rad2Deg * e * Sind(m) * (1.0 + e * Cosd(m));
            var xv = Cosd(eAnom) - e;
            var yv = Math.Sqrt(Math.Max(0.0, 1.0 - e * e)) * Sind(eAnom);
            var v = Math.Atan2(yv, xv) * Rad2Deg;
            trueLongitude = Rev(v + w);
            var xs = Cosd(trueLongitude);
            var ys = Sind(trueLongitude);
            var ye = ys * Cosd(ecliptic);
            var ze = ys * Sind(ecliptic);
            ra = Math.Atan2(ye, xs);
            dec = Math.Atan2(ze, Math.Sqrt(xs * xs + ye * ye));
        }

        private static void MoonEquatorial(double d, double ecliptic, out double ra, out double dec,
            out double trueLongitude)
        {
            var n = Rev(125.1228 - 0.0529538083 * d);
            const double i = 5.1454;
            var w = Rev(318.0634 + 0.1643573223 * d);
            const double a = 60.2666;
            const double e = 0.054900;
            var m = Rev(115.3654 + 13.0649929509 * d);
            var eAnom = m + Rad2Deg * e * Sind(m) * (1.0 + e * Cosd(m));
            eAnom = m + Rad2Deg * e * Sind(eAnom) * (1.0 + e * Cosd(m));
            var xv = a * (Cosd(eAnom) - e);
            var yv = a * Math.Sqrt(Math.Max(0.0, 1.0 - e * e)) * Sind(eAnom);
            var v = Math.Atan2(yv, xv) * Rad2Deg;
            var r = Math.Sqrt(xv * xv + yv * yv);
            var lonArg = (v + w) * Deg2Rad;
            var nRad = n * Deg2Rad;
            var iRad = i * Deg2Rad;
            var xh = r * (Math.Cos(nRad) * Math.Cos(lonArg) - Math.Sin(nRad) * Math.Sin(lonArg) * Math.Cos(iRad));
            var yh = r * (Math.Sin(nRad) * Math.Cos(lonArg) + Math.Cos(nRad) * Math.Sin(lonArg) * Math.Cos(iRad));
            var zh = r * (Math.Sin(lonArg) * Math.Sin(iRad));
            var lon = Math.Atan2(yh, xh) * Rad2Deg;
            var lat = Math.Atan2(zh, Math.Sqrt(xh * xh + yh * yh)) * Rad2Deg;

            var sunM = Rev(356.0470 + 0.9856002585 * d);
            var sunL = Rev(282.9404 + 4.70935e-5 * d + sunM);
            var dElong = Rev(lon - sunL);
            var f = Rev(lon - n);
            lon += -1.274 * Sind(m - 2.0 * dElong)
                   + 0.658 * Sind(2.0 * dElong)
                   - 0.186 * Sind(sunM)
                   - 0.059 * Sind(2.0 * m - 2.0 * dElong)
                   - 0.057 * Sind(m - 2.0 * dElong + sunM);
            lat += -0.173 * Sind(f - 2.0 * dElong);

            trueLongitude = Rev(lon);
            var xh2 = Cosd(trueLongitude) * Cosd(lat);
            var yh2 = Sind(trueLongitude) * Cosd(lat);
            var zh2 = Sind(lat);
            var xe = xh2;
            var ye = yh2 * Cosd(ecliptic) - zh2 * Sind(ecliptic);
            var ze = yh2 * Sind(ecliptic) + zh2 * Cosd(ecliptic);
            ra = Math.Atan2(ye, xe);
            dec = Math.Atan2(ze, Math.Sqrt(xe * xe + ye * ye));
        }

        private static void Horizontal(double lst, double lat, double ra, double dec,
            out double azimuthDegrees, out double elevationDegrees)
        {
            var ha = lst - ra;
            var sinEl = Math.Sin(lat) * Math.Sin(dec) + Math.Cos(lat) * Math.Cos(dec) * Math.Cos(ha);
            var elevation = Math.Asin(Clamp(sinEl, -1.0, 1.0));
            var azimuth = Math.Atan2(
                -Math.Cos(dec) * Math.Sin(ha),
                Math.Cos(lat) * Math.Sin(dec) - Math.Sin(lat) * Math.Cos(dec) * Math.Cos(ha));
            azimuthDegrees = Rev(azimuth * Rad2Deg);
            elevationDegrees = elevation * Rad2Deg;
        }

        private static double JulianDate(DateTime utc)
        {
            var year = utc.Year;
            var month = utc.Month;
            var day = utc.Day + utc.TimeOfDay.TotalDays;
            if (month <= 2)
            {
                year--;
                month += 12;
            }

            var a = Math.Floor(year / 100.0);
            var b = 2.0 - a + Math.Floor(a / 4.0);
            return Math.Floor(365.25 * (year + 4716)) + Math.Floor(30.6001 * (month + 1)) + day + b - 1524.5;
        }

        private static double Sind(double degrees) => Math.Sin(degrees * Deg2Rad);
        private static double Cosd(double degrees) => Math.Cos(degrees * Deg2Rad);

        private static double Rev(double degrees)
        {
            var wrapped = degrees % 360.0;
            return wrapped < 0.0 ? wrapped + 360.0 : wrapped;
        }

        private static double Clamp(double value, double min, double max) =>
            value < min ? min : value > max ? max : value;

        private static double Clamp01(double value) => Clamp(value, 0.0, 1.0);
    }
}
