# Aircraft appearance and liveries — 28 September 2026

This revision was developed from `origin/main` at `a30a3935` in an isolated checkout.
The screenshots use Unity 6000.3.23f1 and the actual `BuildAircraftForType` path,
including live materials, glazing batches, paint, labels and wing attachments.
They are studio review captures, not synthetic thumbnails or a claim of airport
playtest coverage. Software thumbnails are regenerated separately for the game.

## Designs

Each of the 13 types has an individually fitted composition in ADR 0150. All work
with existing player custom colours and live airline names. Three original
fictional colourways are supplied for every type:

- **Coastline Regional:** teal `#0F8B8D`, sand pinstripe and ivory mark.
- **Emu Air:** ochre `#B8742A`, slate pinstripe and ivory mark.
- **Southern Cross Link:** navy `#1F3A93`, sand pinstripe and ivory mark.

The existing Teal, Ochre and Navy swatches apply these colourways in airline setup
and the Airline page. Light custom colours receive a dark fin mark for contrast.
These are paint designs, not additions or replacements to the AI airline roster.

## Reproduce

```sh
python3 scripts/polish-aircraft-glazing.py ATR42 SF34 DH8D E190 A223 A320 B738 B38M A21N A359 A339 B789 B78X
python3 scripts/render-aircraft-thumbnails.py
bash scripts/sync-art-streaming-assets.sh
python3 scripts/test-aircraft-paint.py
python3 scripts/test-aircraft-connectivity.py
python3 scripts/audit-unity-assets.py
bash scripts/test-unity.sh
bash scripts/review-aircraft-liveries.sh
bash scripts/build-mac.sh
```

Python requires NumPy and Pillow. The finishing script also supports regenerating
only the livery from existing polished kits; it restores original spoiler/pylon
source before fitting them, so repeated runs do not progressively clip those parts.

## Baseline and validation

- Baseline headless suite: **933 passed**.
- Baseline full Unity suite: **1,223 passed, four failed, one skipped**.
- The baseline failures concern the 787-10 flight-planning fixture at Gate 13,
  two outdated Gate 13 position fixtures, and two ground-separation conflicts.
- Source/runtime art audit: **1,373 unique GUIDs, 336 matching mirrored art files**.
- Paint geometry: all 13 pass the exact skin distance check (3–25 mm), existing
  glazing/interior/pilot checks and fitted title layouts.
- All 13 retain their previous airframe envelope within 25 mm paint tolerance.
- Paint adds roughly 1,300–6,800 triangles per airframe and three or four mesh renderers.
  Existing shared materials and static glazing batches remain in use.

## Final Unity and geometry results

The revised suite finished with **1,237 passed, the same four failed, one skipped**
(1,242 total). All 14 new aircraft finish tests passed. Failure names match the
preserved baseline exactly; see [unity-results.txt](unity-results.txt). The global
Unity gate remains red, so this revision is on a feature branch for review.

The connectivity audit also passes for all 13 aircraft. Native front captures
cover all 39 type/colourway combinations. Side, opposite side, elevated rear and
overview renders of all 13 were checked during development; these preceded the
final broader cowl bands. Final front captures include the completed cowl bands.

## Visual review

- [Before and after: 737-8](before-after.jpg). Same Unity camera and studio light;
  the revised example also uses the final Coastline teal palette.
- [Nine-example overview](livery-options.jpg).
- [All 13 aircraft / three colourways each](index.html).

These images are native Unity captures, resized and labelled only. Airframe
geometry and materials in the images come from the game's runtime builder.


## Packaged Mac review

The Mac application built successfully in Unity 6000.3.23f1. After the live review
found the duplicate lamp-cache error, all four lamp helpers were corrected,
**18/18 focused paint/lamp regressions passed**, and the application was rebuilt.
All five final capture runs exited successfully with no exceptions in their logs.
The full simulation suite above preceded this four-line lookup fix; it was not
repeated for that change.

- [Saab 340 at noon](gameplay/saab-day.png), following `VH-PAX`.
- [737-8 at noon](gameplay/jet-day.png), following `VH-8IA`.
- [A350-900 at dusk](gameplay/widebody-dusk.png), following `A6-EVA` at 17:30.
- [737-8 at night](gameplay/jet-night.png), following `VH-8IA` at 23:00.
- [Airport overview](gameplay/overview-day.png), noon.
- [Four-view contact sheet](gameplay-review.jpg).

These show existing AI airline names and their colours on the original paint
geometry; the fictional studio examples do not replace the AI roster. The airport
retains its existing lighting, including strong midday highlights. These are short
appearance checks, not a long performance soak or a complete movement acceptance
run. No flight-planning, ground-separation or Gate 13 fix is claimed here.

Reproduce a packaged follow capture (change registration/time or omit the follow
arguments for overview):

```sh
work/builds/Airside.app/Contents/MacOS/Airside \
  -airsideSoak -airsideSoakMinutes 2 \
  -airsideReviewShot /tmp/airside-aircraft.png -airsideReviewDelay 8 \
  -airsideReviewAircraft VH-8IA -airsideReviewFollowZoom 0.8 \
  -airsideReviewTime 12:00 -airsideReviewWeather clear \
  -screen-width 1440 -screen-height 900 -screen-fullscreen 0 \
  -logFile /tmp/airside-aircraft.log
```

The isolated soak mode starts a fresh review airline and does not read the player's
save. The reviewed build is stamped `a30a3935-dirty`, meaning the parent commit plus
this working revision; it is not an unmodified main build. A separately named copy
was saved at `work/builds/Airside-Livery-Review-20260928.app` in the primary Mac
checkout. Screenshots and this evidence are committed; the application is local.
