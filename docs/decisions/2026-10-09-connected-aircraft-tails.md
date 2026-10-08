# Connected, type-specific aircraft tails

Status: Accepted under Bailey's fleet-tail correction request
Date: 2026-10-09
Task: #684

## Decision

Refit the tails of all fifteen active airframes (thirteen airliners, Bell 412 and
Parafield trainer) at their existing runtime paths. Also refit the A320 authored
fallback used by the source-adaptation pipeline. Original metre-authored profiles
live in `scripts/aircraft_tail_profiles.json`; `aircraft_tail.py` creates rounded
sections, embeds fin/dorsal roots into the actual curved hull, and matches rudders
and elevators to the fixed tail's local section. Preserve moving-part names.
Conventional stabiliser carry-throughs follow the aft hull; ATR/Dash 8 retain
T-tails with an overlapping crown saddle. Bell boom/rotor datums are retained.

Correct directly documented stabiliser widths: Dash 8 9.27 m, E190 12.08 m,
A320/A321 12.45 m, A350 18.79 m, A330-900 19.40 m and 787 19.81 m.
The A321 and 787-10 tails translate aft with the fuselage extension instead of
stretching their shorter sibling's physical tail. Fin sweep/chords remain
project-authored approximations informed by manufacturer three-view drawings;
these are miniature game models, not certified aerodynamic surfaces.

## Reason

The previous straight fin roots bridged over the curved pressure body. Separate
box elevators and short rudder boards did not follow their parent tail's taper,
creating daylight gaps and mismatched edges. Scaled donor tails also made some
stabilisers substantially too wide and stretched tail chords on longer variants.

## Affected systems

Aircraft model generators/finishing, active glTF/bin/FBX, Hangar thumbnails and
StreamingAssets mirrors. The finishing pass preserves all unrelated mesh arrays,
including cockpit/cabin glazing, wings, engines, gear and existing hull paint.
The Bell retains its two-blade tail rotor. Lamps/antennae mounted on revised fins
move to their actual attachment surfaces. No new external mesh or texture.

## Migration impact

None: same model paths/GUIDs, same articulated part names, no simulation or save
change. The preceding git revision is the recoverable asset fallback.

## Evidence and limits

`docs/testing/aircraft-tails-2026-10-09/README.md` records references, all-fleet
renders and focused numeric checks. No Unity compile/build, animated deflection,
lighting matrix or performance pass is implied by the software renders.
