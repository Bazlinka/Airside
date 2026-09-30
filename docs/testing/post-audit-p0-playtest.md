# Post-audit P0 playtest checklist

Date: 2026-09-30 · Plan: `docs/plans/post-audit-improvement-plan.md` (ADR 0203)

**Goal:** eyes and ears on recent merges that are green in tests but “not yet seen /
listened / felt” in a rebuilt game. Mark each row **keep / fix / revert** before
adding more content (freight AI, visual overhaul Phase 1, expansion).

## How to run

On a Mac with Unity 6.3 LTS and a display that stays awake. Prefer branch
`cursor/post-audit-improvement-plan-709e` (or `main` once merged).

### Option A — shell (you on the Mac)

```bash
cd ~/Code/Airside   # or ~/Documents/Codex/Airside
git fetch && git checkout cursor/post-audit-p0-results-709e && git pull
scripts/run-post-audit-p0.sh   # build + capture matrix + RESULTS inventory stamp
# then fill keep/fix/revert in docs/testing/post-audit-p0-<date>/RESULTS.md and push
```

### Option B — Cursor agent *on* the Mac worker (recommended)

Cloud `Task` subagents from a Linux cloud run **cannot** pin My Machines
(schema only allows `same_machine` | `new_cloud_vm`). Placement that works:

- UI: environment dropdown → **Bailey's MacBook Pro**
- `CreateAgent` with `machine: { "type": "self_hosted_worker", "worker_id": "<id>" }`
  (team **Remote Control** must be on; list ids via `list-self-hosted-workers`)
- API v1: `env: { "type": "machine", "name": "Bailey's MacBook Pro" }`

One-paste prompt: `docs/testing/post-audit-p0-mac-agent-prompt.md`.

1. Keep `agent worker start --name "Bailey's MacBook Pro"` running in `~/Code/Airside`
   (or the Codex checkout).
2. Open [cursor.com/agents](https://cursor.com/agents) → environment dropdown →
   **Bailey's MacBook Pro** (not the default cloud VM).
3. Paste the prompt from `post-audit-p0-mac-agent-prompt.md` — build,
   `scripts/review-post-audit-p0.sh`, fill keep/fix/revert, commit
   `docs/testing/post-audit-p0-2026-09-30/RESULTS.md` + GAME.md handoff, push
   `cursor/post-audit-p0-results-709e`.

Captures land in `work/captures/post-audit-p0-<date>/` with a copy of this checklist
as `RESULTS.md`. Also useful:

- `scripts/review-weather.sh` — full weather matrix
- `scripts/test-unity.sh` — EditMode truth before merge
- Manual Play for audio and freight (below)

## Automated captures (`scripts/review-post-audit-p0.sh`)

| Shot | Checks | ADR | Verdict (keep / fix / revert) | Notes |
|---|---|---|---|---|
| `overview-day-clear.png` | Readable field, buildings, traffic | baseline | | |
| `overview-far-land-cover.png` | Suburbs/crops/water beyond satellite; no haze ring | 0185, 0190, 0191 | | |
| `overview-night-sky-traffic.png` | Overflights cruise (not crawl); no double inbound | 0195 | | |
| `terminal-airside-day.png` | T1 doors, piers, aerobridges | 0185, 0197 | | |
| `terminal-airside-night.png` | Night glow, door packs | 0197, 0199 | | |
| `terminal-kerb-day.png` | Landside entrances, kerb detail | 0197 | | |
| `hangar-row-day.png` | Eastern hangar row (Rex/Cobham) | 0186–0188 | | |
| `suburb-edge-day.png` | Suburb edge framing | 0190 | | |
| `coast-day.png` | Coast / West Beach | 0190 | | |
| `freight-qantas-day.png` | Freight shed roof, plinth, dock | 0199 | | |
| `fire-station-night.png` | Fire station roof + lit packs | 0199 | | |
| (any apron/stand overview or follow) | Oil stains under bays/gates; apron patches/pits | 0201, 0202 | | Phase 1 merged ahead of P0 |
| `weather-storm-overview.png` | Storm depth; note fps feel | 0193 | | |
| `weather-fog-overview.png` | Height fog; close aircraft clear | 0193 | | |
| `follow-jet-day.png` | Follow framing | 0189 | | |
| `follow-jet-close.png` | Close glazing / gear | 0194 | | |

Player logs next to each PNG must stay free of exceptions / soak STALL (the script fails if `rg` finds them).

## Manual checks (cannot be PNG-only)

| Check | How | ADR | Verdict | Notes |
|---|---|---|---|---|
| Aircraft audible at overview near apron | Play → stand on apron overview; then Follow | 0192, 0196 | | Tune `AircraftAudioMix.ZoomGain` if needed |
| Touchdown chirp / reverse / rollout | Follow an arrival through landing | 0192 | | |
| Freighter cargo livery + title | Fleet card → refit freighter → Follow | 0194 | | |
| Tyres on ground at rotation and flare | Follow a jet from the side | 0194 | | |
| Arrival already on final lands in storm | Force storm / wait; aircraft on final must land | 0190 | | Departures stay held |
| Hangar tow for a check | Send aircraft to check; watch tow in/out | 0186–0188, 0196 | | Quiet while towed |
| Boarding tape only while walking | Regional bay board/deplane | 0187, 0196 | | Temporary tape |
| Follow feel (no lag / swing / bob) | Follow climb-out and landing | 0189 | | |
| Zoom-in stays under cursor after far zoom | Zoom out over city, scroll in on suburb | 0191 | | |
| Human-ops close matrix | Airstair / bus+stairs / bridge glass | 0174 | | |

## Exit criteria

- [ ] Every automated row has a verdict
- [ ] Every manual row has a verdict
- [ ] Fixes filed as narrow branches (or keep accepted)
- [ ] `GAME.md` handoff updated: P0 complete or list remaining fixes
- [ ] Only then start P1 (visual overhaul Phase 0) or P2 (freight mode)

## Blocked without a Mac

Cloud Linux agents cannot run this script (no Unity player / display). They prepare
the checklist and must not mark P0 complete from headless tests alone. Do not use
Task + `privateWorkerId` from a managed cloud VM — use Option B’s UI picker (or
CreateAgent with `machine.worker_id`) instead.
