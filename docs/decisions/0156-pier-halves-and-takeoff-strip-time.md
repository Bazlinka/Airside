# 0156 — Pier halves count as wasting a code E gate; restore uses the tower's takeoff strip time

Date: 28 September 2026. Author: Claude, bug-fix pass at Bailey's request.

## 1. A 787-10 could find nowhere to park

`FlightPlanning_UsesEachTypesOwnCruiseAndPracticalRange` had failed on `main` for a while with "a
Boeing 787-10 cannot park on GATE-13". The test's fallback took the next gate in list order whatever
its code, but the fallback only ran because `SuggestStandFor` returned nothing for the 787-10, and that
was a real allocation bug.

Adelaide has six code E gates (18, 20, 22L, 25, 26L, 28L). 20, 22L and 28L share a pier with 20R, 22R
and 28R (`SharedPierPairs`), so an aircraft on either half closes the other. `WastesStand` kept code C
jets off the code E gates themselves but not off those R halves. With one of every type parked in
catalogue order, the 737-8, A321neo and E190 took 20R, 22R and 28R while ten plain code C gates (13, 15,
12L, 14L, 16L, 16R, 17, 19, 21, 27) stood empty. The last widebody then had no stand.

`WastesStand` now also counts a stand whose pier sibling is a gate the type is too small for
(`BlocksLargerSibling`). It is a ranking preference like the rest of `WastesStand`, not a ban: a jet still
takes 20R when nothing else is free. The test's fallback now picks a free stand that fits.

## 2. A loaded save drifted from one that never stopped

GAME.md (ADR 0153 notes) recorded that `ReconcileRunwayFreeAt` "recomputes on restore instead of
trusting the saved value". It only ever raises a strip's free time to cover a movement in progress,
but its idea of "in progress" was wrong for takeoffs. When the tower clears a takeoff it frees the
strip after lineup + ground roll + wake: an earlier fix removed the climb-out from the hold. The
restore path still held the strip for the whole `TakingOff` state (climb-out included) + wake. A
save taken mid-takeoff therefore loaded with a later free time: 10 974 s saved became 10 986 s in
`ResumedGame_ContinuesExactlyLikeOneThatNeverStopped`, and every tower decision after that could differ.

`StripBusyUntil` now uses the tower's own rule for a takeoff (lineup + rounded roll, capped at the
state end). `IsOccupyingRunway` shares it, so hold reasons and the one-per-strip checks now agree with
the tower as well.

## Evidence

Headless: domain suite 936/936, with two new tests that fail on the old code —
`OperationsRealismTests.CodeCJets_KeepOffThePierHalfThatClosesACodeEGate` and
`DualRunwayTowerTests.ReconcileRunwayFreeAt_KeepsTheTowersOwnFreeTimeMidTakeoff`.

The Unity-only suites `AirlineSaveTests`, `TerminalGateOperationsTests` and `GroundSeparationTests`,
plus the catalogue test, were run headlessly with small stand-ins for `Mathf`, `Vector2/3` and
`JsonUtility` (System.Text.Json with fields). That run was only a check and is not committed. On clean
`main` it matched the recorded state: only `Reservations_GateLeadIn...` failed. With both fixes,
`ResumedGame_ContinuesExactlyLikeOneThatNeverStopped`, `BusyDay_NoAircraftDriveThroughEachOther`, both
`Gate13_*` tests and the 787-10 test pass. `Reservations_GateLeadInAndRunwayHeldBeforeMovementAndReleased`
still fails; it is the open "hold a scheduled operator's gate while it is away" question and needs
Bailey's call. It now resolves to GATE-12L rather than 23, and is still never double-booked.

**Not run in Unity here.** `scripts/test-unity.sh` is the source of truth.

## Balance note

Stand choice for code C jets changes: they now prefer plain code C gates over 20R/22R/28R. Nothing is
charged differently. Taxi distances, and so timings, shift slightly for those jets.
