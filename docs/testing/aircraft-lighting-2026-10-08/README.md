# Fleet exterior lighting pass — 8 October 2026

## Task packet

Bailey requested realistic exterior aircraft lighting during taxi, movement, takeoff and flight,
building on the merged 7 October beam-profile work (#583/#584). Scope: all 13 fixed-wing types
and Bell 412; shared light presentation, fitted runtime lamp transforms, flight-state rules and
native regression/capture fixtures. Simulation, schedules, aircraft asset meshes, liveries,
saves and unrelated apron/world lighting are unchanged.

## Implemented

- Boeing 737-800/MAX 8 and 787-9/10 have a single white flash. A320/A321/A330/A350 retain two.
  Stable identity offsets prevent the entire fleet flashing in synchrony.
  Beam profiles remain specific to airframe size; A220/E190 and turboprop timing remain visual
  approximations pending installation-specific reference data. Retrofit LED fits can vary.
- Landing lamps operate during runway/low-altitude flight phases and extinguish at 3,048 m
  above the displayed field datum. They return on descent. This is the game's 10,000 ft policy,
  **not a universal regulatory rule or an MSL/pressure-altitude avionics simulation**.
- Taxi lights attach to the nose leg, following steering and retraction. They remain dark at
  stands, on tail-first pushes and while stopped. Existing jet nose/takeoff lamp policy stays.
- Native night fixtures showed the old Unity spot intensities barely reached the ground.
  Landing cones are tightened to prevent broad self-illumination of the forward fuselage,
  and emitters start just in front of the physical lens.
  Landing/taxi intensities are calibrated for visible ground illumination, while nav spill
  is reduced and directed outward. These are Unity intensity units, not certified candela.
- Landing beam aim uses each built lamp's actual height, keeping its axis inside the family
  range. Installation pitch stays fixed with altitude. Existing outward toe angles remain.
- Jet beacons that were authored at fin-top height move to the fuselage crown; missing jet
  belly beacons and missing white tail position lenses are added to the runtime installation.
  These are measured model fits, not claimed manufacturer-exact station coordinates.
- Red/green nav lenses remain coloured during the white flash. Each strobe gets a small clear
  lens beside its nav fitting. Nav emission follows port/starboard 110° and aft 140° sectors
  with a small edge fade. The local navigation-light spill uses directional cones; vertical coverage is an approximation.
- Unlit landing/taxi fittings remain visible. The existing graphics option disables
  Light components while retaining lens glow. Distant fleet/heli glows use the same landing-light policy,
  show forward-facing landing light only from ahead, show nav colours from the sides/aft,
  pulse with the type strobe pattern, and extinguish on an unpowered aircraft.

## Primary references

- [Airbus exterior-light overview](https://www.airbus.com/en/newsroom/news/2017-05-red-green-and-white-shedding-light-on-aircraft-illumination):
  light roles, position colours, and differences in installation counts. This does not certify
  the generated models' individual lamp coordinates or game intensity values.
- [Lufthansa aircraft-light explanation, 15 September 2026](https://www.lufthansa.com/tn/en/articles/explore-flying/aircraft-lights-explained):
  typical Airbus double/Boeing single strobe patterns, anti-collision engine-start/shutdown
  context and low-altitude landing-light use. Its taxi/turnoff wording is general rather than
  a type-specific maintenance diagram.
- [FAA AIM 4-3-24](https://www.faa.gov/air_traffic/publications/aim_html/chap4_section_3.html):
  moving/stopped taxi-light signalling and runway-entry strobe use; operator procedures vary.
- [FAA AC 20-30B](https://www.faa.gov/airports/resources/advisory_circulars/index.cfm/go/document.information/documentNumber/20-30B):
  position/anti-collision installation guidance. Beam ranges, intensity and exact flash
  durations are visual tuning rather than certified photometric values.

- [FAA position-light sectors, 14 CFR 25.1387 (2023 published edition)](https://www.govinfo.gov/content/pkg/CFR-2023-title14-vol1/pdf/CFR-2023-title14-vol1-sec25-1387.pdf):
  110° forward-side sectors and 140° aft sector. The game implements horizontal observer
  visibility and an edge fade; it does not model certified full solid-angle photometry.

## Validation

- Baseline headless: 1,866 passed, zero failures.
- Integrated headless with the profile fixture included: **1,888 passed, zero failures**.
- Final profile rules: **23 passed, zero failures** (headless).
- Final native lighting checks: **37 passed, zero failures**, including all 14 actual runtime builders.
- First full native suite: **2,359 passed, two failed, two inconclusive**. The Bell tail-light
  finding is repaired and passes the final focused rerun. The remaining existing
  `AdelaideEmergencyAviationTests.Air017_LoadsAtBell412ClassDimensions_WithRequiredSilhouetteParts`
  assertion expects 45 renderers; the unchanged committed Bell asset produces 51. It directly
  invokes the art loader, bypassing this pass's aircraft builder/fitting code.
- Static asset audit passes; final GUID/mirror counts are recorded in `validation.json`.
- Beam ground-hit checks pass.
- **Mac universal build passed**, executable verified at `work/builds/Airside.app/Contents/MacOS/Airside`.
  The 60-second script detector interrupted active Metal shader compilation twice; retrying
  with `AIRSIDE_BUILD_STALL_SECONDS=300` completed. The build stamp records base `0dc6e629`
  plus this final dirty lighting working tree. Editor-generated package/pipeline metadata
  changes were removed before commit.

Native fixture renders are separate from packaged gameplay.

Regression coverage: all 14 actual runtime builders; position-light presence; jet crown/belly
beacons; taxi lamp follows nose leg; forward/down landing-beam direction; cruise extinguishing;
physical off-lens visibility and idempotent fitting. Pure tests cover flash differences, phase
and height switching, stopped/backward taxi and nav visibility sectors.

Native captures: `AircraftLightingReview.Render`, producing five night views per aircraft
(taxi at beacon peak, takeoff at strobe peak, rear, cold stand, beam footprint) in `work/aircraft-lighting-review`. Reproduction:

```sh
/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath game/Airside \
  -executeMethod AircraftLightingReview.Render -logFile work/lighting-review.log
```

Packaged acceptance: watch taxi, stop/hold, takeoff, gear retraction, climb above the light
threshold and descent/landing in follow, cockpit and window views. Check busy-apron night
frame time and daylight readability. Fixture renders do not prove this complete live journey.

## Native visual review

70 original Unity captures were reviewed through the two fleet sheets and the beam sheet.
The first renders exposed weak beam reach and broad navigation spill; the final set uses
brighter, narrower landing/taxi illumination and outward navigation cones. The earlier
forward-fuselage overexposure is removed. Fixtures keep propellers stationary, so their
blade shadows are static and do not certify moving-propeller appearance.

- [Fleet part 1](night-fleet-1.jpg): ATR42, SF34, DH8D, E190, A223, A320, B738.
- [Fleet part 2](night-fleet-2.jpg): B38M, A21N, A359, A339, B789, B78X, Bell 412.
- [Beam footprints](beam-footprints.jpg): forward ground-light views for all 14 types.
- [Offline beam axes](beam-geometry.svg): geometric checks only.
- [Machine-readable validation](validation.json).
