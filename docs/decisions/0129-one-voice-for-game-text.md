# 0129 — One voice for the game's text

Date: 27 September 2026. Author: Claude, at Bailey's request: "do wording across the whole game to
better fit and flow without ai".

## Context

The game's text was written in many passes by several contributors. It read like it:

- The same thing had four names: rotation, service, trip and flight.
- Em dashes were in almost every sentence.
- Semicolons turned up in refusals.
- There were stiff phrases such as "Aircraft capability, reliability or service requirement is not
  met", "Delegation unlocks after 12 manually planned services" and "service commitment".
- The pay toast showed internal contract ids (`MKT-12-0-KGC-saab-CH`).

## Decision

`docs/product/WRITING.md` is the house style for every string the player sees. In short:

- Short, plain sentences in a duty manager's voice.
- No em dashes in player text (a lone "—" is still used for an empty board cell).
- No semicolons.
- One word per thing. A **flight** is Adelaide out and back. "Rotation" and "service" stay in code
  only.
- Refusals say what blocks you and how to fix it.

Everything the player reads was rewritten to it:

- **Simulation and Domain:** refusal messages, career events, the daily report, goal, milestone and
  challenge titles, and demand-event headlines. `RouteAccess.Label` gives the route band in a
  sentence.
- **HUD:** hold lines, toasts, the first-flight coach, the Ops, Map, Fleet, Contracts, Stats and
  Career pages, the selected-aircraft card and the controls list.
- **Onboarding:** the title card, the setup wizard, and the whole Flight Manual. Its quoted numbers
  now come from the game's own constants.

Fixes found along the way:

- The pay toast no longer shows contract ids.
- "1 contracts" is now "1 contract".
- The Punctuality row no longer takes the "Next milestone" slot. It sits under the contract record.
- Manual sections no longer get cut off at 800×600. A test now checks every section heading is drawn
  at every supported window size.
- On the Map, the line for a route you can fly no longer reads as a requirement.

## Not changed

- The stats card for base capability is still cut short in narrow layouts. Its content is pinned by
  tests, so it needs a layout change, not a wording change.
- Dev tools text and object names in the scene were left alone.
- Save data is unchanged. Only wording moved.

## Verification

- 849/849 headless tests pass, with every changed assertion updated to the new wording.
- Per-assembly type-check is clean.
- The offline HUD renders of the setup wizard, manual, Map, Fleet, Contracts and Stats were checked
  by eye.
