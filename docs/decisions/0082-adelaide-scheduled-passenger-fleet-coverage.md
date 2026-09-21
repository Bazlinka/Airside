# ADR 0082 — Adelaide scheduled-passenger fleet coverage

**Date:** 2026-09-21  
**Status:** Accepted  
**Decision owner:** Bailey Fleming

## Decision

Airside models the recurring scheduled-passenger aircraft types evidenced at Adelaide Airport,
not every private, freight, charter or one-off visitor. Six gaps are added as genuine, true-scale,
project-owned procedural kits: Airbus A320-200, Boeing 737-800, Embraer E190, Airbus A220-300,
Airbus A330-900neo and Boeing 787-9.

Existing types remain: ATR 42-600, Saab 340B, Dash 8-400, Boeing 737-8, Airbus A321neo,
Airbus A350-900 and Boeing 787-10. Charter-only Fokker types, Metroliners, freighters and
general aviation are outside this pass.

## Reason

Adelaide Airport's own 2025–26 releases explicitly identify the six missing types on normal
services: Jetstar and Indonesia AirAsia A320s; Qantas 737-800; QantasLink E190 transitioning
to A220; Malaysia Airlines A330-900neo; and United 787-9. The live ADS-B layer previously drew
all six as visibly wrong stand-ins.

## Affected systems

- Aircraft catalogue, performance, true-scale art profiles and title placement.
- Live ADS-B ICAO-type mapping.
- Existing representative Qantas, Jetstar, Malaysia, Emirates and Fiji operator equipment.
- Hangar thumbnails and the asset/data registers.

## Migration impact

No save-schema change. Type IDs are additive. Existing saved aircraft keep their recorded type;
new games use corrected operator equipment. The A220 and E190 are catalogue/live-traffic models
only in this slice so airport congestion and the player progression ladder remain unchanged.
