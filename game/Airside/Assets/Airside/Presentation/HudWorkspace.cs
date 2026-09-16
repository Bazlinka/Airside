namespace Airside.Presentation
{
    /// <summary>
    /// The single player workspace open at a time (ADR 0053). Replaces the previous
    /// independent per-panel booleans so exactly one of these — or none — is ever open.
    /// </summary>
    public enum HudWorkspace
    {
        None,
        Operations,
        Map,
        Fleet,
        Contracts
    }
}
