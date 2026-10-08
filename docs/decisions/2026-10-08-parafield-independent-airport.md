# Independent Parafield airport

Date: 2026-10-08. Status: accepted scope; owner-authorised merge with deferred packaged validation.

## Decision

Bailey agreed to start Parafield as an independent working airport: real location,
four runways, mapped taxiways/apron/hangars, one light trainer type and local traffic.
It is watchable from Operations; Adelaide remains the player airline's starting airport.
Player bases, charter economics and the aircraft market are outside this first version.

## Implementation and boundaries

- Retained OpenStreetMap/OurAirports source snapshots and offline geometry generator.
- Original unbranded high-wing trainer, 11 m span, fixed tricycle gear, animated propeller.
- Four dedicated parking positions. One trainer moves at a time; its movement corridor
  reserves all four runways and crossing taxiways before leaving the stand.
- Main runway 21R training circuit. Routes use U/J/J3/H6/H/B/B5 outbound and B2/B inbound.
  Flight heights/speeds are simplified, independently authored background traffic,
  not a reproduction of current operational ATC procedures.
- Poses derive from an injected simulation clock and seeded roster order. No per-frame
  integration, live data feed, player commands, persisted state or save migration.
- Elevation platform blends back into the existing Adelaide DEM outside the airfield.
- Camera watches and pans around Parafield; Adelaide/R/Overview returns to Adelaide.

## Data and art

OSM contributors: ODbL 1.0. OurAirports: public domain. Official Parafield master-plan
facts resolve outdated runway surface/lighting data. Costs: zero. Source evidence and
fallbacks are recorded in the asset register. No copied aircraft geometry or airline marks.

## Verification

See `docs/testing/parafield-2026-10-08/README.md`. Packaged screenshots are distinct from
headless/native tests. Native compilation passed; full suites stopped at Bailey's explicit request for immediate merge.
Packaged build and visual validation remain pending.

## Migration

No persisted schema or player economics changes. Removing the build/update hooks removes
the independent airport; previously approved Adelaide art/traffic remains the fallback.
