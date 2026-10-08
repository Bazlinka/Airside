# Airside player-flow study

7 October 2026. Interactive design prototype for the 24-task [player-flow contract](../../plans/player_flows_and_interface_contract.md). Task [#567](https://github.com/Bazlinka/Airside/issues/567). The new horizontal navigation, bottom flight tray and opaque workspaces are proposed interactions to review, not an implemented Unity HUD.

## Open and try it

Serve the existing `docs` directory, then open the prototype:

```bash
python3 -m http.server 8765 --directory docs
```

Open `http://localhost:8765/art/player-flow-prototype-2026-10-07/index.html`.
The portable ZIP has the same application with bundled reference images: extract it and open `index.html`, or serve its extracted folder if the browser restricts local files. There are no external network requests or web-library dependencies.

1. **Start a new airline:** name/code → livery → briefing/coach → airport.
2. **Plan your first service:** select VH-PAX → Plan rotation → choose route/time → Review → Schedule. Dismiss review first to check that nothing was committed.
3. **Watch it and review its result:** open views, then use **Next sample event** to preview automatic stages. After seven events the aircraft returns and its result appears once.
4. **Choose a contract:** Career → Contracts → review/accept → Plan eligible flight. Eligible returns increment the same commitment and pay a separately itemised reward.
5. **Try Growing airline:** offers, bases, sales, outstation profiles/ferry and repeats are available without hours of progression. This replaces only the current unsaved sample.
6. **Try Disruption recovery:** attention links to a ground hold, required check and paused repeat. Study controls can release the automatic hold, preview low funds or add a return-stand wait.

The **Study controls / Next sample event** buttons are review controls, not proposals for game time acceleration. They replace elapsed time with explicit fixture transitions so a design review takes minutes. Close/Back/Escape dismisses UI without cancelling an accepted flight or check.

## What is connected

| Task | Interactive path / outcome |
|---|---|
| F01 | Three-step airline setup and optional first-service guidance |
| F02 | Continue saved browser study, factual return report, preserved load-error preview and backup restore |
| F03 | Attention queue identifies player decisions versus automatic waits and opens affected aircraft |
| F04 | Network route cards explain tier/range/origin restrictions and carry selection to planner |
| F05 | Aircraft/route/contract entry → persistent planner draft → review → accepted booking/funds update |
| F06 | Review cancellation consequences → explicit cancellation; closing details preserves the booking |
| F07 | Service stages, ground hold, blocker inspection and Back; automatic release through study controls |
| F08 | Compatible stand review, occupied/incompatible refusal and accepted assignment |
| F09 | Follow/exterior/cockpit/cabin reference views; remote/cargo limits; live-feed marker explanation |
| F10 | Once-only settlement, funds/contract/goal change, durable result history and next planning decision |
| F11 | Review/accept, eligible-flight handoff, progress/completion, abandonment confirmation and history |
| F12 | Pin goal, linked requirements/actions and deliberately shortened sample capability milestone |
| F13 | Unified base-filtered/sorted roster, persistent aircraft profile and logbook |
| F14 | Base-specific offers, eligibility reasons, cost/capacity review, purchase and selected new aircraft |
| F15 | Eligibility, proceeds/repeat-plan warning, explicit sale and updated roster/balance |
| F16 | Check review/cost, local stage sequence or timed outsourced path, repair-versus-return and wait |
| F17 | Sample supported Saab role change, eligibility update and disabled freighter seat view |
| F18 | Open outstation/upgrade Adelaide review, cost/capacity update and purchase handoff |
| F19 | Supported outstation → Adelaide ferry, inbound stand/capacity reservation and non-commercial arrival |
| F20 | Set/pause/resume/remove repeats; existing commitment preserved; booking refusal pauses future automation |
| F21 | Settlement cost/return/reward/net breakdown, contract and activity history, sample fleet results |
| F22 | Name/colour form, validation and updated shared identity |
| F23 | Help, reference controls/credits, dark/large settings, defaults, modal focus and Escape |
| F24 | Explicit browser-study save/title/continue; previously committed services finish once while away; no offline repeat bookings |

## What remains a prototype

- `flow-model.js` is a bounded, local fixture model, not a port of Domain/Simulation. Prices, demand, cancellation policy, capability milestones, costs and durations are illustrative. The UI cannot establish that the actual game will accept a command.
- Real elapsed time, taxi reservations, physical movement, service vehicles, 3D cameras, performance, audio, full route/base catalogue and actual forecasts are not simulated. Repeats use explicit sample events rather than a continuous interval scheduler.
- Immediate sample delivery, stand reassignment and short career advancement are conveniences for review, not changes to the approved game. Commercial flights and ferry moves remain distinct.
- The aircraft view images can show another type/operator or earlier build. They demonstrate camera-control placement; selected sample identity is not proof that the pictured aircraft is that identity.
- A remote check finishes as a timed sample event. A local check carries its phase through browser save/reload; this is not proof of Unity maintenance restore or swept-clearance behaviour.
- Save/load uses only browser key `airside-player-flow-study-v1`. It never reads, writes or imports a Unity save. Backup import accepts study schema 1 only. Load-error preview preserves the saved sample.
- This implements the new design-study scope only. Any Unity integration still needs an approved task packet, authoritative read-model/command wiring, layout/input checks and native Mac validation.

## Source and rights

All HTML/CSS/JS and schematic diagram geometry are original project-owned design/tooling source. System fonts only; no packages, downloaded fonts or generated image edits introduced by this prototype.

The background is the existing **30 September** unlabelled packaged game overview (`docs/testing/post-audit-p0-2026-09-30/overview-day-clear.png`), cropped and colour-adjusted in CSS only. It avoids presenting the baked traffic labels as part of the new UI. The actual 6 October airport/planning captures and current source were inspected during design; they are not live backgrounds.

Camera references: existing 6 October native Saab review, 1 October cockpit-mode native forward capture and passenger-window sheet. All are linked unchanged. Geographic/asset credits in the existing asset register continue to apply; this study is not new sourced geographic data. Existing Unity visuals/interface remain the fallback.

## Verification

Run `python3 scripts/verify-player-flow-study.py` while the docs server is running. It executes real browser clicks through complete successful/refused journeys and checks root overflow/focus at 1440×900, 1280×720, 900×720, 800×600 and 390×844. Browser screenshots are prototype captures, not Unity screenshots. Retained evidence is in `docs/testing/player-flow-study-2026-10-07/`.

This does not certify player usability, physical game-camera input, native IMGUI font metrics or runtime performance. Bailey's review of the interactions is the next design gate. The stage counters and shortened sample economy must never become production tuning through a copy/paste implementation.
