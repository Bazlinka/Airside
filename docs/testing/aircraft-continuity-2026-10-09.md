# Aircraft continuity audit — 9 October 2026

## Confirmed fixes

- The local airport projection intentionally ends outbound visibility after climb-out.
  Overview and ordinary follow now retain a real journey pose while within the camera
  volume, independently of that timer. Explicit follow is pinned outside that budget.
- Ordinary follow now shares cockpit flight terrain/origin handling, rather than
  following a departing model into an empty scene.
- The view identity registry was coupled to camera cycling, and distant approach
  filtering removed aircraft from lookup. Register physical presence first; filter
  cycling separately. Direct selection can follow a physically present model.
- An established final whose ETA is lost or postponed beyond the 32 km display
  window now retains the same model in a tangent holding orbit, with a single climb
  to at least 1.5 km. A usable estimate rejoins through bounded final-pose slew.
  Genuine clearance/state changes retain the held pose through a rate-bounded handoff,
  instead of discarding offsets larger than 4 km or forcing a six-second blend.
- Mini-map, ordinary follow origin/terrain and selected aircraft altitude/status use
  the actual final/held pose rather than a deadline-derived route that may rewind.

## Inspected lifecycle and limits

Local parked/taxi/runway/arrival/go-around views retain identity across fleet sorting;
models are destroyed when fleet identities are removed, rather than on each state
change. Rotorcraft have their own continuous track. Cockpit exit and destruction
restore exterior renderers. The preceding storm commitment fix protects established
storm finals and holds new departures at stands.

Other inbound timer extensions still affect the operational route estimate; an
already established rendered arrival stays physically present instead of rewinding
with that estimate. Holding is a transient presentation of an existing delay, not
new clearance or a complete ATC/fuel/diversion system. Its orbit is not saved; a reload
reconstructs presentation from saved operational state rather than preserving the
exact holding phase. Native final/hold/rejoin and cleared-landing timing remain
unverified, especially when clearance occurs between rendered ETA updates.
Live weather remains cosmetic while operational weather uses the deterministic
chain; this pre-existing split remains a broader design mismatch to address explicitly.
An expired live-feed target and an unsupported network outstation still leave their
limited presentation, with service retained in Fleet. They are separate from the
authored Adelaide arrival lifecycle. Regional ground presentation requires a known
runway; this change does not create airport scenery for unsupported destinations.

## Parked activity recommendation

Keep all owned aircraft in Fleet and physically parked in 3D. Exclude long-idle parked
aircraft from active-flight counts and automatic follow cycling until scheduled
pushback is within two hours; retain explicit selection. An overdue aircraft waiting
for weather stays active, and moving/airborne aircraft never depend on that window.
This recommendation has not changed activity UI policy yet.

## Evidence

Headless compile checks the pure presence policy. Roslyn parses changed Unity-facing
C#; neither establishes Unity compilation, camera appearance or terrain performance.
Holding/approach regressions pass 16/16, including retained-estimate boundaries,
initial heading/speed, sustained orbit motion and monotonic climb over several laps.
No broad suite, packaged build or player review under the standing policy.
