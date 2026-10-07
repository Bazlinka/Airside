# An arrival on final never waits: it lands or goes around

Status: accepted
Date: 2026-10-07 (Bailey: no aircraft on final may be frozen waiting for a runway or taxiway to clear; arrivals always have priority)

- **Decision:**
  1. **Landing clearance depends on the runway only.** The tower's landing gate (`VacateClearOfTaxiing`) now checks the runway and its exit. The taxiway beyond the exit and the stand route are
     no longer part of it (they were the first 30 s of the taxi-in, ADR 0180): an aircraft elsewhere on the airport cannot hold an arrival. After touchdown the arrival waits at the exit in
     the existing `AwaitingStand` flow, where ground control resolves it.
  2. **Decision point.** `ApproachRules.FinalHoldLimitSeconds` (4 min) after joining final, an arrival with no clearance goes around (`SendAroundUnclearedArrivals`, deterministic, not the old
     1-in-11 draw). It re-joins after `GoAroundCircuitSeconds`. Curfew-held arrivals are left to the curfew rules (a go-around must not become a way past the curfew).
  3. **Metering.** An inbound joins final (`Inbound` → `HoldingForLanding`) only when its landing is expected within `ApproachRules.MeterHorizonSeconds` (3 min); otherwise it stays in the circuit
     (`FinalJoinTime`) so a queue is spaced out instead of stacked on short final. `ExpectedLandingQueueTime` takes an `asOf` instant so a big clock step meters from the instant being processed.
  4. A missed approach no longer counts as occupying the strip (`IsOccupyingRunway`): it climbs away and never touches the pavement.
- **Basis (to be re-checked against current publications before relying on it):** ICAO Doc 4444 landing clearance and runway separation (an arrival is not cleared to land unless the runway will
  be clear; otherwise it goes around), PANS-OPS stabilised approach (~1000 ft), CASA MOS 172 §10.12 wake minima (unchanged, `WakeSeparation`). The 4 min and 3 min are gameplay-scaled, like the
  90 s `RunwaySeparationSeconds` (ADR 0243), not published figures.
- **Reason:** `HoldingForLanding` was open-ended and cleared only when the whole ground picture was clear, so an arrival could sit on the approach for as long as any aircraft occupied its vacate
  or taxi-in path, or a long-held departure took the strip first.
- **Affected systems:** `AirlineOperations.Runway.cs`, `AirlineOperations.cs` (join, `NextEventAt`), new `ApproachRules`. No save field added: everything derives from state times.
- **Evidence:** `scripts/test-domain.sh`; `ArrivalPriorityTests` (three seeds over two days: no aircraft is on final past its decision point); tower tests updated for the new rule.
- **Unverified:** behaviour in Unity (drawn finals, go-around visuals, climb-out at overview/follow cameras, day/dusk/night); busy-day feel (more go-arounds are possible); a dedicated test that
  far-side taxi traffic does not hold an arrival (covered only by the soak).
- **Revert:** restore the taxi-in check in `VacateClearOfTaxiing`, remove `SendAroundUnclearedArrivals`/`FinalJoinTime` and the `IsOccupyingRunway` exemption.
