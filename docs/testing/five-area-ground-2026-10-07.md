# Ground safety and save restore — 7 October 2026

**Historical branch packet.** The full-route pre-landing guard below was superseded
by Bailey's accepted runway/exit-only arrival policy on current main. Final geometry,
positive static clearance and current-policy audit are recorded in
[five-area-ground-static-clearance-2026-10-07.md](five-area-ground-static-clearance-2026-10-07.md).
The 43-test and 24-hour results below describe the earlier isolated branch, not the
final integrated implementation.

## Task packet

Player outcome: taxi routes hold for parked aircraft along the shared regional bay
corridor; helicopter liftoff does not push a runway holding queue backward; optional
save metadata deserialized as an empty inline object does not prevent resume.

Scope: `GroundTraffic`, the rotorcraft exclusion in `FleetVisual.IsLiningUp`, strict
optional-record normalization in `AirlineSave`, and focused EditMode tests. The
native busy-day fixture now excludes rotorcraft from its fixed-wing runway curves;
`HelicopterTrack` owns the actual pad pose, and `RotorcraftTests` still cover pad and
rotor-disc clearance. It previously manufactured a helicopter runway collision.

Invariants: injected clock, deterministic traffic decisions, reservations before
movement, exact-once repair completion and backwards-compatible v22 save fields.
No new persisted field/version, aircraft art, cameras, buttons or UI behavior.

Acceptance: parked traffic outside the candidate's own stand approach blocks a
route; the same route clears after its blocker leaves. Rotorcraft contribute no
fixed-wing lineup queue slots and expose no fixed-wing runway ground pose. Only an
entirely empty optional wake/maintenance record is treated as absent; partially
populated corruption and a Maintenance aircraft without a real job still fail.

## Findings and changes

A Unity-free 24-hour seed-2026 probe reproduced a taxi-in Dash 8-400 overlapping a
parked Dash 8-400 beside the bay corridor at t=49626: `(1023.56, 527.50)` versus
`(1028.20, 501.90)`. `PathClear` previously excluded every AtStand aircraft. It now
includes their stationary bounds/poses. Existing own-stand geometry tolerance
remains limited to the candidate's 90 m lead-in/push area; parked aircraft elsewhere
block the whole route. A blocked route waits for clearance, without route or graph
changes. The initial post-landing guard exposed a seed-2026 mutual hold: QOM
reserved BAY-4 after landing and waited for parked QOK at BAY-2, while QOK's booked
departure waited for QOM at the exit. Its 24-hour probe found zero overlaps but no
fixed-wing progress, so that implementation was not accepted.

The follow-up checks the full taxi-in for parked obstacles before clearing the
landing, retaining the existing 30-second horizon for moving traffic. QOM now waits
airborne while QOK leaves its stand; the exit remains available to the departure.
A focused fixture proves the obstacle lies beyond the original 30-second horizon,
blocks landing, and releases landing once the parked aircraft leaves. In the actual
seed-2026 opening bank, at one hour QOK is Outbound and QOM has completed its inbound
trip and is TaxiOut on its next rotation.

`FleetVisual.IsLiningUp` counted a TakingOff helicopter as a runway lineup. That
shifted actual fixed-wing queue positions until the helicopter's nominal lineup
period elapsed. It now excludes rotorcraft; `GroundTraffic.TryPose` also refuses
their fixed-wing runway reconstruction.

Unity serializes ordinary inline classes by value and cannot reliably retain a
CLR null for these fields. The v21 wake and v22 maintenance records lacked presence
flags. An all-default deserialized MaintenanceJob attempted restoration on a parked
or airborne aircraft, and an empty wake had an invalid type ID. Restore now
normalizes exactly empty placeholders to absence and retains validation for every
non-default persisted field. The regression simulates the deserialized values; an
actual JsonUtility roundtrip remains a native verification gate.

## Evidence and remaining gates

Focused headless command (using `/workspace/airside-tools/activate.sh`):

```sh
dotnet test scripts/dotnet-harness --no-restore \
  --filter 'FullyQualifiedName~AirlineSaveRestoreTests|FullyQualifiedName~TaxiCrossingAndQueueTests|FullyQualifiedName~MaintenanceJourneyTests' \
  --logger 'console;verbosity=normal'
```

**Initial focused headless result: 41 passed, 0 failed.** `git diff --check` passed.
**Follow-up focused result: 43 passed, 0 failed (4.81 seconds)**, including the
pre-landing parked-blocker and actual opening-bank liveness regressions.
**Existing opening/day throughput checks: 2 passed, 0 failed**:
`NewGame_OpeningDepartureReachesTakingOffWithinAutoTakeoffCaptureWindow` and
`RealisticAdelaideDay_AboutAHundredAndTenDeparturesWithABigDawnWave` (the latter
requires 95–140 airline departures, at least 15 dawn departures and 15 night stops). **Historical isolated-branch supplementary 24-hour seed-2026 probe: 0 overlapping pairs; 129 completed
trips (114 fixed-wing, 15 rotorcraft).** QOK and QOM each completed four trips. It
samples every two seconds, uses the native busy-day test's operating-hour and
own-stand exemptions, places parked aircraft from their stand pose and other
fixed-wing ground traffic through `GroundTraffic.TryPose`, and leaves rotorcraft
placement to their dedicated tests. This checks simulation geometry rather than
native presentation; the Unity test still needs its own rerun.

Retained probe source: [five-area-ground-2026-10-07-probe.cs.txt](five-area-ground-2026-10-07-probe.cs.txt).
Output: [five-area-ground-2026-10-07-probe.log](five-area-ground-2026-10-07-probe.log).
The source ran in a temporary net8.0 console project with implicit usings enabled,
referencing the built `scripts/dotnet-harness/bin/Debug/net8.0/Harness.dll`.

These historical branch results do not certify current main. Root runs the
integrated current-policy full harness after merging the final geometry and fixed
obstacle margin. The failed intermediate
mutual-hold probe is superseded by the pre-landing guard, not accepted as successful
traffic validation.

The checks include empty metadata plus exact continuation for three hours,
partial-corruption rejection, all existing maintenance phase restore/catchup checks,
rotorcraft queue exclusion and parked-blocker clearance/release. Root runs the
integrated full harness after combining all five areas.

No Unity editor/player, builds, native EditMode run or visual capture was executed,
per the retained user restriction. The three previously reported native save
failures and native busy-day test must be rerun once that restriction is lifted;
this packet does not certify their native outcome. Native JsonUtility serialization,
actual taxi/helipad appearance and busy-day visual smoothness remain unverified.
