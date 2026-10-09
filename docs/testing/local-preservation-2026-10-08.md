# Local preservation review — 8 October 2026

Base: main 1ce9b727. Task #608; Codex.

## Editor approach helper

Preserved the 12-line approachNN case from the Airside-sky working tree in
AircraftArticulationReview. It seeds gear retracted during circuit, then advances
100 approach samples at 0.05 seconds per sample. The suffix supplies final progress.
Editor inspection only; no game behaviour, save, simulation or display changes.
Native compilation/focused articulation and journey run was attempted, but Unity
stalled during script compilation with no result XML and no new log progress for
several minutes. Stopped only that task process. Compile/tests remain unverified. Running the
helper itself to generate/inspect an approach image remains unverified.

## Stash audit

Two 1 October stashes were compared with current main, including the larger
stash's untracked tree. All added runtime/test lines are present in main except
these two superseded lines:

- Selection readouts used `drawn ? card.LiveLine : null`; current main also includes
  off-map aircraft (`drawn || aircraft.IsOffMap`).
- OperationsRow's old constructor ended at progressText; current main extends it
  with baseCode for multi-base aircraft.

All seven saved untracked files (ADR 0206, paint widening helpers, tests and meta)
are byte-identical to main. Door-fit CI and AGENTS guidance are present. Generated
harness changes are superseded by the current generated list. Old GAME/CHANGELOG
entries describe features now present; no unique implementation was found.

Audited stash commits:
- 565c366b04858619a0f04eda79d4864820fec13d
- 93e6a01e02ad7e319016a5a1b5ccb74245923a1c
