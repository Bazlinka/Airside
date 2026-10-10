using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>Automatic flight crew. Clock-derived inspection, briefing and boarding, with no persisted people.</summary>
    public static class FlightCrewWork
    {
        public const float WalkPace = 1.3f;
        public const double CheckSeconds = 6;
        public const double BoardingSpacingSeconds = 7;
        public static int CabinCrew(AircraftType type) => AircraftCatalogue.TypicalSeats(type) > 250 ? 4
            : AircraftLayout.For(type).IsTurboprop ? 1 : 2;
        public static int Count(AircraftType type) => 2 + CabinCrew(type);
        public static (float X, float Z)[] InspectionRoute(AircraftType type)
        {
            var l = AircraftLayout.For(type);
            var wing = l.HalfSpan + 2.0f;
            var nose = l.NoseZ + 3f;
            var tail = l.TailZ - 3f;
            // Walk outside the wing/propeller envelope; inspect nose, right wing, tail and left wing.
            return new[] { (0f, nose), (wing, nose), (wing, tail), (-wing, tail), (-wing, nose), (0f, nose) };
        }
        public static CrewAction Waiting(AircraftType type, int slot)
        {
            var layout = AircraftLayout.For(type); var door = layout.PassengerDoor;
            var at = layout.Clear(door.X + AircraftLayout.SideOf(door) * (5.8f + slot * .8f), door.Z + 4, .5f, false);
            return new CrewAction(at.X, at.Z, 0, (float)(Math.Atan2(door.X-at.X, door.Z-at.Z)*180/Math.PI), false, CarriedItem.None);
        }
        public static long InspectionSeconds(AircraftType type)
        {
            var route = InspectionRoute(type);
            double seconds = 0;
            for (var i = 1; i < route.Length; i++)
                seconds += Length(route[i - 1], route[i]) / WalkPace + CheckSeconds;
            return (long)Math.Ceiling(seconds + TurnaroundCrewWork.ClearSeconds);
        }
        /// <summary>Captain's walkaround. Check pauses occur at each safe inspection station.</summary>
        public static CrewAction Inspection(AircraftType type, double elapsed)
        {
            var route = InspectionRoute(type);
            elapsed = Math.Max(0, elapsed);
            for (var i = 1; i < route.Length; i++)
            {
                var a = route[i - 1]; var b = route[i];
                var walk = Length(a, b) / WalkPace;
                if (elapsed < walk)
                {
                    var t = (float)(elapsed / walk);
                    return new CrewAction(a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t, 0,
                        (float)(Math.Atan2(b.X - a.X, b.Z - a.Z) * 180 / Math.PI), true, CarriedItem.None);
                }
                elapsed -= walk;
                if (elapsed < CheckSeconds)
                    return new CrewAction(b.X, b.Z, 0, (float)(Math.Atan2(-b.X, -b.Z) * 180 / Math.PI), false, CarriedItem.None);
                elapsed -= CheckSeconds;
            }
            return new CrewAction(route[0].X, route[0].Z, 0, 180, false, CarriedItem.None);
        }
        private static float Length((float X, float Z) a, (float X, float Z) b) =>
            (float)Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));
    }
}
