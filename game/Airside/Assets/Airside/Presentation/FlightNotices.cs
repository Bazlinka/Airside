namespace Airside.Presentation
{
    /// <summary>Player toasts for a flight that has landed away, and for one that is home and free to send again (ADR 0164).</summary>
    public static class FlightNotices
    {
        public static string LandedAtDestination(string registration, string destination)
        {
            var where = string.IsNullOrEmpty(destination) ? "its destination" : destination;
            return $"{registration} has landed at {where}.";
        }

        public static string ReturnedHomeAwaitingDispatch(string registration) =>
            $"{registration} has returned home and is awaiting dispatch.";
    }
}
