# Aircraft body revision — 8 October 2026

Task #651. All 13 scheduled aircraft, Bell 412 and the Parafield trainer retain
existing asset IDs, Unity GUIDs and overall envelopes. The A320 base and preferred
v02 derivative are both updated. Body contours use bounded cubic station
interpolation, rounded closures and shared ring vertices. Skin panels move with
the hull; A320 donor doors are refitted. Bell/trainer glazing follows the curved
shell. Existing moving wings, engines, propellers/rotors and landing gear retain
their source geometry. Fitted title/registration and jet L1 docking tables are
regenerated to the new meshes. Simulation rules and save schemas are unchanged.

## Checked

- `check-aircraft-bodies.py`: all 15 types pass applicable finite geometry,
  profile-radius, C1 station-join, positive-volume, 16-bit budget, envelope and
  moving-part checks; trainer side glazing is curved.
- `fit-aircraft-doors.py audit`: all audited doors clear their hull; outlines are
  about 6–10 mm proud and leaves about 10–14 mm proud.
- `test-aircraft-paint.py`: all 13 scheduled types pass 3–25 mm paint fit,
  glazing/interior/pilot contracts and fitted title/door-table checks. The probe
  now compares against the continuous uncut hull, matching production order.
- Selected existing connectivity regressions: SF34 and B78X pass the 5 cm
  attachment-chain check.
- `test-bell-tail-rotor.py`: opposed half-span blades, radius, finite FBX normals,
  FBX/glTF positions and packaged mirrors pass.
- Focused headless checks for the regenerated C# tables: 34/34 pass
  (`test-quick.py --changed`, using the already installed .NET 8 SDK).
- Changed Python sources compile; `git diff --check` passes.
- Every changed aircraft glTF/bin/thumbnail has a byte-identical packaged mirror.
  Metadata GUIDs are preserved. Hangar thumbnails were regenerated as runtime
  UI assets, not used as native appearance evidence.

The complete Unity asset audit reports the same unrelated existing satellite
JPEG mirror mismatch (`tx_adelaide_sentinel2_l2a_v02.jpg`). The generic art sync
would repair it incidentally; that unrelated change was restored to keep this
PR scoped. No new metadata/aircraft-mirror issue is reported.

## Limits and next native review

No Unity compile/player build, broad simulation suite, rendered review or
performance pass was run. Bailey chooses the Mac timing. Review overview/follow
at day/dusk/night, the nose/tail silhouette, animated doors and boarding access,
internal/external cockpit and cabin glazing, gear/wing clearance and frame time.
The helicopter/trainer hull is still opaque beneath its dark glazing; this pass
does not add cabin apertures or interiors for those two models. These remain
representative authored aircraft with inherited wing/engine/gear approximations,
not manufacturer CAD. Separate WIP #543's taller 787 windows remain unchanged;
its generated assets will need regeneration after rebasing onto this body pass.

## Geometry cost

Node/draw grouping is retained. Curved nose/tail rings use a 2.5 mm chord-error
target; barrel ring spacing is capped at 60 cm to prevent runaway subdivision
around apertures. The large jets generally use fewer final triangles; the
helicopter and trainer gain detail from their formerly primitive shells. This
is an asset count, not runtime performance evidence.

| Model | Previous triangles | Revised triangles |
| --- | ---: | ---: |
| mdl_737_800_v01 | 99,244 | 105,594 |
| mdl_737_8_narrowbody_v01 | 101,183 | 106,297 |
| mdl_787_10_v01 | 220,411 | 178,882 |
| mdl_787_9_v01 | 200,686 | 166,655 |
| mdl_a220_300_v01 | 77,544 | 90,815 |
| mdl_a320_200_v01 | 87,608 | 102,053 |
| mdl_a320_200_v02 | 91,082 | 105,527 |
| mdl_a321neo_v01 | 104,048 | 108,651 |
| mdl_a330_900neo_v01 | 158,402 | 164,268 |
| mdl_a350_900_v01 | 263,740 | 215,765 |
| mdl_atr42_starter_v03 | 63,923 | 66,796 |
| mdl_bell_412_rescue_v01 | 1,993 | 31,038 |
| mdl_dash8_q400_v01 | 101,903 | 106,958 |
| mdl_e190_v01 | 65,927 | 79,128 |
| mdl_parafield_trainer_v01 | 1,028 | 15,768 |
| mdl_saab_340b_v01 | 74,920 | 62,548 |
