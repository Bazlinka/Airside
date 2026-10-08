# Handoff before weather realism (8 October 2026)

## Where to resume

**Aircraft bodies (8 Oct, Codex, #651):** all 13 scheduled aircraft plus Bell 412
and Parafield trainer have continuous body contours, rounded tips and fitted
skin details at their existing asset paths. Bell/trainer glazing and doors now
follow the shell; A320 inherited doors are refitted and v02 source-adapted engines,
fans and wheels retained. Regeneration: shared body pass before glazing/paint,
Bell/trainer generators, A320 free-source adaptation, runtime art sync.
Bounded numeric/fit evidence and limits:
`docs/testing/aircraft-bodies-2026-10-08/README.md`.
Native Unity appearance, doors/cabin views and performance are unverified.
No simulation/save changes; these remain representative models, not manufacturer CAD.
The WIP 787-window-height proposal (#543) is separate.

**Flight times, maps and HUD redesign (8 Oct, Claude, #621/#624/#632/#641/#646/#652/#656/#659):** all merged; Unity compile and appearance
unverified (pure maths and draw lists checked headlessly: `dotnet test` filters plus `scripts/render-hud-mockups.py`).
- *Flight times:* ATR 42/Dash 8 planning cruise 510/620 km/h; A350/787-9 practical range 13,500 km, 787-10 11,200 km; jets get a cruise-time
  factor of 1.0 (600 km) to 1.08 (2,500 km+) in `LegTiming.AirborneSeconds` for routing/headwind (no wind is modelled; HUD Mach reads lower on long legs).
- *Maps:* flight-view moving map simplified (one-line footer, no rings) with worker-thread textures and a windowed anti-aliased coast.
  Route Map has an async-baked land fill (`RouteMapLandLayer`, `RouteMapLandWindow`), faint coast/borders, rimmed dots, codes then names by zoom,
  and on-field aircraft collapse to dots plus "N on field" below zoom 60.
- *HUD:* ADR `2026-10-08-hud-chrome-redesign.md`. Floating capsule + action group, 72 pt rail, new Glass palette, milestone card, flight-view
  instrument tiles, Operations rows, Map plan pane, Fleet/Contracts cards, Career stat cards. Not yet redesigned: selected-aircraft card,
  airport-movements board, radar, Fleet detail pane, Career layout (roadmap/activity/right column), setup/splash/menu screens.
- *Open/unverified:* look at the Route Map and HUD in the Mac build; check `PC_RPAsset`/`packages-lock` local edits are intentional (left uncommitted).
  `Contracts_LayoutKeepsBothColumnsInsideTheSurface` and `GrowingOverview_LastAircraftRemainsReachableInCompactWindows` fail on main (layout-only,
  compact windows; not caused by the HUD painter work) and need a cause found. Next approved work: Bailey's call — remaining HUD screens above.

**Regional departure climb (8 Oct, Claude):** the route profile starts at the height a local climb-out reaches, but a regional leg begins at brake release, so the old code had the aircraft gain the whole gap in 120 s (up to ~5,200 ft/min on a 737). `RegionalFlightPath.ClimbLagSeconds` + `ClimbAltitudeFeet` start the route climb late (finishing out of the cruise), and `DepartureClimbHeight` replaces the smoothstep with one steady rate. Peak now <= 3,000 ft/min in `RegionalDepartureClimbTests`. Board/card/map altitude text now uses `BoardAltitudeFeet` (same lag). Unity unverified.

**Next:** Bailey chooses Mac build/playtest timing. Check the fleet's nose and
tail contours, opened/shut doors and interior glazing at overview/follow distances,
then day/dusk/night and performance. Standing policy: quick relevant checks only;
broad suites/builds/player reviews only on request; merge completed authorised
work without repeated approval.

**Regional flight updates (8 Oct, Claude):** regional departure altitude now
uses a delayed route climb and a steady local climb-out rate (peak <= 3,000 ft/min);
Operations/card/map text shares that drawn altitude. Regional landings integrate
route/approach/touchdown speeds and brake at the aircraft's rollout deceleration.
`FlightSpeedEnvelope` holds stall margins, the 250 kt CAS cap and acceleration
limits; its bank-dependent minimum is not yet enforced on the live bank. Unity
appearance remains unverified. Main also includes HUD stage 3 (Map/Fleet/Contracts/
Career pages) and road turning heads/give-way teeth from other tools.

**Other current work:** main includes opt-in Mac notifications (#645), redesigned
toast cards (#642 / #644), turn banking, flight-transition pitch/flare easing,
aircraft-relative exit glides, control-tower view, flight moving map, aerodrome
beacon, landing-light flare and the Adelaide opening (#626 / #638). Their native
appearance/packaging/performance checks remain open. Mac notifications need
Options → Notifications → switch ON → allow permission → SEND TEST, then background
delivery and click activation. Registration repair (#663) retries permission from
the status row, registers the player with Launch Services, verifies plugin packaging
and preserves Unity’s app signature (no post-sign plist edits). Missing-plugin and
native-request failures now have separate status/help; Mac confirmation remains open.
Check title/Continue, skip, animation-off and returned-airline paths in the next Mac build. The v03 Dock/app icon is retained. Flight-dispatch
cost rebalance retains prices/pay/start funds; next economy work is standing
aircraft daily cost and fuel price, with career pacing unverified.

Existing opening FlightManual page 7 failure and satellite JPEG mirror mismatch
are unrelated. Earlier audio/terrain/aircraft/save/native validation limits and
other tools' branches remain in
`docs/history/game-handoff-before-adelaide-opening-2026-10-08.md`.
Do not treat the title illustration or generated Hangar thumbnails as native captures.

*One block, replaced at the end of each session. Updated 2026-10-08.*

