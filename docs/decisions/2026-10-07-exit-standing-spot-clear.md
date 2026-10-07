# Landing gate also checks the exit's standing spot

- **Date:** 2026-10-07
- **Decision:** `VacateClearOfTaxiing` (the tower's landing gate, shared by the displayed estimate and the real clearance) now
  also asks `GroundTraffic.StandingSpotClear`: no other aircraft may come within planning clearance of the end of the arrival's
  exit from 8 s before it stops to 20 s after (`ExitStandingWindowSeconds` = 28 s). The runway-and-exit-only scope of ADR
  2026-10-07-arrivals-never-hold-on-final is unchanged; this only extends the check in time at the same place.
- **Reason:** `GroundSeparationTests.BusyDay_NoAircraftDriveThroughEachOther` went red on `main` after #552 tightened it (real runway
  poses): DH8D VH-QON stopped at its exit end (472,244) as B38M DQ-FAE, cleared earlier, taxied past 25 m away (overlap limit
  29 m). The old check sampled the arrival only until it reached the exit end; it then stands there until ground control moves it,
  so traffic arriving moments later, or the landing estimate drifting by seconds, went unchecked.
- **Tuning:** an 85 s window also passed the separation test but dropped the displayed-estimate test (`ArrivalClearanceTests`) to
  67/82 exact (needs 85 %), because a long window depends on traffic that has not been cleared yet. 28 s keeps both.
- **Affected systems:** `GroundTraffic.StandingSpotClear` (new), `AirlineOperations.Runway.cs`. No save change; derived from state times.
- **Migration impact:** none. A few more go-arounds are possible when a taxi-out passes the exit as an arrival lands.
- **Verification:** `scripts/test-domain.sh`. NOT verified in Unity (drawn finals, go-around frequency, busy-day feel).
- **Revert:** remove the `StandingSpotClear` call in `VacateClearOfTaxiing`.
