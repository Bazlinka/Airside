# Recorded weather and arrival continuity

Date: 2026-10-09
Status: Accepted — Bailey's instruction to continue fixing the audited mismatches
Supersedes: ADR 0099's presentation-only weather boundary and the transient-save
restriction in `2026-10-09-established-arrival-holding-presence`.

## Decision

Live weather and wind cannot describe a storm in the scene while airport rules use
an unrelated clear forecast. Record validated Open-Meteo samples in a pure simulation
timeline at the current processed time. Ground stops, arrival-entry commitment,
rotor movement/wind and runway selection read that timeline; presentation reads the
same kind/look/wind. The injected sample is an input, not a camera-driven decision.
Unknown periods use existing deterministic Weather/RunwayWeather. Preserve old input
intervals when newer observations replace them, so entry before storm onset stays
committed and saved outcomes replay. Latch and save existing final commitment before
a live storm replaces weather at the same whole-second timestamp, including the
opening arrival bank; reset that latch on a new inbound/parked phase. Use half-open intervals, explicit expiry event
boundaries, two-hour sample validity and no extrapolation past validity while closed.
Reusing a cached sample for a resumed/new airline grants only its remaining validity,
not another two hours; apply a currently healthy cached input to the new operations
instance after catch-up.
Disabling live weather or a known real-time stale sample ends its interval. Network
failure retains a valid observation until expiry, then uses forecast. Review-only
weather pins remain cosmetic test tools. The legacy demo retains its limited feed
presentation; the shared timeline applies to airline operations.

Persist active final/holding world pose, hold entry/heading/start and final reference.
After restore/catch-up, require registration, type, destination, fleet state and its
start time to match before applying an optional view record. Skip blank, nonfinite
or incompatible view records and invalidate ETA caches. A changed/completed journey
cannot be resurrected by its old model. The orbit continues on simulation time,
including elapsed time while closed. Runway/stand/curfew clearance remains simulation
owned. This does not add fuel/diversion or unsupported destination scenery.

## Migration

Save v23 adds flat optional WeatherObservations and ArrivalViews lists and an
ArrivalCommittedBeforeStorm aircraft flag. Core capture
copies weather records; the Unity host appends presentation snapshots before its
atomic write. Core restore validates weather history before catch-up; the host applies
compatible view records afterward. v1–22 ignore new optional fields and use their
previous deterministic weather, with presentation rebuilt. Missing/default records
are absence; malformed nonblank weather history fails save validation, while malformed
optional visual records are skipped. No nested nullable objects are added.

## Evidence and limits

53 focused headless checks pass: parked window/physical presence, observed-weather
boundaries, replay/copy isolation, pre-v23 migration, final commitment and stale
journey rejection, plus existing save/Operations/runway/hold regressions. Generated
harness is current. A Unity JsonUtility round-trip fixture covers the new flat fields
and omitted legacy lists; it is unrun here. Native compilation, serialization, actual
storm releases, visible reload/rejoin and performance remain unverified. No broad
suite, build or player review under the standing policy. Live input can now affect
operational outcomes; replay is deterministic given the recorded input history,
rather than claiming every network session produces the same outcome.
