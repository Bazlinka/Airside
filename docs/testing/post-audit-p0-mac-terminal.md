# Mac Terminal — post-audit P0 Stage A→B→C (no Cursor agent)

Cloud Linux cannot pin My Machines. If the Mac is awake (`cursor worker` may be
running), you can run the remaining stills **directly in Terminal.app** — no
agents UI pin required. Keep the display awake. Canonical checkout: `~/Code/Airside`.

Verdicts (keep/fix/revert) still need your eyes/ears afterward — inventory is not a verdict.

```bash
cd ~/Code/Airside
git fetch origin
git checkout cursor/p0-auto-landing-follow-709e
git pull --ff-only origin cursor/p0-auto-landing-follow-709e

# One paste — Stage A → B → C (~25+ min; keep display awake)
scripts/run-post-audit-p0-stages.sh

# Or run stages separately:
# Stage A (minutes — night-sky + freighter; unblocks P2 freighter evidence)
# AIRSIDE_P0_ONLY=overview-night-sky-traffic,follow-freighter scripts/run-post-audit-p0-remaining.sh
# Stage B (~17 min soak — TakingOff / tyre rotation; reuse player; dirty RESULTS OK)
# AIRSIDE_P0_SKIP_BUILD=1 AIRSIDE_P0_SKIP_PULL=1 AIRSIDE_P0_ONLY=follow-jet-takeoff scripts/run-post-audit-p0-remaining.sh
# Stage C (hangar / boarding / landings)
# AIRSIDE_P0_SKIP_BUILD=1 AIRSIDE_P0_SKIP_PULL=1 AIRSIDE_P0_ONLY=follow-hangar-tow,follow-boarding-tape,follow-human-ops-close,follow-jet-day,follow-jet-close,follow-storm-landing scripts/run-post-audit-p0-remaining.sh
```

Then fill Verdict columns in `docs/testing/post-audit-p0-2026-09-30/RESULTS.md`
(do not invent), finish listening/play rows in
`docs/testing/post-audit-p0-manual-checklist.md`, update `GAME.md`, and push via
the protected-main PR workflow.

Cursor-agent paste path (optional): `docs/testing/post-audit-p0-mac-agent-prompt.md`.

GitHub trigger (optional, if Terminal is inconvenient): with the worker running as
`--name "Bailey's MacBook Pro"`, a trusted commenter can post on #491:

```
@cursoragent worker=Bailey's MacBook Pro
Confirm Darwin. cd ~/Code/Airside; checkout tip cursor/p0-auto-landing-follow-709e;
scripts/run-post-audit-p0-stages.sh; do not invent RESULTS verdicts; push PNGs/inventory.
```
