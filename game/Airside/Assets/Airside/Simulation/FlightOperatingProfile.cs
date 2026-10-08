using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>Normal-flight planning envelope, separate from aircraft certification and calculated V-speeds.
    /// Source/variant coverage and inferred family values: docs/data/FLIGHT_PERFORMANCE_RESEARCH.md.</summary>
    public readonly struct FlightOperatingProfile
    {
        public FlightOperatingProfile(double cruiseFeet, double climbCas, double cruiseMach,
            double lowClimb, double midClimb, double highClimb, double lowDescent, double highDescent)
        {
            NormalCruiseFeet=cruiseFeet; ClimbCas=climbCas; CruiseMach=cruiseMach;
            LowClimb=lowClimb; MidClimb=midClimb; HighClimb=highClimb;
            LowDescent=lowDescent; HighDescent=highDescent;
        }
        public double NormalCruiseFeet { get; }
        public double ClimbCas { get; }
        public double CruiseMach { get; }
        public double LowClimb { get; }
        public double MidClimb { get; }
        public double HighClimb { get; }
        public double LowDescent { get; }
        public double HighDescent { get; }
        public double ClimbRate(double feet)
        {
            if(feet<5000) return LowClimb;
            if(feet<15000) return Lerp(LowClimb,MidClimb,(feet-5000)/10000);
            if(feet<24000) return Lerp(MidClimb,HighClimb,(feet-15000)/9000);
            // Thrust margin reduces near the normal upper level; never climb at the low-altitude rate at FL400.
            return Lerp(HighClimb,Math.Min(600,HighClimb), (feet-24000)/Math.Max(1,NormalCruiseFeet-24000));
        }
        public double DescentRate(double feet) => feet<10000 ? LowDescent
            : feet<24000 ? Lerp(LowDescent,HighDescent,(feet-10000)/14000) : HighDescent;
        public double SpeedLimitTrueKnots(AircraftType type, double fieldFeet, bool climbing = false)
        {
            var altitude=FlightAtmosphere.AltitudeMetres(fieldFeet);
            // Conservative game SOP in all airspace; Australian legal applicability/exemptions are documented.
            // Decelerate before FL100 and accelerate smoothly above, never impose a GS limit as an IAS limit.
            var cas=altitude*EnrouteProfile.FeetPerMetre<=10000 ? 250
                : Lerp(250,climbing ? Math.Max(250,ClimbCas) : 300,(altitude*EnrouteProfile.FeetPerMetre-10000)/2000);
            if(climbing) cas=Math.Min(cas,ClimbCas);
            var limit=FlightAtmosphere.TrueKnots(cas,altitude);
            if(CruiseMach>0) limit=Math.Min(limit,CruiseMach*FlightAtmosphere.SoundKnots(altitude));
            return Math.Min(limit,EnrouteProfile.MaxCruiseMetresPerSecond(type)/CircuitProfile.KnotsToMetresPerSecond);
        }
        private static double Lerp(double a,double b,double t) => a+(b-a)*Math.Clamp(t,0,1);
        public static FlightOperatingProfile For(AircraftType type) => type?.Id switch
        {
            "SF34" => new(23000,210,0,1500,1000,700,1400,1500),
            "DH8D" => new(25000,210,0,2500,1500,1200,1500,1700),
            "E190" => new(37000,300,.78,2800,2200,1800,1600,2100),
            "A223" => new(37000,280,.78,2600,1800,1500,1700,2200),
            "A320" => new(37000,290,.78,2200,1400,1000,1500,2000),
            "A21N" => new(37000,290,.78,2200,1400,1000,1500,2000),
            "B738" => new(39000,290,.79,2500,2000,1500,1500,2200),
            "B38M" => new(39000,290,.79,2500,2000,1500,1500,2200),
            "A339" => new(39000,290,.82,2000,1200,1000,1500,2000),
            "A359" => new(39000,300,.85,2400,1800,1200,1500,2200),
            "B789" => new(39000,300,.85,2400,1800,1200,1500,2200),
            "B78X" => new(39000,300,.85,2200,1600,1100,1500,2200),
            "B412" => new(1500,110,0,1000,700,500,700,700),
            _ => new(25000,160,0,1200,1100,800,1500,1500)
        };
    }
}
