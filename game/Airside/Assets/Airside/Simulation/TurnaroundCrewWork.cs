using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>Airport-provided labour, sampled from simulation time. No character/render state owns completion.</summary>
    public static class TurnaroundCrewWork
    {
        public const double SetupSeconds = 12;
        public const double ClearSeconds = 12;
        public const double CarrierStaggerSeconds = 2;

        // A deterministic handling allowance, not a claim about actual booked passenger baggage.
        public static int BagCount(AircraftType type) => Math.Max(1,
            (int)Math.Ceiling(AircraftCatalogue.TypicalSeats(type) * 0.65));
        public static int BagCarriers(AircraftType type) =>
            AircraftLayout.For(type).HalfSpan > 25f ? 4 : AircraftLayout.For(type).IsTurboprop ? 2 : 3;
        public static int CarrierBagCount(AircraftType type, int lane) =>
            Math.Max(0, (BagCount(type) + BagCarriers(type) - 1 - lane) / BagCarriers(type));

        /// <summary>Distinct lanes from the train to the same hold/belt, without multiplying the load.</summary>
        public static ((float X, float Z) A, (float X, float Z) B) BagLane(AircraftType type, int lane)
        {
            var layout = AircraftLayout.For(type);
            var door = layout.CargoDoor;
            var side = AircraftLayout.SideOf(door);
            var train = layout.BaggageTrain;
            var shift = lane * 0.85f;
            var a = layout.Clear(train.X - side * 1.2f, train.Z + Math.Sign(door.Z - train.Z) * 1.2f + shift, 0.35f, false);
            var b = layout.Clear(door.X + side * (layout.IsTurboprop ? 1.0f : 6.3f),
                door.Z + (layout.IsTurboprop ? -0.6f : 0.9f) + shift, 0.35f, false);
            return (a, b);
        }
        public static double BagCycle(AircraftType type, int lane)
        {
            var (a, b) = BagLane(type, lane);
            var dx = a.X - b.X; var dz = a.Z - b.Z;
            return ServiceChoreography.BagCycle((float)Math.Sqrt(dx * dx + dz * dz));
        }
        public static long BaggageSeconds(AircraftType type)
        {
            double done = 0;
            for (var lane = 0; lane < BagCarriers(type); lane++)
                done = Math.Max(done, SetupSeconds + lane * CarrierStaggerSeconds
                    + CarrierBagCount(type, lane) * BagCycle(type, lane));
            return (long)Math.Ceiling(done + ServiceChoreography.BeltSeconds + ClearSeconds);
        }
        public static RampActivity Activity(DeparturePrepStage stage) => stage switch
        {
            DeparturePrepStage.Fuel => RampActivity.Fuel,
            DeparturePrepStage.Catering => RampActivity.Catering,
            DeparturePrepStage.Baggage => RampActivity.Baggage,
            DeparturePrepStage.Boarding => RampActivity.Boarding,
            _ => RampActivity.None
        };
    }
}
