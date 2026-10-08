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

## Inspected lifecycle and limits

Local parked/taxi/runway/arrival/go-around views retain identity across fleet sorting;
models are destroyed when fleet identities are removed, rather than on each state
change. Rotorcraft have their own continuous track. Cockpit exit and destruction
restore exterior renderers. The preceding storm commitment fix protects established
storm finals and holds new departures at stands.

Other inbound timer extensions (curfew, metering and a full apron) can still rewind
a route estimate. This needs a physical holding/diversion model, not permission to
land without a stand or through curfew; it is not claimed fixed by route visibility.
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
No broad suite, packaged build or player review under the standing policy.
