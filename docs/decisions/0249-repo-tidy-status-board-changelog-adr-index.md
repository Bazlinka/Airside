# 0249 — Repo tidy: short status board, short changelog, ADR index

Status: accepted
Date: 2026-10-07 (requested by Bailey: "implement a much more uniform and tighter design in how we view and manage things")

- **Decision:**
  1. `GAME.md` is **current state only** (~170 lines): one replaceable "Where to resume" block, current milestone,
     visual contract, invariants, run-it/controls, short handoff protocol. The old 537 KB / 7,000-line file is kept
     verbatim in `docs/history/GAME-handoff-log-through-2026-10-07.md`.
  2. `CHANGELOG.md` is **one line per change** (~160 chars) with the 25 newest entries kept; the ~1,000-entry original
     is kept verbatim in `docs/history/CHANGELOG-through-2026-10-07.md`.
  3. `docs/decisions/README.md` indexes all ADRs, shows only the status each ADR states, and records the rule
     "take the next free number after checking origin/main and open branches". Existing numbers are NOT renumbered.
  4. `docs/README.md` is the one-page map of the docs tree.
  5. Fully merged remote branches are listed for pruning in `docs/history/branch-inventory-2026-10-07.md` (not yet deleted —
     awaiting Bailey's go-ahead); unmerged branches are never touched.
- **Reason:** `GAME.md` could not be read in a single pass, the changelog was never versioned, 10 ADR numbers were
  duplicated by parallel work, and 77 remote branches obscured what was live.
- **Affected systems:** documentation and `AGENTS.md` (handoff protocol, repo map) only. No code, asset, save or
  simulation change. No script or CI job depends on the removed `GAME.md` sections (checked).
- **Migration impact:** none. Nothing was deleted from history; links to the old sections should use the archive files.
- **Not done:** judging which ADRs are superseded (needs per-ADR review — the index shows blanks, not guesses);
  Unity tests in CI; splitting `AirsidePrototype` (separate, riskier work).
