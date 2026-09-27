# 0148 — Propellers that spin like propellers

Date: 28 September 2026. Author: Claude, at Bailey's request ("improving propellers logic and how they
spin").

## Context

- **Strobing.** Blades rotated at true rpm, so at taxi power (420 rpm) each moved about 42° per
  frame at 60 fps. The blades strobed (wagon-wheel) until the blur took over, which was judged by
  rpm alone and ignored blade count and frame rate.
- **Flat disc.** The blur disc was a flat grey glass cylinder that read as a pancake.
- **Instant spool.** Spool-up was an exponential ease with no cap, so engines reached speed almost
  at once.
- **Spinning while paused.** Props and fans ran on unscaled time, so they kept spinning through a
  pause.

## Decision

- **Blur by what a frame can draw.** `AirsideReusableMotion.PropBlurForStep` blends blades into the
  disc from how far a blade moves in one frame against the gap between blades: blades show below
  15% of the gap and the disc is full by 35%. The blade count is read from each propeller model,
  defaulting to 4.
- **A real blur disc.** A double-sided quad in the propeller plane with a procedural texture: clear
  at the hub, thicker toward the tips, faint blade ghosts and a thin bright tip ring (URP Unlit,
  alpha blended). The disc is counter-rotated against the spin (97%), so its ghosts drift slowly
  instead of strobing.
- **Spool limits.** Speed changes are capped at +170 rpm/s up and −75 rpm/s down, on top of the
  existing ease. A start turns the blades visibly before they blur, and a shutdown runs down
  slowly.
- **Pause.** Propellers and jet fans stop when the presentation clock stops (`PropDeltaTime`).

## Verification

The type-check is clean. Bailey asked to skip new tests this round.

Mac checks:

- Saab, ATR and Q400 at the gate, at taxi and on takeoff: blades visible only at start and
  shutdown, no strobing, the disc reads as a propeller;
- pausing stops props and fans;
- jet fans unchanged apart from the pause.
