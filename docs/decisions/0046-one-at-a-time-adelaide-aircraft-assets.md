# 0046 — One-at-a-time Adelaide aircraft asset rollout

Date: 2026-09-14. Bailey approved the first assets in a deliberately incremental
fleet rollout: Boeing 737-8, Airbus A321neo, Airbus A220-300, Boeing 787,
Airbus A320, Dash 8 Q400 and Saab 340B. The models are to arrive one at a time
and can be refined after their first in-game integration.

## Decision

AIR-005 is an original, unbranded 737-8-class narrowbody. It is modelled at
real-world reference scale (39.47 m length, 35.92 m span and 12.42 m height)
using project-owned procedural geometry. Its visual read must prioritise the
low wing, large under-wing turbofans, conventional tail and dual-feather
winglets; it must not use airline logos, registrations, wordmarks or a copied
commercial livery.

The first integration is a parked, selectable Gate 13 presentation asset at
the OSM-derived nose-stop datum. It is not an airline-simulation aircraft yet.
The asset exists in the visible airport now, while the next airport-operations
slice can add the necessary terminal-gate lead-in, pushback turnout,
reservations and timetable without pretending those systems are already safe.

## Why this order

Gate 13 is a real Adelaide terminal position and the 737-8 family is a useful
first common narrowbody class for the airport. The current regional traffic is
correctly confined to bays 50A–50F. Treating a terminal gate as a regional bay
would silently fall back to BAY-1 and put a jet in the wrong place; animating
the current Gate 13 parking line would also cross an uncovered section of
apron. A parked visual preview gives the player a truthful first asset while
keeping simulation ownership and reservations unchanged.

## Affected systems

- `Assets/Airside/Art/Models/Aircraft/` and StreamingAssets copy for AIR-005.
- Presentation-only aircraft model/profile, framing, shadow and hit footprint.
- OSM layout generation emits the Gate 13 anchor but does not add it to the
  regional-bay resolver.
- No simulation, save schema, reservations, schedule or fleet ownership changes.

## Exit criteria for the next slice

Before a 737 can move under its own simulated state, add a paved gate lead-in,
an explicit pushback/turnout, a terminal-stand model separate from regional
bays, stand/runway reservations and tests for the complete taxi-in/out path.
Only then may it join an airline timetable.
