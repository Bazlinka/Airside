using System;
using Airside.Domain;

namespace Airside.Simulation
{
    public interface ISimulationClock
    {
        SimulationTime Now { get; }
    }

    public sealed class ManualSimulationClock : ISimulationClock
    {
        public ManualSimulationClock(SimulationTime initialTime)
        {
            Now = initialTime;
        }

        public SimulationTime Now { get; private set; }

        public void Advance(long seconds)
        {
            if (seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));

            Now = Now.Advance(seconds);
        }

        public void Set(SimulationTime time)
        {
            if (time.CompareTo(Now) < 0)
                throw new ArgumentOutOfRangeException(nameof(time), "Simulation time cannot move backwards.");

            Now = time;
        }
    }
}
