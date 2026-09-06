namespace Airside.Simulation
{
    /// <summary>
    /// Physical capacity of the airfield. Starts with two stands; the player can
    /// buy a third as the first buildable upgrade. Stand count is reconstructed
    /// by replaying the <c>build-stand</c> command — no save-schema field.
    /// </summary>
    public sealed class AirportCapacity
    {
        public const int BaselineStands = 2;
        public const int MaximumStands = 3;
        public const long ThirdStandCost = 8000;

        public int StandCount { get; private set; } = BaselineStands;

        public bool HasThirdStand => StandCount >= MaximumStands;

        public bool CanExpand => StandCount < MaximumStands;

        public bool Expand()
        {
            if (!CanExpand)
                return false;

            StandCount++;
            return true;
        }
    }
}
