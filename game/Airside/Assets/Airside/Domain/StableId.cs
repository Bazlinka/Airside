using System;

namespace Airside.Domain
{
    public readonly struct StableId : IEquatable<StableId>
    {
        public StableId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A stable identifier is required.", nameof(value));

            Value = value.Trim();
        }

        public string Value { get; }

        public bool Equals(StableId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is StableId other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value;
    }
}
