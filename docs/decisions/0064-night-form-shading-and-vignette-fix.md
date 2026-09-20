# 0064 — Night moonlight for form shading, and a backwards vignette

Date: 2026-09-20

## Decision

A direct continuation of ADR 0063's night visibility floor, from the same report ("I need you
like crazy to fix lighting at night! I can't see anything!") — two further, narrower fixes:

1. **Raised the night key light floor.** `AirsidePrototype.ApplyDayCycle`'s directional "Sun"
   light's night-floor intensity: 0.18 → 0.30. Still well under every flood/runway light's own
   intensity (52 apron / 1.55–2.1 runway edge/threshold/PAPI/ALS — ADR 0063), and under the
   daytime intensity (2.05).
2. **Fixed a backwards vignette.** `AirsideDayVolume.Apply`'s vignette intensity was
   `Lerp(0.1, 0.05, daylight)` — *stronger* at night (0.1) than day (0.05). Flipped to
   `Lerp(0.04, 0.07, daylight)`: night now gets the lighter vignette, day (which has exposure
   headroom to spare) keeps the slightly heavier one.

## Reason

ADR 0063 raised the ambient trilight floor and night exposure, on the reasoned diagnosis that
the default ~2400 m Fleet/career overview camera sees mostly ambient-only field (every actual
light source's range tops out around 115 m). That fix addressed *how dark* the floor is, but
ambient light is flat — it has no direction, so it can raise brightness without giving anything
shape. Even with the floor lifted, a field lit by ambient alone reads as a uniform grey wash: an
aircraft, a hangar roof and open grass all shade the same way, so there's still nothing to
actually *see*, just a lighter version of the same undifferentiated fog. A moonlight-strength
directional key (still far under the floods) gives every surface a lit side and a shaded side —
the difference between "less dark" and "you can tell what you're looking at".

The vignette going the *wrong* direction was found while re-reading `AirsideDayVolume.Apply` for
this pass: whatever the intent (arguably a moodier night frame), the practical effect was to
darken exactly the corners of exactly the frame that a real player already reported as too dark
to see. That's a straightforward bug against the stated goal, independent of the moonlight
question, and cheap to fix outright rather than leave for a future pass.

## Consequences

- Both changes are Presentation/Unity-only (`AirsidePrototype.cs`, `AirsideDayVolume.cs`) —
  outside the headless harness, reviewed by inspection only. `scripts/test-domain.sh`: 500/500
  (unchanged; this round touches no Domain/Simulation code).
- Like ADR 0063, this is a reasoned correction, not a confirmed fix — no Unity editor was
  available to watch the actual result. The moonlight bump is deliberately small (0.18 → 0.30,
  still ~15% of daytime intensity) so it adds shading without itself becoming a second "why is
  it so bright at night" report if it overshoots; the vignette fix has no such trade-off, it was
  simply backwards.
- Getting an actual look at the default Fleet-mode overview at night — first flagged in ADR
  0063 — remains the single highest-priority open item across this session's Presentation work,
  now touching ambient, exposure, key light and vignette together. If the combined effect is
  still too dark, still too flat, or now overshoots into washed-out, the follow-up is tuning
  these same handful of numbers, not a redesign.

## Tests

`scripts/test-domain.sh`: 500/500 (no Domain-layer change this round). Night lighting remains
verified by inspection only; `scripts/test-unity.sh` and an actual on-screen look at night are
still owed.
