# Freight and interface refresh — 6 October 2026

Branch: `feature/freight-and-interface-refresh`. Task packet:
`docs/plans/freight-and-interface-refresh.md`. Decisions: ADRs 0230 and 0231.

## Executed evidence

- Baseline full headless suite: 1,477 passed, zero failed (2 m 55 s).
- Full implementation checkpoint: **1,487 passed, zero failed** (3 m 4 s),
  `full-regression.trx`. Includes contract/refit acceptance, passenger vs freighter
  completion, deadline eligibility and saved progress; camera-control bounds and
  separation at four viewport sizes; return-report bounds; typed catch-up facts
  and click attack/peak/tail checks.
- Initial focused checks: 44/44. Final focused checks: **47/47** after adding
  selected-aircraft action separation cases for player, AI and stand-choice states.
  `final-focused.trx` covers the final UI wording/route-direction and opacity
  refinement. The full regression checkpoint predates these three added cases.
- Pure shared HUD exporter builds and produces 19 pages at 1440×900 and 1280×720.
  The retained PNGs rasterise the actual shared draw commands on a synthetic
  airfield backdrop. They are **offline layout previews, not Unity screenshots**.
  Font metrics differ from IMGUI. Flight preview telemetry is representative;
  return-report figures come from an isolated real save advanced by three hours.
- Five Unity-facing C# source files parsed with Roslyn: zero syntax errors.
  This does not establish a native Unity semantic compile.
- Retained TRX files have trailing line whitespace trimmed; test results are unchanged.
- `git diff --check` passes. Generated harness retains all original tests plus
  the new layout tests; only the same pre-existing exclusions remain.
- Unity asset audit reports the existing Resources directory metadata omission
  and orphan metadata for the three empty Animation/Aircraft, Vehicles and World
  directories. New scripts/tests have stable metadata; no new audit issue.

## Audio preview

`ui-click-preview.wav` exports three actual `HudSounds.UiClick()` ticks at the
runtime 0.45 playback gain. The synthetic waveform is project-owned; no external
sample or new shipped audio file. This export is for comparison/listening only,
not proof of a reviewed in-game sound mix.

## Native verification

`scripts/test-unity.sh` exits before execution because the Mac Unity 6.3 LTS
executable is absent. No new Unity build, runtime screenshot, native test or
listening acceptance is claimed. `AGENTS.md` requires native compile/EditMode
before merging behaviour changes. Bailey explicitly authorised merging PR #526
on 6 October 2026; it merged at `19fd9688` with headless CI green. Native
verification remains outstanding; no native pass is claimed.
The prior current-game playtest sign-off is preserved and is not reused as new
validation of these changes.

## Scope still open

This completes the freighter-required-contract slice, not all freight mode.
AI freight, cargo stands and an outstation freighter role remain backlog. Existing
freight contracts preserve earned progress/pay and deadlines; further progress
now requires a matching refitted aircraft. Save schema stays unchanged.
