# Unique aircraft liveries and five presets — 30 September 2026

ADR 0204 implements thirteen individual fin symbols/fitted ribbon compositions and
exactly five player paint presets. The setup wizard has named two-tone rows;
the Airline page offers the same five repaints. The airline's own name and old
custom colours are preserved. No save migration is required.

- [All thirteen aircraft](all-aircraft.jpg): original geometry proof sheet,
  displaying the five palettes across different types.
- [Five player presets](five-presets.jpg): the same A320 in Coastline,
  Southern Cross, Outback, Gulf and Redgum.

These are software renders of the final runtime glTF/bin assets, without operator
text. They are **not Unity captures** and cannot validate shaders, lighting,
interactive selection, cargo appearance, performance or gameplay. Side views were
inspected and symbols refitted to local fin spans after initial renders exposed
clipped rays/pointers. Native Unity review remains required before merge.

## Validation

- Baseline supplementary headless suite: 1,130 passed.
- Revised supplementary headless suite: 1,131 passed, zero failures. NUnit
  also prints two pre-existing assumption skips; its final summary reports no
  counted skips. The harness excludes tests requiring Unity.
- Final setup and Airline page focused suite: 28 passed, zero failures. Includes
  exactly five clickable setup choices, no visible custom mixer, shared repaint
  colours, and existing supported viewport layout checks.
- Final fitted-paint audit: all thirteen pass, paint remains 3–25 mm proud,
  bilateral nonempty fin marks, single-layer glazing/interiors/pilots retained,
  title layouts match.
- Non-paint geometry comparison: all original position and index arrays for all
  thirteen models are byte-identical to the parent, excluding `livery_*` meshes.
- Additional Unity preset material test checks the actual secondary paint matches
  the selection swatch; **not run here**.
- `scripts/test-unity.sh`: cannot run; Mac Unity 6000.3.23f1 is unavailable in this
  Linux environment. No native compilation, Mac build or gameplay claim is made.

- Final connectivity audit: all thirteen pass; every part chains back to the
  fuselage within 5 cm.
- Packaged-art parity: 347 source/runtime art files are byte-identical after sync;
  no Unity `.meta` files changed.
- Full Unity asset audit reports four **pre-existing checkout findings**: missing
  `Assets/Resources.meta` and orphan folder metadata for `Art/Animation/Aircraft`,
  `Vehicles` and `World` (empty directories are absent in git). Parent git-tree
  inspection confirms the same missing root meta/empty folders. No missing,
  stale or mismatched runtime art, duplicate/malformed GUIDs or new metadata
  problem was reported. The global audit is not claimed green.

## Main integration

PR #487's merge conflicts with current `main` were limited to the status-board
and changelog entries. Both contributors' entries are retained. The livery
record is renumbered ADR 0204 because the visual/ground work now uses ADRs 0200–0203.
The combined branch's headless suite passes **1,145 tests**, zero failures.
After the subsequent coast PR landed (`7c80dde9`), its documentation conflicts
were also resolved, and setup/Airline/coast tests passed **34/34** on the latest
combined source. The 1,145-test run predates that coast integration.
Native Unity verification remains pending as described above.

## Reproduce

```sh
python3 scripts/finish-aircraft-liveries.py
python3 scripts/render-aircraft-thumbnails.py
bash scripts/sync-art-streaming-assets.sh
python3 scripts/test-aircraft-paint.py
python3 scripts/test-aircraft-connectivity.py
python3 scripts/audit-unity-assets.py
bash scripts/test-domain.sh
python3 scripts/render-livery-overhaul.py docs/testing/aircraft-liveries-2026-09-30
```

On the Mac, run `scripts/test-unity.sh`, `scripts/review-aircraft-liveries.sh`
and `scripts/build-mac.sh`. Check both sides and elevated front/rear, all five
player options during setup and in-place repainting, old custom save loading,
freighter repaint, AI and sky traffic, then follow/overview in day/dusk/night.
The native review script now covers all five palettes and both sides/front/rear/
overview (set `AIRSIDE_REVIEW_VIEWS=front` for a shorter check). No frame-time or
final build evidence is available for this revision.
