using System;

namespace Airside.Domain
{
    public readonly struct SimulationTime : IComparable<SimulationTime>, IEquatable<SimulationTime>
    {
        public SimulationTime(long elapsedSeconds)
        {
            if (elapsedSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));

            ElapsedSeconds = elapsedSeconds;
        }

        public long ElapsedSeconds { get; }

        public SimulationTime Advance(long seconds) => new(checked(ElapsedSeconds + seconds));

        public int CompareTo(SimulationTime other) => ElapsedSeconds.CompareTo(other.ElapsedSeconds);

        public bool Equals(SimulationTime other) => ElapsedSeconds == other.ElapsedSeconds;

        public override bool Equals(object obj) => obj is SimulationTime other && Equals(other);

        public override int GetHashCode() => ElapsedSeconds.GetHashCode();

        public override string ToString() => $"T+{ElapsedSeconds}s";
    }
}
