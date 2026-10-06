# Aircraft identity refresh — 6 October 2026

Parent main: `19fd9688`. ADR 0234. All fourteen current flying types are covered:
thirteen fixed-wing types and the Bell 412. The parked static rescue model shares
the Bell kit with its default emergency colour; player/AI fleet helicopters use
their operator colour and support in-place palette repaint.

[Full fleet](all-aircraft.jpg) and [five palettes on the A320](five-presets.jpg)
are software geometry proof sheets, without live titles. They are not Unity or
packaged-game captures. Fixed-wing paint shapes differ in coverage and placement;
the A220 sun rays and 787-10 meridian are enlarged, and the Bell gets fitted panels.

## Evidence

- Baseline supplementary headless suite: 1,490 passed, zero failures.
- Fixed-wing geometry audit: all thirteen preserve every non-paint position/index
  array byte-for-byte relative to parent main (`geometry.log`).
- Fitted paint/glazing/pilot/title audit: thirteen types pass, skin offsets 3–25 mm.
  The first run found a pre-existing stale A320 door lookup: committed x −1.91 m,
  mesh x −1.92 m. Parent meshes reproduce it; the derived lookup is corrected by
  1 cm to match the unchanged door. No door mesh or title layout was changed.
- Existing audio fixes already on main via PR #507 (`e5151ded`). All thirty locally
  modified jet WAVs and their manifest match current main byte-for-byte. The
  remaining two local source differences omit main's helicopter support, so
  overwriting main with those would regress it. Current generator byte check:
  41 AudioClips + thirteen fixed-wing profiles pass (`audio.log`).
- Unity asset audit: 1,663 unique GUIDs; 349 mirrored art files byte-identical;
  seventy committed character materials; passes.

- Changed supplementary headless suite: 1,490 passed, zero failures (`domain.log`).
- Unity EditMode suite: 1,933 passed, zero failures, two existing inconclusives
  (`unity-results.xml`). This run covered the paint/Bell changes; the subsequent
  1 cm door-table correction is covered by the changed headless suite and final
  native renderer compilation.
- Connectivity: all thirteen fixed-wing assemblies chain to the hull within 5 cm.

- Native runtime-builder review: all fourteen types rendered and inspected from
  side, opposite side, elevated front and overview (56 final captures). Compressed
  four-view sheets are under `native/`; aggregate [side](native/all-side.jpg) and
  [front](native/all-front.jpg) sheets cover the complete fleet. Live title paint
  is included for fixed-wing types. Bell review exposed inward paint winding and
  coincident fin paint layers; the final shells face outward and its emblem sits
  22 mm proud, above the 12 mm primary layer.
- Fixed-wing triangle total: 1,579,518 → 1,619,539 (+40,021; about 2.5%). No packaged
  frame-time claim is made.
- Native compilation hit the local shared C# compiler stall. Only that review's
  verified compiler children were stopped; `/shared` was removed from this isolated
  checkout's ignored Bee JSON and compiled DAG cache. The next compile completed in
  20 seconds. No tracked project or compiler configuration changes are shipped.

- Mac player build: passed from clean source commit `4d5b3965` (`build.log`).
  Bundle: `work/builds/Airside.app`. Build identity records `dirty=false`.
  The later validation/compositor commit changes review tooling and evidence only.
- Publication and merge explicitly approved by Bailey on 6 October 2026. Latest
  freight/metadata main (`18a65490`) is integrated before publication; combined
  regression evidence follows below. Original review/build evidence above is from
  the pre-integration source and is retained with its exact revision. A native
isolated render does not validate packaged camera interaction, night/weather,
repainting UI, freighter appearance or frame time. No claim of full-fleet packaged
playtesting is made by this evidence.

## Reproduce

```sh
python3 scripts/finish-aircraft-liveries.py
python3 scripts/generate-air-017-bell-412.py
python3 scripts/render-aircraft-thumbnails.py
bash scripts/sync-art-streaming-assets.sh
python3 scripts/test-aircraft-paint.py
python3 scripts/test-aircraft-connectivity.py
python3 scripts/audit-unity-assets.py
python3 scripts/audio/generate_aircraft_audio.py --check
bash scripts/test-domain.sh
bash scripts/test-unity.sh
python3 scripts/render-livery-overhaul.py docs/testing/aircraft-identities-2026-10-06
```

Native sheets can be reproduced after `AircraftAppearanceReview.Render` has
written all fourteen types with `-aircraftReviewViews front,side,opposite,overview`:

```sh
python3 scripts/assemble-aircraft-identity-review.py work/identity-native docs/testing/aircraft-identities-2026-10-06/native
```

All new art is original deterministic project-owned geometry. Existing airline
names in simulation are unchanged; no real airline logo or livery has been copied.

## Combined main regression

Current main `18a65490` (AI freight and Unity metadata repair) is integrated at
`a392b080`. Combined headless suite: **1,508 passed, zero failures**
(`combined-domain.log`). Required GitHub CI passed on PR #529.

Combined native Unity: **1,951 passed, one failure, two inconclusives**, 1,954 total
(`combined-unity-results.xml`). The failing busy-day ground-separation test records
a Dash 8 parked/taxi-in overlap and projects the Bell helicopter onto a fixed-wing
runway takeoff. To distinguish this from the paint change, all six changed C# files
were temporarily replaced with exact `origin/main` source and only that test run.
It reproduces the same two episodes on main (`baseline-main-ground-results.xml`).
The refresh source was then restored. This inherited simulation/test issue remains
open; the native suite is not reported as wholly passing. No ground-traffic change
is included in this visual refresh.

Combined asset audit passes (1,665 unique GUIDs, 349 mirrored art files and
70 character materials). The comparison encountered the same local shared-compiler
stall and used the earlier ignored Bee-cache workaround. The pre-integration clean
Mac build remains the recorded build; the combined revision is not claimed to have
a new packaged build or full gameplay acceptance.
