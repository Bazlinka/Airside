# Airside: 15 findings from local playtesting

9 October 2026. Report-only gameplay investigation; no fixes were made during this hunt. Bailey requested publication of the report for other AI tools. Final target: 15 findings.

Gameplay used the existing40× QA allowance and isolated test saves. A clean stamped macOS build at aec646166196b269dd6c9d4a05e9059c2212060e was built once and reused. The initial four findings came from an earlier local build; audio, guidance and capture inversion were corroborated against the clean build/source. The earlier Saab window capture is identified explicitly. Original checkout changes were preserved.

Coverage included ADL–KGC–ADL progression, booking/cancellation/rebooking, fleet purchase and maintenance, contracts, achievement interaction, manual/help, overview/radar/tower/Parafield, Bell412 regional travel,960×600 and1280×800 views, and several aircraft exterior/passenger views. A separate day/dusk/night weather render pass used ordinary clock progression. This was exploratory playtesting, not a full release or performance pass.

Severity is triage judgement. UX findings and the QA-tool defect are marked; uncertain visual causes are not presented as diagnosed. Repeated symptoms across models were not counted separately.

## 1. Cockpit and passenger audio throws repeatedly

**High.** Enter a cockpit or passenger view. Unity rejects AudioLowPassFilter because AudioSource has not yet been added; the rejected component is then dereferenced. Setup retries each frame, creating objects and growing the error log.

Evidence: `models/ATR42/player.log; automation/20261009-115859-6d69341e` (original local evidence paths; see the portable bundle below).

Trace/limits: AirsidePrototype.CockpitAudio.cs:58–64. Reproduced across several types; counted once.

## 2. Saab right passenger window has jagged black obstructions

**Medium.** Enter Right window during the ADL–KGC journey and wait for the camera to settle. Black triangular geometry intrudes around the opening and adjacent window.

Evidence: `../bug-hunt-40x-20261009/journey/0093.5-AtDestination.png` (original local evidence paths; see the portable bundle below).

Trace/limits: Confirmed visually; exact cabin/mesh cause remains undiagnosed.

## 3. First-flight return guidance still describes the outward leg

**Low.** At Kingscote turnaround, return to overview. The guide says Watch VH-PAX return while its description says On its way to Kingscote.

Evidence: `manual40; earlier journey/player.log` (original local evidence paths; see the portable bundle below).

Trace/limits: AirsidePrototype.Airline.cs:698–708 uses CurrentDestination and outward wording for Away states, including Inbound.

## 4. Native QA PNGs are upside down — subsequently fixed on main

**Medium — QA tooling.** Compare any retained native capture with the live game window. Text and scene are vertically inverted in the PNG; the live window is upright.

Evidence: `models/ATR42/exterior.png and upright/exterior.png; all other capture sessions` (original local evidence paths; see the portable bundle below).

Status: historical finding from the tested revision. Main subsequently fixed Metal row orientation and verified upright native frames; see [diagnostic evidence](../agent-gameplay/DIAGNOSTIC_EVIDENCE_2026-10-09.md). Do not reimplement this fix without a new reproduction.

Trace/limits: ReviewFrameCapture raw GPU-row encoding. Upright analysis copies are provided; originals retained.

## 5. Achievement card permits confusing interaction behind it

**Low — UX.** With the First flight achievement card visible, click background Plan flight or press Tab. The planner opens underneath the card and the view changes behind it.

Evidence: `manual40 snapshots around observe-16 through observe-20` (original local evidence paths; see the portable bundle below).

Trace/limits: Observed overlapping UI/input. Whether to make the card modal or otherwise clarify interaction is a design choice.

## 6. Contract ACCEPT silently requires a second click

**Low — UX.** Click ACCEPT on an unselected contract. The first click only selects/highlights it; the label still reads ACCEPT and no second-click instruction appears. The next click accepts.

Evidence: `manual40 and manual40b contract interaction` (original local evidence paths; see the portable bundle below).

Trace/limits: ContractsWorkspace.cs:558–562; AirsidePrototype.Airline.cs:3099–3128. ABANDON does display a second-click instruction.

## 7. Doorway generation fails and leaves bare hull

**Medium.** Watch opening cabin doors. Runtime doorway creation rejects the door shell and removes the generated doorway, leaving hull behind the opening. Repeated explicit warnings corroborate the visual failure.

Evidence: `manual40b/player.log; models/ATR42/player.log` (original local evidence paths; see the portable bundle below).

Trace/limits: AirsidePrototype.Doorways.cs BuildDoorway failure path. Counted once across affected types.

## 8. Kingscote turnaround remains on the runway

**Medium.** Follow the Saab arrival at Kingscote. After landing it remains at the runway endpoint throughout AtDestination rather than moving to an apron or stand.

Evidence: `manual40b/observe-23.png and observe-24.png` (original local evidence paths; see the portable bundle below).

Trace/limits: FlightWorld JourneyWorld AtDestination uses RegionalFlightPath.Landing with remaining=0. Presentation issue; simulation round trip still completes.

## 9. Outbound landing toast uses the return flight number

**Low.** Book/depart SA100 to Kingscote. The arrival toast says SA101 has landed at Kingscote, although SA101 is the return leg.

Evidence: `manual40b native landing observation and journey state` (original local evidence paths; see the portable bundle below).

Trace/limits: AirsidePrototype.Airline.cs arrival-event formatting treats AtDestination as returning. Confirmed by runtime observation and source trace.

## 10. Flight Manual describes obsolete controls and colours

**Low.** Open the manual and first-flight help. It places flights top-right, radar bottom-right and selected aircraft bottom-centre; actual placements are bottom-left, bottom-left and right. It also describes green destinations where available destinations are yellow.

Evidence: `manual40b native help pages` (original local evidence paths; see the portable bundle below).

Trace/limits: Grouped as one outdated-help finding.

## 11. Parafield watch retains an unidentified Adelaide radar

**Low.** Enable overview radar, then watch Parafield. The watch card says Parafield YPPF but the generic AIRPORT radar retains the Adelaide outline and RWY06 without identifying its airport.

Evidence: `manual40b/observe-34.png and observe-35.png` (original local evidence paths; see the portable bundle below).

Trace/limits: DrawMiniMap always uses FieldMiniMap and the Adelaide operations runway; MiniMapShows does not exclude WatchingParafield. This is a context/label defect, not evidence of incorrectly plotted Parafield traffic.

## 12. Bell 412 regional view never reaches the geographical destination

**High.** Book the Bell 412 to Kingscote and follow its exterior view. At Kingscote is reported while it hovers over sea approximately 40 km from Adelaide, with about 86 km still to KGC and 113 ft height. Earlier outbound progress also disagrees with geographical distance.

Evidence: `manual-heli/observe-06.png onward; native transition observation` (original local evidence paths; see the portable bundle below).

Trace/limits: HelicopterTrack.SiteWorld uses compressed SkyTraffic.ProjectLocal coordinates in the geographical FlightWorld. Position, route-progress and hovering symptoms counted together.

## 13. Saab return climb exceeds the implemented low-altitude speed cap

**Medium.** Watch the return departure from Kingscote at 40×. The native HUD showed GS320 kt / IAS308 kt at2698 ft, exceeding the project maximum CAS250 kt below10000 ft.

Evidence: `manual-heli return climb, native screenshot observed during session` (original local evidence paths; see the portable bundle below).

Trace/limits: FlightSpeedEnvelope.MaximumCasKnots defines the cap; FlightWorld regional departure handover blends authored and route positions without limiting blend velocity. Normal-speed reproduction remains unverified.

## 14. Quitting from exterior view throws a teardown exception

**Medium.** Select Exterior then quit. EndCockpit accesses a destroyed camera-controller transform through AirsidePrototype.OnDestroy and throws NullReferenceException.

Evidence: `manual-quit/player.log; manual-heli/player.log` (original local evidence paths; see the portable bundle below).

Trace/limits: Reproduced in a short exterior-only session. The runner correctly rejects the session log even when individual gameplay actions passed.

## 15. ATR 42 horizontal T-tail is absent in the rendered game

**Medium.** Add/select ATR42 and enter Exterior. Its vertical fin is visible, but the horizontal T-tail is missing from two rear-quarter angles. The packaged glTF contains tailplane/elevator geometry at the fin top, so the displayed silhouette disagrees with its authored model.

Evidence: `models/ATR42/upright/exterior.png; models/ATR42-repeat/upright/angle-two.png and angle-three.png` (original local evidence paths; see the portable bundle below).

Trace/limits: Confirmed in native live view and repeated capture. Model tailplane bounds y7.374–7.570 m, fin top7.590 m; art contract explicitly calls for a T-tail. Exact runtime rendering cause is not diagnosed.

## Remaining limits

Acceleration does not establish normal-speed behaviour or performance. Audio exceptions caused several view sessions to fail the runner’s log checks; those failures are reported, not converted into passes. No fixes were attempted. Seasonal headlines, coarse tracker wording, transient camera frames, weather transitions and QA-seeded fleet overcapacity were excluded from the15.

Closure verification: no Airside game process remained after the final test session.

## Handoff for other AI tools

This is a snapshot of findings on tested revision `aec646166196b269dd6c9d4a05e9059c2212060e`, not a declaration that all 15 still exist on latest main. Finding 4 has since been fixed and verified on main. Reproduce the other findings against current main before changing code. Source line numbers refer to the tested revision and can drift. Findings 5/6/11 include UX judgement; confirm the intended behaviour when choosing a remedy. Finding 13 has only accelerated-runtime evidence.

Claim each selected fix with a scoped task issue and branch under AGENTS.md; avoid parallel ownership of overlapping Presentation files. Use the reported-issue diagnostic workflow, inspect actual frames and errors, and rerun the same scenario after fixing. Publishing this report does not itself implement or claim any fix. Preserve simulation determinism, save compatibility, operational weather and personal saves.

### Portable evidence

The `evidence/` directory contains selected original PNGs, explicitly labelled upright analysis copies, protocol-1 plans, gameplay reports and numbered player-log excerpts. No personal or test saves are bundled. `evidence/manifest.json` records SHA256 hashes and original local provenance for copied files; absolute source paths are provenance only. Use the relative bundled files on another machine. Logs are excerpts, not a clean-log verdict. Runtime failures remain failures even when per-action reports say passed.

- `evidence/manual40/`: achievement overlap and contract-session context.
- `evidence/manual40b/`: Kingscote turnaround, Parafield radar, full action/state report and doorway warnings.
- `evidence/manual-heli/`: rotorcraft journey and return-flight context. The exact overspeed HUD observation was live; the retained nearby frame does not independently establish the exact quoted IAS/height. Reproduce finding 13 before fixing.
- `evidence/manual-quit/`: short exterior/quit reproduction and teardown stack.
- `evidence/models/ATR42/` and `ATR42-repeat/`: missing T-tail from two angles, inversion comparison and audio stack.
- `evidence/saab-right-window-original.png`: earlier dirty-stamped build window obstruction; reproduce on clean current main.

For findings 3/6/9/10, some exact live UI observations were not retained as a standalone selected screenshot. Their reproduction instructions and source traces are recorded above; the portable report does not pretend every quoted live HUD string has independent screenshot proof. Full local evidence remains under `work/bug-hunt-50/` and `work/bug-hunt-40x-20261009/` on Bailey's Mac.
