namespace Airside.Domain
{
    /// <summary>
    /// Airline career capability milestones (ADR 0053). Earned by completing explicit
    /// contract requirements, not by filling an arbitrary experience bar. Declared in
    /// earned order so a tier can be compared with &lt;/&gt;.
    /// </summary>
    public enum OperatingTier
    {
        Provisional,
        Regional,
        Domestic,
        International
    }
}
