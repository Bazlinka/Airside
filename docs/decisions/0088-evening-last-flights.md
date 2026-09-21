# 0088 — Evening last flights sit near curfew

Date: 21 September 2026. Bailey, playing the #366 build around 21:30:
last flights on the board at 21:38. Real Adelaide still has the 22:00
Qatar/Emirates bank and domestics until close to 23:00.

## Decision

The opening bank is a compact 54-minute morning peak only before 19:00.
From 19:00 the same arrival order stretches across the remaining time
until 22:50; parked Qatar and Emirates in that evening window keep the
22:00 slot instead of being pulled into the inbound dump. The airline
clock is assigned before any booking.

## Reason

Two stacking bugs made 21:38 the last row: (1) `AiOpeningDepartureSeconds`
glued a ~40-minute peak to session start, and (2) hour 20–21 density sat
under the 0.45 “useful” threshold so everything after 19:59 skipped to
06:00. Curfew is 23:00, not 21:38.

## Affected systems

`AirlineOperations.StartAtAdelaide` / `SeedOpeningTraffic`,
`AdelaideHourProfile` 20:00–21:00, long-haul 22:00 pin for UAE/QTR.
No save version change.

## Migration

None.
