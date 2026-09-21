# 0082 — Live traffic on the maps and on the ground

Date: 21 September 2026. Bailey chose, from the options after ADR 0081: "Show live on maps"
and "Real ground traffic".

Extends ADR 0081. Everything there still holds: presentation only, adsb.lol (ODbL), never a
simulation input, never saved, authored traffic when the feed is down.

## Decision

- **Feed radius 250 NM** (adsb.lol's maximum; ~20 KB, ~35 aircraft) so the Route Map shows
  the region. The 3D sky still draws only out to 60 NM.
- **Route Map:** real airliners as pale plane icons under the game's own flights; callsign,
  ICAO type and height once zoomed in (≥4×). Light aircraft left off, as in 3D. Not
  selectable.
- **Mini-map and 3D tags:** live aircraft drawn on the field get hollow marks on the mini-map
  and "LIVE · callsign" tags, quieter than the game's and not clickable.
- **Ground and low traffic over the field** (on the ground, or below 500 ft within 6 km) is
  now drawn 1:1 on the pavement, with gear, taxi lights and landing/takeoff attitude. Ground
  positions are dead-reckoned at most 5 s (taxiways turn); stopped aircraft keep their
  heading.
- **The game always wins.** The simulation owns every stand and runway it uses and knows
  nothing of live traffic, so a real aircraft is **not drawn** while it would overlap one of
  the game's aircraft (wingspans plus 10 m), or while it is on a runway strip the game is using
  (a departure lining up or rolling, an arrival holding or landing). Once hidden it stays
  hidden 10 s so it does not flicker.

## Known limits

- Airliners at the gate usually have transponders off, so most real ground traffic is
  aircraft actually taxiing. The Royal Flying Doctor PC-12s and GA are not drawn (no model).
- The game's own AI airlines still run their authored schedule, so a real Qantas and a game
  Qantas can both be on the field. Replacing the AI day with the real one would make the
  feed a simulation input and needs its own ADR.

## Guardrails

Unchanged from ADR 0081: nothing from the feed may reach `AirlineOperations`, saves,
settlement or the Operations board.
