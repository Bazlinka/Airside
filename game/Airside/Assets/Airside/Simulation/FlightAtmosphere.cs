using System;

namespace Airside.Simulation
{
    /// <summary>ISA subsonic pitot conversions. IAS is represented by CAS (no instrument/position error).
    /// TAS and ground speed are equal only in the zero-wind route model. Pressure altitude is metres AMSL.</summary>
    public static class FlightAtmosphere
    {
        public const double FieldElevationMetres = 6; // YPAD: 20 ft, rounded to the terrain datum.
        private const double R = 287.05287, Gamma = 1.4, T0 = 288.15, P0 = 101325;
        public static double Temperature(double metres) => T0 - .0065 * Math.Clamp(metres, 0, 11000);
        public static double Pressure(double metres)
        {
            var h = Math.Clamp(metres, 0, 20000);
            var p = P0 * Math.Pow(Temperature(h)/T0, 9.80665/(R*.0065));
            return h <= 11000 ? p : p*Math.Exp(-9.80665*(h-11000)/(R*216.65));
        }
        public static double SoundKnots(double metres) => Math.Sqrt(Gamma*R*Temperature(metres)) / CircuitProfile.KnotsToMetresPerSecond;
        public static double Mach(double trueKnots, double metres) => Math.Max(0, trueKnots)/SoundKnots(metres);
        public static double CalibratedKnots(double trueKnots, double metres)
        {
            var mach = Mach(trueKnots, metres);
            var impact = Pressure(metres)*(Math.Pow(1+.2*mach*mach, 3.5)-1);
            return SoundKnots(0)*Math.Sqrt(5*(Math.Pow(impact/P0+1, 2.0/7)-1));
        }
        public static double TrueKnots(double calibratedKnots, double metres)
        {
            var seaMach = Math.Max(0, calibratedKnots)/SoundKnots(0);
            var impact = P0*(Math.Pow(1+.2*seaMach*seaMach, 3.5)-1);
            return SoundKnots(metres)*Math.Sqrt(5*(Math.Pow(impact/Pressure(metres)+1, 2.0/7)-1));
        }
        public static double AltitudeMetres(double fieldFeet) => FieldElevationMetres + fieldFeet/EnrouteProfile.FeetPerMetre;
    }
}
