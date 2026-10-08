# Performance bug fix — 7 October 2026

## Task packet

Player outcome: reduce garbage-collection pressure from repeated tower and presentation
queries while preserving crossing clearance times and aircraft catalogue matches.

Scope: `Simulation/RunwayCrossings.cs`, `Domain/AircraftCatalogue.cs`,
`Tests/EditMode/RunwayCrossingsCacheTests.cs` and its Unity metadata;
regenerate `scripts/dotnet-harness/Harness.Generated.props`.

Relevant invariants: deterministic clock/traffic clearance; reservations before movement;
frame rate independent simulation; unchanged saved schema. This is a narrow correction
to existing caching/lookup behavior and needs no design decision or asset import.
Aircraft motion, cockpit/camera, graphics, ground routing, HUD controls and save data
must remain unchanged.

Acceptance: warm crossing queries reuse filtered results; each runway/end and aircraft
envelope retains exactly the full scan's matching values; catalogue lookup preserves
fixed-wing and rotorcraft matches. Repeated warm headless queries allocate zero bytes.

## Proven defects

`RunwayCrossings.For` cached its expensive geometry scan, but re-filtered into a new
array every query whenever the full result contained the aircraft's own strip.
Even an empty filtered result allocated. `AirlineOperations.Runway.cs` polls this
function in both active-aircraft runway occupancy and prospective crossing clearance
(the loops near lines 179 and 196). Hold explanations reuse those checks.

The same query calls `AircraftCatalogue.TryFor`. Its `foreach` traversed arrays
through `IReadOnlyList<AircraftSpec>`, boxing/allocating an enumerator. This lookup
also serves per-frame presentation profiles and engine audio. Indexed traversal
preserves the original lookup order and equality comparison.

The test `RepeatedWarmQueries_AllocateNoFilteredArrays` uses a fixed ground leg,
Airbus A350 envelope, runway 05 and 100 warmup calls, then measures 10,000 queries
with .NET 8's `GC.GetAllocatedBytesForCurrentThread` outside assertion/reporting code.
With the original runtime source it measured **560,000 bytes** and failed.
Two result-identity tests also failed (3 failed / 3 passed before the fix).
With both corrections the same measurement reports **0 bytes**.
This establishes the allocation defect; it is not a native Unity frame-time result.
The allocation assertion is guarded by `NET8_0`; behavioral tests also compile for Unity.

## Implementation

Store full, main-only and cross-only arrays together in the existing leg/envelope
cache. Partition once when scanning; reuse the full array when it already matches
and `Array.Empty<RunwayCrossing>()` for empty partitions. No cache keys, scanner
sampling, strip boundaries, aircraft envelope or crossing ordering changed.
Use index loops for both fixed-wing and rotorcraft catalogue searches.

## Validation

Focused command (after sourcing `/workspace/airside-tools/activate.sh`):

```sh
dotnet test scripts/dotnet-harness/Harness.csproj --filter 'FullyQualifiedName~RunwayCrossingsCacheTests|FullyQualifiedName~TaxiCrossingAndQueueTests|FullyQualifiedName~AdelaideProtocolTests|FullyQualifiedName~MaintenanceJourneyTests' --logger 'console;verbosity=normal'
```

Final focused run: **50 passed / 0 failed**. `git diff --check`, harness
derivation check and Unity asset metadata/mirror audit pass (1,794 GUIDs, 386 art
mirrors, 70 character materials).

The final focused run covers partition equivalence and repeated result identity for
all four runway ends using a leg crossing both strips, separate envelope timing,
every catalogue type, warm allocation measurement, runway crossing/queue clearance,
Adelaide protocols and maintenance journey/save restoration.

Full integrated headless regression is coordinated by the root agent. No Unity
execution was performed, honoring the recorded owner restriction. Native compile,
Unity allocation profiling and graphics/frame-time impact remain unverified.
Native follow-up when authorized: run EditMode tests and profile a busy airport;
confirm allocations fall in these query paths and traffic clearance remains identical.
