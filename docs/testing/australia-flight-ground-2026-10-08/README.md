# Australia flight ground verification — 8 October 2026

Task #613; Codex cloud Agent 1. Runtime and offline real-data overhaul, no simulation/save changes.

## Bounded checks

- Seven changed runtime/test C# files: Roslyn C#9 syntax check, zero errors.
- Focused no-package console (source retained as `FocusedChecks.cs`): 1,700 assertions passed
  against every shipped country/approach SATG and SALC file, every Australian destination,
  mapped runway touchdown coordinates/elevation, all 250 m approach cell corners, cruise
  hysteresis and the nine-tile detail bound. It compiles the six actual changed/pure runtime
  sources against the cached pure core; it does not substitute for Unity compilation.
- Source-generating scripts pass Python compilation. New runtime assets have distinct committed
  Unity GUIDs and exact editor/StreamingAssets copies. OSM feature coordinates and grid payloads
  have an offline audit in `scripts/check-australia-flight-data.py`.
- Standard local focused NUnit and `update-harness.py` build attempts were blocked by NuGet
  repository-signature HTTPS503; no extended package setup/full local suite was run. The added
  harness entry was written using the generator's own writer. Independent CI confirms the
  harness list is current and compilation against Unity NUnit3.5/no implicit usings passes.
- The repository-wide Unity asset audit reports the already existing Adelaide satellite image
  mismatch: at initial main 372c0ad8 the source blob is `4ba0fff9af840bb7895f2787a6c6ad5a788627a2`
  and packaged blob is `9cb46a8af24bc46c75001f0ab0a2c54848416ed0`. This task does not change them.

## Independent acquisition

Cloud fresh resource requests and uploads returned HTTPS503 from the session proxy's upstream
Cloudflare tunnel. Requests preserved the proxy and verified TLS. The successful independent
GitHub runner acquired all eighteen regional airport approach grids (run 37732831774), then
another runner publishes the verified national/airport assets to this task branch. Temporary
acquisition workflow is removed in the final ready PR. The shipped source/hash manifest and
successful offline audit establish the actual data; a resampled grid is not higher resolution
than its source. Country heights come from zoom6 Terrarium (~2 km); airport heights come from
zoom11 (~60 m); terrain renders 2 km cruise cells or 250 m airport cells. WorldCover class grids
are .025° country and .001° around fields. Coastlines and terrain fall back offline if absent.

## Final data and CI evidence

Independent publication run 37733327101 succeeded and committed actual datasets at
`8616b88c258200cc13741d5949515ef1871f4801`. Its offline audit passed all 56 shipped geodata
files, every packaged mirror and OSM coordinate checks: Hobart field 4 m, kunanyi 1262 m,
Frederick Henry Bay water class 0. The same audit passed again on the full local dataset.
The final focused console passed 1,700 assertions. Seven runtime/test C#9 source checks pass.
CI run 37733327108 passed generated data, current harness list and Unity NUnit3.5 compile;
automatic broad headless run has 2,005 passes and precisely the three existing failures:
`BusyDay_NoAircraftDriveThroughEachOther`, `NightSkyReviewFraming_PutsDrawableCruiseInUpperHalfOfFrame`
and `NightSkyReviewYaw_FacesADrawableOverflightSector`. No new failure appears in that run.

## Native checks left for Bailey's chosen playtest

No Unity launch, packaged build, rendered review, gameplay journey or long-flight soak was run.
Check the Adelaide–Hobart journey at cruise and on final: Tasmanian relief and shoreline,
kunanyi behind the airport, real runway position/paint, mapped taxiways/apron/terminal and
surrounding roads/buildings. Check day/dusk/night, aircraft-origin changes, airport density
transitions and frame time. Confirm return to overview hides the mapped airport with the
flight world. Resident terrain is bounded to 49 near and 49 cruise horizon tiles (wide overview
retains its earlier bound), with one terrain mesh and at most 24 airport features built per frame; each field retains at most 4,096 features, with mapped airside infrastructure prioritised.
These budgets describe implementation limits; they are not a measured frame-rate guarantee.
