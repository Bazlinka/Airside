# 0063 — Night visibility floor, and Fleet copy that names real destinations

Date: 2026-09-20

## Decision

1. **Raised the night ambient/exposure floor.** `AirsidePrototype.ApplyDayCycle`'s night
   trilight ambient (`ambientNight` (0.20,0.23,0.32) → (0.28,0.32,0.42); ground tone
   (0.13,0.14,0.17) → (0.19,0.20,0.24); `ambientIntensity` floor 0.88 → 1.05) and
   `AirsideDayVolume.Apply`'s night `postExposure` (-0.12 → 0.06) are all lifted. Every light
   *source* (apron floods, runway edge/threshold/PAPI/ALS, stand markers) is unchanged — this
   only raises the floor for everything those lights don't reach.
2. **Fleet market and detail copy names real destinations instead of a bare band label.**
   `RouteAccess.ExampleDestinations(RouteBand)` (new, Domain, unit-tested) gives two
   recognisable place names per band. `FleetWorkspaceModel`'s "Cleared to buy · flies X
   routes" and "X capability" lines now read e.g. "Domestic capability (Melbourne, Sydney,
   +closer)" instead of just "Domestic capability". The tier-requirement line now says
   "Requires Regional **career** tier" instead of bare "Requires Regional tier".

## Reason

Bailey, from an actual play session (not a report about hypothetical code): "I need you like
crazy to fix lighting at night! I can't see anything! There should be lights right?" — and
separately, after buying an aircraft: confusion about what it gives you, what it costs, where
it can fly, and "what is Regional?"

**Night lighting:** a dedicated research pass traced every light source's actual computed
intensity at night and found them all correctly bright (apron floods 52, runway edge 1.55,
threshold/approach 1.85, ALS 2.1, stand markers 1.1 — none reversed or zeroed). The real
mechanism: `AirsideBareField.OverviewDistance` (2400 m) is the *default* Fleet/career camera
distance over a ~3900×2800 m field, but every one of those lights has a 9–115 m range. From
the player's actual starting view, almost the entire frame sits outside every light's reach and
is lit by ambient alone — which a comment in `AirsideDayVolume.cs` already worried about
("do not crush midtones into a purple soup") but, per GAME.md's own 2026-09-17 entry, was
never actually checked on screen ("The grading and camera feel are unverified in a build").
Combined with a -0.12 EV night exposure and ACES tonemapping's toe curve, that ambient-only
majority of the frame plausibly crushed toward black — which is exactly "I can't see
anything": not broken lights, an unlit-in-practice everything-else.

**Fleet clarity:** `OperatingTier.Regional` (a career milestone — completing contracts unlocks
it) and `RouteBand.Regional` (a destination category — which places an *aircraft* may fly) are
two unrelated systems that both use the word "Regional", right next to each other on the same
screen ("Dash 8-400 · Domestic" ... "Requires Regional tier"). "What is Regional?" is a
completely reasonable question to ask when the same word means two different things one line
apart with nothing distinguishing them.

## Consequences

- **Night lighting is a diagnosis and a conservative, reasoned correction — not a confirmed
  fix.** No Unity editor was available to actually watch the result. The change is deliberately
  a *floor* raise, not a rewrite of the lighting or post-processing system, and every light
  source's own intensity/range is untouched, so floods/runway lights should still read as the
  brightest features relative to the raised ambient — but this needs a real on-screen look
  before it can be trusted, and if it's still too dark (or now reads as washed out) both
  directions are a small, easy follow-up tweak to the same handful of numbers, not a redesign.
- Fleet copy change is Presentation-only, `ContractsWorkspace`/`FleetWorkspace.cs`-style —
  verified for real this time via `scripts/hud-mockup` + `render-hud-mockups.py` (a first
  version clipped against its own text box at the widest band; caught immediately by
  re-rendering and fixed by shortening `ExampleDestinations` to two names and the "and
  everything closer" suffix to "+closer").
- `RouteAccess.ExampleDestinations` is in the headless harness (Domain) —
  `scripts/test-domain.sh` **500/500** (499 baseline + 1 new: every band has a non-empty,
  distinct example string). `FleetWorkspaceTests`' existing capability-line assertion updated
  to match the new, more informative text.
- The night-lighting change (`AirsidePrototype.cs`, `AirsideDayVolume.cs`) is Unity-only,
  outside the harness, reviewed by inspection only.

## Tests

`CareerExpansionTests.ExampleDestinations_NamesRealPlacesForEveryBand` (new).
`FleetWorkspaceTests.Fleet_SelectionReportsRealCapabilityAssignmentAndPreparation` (updated
assertion). `scripts/test-domain.sh`: 500/500. Fleet copy re-verified visually via the mockup
renderer after the first version clipped. Night lighting: `scripts/test-unity.sh` and, more
importantly here, an actual look at the default Fleet-mode overview at night are the real
verification still owed — this is the highest-priority item to check first next Unity session.
