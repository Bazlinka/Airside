# Branch inventory — 2026-10-07 (ADR 0249)

Snapshot against `origin/main` at `baab39e646`. **Nothing below has been deleted yet** (pending Bailey's go-ahead). If a branch is deleted, recover it with: `git branch <name> <sha>` (the commits are all in `main`).

## To delete — fully merged into main (43)

| Branch | Tip SHA | Last commit |
|---|---|---|
| `claude/flights-panel-doorways-20261001` | `f6019236a0` | 2026-10-01 |
| `cursor/phase1-hills-haze-709e` | `74d3917cb0` | 2026-10-01 |
| `cursor/phase1-seasonal-tint-709e` | `1868d0fa48` | 2026-10-01 |
| `cursor/phase2a-eucalypt-crowns-709e` | `4fb1e0af63` | 2026-10-01 |
| `cursor/phase2b-carpark-palms-709e` | `b167446a14` | 2026-10-01 |
| `cursor/post-audit-improvement-plan-709e` | `78fee9c57c` | 2026-09-30 |
| `docs/aircraft-immersion-audit-20261006` | `c7a43c6110` | 2026-10-06 |
| `docs/cockpit-merge-handoff` | `f64bdc8641` | 2026-10-05 |
| `docs/development-readiness-clarification` | `643e172b62` | 2026-10-06 |
| `docs/playtest-acceptance-2026-10-06` | `1b795431b5` | 2026-10-05 |
| `docs/visual-review-continuation` | `7acfb30676` | 2026-10-06 |
| `feature/ai-freight-traffic` | `50a6ee514e` | 2026-10-06 |
| `feature/aircraft-cabin-immersion-20261006` | `d110fb224d` | 2026-10-06 |
| `feature/aircraft-cockpit-realism-20261006` | `c9a26573b9` | 2026-10-06 |
| `feature/aircraft-identity-refresh` | `72a51cb985` | 2026-10-06 |
| `feature/aircraft-interior-details-20261006` | `e60a9efac2` | 2026-10-06 |
| `feature/all-jet-cockpits` | `f13ec2803d` | 2026-10-01 |
| `feature/cockpit-flight-feel-2` | `f01708540a` | 2026-10-01 |
| `feature/cockpit-haptics-look-20261006` | `10389f42d8` | 2026-10-06 |
| `feature/cockpit-immersion` | `d2f4f01bbf` | 2026-10-01 |
| `feature/cockpit-sound-and-controls` | `dfd1acb6a6` | 2026-10-05 |
| `feature/cockpit-window-view` | `c458f3644d` | 2026-10-01 |
| `feature/freight-and-interface-refresh` | `aa0be147b6` | 2026-10-06 |
| `feature/full-map-flight-inspector` | `caf25d2d33` | 2026-10-06 |
| `feature/map-accuracy-20261006` | `45d65099bf` | 2026-10-06 |
| `feature/passenger-and-exterior-flight-views` | `9ded62a9ee` | 2026-10-05 |
| `feature/prop-spool-blur` | `1e6e2c3bee` | 2026-10-01 |
| `feature/refined-interface-maintenance-20261006` | `f613ceeee2` | 2026-10-06 |
| `feature/sa-minimap-flight-selection` | `52977c0c67` | 2026-10-06 |
| `feature/saab-cockpit-mode` | `6614d378fd` | 2026-10-01 |
| `feature/south-australia-flight-world` | `b816002e33` | 2026-10-01 |
| `feature/turboprop-cockpits` | `623885e7a7` | 2026-10-01 |
| `feature/unified-fleet-management-20261006` | `938c81c8ee` | 2026-10-06 |
| `fix/adelaide-ground-protocols-20261006` | `f89b08ecc9` | 2026-10-06 |
| `fix/ground-fog-follows-camera` | `3565fa8fc2` | 2026-10-01 |
| `fix/long-flight-stall-20261006` | `d23533ed94` | 2026-10-06 |
| `fix/regional-weather-coverage` | `3718039c1c` | 2026-10-01 |
| `fix/saab-cockpit-shell` | `5eb52b8759` | 2026-10-01 |
| `fix/sky-star-parallax-20261006` | `b71522e8e1` | 2026-10-06 |
| `fix/takeoff-flight-details-20261006` | `fb4276b672` | 2026-10-06 |
| `fix/unity-folder-metadata` | `801ff11a0d` | 2026-10-06 |
| `fix/visual-audit-batch1-20261006` | `98188f3e48` | 2026-10-06 |
| `fix/visual-audit-batch2-20261006` | `2d012a97d2` | 2026-10-06 |

`claude/brave-babbage-ybgffc` is also fully merged but was in use for this tidy; delete it after its PR merges.

## Kept — not merged into main (31)

Not touched. Each has commits that are not on `main`; the owner should merge, rebase or close them. The date is the last commit.

| Branch | Commits not on main | Last commit | Open PR |
|---|---|---|---|
| `claude/check-tow-and-temporary-tape` | 22 | 2026-09-30 |  |
| `claude/terminal-doors-detail` | 23 | 2026-09-30 |  |
| `cursor/aircraft-audio-focus-listener` | 21 | 2026-09-30 |  |
| `cursor/p0-auto-landing-follow-709e` | 82 | 2026-09-30 |  |
| `cursor/phase1-landform-coast-5ea8` | 34 | 2026-09-30 |  |
| `cursor/phase1-stand-oil-stains-5ea8` | 35 | 2026-09-30 |  |
| `cursor/post-audit-p0-results-709e` | 41 | 2026-09-30 |  |
| `cursor/visual-baseline-phase0-5ea8` | 31 | 2026-09-30 |  |
| `feature/aircraft-livery-overhaul` | 36 | 2026-09-30 |  |
| `plan-visual-overhaul` | 25 | 2026-09-30 |  |
| `visual-overhaul-ground` | 29 | 2026-09-30 |  |
| `claude/hollow-hangars` | 1 | 2026-10-01 |  |
| `claude/live-weather-accuracy` | 1 | 2026-10-01 |  |
| `claude/runway-paint-widening` | 1 | 2026-10-01 |  |
| `cursor/p0-closed-bailey-709e` | 7 | 2026-10-01 |  |
| `cursor/p0-freighter-pick-lock-709e` | 15 | 2026-10-01 |  |
| `cursor/phase1-cbd-skyline-709e` | 10 | 2026-10-01 |  |
| `cursor/phase1-cbd-skyline-design-1804` | 9 | 2026-10-01 |  |
| `cursor/phase1-seasonal-tint-design-0e39` | 12 | 2026-10-01 |  |
| `cursor/phase3-arff-fire-station-design-f197` | 1 | 2026-10-01 |  |
| `feature/cockpit-cloud-breakout` | 1 | 2026-10-01 |  |
| `feature/helicopter-operations` | 13 | 2026-10-01 |  |
| `fix/arrival-final-spacing` | 4 | 2026-10-01 |  |
| `fix/audio-ambient-loops` | 1 | 2026-10-01 |  |
| `fix/jet-engine-audio-loop` | 1 | 2026-10-01 |  |
| `claude/confident-allen-c35v79` | 2 | 2026-10-06 |  |
| `docs/refined-interface-maintenance-20261006` | 1 | 2026-10-06 | #547 (draft) |
| `docs/refined-interface-merge-handoff-20261007` | 1 | 2026-10-06 |  |
| `feature/787-window-realism-20261006` | 1 | 2026-10-06 | #543 (draft, WIP do-not-merge) |
| `feature/lidar-ground-detail` | 1 | 2026-10-06 |  |
| `fix/native-editmode-failures-20261007` | 1 | 2026-10-06 | #552 (draft) |
