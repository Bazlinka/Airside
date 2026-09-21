# 0081 — Live Adelaide sky traffic from adsb.lol (presentation only)

Date: 21 September 2026. Requested by Bailey: "See if we can get live traffic connected
to the game for Adelaide airport? Ideally free."

Overrides the "no live traffic feed" section of ADR 0071 **only** within the limits
below. ADR 0071's guardrail asked for a new ADR naming the licence, offline fallback and
determinism story; this is it.

## Decision

Draw real aircraft around Adelaide in the 3D sky, from the free adsb.lol ADS-B feed.

- **Source:** `https://api.adsb.lol/v2/point/-34.945/138.531/60` (60 NM around YPAD). No
  key, no account. Polled every 10 s, backing off to 2 min while offline. Checked on
  21 Sep 2026: ~17 aircraft in range including YPAD arrivals, departures and ground movers.
- **Licence:** ODbL 1.0, the same licence as the OSM layout already credited. The on-screen
  credit becomes "Map data © OpenStreetMap contributors · Live traffic: adsb.lol (ODbL)"
  while live aircraft are drawn. adsb.fi (personal/non-commercial only), airplanes.live
  (non-commercial, 403 without terms) and OpenSky (research/non-commercial) were rejected
  on licence.
- **Determinism:** presentation only. Live aircraft are never a simulation input: they
  book no runway, stand or slot, are never saved, never affect the player's airline, and
  the Operations board still shows the authored day. Replay, soak and save tests are
  untouched because the simulation never sees the feed.
- **Offline fallback:** when the feed has not answered for 60 s, or is switched off in
  Options, the authored `SkyTraffic` / `AdelaideDayPlan` sky traffic draws as before. While
  the feed is healthy the authored sky is hidden so arrivals are not doubled.
- **What is drawn:** airborne airliner types the catalogue can stand in for (Saab, ATR,
  Dash 8, A32x/737/E-jets, widebodies). Light aircraft, helicopters and unknown types are
  skipped rather than misdrawn. Aircraft on the ground, or below 500 ft within 6 km of the
  field, are **not** drawn: real ground traffic would drive through the player's fleet on
  runways and stands the simulation believes are its own. Ground traffic needs its own
  design (a later ADR) before it appears.
- **Placement:** `YpadFrame` uses the layout generator's exact frame (runway-midpoint
  origin, +x along 05, +z to the left), so a real final lines up with the drawn runway.
  1:1 inside 5 km; the rest of the 60 NM radius is squeezed into the last 2.5 km of draw
  distance, height squeezed with it.
- **Privacy:** the request carries only the fixed airport coordinates and an identifying
  User-Agent. No player data leaves the machine.

## Found alongside

`SkyTraffic.ToRunwayFrame` took +z as the right of runway 05 while the layout, coast and
satellite ground use the left, mirroring every authored overflight across the runway.
Authored sky pitch was also inverted (departures nose-down). Both fixed.

## Guardrails

- Never read live traffic into `AirlineOperations`, saves, settlement or the board.
- Keep the credit on screen whenever live aircraft are drawn.
- Adding ground traffic, another feed or a paid feed needs a new ADR.
