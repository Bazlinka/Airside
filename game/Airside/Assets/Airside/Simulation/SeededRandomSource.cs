using System;

namespace Airside.Simulation
{
    public interface IRandomSource
    {
        int NextInt(int minimumInclusive, int maximumExclusive);
    }

    public sealed class SeededRandomSource : IRandomSource
    {
        /// <summary>
        /// Remap for a zero seed: the xorshift state must never start at 0, or the
        /// sequence sticks there. A caller-supplied 0 is substituted instead of rejected.
        /// </summary>
        public const uint ZeroSeedSubstitute = 0x6D2B79F5u;

        private uint _state;

        public SeededRandomSource(uint seed)
        {
            _state = seed == 0 ? ZeroSeedSubstitute : seed;
        }

        /// <summary>Current generator state. Never zero; feed it back to the constructor to resume the sequence.</summary>
        public uint State => _state;

        public int NextInt(int minimumInclusive, int maximumExclusive)
        {
            if (maximumExclusive <= minimumInclusive)
                throw new ArgumentOutOfRangeException(nameof(maximumExclusive));

            var range = (uint)(maximumExclusive - minimumInclusive);
            return minimumInclusive + (int)(NextUInt() % range);
        }

        private uint NextUInt()
        {
            var value = _state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            _state = value;
            return value;
        }
    }
}
