using System;
using Airside.Simulation;

namespace Airside.Presentation
{
    public static class ArrivalViewContinuity
    {
        public static bool Matches(ArrivalViewRecord record, FleetAircraft aircraft, long savedAt) =>
            record != null && aircraft != null && !aircraft.Type.IsRotorcraft
            && aircraft.Registration == record.Registration && aircraft.Type.Id == record.TypeId
            && (int)aircraft.State == record.FleetState && aircraft.StateStartedAt.ElapsedSeconds == record.StateStartedAt
            && aircraft.CurrentDestination?.Code == record.DestinationCode
            && Enum.IsDefined(typeof(RunwayDirection), record.Runway) && Valid(record, savedAt);

        public static bool Valid(ArrivalViewRecord record, long savedAt)
        {
            if (record == null) return false;
            static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
            var values = new double[] { record.X, record.Y, record.Z, record.EntryX, record.EntryY, record.EntryZ,
                record.ForwardX, record.ForwardZ, record.LastTime, record.HoldingStartedAt,
                record.Metres, record.Speed, record.Lateral };
            foreach (var value in values) if (!Finite(value)) return false;
            var direction = record.ForwardX * record.ForwardX + record.ForwardZ * record.ForwardZ;
            return record.Speed > 0f && record.Speed < 500f && record.LastTime >= 0
                && record.LastTime <= savedAt + 2 && record.Metres >= 0f
                && Math.Abs(record.X) < 10000000f && Math.Abs(record.Z) < 10000000f
                && record.Y >= -1000f && record.Y <= 50000f
                && Math.Abs(record.EntryX) < 10000000f && Math.Abs(record.EntryZ) < 10000000f
                && record.EntryY >= -1000f && record.EntryY <= 50000f
                && (!record.Holding || record.HoldingStartedAt >= 0 && record.HoldingStartedAt <= record.LastTime
                    && direction > 0.99f && direction < 1.01f);
        }

    }
}
