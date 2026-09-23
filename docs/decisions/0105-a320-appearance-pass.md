# 0105 — A320 appearance before dimensional perfection

Date: 23 September 2026.

## Decision

AIR-011 keeps its existing asset ID, nose-stop datum, 35.80 m span and 11.76 m
height while replacing the parts that made it read as a scaled 737: fuselage
stations, flight-deck glazing, cabin-window rhythm, wing planform and split
winglets. Its livery is a thin geometry ribbon conforming to the fuselage and
coloured through the existing operator-colour material path. The existing
running gear, engines and operational animation nodes stay until a later
reviewable slice can improve them without destabilising the aircraft loop.
The A320 alone stops receiving the old repeating generic fuselage decal: in the
packaged build it produced dark barcode bands over the metre-UV skin. Its fitted
ribbon and existing identity markings retain the operator colour instead.

## Reason

At overview and close-follow distance, silhouette and colour blocking matter
more than sub-centimetre measurements. Reusing the entire 737 mesh made the A320
visually indistinguishable and gave it the wrong tip device. A source-generated,
unbranded ribbon improves its identity without importing real airline art.

## Affected systems and migration

Presentation and AIR-011 art only. No simulation, state-changing commands,
controls, save format, material identity, routes or external data change.
The true-scale primitive remains the missing-art fallback. There is no migration.

## Acceptance

The A320 must read as distinct from the 737 in overview and follow views,
retain the existing planning envelope and tyre/nose datums, show no floating
parts or real markings, load in a packaged Mac build, and hold up in day,
dusk and night lighting. `scripts/test-air-adelaide-fleet.py` protects the
envelope and visible family cues; `scripts/audit-aircraft-geometry.py floating
A320` checks disconnected details.
