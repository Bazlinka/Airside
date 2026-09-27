# 0133 — A dev-tools Showcase, and sounds for the moments

Date: 27 September 2026. Author: Claude, at Bailey's request ("do all of them").

## Context

Four rounds of work (ADR 0126–0132) are on main and none has run in Unity. Most of the new
moments are rare in play. A tier-up arrives hours apart, and a late pushback needs traffic. Checking
them by playing would take a day. The moments were also silent.

## Decision: Showcase

The dev tools (F8) gain a Showcase row. Each button fires one moment on demand:

| Button | What it does |
|---|---|
| Tier card | Shows the card for your current tier. |
| Contract card | Shows a contract-done card. |
| Finale card | Shows the finale card. |
| Funds count | Replays the count up from $4,200 less. Only the display moves. |
| Flip board | Every field on the Ops board flips in from blank. |
| Late toast | Shows the pay message of a flight 7 minutes late behind a runway crossing. |
| Add one of each new aircraft (test) | Adds the six ADR 0131 types to your fleet, each on a free stand that fits, so liveries, titles and gates can be checked. |

Only the last button changes the save. Use it on a test airline.

## Decision: sounds

`Presentation/HudSounds.cs` synthesises the sounds in code, like the game's engine and weather audio:
no files and nothing to register. The generators are pure and deterministic, and the runtime wraps
them as clips on first use. Mute (M) silences them.

| Sound | When | Rate |
|---|---|---|
| Flap rattle (clacks that slow and fade) | Board tiles flip | At most every 0.4 s |
| Cash ching (two bell strikes) | Funds start counting up | Once per rise |
| Tier sting (rising arpeggio) | A tier or finale card appears | Once per card |
| Contract chime (two notes) | A contract card appears | Once per card |

## Verification

- `HudSoundsTests`: each sound is short and audible, peaks at or below 0.75, fades out rather than
  clicking, and is identical every run.
- The Showcase is runtime code, checked by the type-check.
- The Mac checklist is in GAME.md.
