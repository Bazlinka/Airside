# Post-audit P0 playtest — 2026-09-30

Ran on Bailey's MacBook Pro (Apple M1 Pro), display awake.

- **Player:** `work/builds/Airside.app`, identity `commit=56cb46a3` `branch=main` `dirty=false`
  (Merge pull request #483, stamped 2026-09-30 12:47 UTC). Game tree matches that commit, so this
  run reused that packaged build instead of compiling it again.
- **Matrix:** `scripts/review-post-audit-p0.sh` from `cursor/post-audit-improvement-plan-709e`
  (`9c12f0b6`). Fourteen shots, 1600×900, soak career "Soak Air".
- **Logs:** no `Shader error`, `NullReferenceException`, `InvalidOperationException`,
  `IndexOutOfRangeException`, or `[Airside soak] STALL`. The script's log sweep exited clean.
- **Shots:** the PNGs in this folder. Originals also sit in `work/captures/post-audit-p0-20260930/`.

Verdicts below are from these stills and the player logs. They are not a substitute for the
manual rows. **2026-10-01 — Bailey closed P0 and waived the remaining unverified
rows** (move on to P1). Freight is no longer parked behind P0.

Frame times are the settled soak sample (the `1 min` line), after the first-frame hitch.

| Shot | Settled fps | p95 | Weather log |
|---|---:|---:|---|
| overview-day-clear | 59 | 17.4 ms | Clear |
| overview-far-land-cover | 75 | 17.6 ms | Clear |
| overview-night-sky-traffic | 56 | 33.3 ms | Clear, 23:30 |
| terminal-airside-day | 60 | 17.6 ms | Clear |
| terminal-airside-night | 59 | 17.6 ms | Clear, 23:30 |
| terminal-kerb-day | 58 | 17.7 ms | Clear |
| hangar-rex-day | 60 | 25.0 ms | Clear |
| hangar-cobham-day | 60 | 25.0 ms | Clear |
| freight-qantas-day | 75 | 17.3 ms | Clear |
| fire-station-night | 82 | 17.2 ms | Clear, 23:30 |
| weather-storm-overview | 39 | 41.7 ms | Storm, mist 0.48, fog on |
| weather-fog-overview | 40 | 42.2 ms | Fog, mist 1.00, fog on |
| follow-jet-day | 60 | 17.6 ms | Clear, follow VH-PAX |
| follow-jet-close | 61 | 23.5 ms | Clear, follow VH-PAX |

## Automated captures

| Shot | Checks | ADR | Verdict | Notes |
|---|---|---|---|---|
| `overview-day-clear.png` | Readable field, buildings, traffic | baseline | keep | Terminal, runways, taxiways and parked traffic read. HUD is the current Glass Cockpit. |
| `overview-far-land-cover.png` | Suburbs/crops/water beyond satellite; no haze ring | 0185, 0190, 0191 | keep | City, river, coast and hills are in frame. Rim darkening is the coast and the hills: the north rim matches the centre (luminance ratio 0.99) and the south/west rim is ocean and ranges. Not a symmetric camera ring. |
| `overview-night-sky-traffic.png` | Overflights cruise; no double inbound | 0195 | unverified | Night field, edge lights and clouds read. This still is pitched down at the airport, so it does not show a cruising overflight and cannot prove an inbound is drawn once. Re-run via `scripts/review-post-audit-p0-remaining.sh` (11 km / pitch-8 / yaw-270) after rebuilding with that framing. |
| `terminal-airside-day.png` | T1 doors, piers, aerobridges | 0185, 0197 | keep | Terminal mass, aerobridge run and parked aircraft read at 420 m. Individual door leaves are not resolved at this distance. |
| `terminal-airside-night.png` | Night glow, door packs | 0197, 0199 | keep | Apron and airfield lights read. Door-pack glow is not separable at 420 m. |
| `terminal-kerb-day.png` | Landside entrances, kerb detail | 0197 | keep | Landside roads and the forecourt sit beside the terminal. Entrance door banks are not readable at 380 m. |
| `hangar-rex-day.png` | Rex hangar roof / berth read | 0186–0188 | keep | Aimed at the surveyed Regional Express hangar. Roofs are ribbed and pitched, walls are corrugated. No aircraft in a berth in this frame. |
| `hangar-cobham-day.png` | Cobham hangar row | 0186–0188 | keep | Cobham row is in frame. The large hangar has a ribbed pitched roof. Berths are empty in this frame. |
| `freight-qantas-day.png` | Freight shed roof, plinth, dock | 0199 | keep | Qantas Freight (OSM footprint) shows a low roof, ribbed walls and a stepped dock annex. Bumper-level detail is not readable at 260 m. |
| `fire-station-night.png` | Fire station roof + lit packs | 0199 | keep | Barrel roof on the station and warm window lights at night. |
| `weather-storm-overview.png` | Storm depth; note fps | 0193 | keep | Storm is active (darker frame: mid luminance 103 vs 196 clear; a cloud mass in frame). Settled 39 fps, p95 41.7 ms. The field stays readable. 60 fps is still the P3 budget, not a P0 revert. |
| `weather-fog-overview.png` | Height fog; close aircraft clear | 0193 | keep | Fog is on (mist 1.00). From ~1.8 km up the shallow layer does not white-out the field (mid luminance 156 vs 196). Settled 40 fps. No close aircraft in this overview, so "close stays clear" is not shown here. |
| `follow-jet-day.png` | Follow framing | 0189 | keep | Follow locks onto VH-PAX. In this soak that aircraft is a parked Saab 340B at the stand, not a jet airborne. Crew stand on the apron with contact shadows. |
| `follow-jet-close.png` | Close glazing / gear | 0194 | keep | Close view shows cabin glazing, registration and titles. The aircraft is parked, so main-gear rotation and flare are not in this shot. |

## Remaining captures (after #491/#492 helpers — not taken yet)

Run on Mac after rebuild: `scripts/review-post-audit-p0-remaining.sh`
(or `AIRSIDE_P0_ONLY=<shot,shot>` for a subset). Copy keep PNGs into this folder
and fill Verdict. Do **not** invent keep/fix/revert without the still or play.

| Shot | Checks | ADR | Verdict | Notes |
|---|---|---|---|---|
| `overview-night-sky-traffic.png` (re-run) | Overflights cruise; no double inbound | 0195 | unverified | Framing: 11 km / pitch 8 / yaw 270 (early-soak corridor), ~45s. Prior still was nose-down. |
| `follow-jet-day.png` (re-run) | Follow framing on live arrival | 0189, 0194 | unverified | Needs `auto-landing` batch (~780s jet; 360s is turboprop). Prior still was parked Saab. |
| `follow-jet-close.png` (re-run) | Close glazing / gear on arrival | 0194 | unverified | Same soak as day (~783s, zoom 0.35). |
| `follow-jet-takeoff.png` | Tyres at rotation | 0194 | unverified | Needs `auto-takeoff` (~1330s mid *jet* TakingOff; 830s is turboprop; 900s → HoldingShort). Not in the #490 matrix. |
| `follow-storm-landing.png` | Arrival on final lands in storm | 0190 | unverified | Same landing soak (~786s, weather storm). Code green in `RunwayWeatherTests`. |
| `follow-freighter.png` | Cargo shade + "... CARGO" title | 0194 | unverified | Needs `-airsideReviewFreighter`. |
| `follow-hangar-tow.png` | Tow mid-move | 0186–0188 | unverified | Needs `-airsideReviewHangarCheck` (~90s). |
| `follow-boarding-tape.png` | Walkway tape mid-board | 0187, 0196 | unverified | Needs `-airsideReviewBoarding` (~320s). |
| `follow-human-ops-close.png` | Airstair / tape scale | 0174 | unverified | Boarding + zoom 0.35. Bridge glass still needs a jet-gate follow by hand. |

## Manual checks

These cannot be closed from a PNG. Left open for Bailey.

| Check | How | ADR | Verdict | Notes |
|---|---|---|---|---|
| Aircraft audible at overview near apron | Play, then Follow | 0192, 0196 | unverified | Not listened to. |
| Touchdown chirp / reverse / rollout | Follow an arrival through landing | 0192 | unverified | Not listened to. |
| Freighter cargo livery + title | Fleet card → refit → Follow | 0194 | unverified | Soak did not open the Fleet card. |
| Tyres on ground at rotation and flare | Follow a jet from the side | 0194 | unverified | VH-PAX stayed `AtStand` for the whole matrix. |
| Arrival already on final lands in storm | Aircraft on final must land | 0190 | unverified | Storm shot is an overview still, not a landing. |
| Hangar tow for a check | Send an aircraft to check | 0186–0188, 0196 | unverified | No check was started. |
| Boarding tape only while walking | Regional bay board/deplane | 0187, 0196 | unverified | VH-PAX was fuelling, not boarding. |
| Follow feel (no lag / swing / bob) | Follow climb-out and landing | 0189 | unverified | One parked frame cannot show camera feel. |
| Zoom-in stays under cursor after far zoom | Zoom out over the city, scroll in | 0191 | unverified | The far shot is a fixed camera, not a scroll. |
| Human-ops close matrix | Airstair / bus+stairs / bridge glass | 0174 | unverified | Not framed. |

## Exit

- [x] Every automated row has a verdict (keep, or unverified where a still cannot answer)
- [x] Remaining manual / remaining-capture rows **waived** by Bailey (2026-10-01) — not eyes-on keep
- [x] Fixes filed — none from the automated keep rows; waived rows stay `unverified` on purpose
- [x] `GAME.md` handoff: P0 closed; next is P1 visual overhaul gate
- [x] P0 complete by owner override; P2 freight no longer blocked by P0
