using System;

namespace Airside.Domain
{
    /// <summary>
    /// Identifies one flight's career settlement (ADR 0053): an aircraft registration plus
    /// its completed-trip number at the moment it settled. Registration is unique and a
    /// trip number only ever increases for that aircraft, so the pair is stable and can
    /// never repeat — the guarantee a settlement is applied exactly once relies on it.
    /// </summary>
    public readonly struct SettlementId : IEquatable<SettlementId>
    {
        public SettlementId(string registration, int tripNumber)
        {
            if (string.IsNullOrWhiteSpace(registration))
                throw new ArgumentException("A registration is required.", nameof(registration));
            if (tripNumber <= 0)
                throw new ArgumentOutOfRangeException(nameof(tripNumber));

            Registration = registration.Trim();
            TripNumber = tripNumber;
        }

        public string Registration { get; }
        public int TripNumber { get; }

        /// <summary>Stable string form for storage (save data, processed-settlement sets).</summary>
        public string Key => $"{Registration}#{TripNumber}";

        public bool Equals(SettlementId other) =>
            string.Equals(Registration, other.Registration, StringComparison.Ordinal) && TripNumber == other.TripNumber;

        public override bool Equals(object obj) => obj is SettlementId other && Equals(other);

        public override int GetHashCode()
        {
            var registrationHash = Registration == null ? 0 : StringComparer.Ordinal.GetHashCode(Registration);
            unchecked
            {
                return registrationHash * 397 ^ TripNumber;
            }
        }

        public override string ToString() => Key;
    }
}
