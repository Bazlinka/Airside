# Free visual upgrade verification — 2026-10-08

Task #585. User approved all ten workstreams, free assets only, and supplied A320.zip.
Implementation and tradeoffs: `docs/decisions/2026-10-08-free-visual-upgrade.md`.
Source geometry, source licences and an offline generator are committed. The
source/derivative hashes are in `sources.json`; regeneration produces no byte or
Unity GUID drift. No paid assets, runtime downloads or account requirements.

## Checks completed before the latest-main integration

- Unity EditMode rerun: 2342 passed, zero failures, two precondition-dependent inconclusive cases.
- Domain suite: 1866 passed, zero failures; Unity NUnit 3.5 compile check passed.
- Asset audit: 1854 unique GUIDs, 405 byte-identical runtime mirrors, 70 character materials.
- Fan-disc coverage: all eight jet engine sides pass, including imported A320 fan blades.
- Fitted paint/title/door checks: all 13 fixed-wing layouts and paint checks pass.
- Native reference board: inspected actual Unity renders. Reduced concrete contrast
  after the first board was too mottled. Replaced blocky source foliage with a
  subdivided, rounded thin-tree derivative before accepting the second board.

The first broad tests found three facade regressions: downpipes were grouped with
rooftop plant. Fittings now batch as trim, omitting real doorway intervals. They
also exposed an existing Bell 412 source-part count assertion invalidated by the
six common surface-detail renderers added in task #578; count now excludes only
those explicitly named detail children and still checks the original 45 parts.

- Final facade and original-part assertion corrections are covered by the rerun, not by the failing first pass.

## Runtime scope

`reference-native.png` and `vehicles-native.png` are graphics-on Unity editor
renders under common daylight. They are not screenshots from a packaged airport
session. A whole-game capture and native aircraft/cabin checks are recorded below
once completed. Frame-rate/performance acceptance across the airport, weather,
aircraft motion and camera transitions remains separate from still-image checks.

## Latest-main integration

Rebased onto main `028e8b73` (task #580 bug sweep and #587 exterior lights).
The final native suite passed **2411 tests, zero failures, two inconclusive**
precondition-dependent cases (`editmode-final.xml`, 505 seconds).
Final source/derivative hash check: all 27 inputs/outputs match the ledger.
Offline regeneration: zero asset-byte or Unity-meta/GUID drift.
Final A320 thumbnail uses the adapted open engine casing; source intake caps are
removed. Cabin fabric removes the scan's broad plaid so only small grain remains.

Native final renders: `a320-native.png`, `a320-side-native.png` and
`cabin-native.png` inspected in Unity. Intakes reveal the fitted fan blades;
headrests show fine cloth grain rather than the initial oversized plaid.
All 13 fixed-wing cabin/left-window/right-window/outside views rendered (52 PNGs).
