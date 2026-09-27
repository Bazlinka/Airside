# 0123 — First-time airline setup, difficulty and the Flight Manual

Date: 26 September 2026. Author: Claude, at Bailey's request to improve how the player founds
their airline (difficulty, colours, customisation, a first-time-setup feel) and to make sure
instructions are covered.

## Decision

**Setup wizard.** New airline opens a four-card wizard over the title art, with a live preview
(the starting Saab on a livery-coloured band, the fuselage title, a sample flight number and the
difficulty):

1. **Identity** — airline name (24 characters) and a two- or three-letter flight code, suggested
   from the name until the player types one. Codes used by the real operators at Adelaide are
   refused so flight numbers never collide.
2. **Livery** — twelve curated colours, or mix your own from a 36-step hue strip and an 8-step
   shade strip (kept saturated and dark enough to read on a white fuselage).
3. **Difficulty** — Relaxed, Standard or Demanding, each listing its concrete effects.
4. **Briefing** — the four-point summary of how Airside works, a "coach my first flight" toggle
   and a button to the Flight Manual.

**Difficulty** is chosen once and never changes. It sets the starting float (Relaxed $6,000,
Standard $2,800, Demanding $2,000), scales route revenue (×1.20 / ×1.00 / ×0.90) and dispatch
cost (×0.85 / ×1.00 / ×1.10), and scales reliability losses from lateness and overdue checks
(Relaxed halves toward zero; Demanding ×1.5 rounded away from zero). Gains are never scaled,
and contract terms are never scaled — a contract pays exactly what it advertises. All scaling
goes through `AirlineOperations.DispatchCost` / `Forecast`, so the forecast the player sees is
the one they are settled on.

**Flight Manual.** Seven pages — Welcome, Your first flight, Money and contracts, Reliability,
Career, Growing the airline, Controls — reachable from the title screen (HOW TO PLAY), the
setup briefing, the rail's **?** button and F1 (which replaces the old controls sheet). A test
pins the numbers it quotes to the constants the game uses.

## Persistence and migration

Save v14 adds `Difficulty`, `CoachingOff` and the player airline record's `Code`. Versions 1–13
load as Standard, coached, with the code derived from the name as before. An unknown difficulty
in a v14 save is rejected like any other malformed field.

## Affected systems

Domain (`Airline.Code`), Simulation (`CareerDifficulty`, career state, operations, save,
first-flight guide) and Presentation (setup wizard, title screen, Flight Manual, rail help,
flight numbers). The first-flight guide is unchanged apart from being skippable.
