# 0087 — Adelaide curfew, short-final freeze, recorded engines

Date: 21 September 2026. Bailey, after playing the maintenance-planning
build: Adelaide closes at a real hour; flights should follow that except
the player and emergency RFDS; the player froze at ~130 ft while another
aircraft landed; inject free recordings that match the actual types and
follow startup / takeoff.

## Decision

### Curfew 23:00–06:00 local

Adelaide Airport Curfew Act 2000. Commercial AI does not start a push or
a landing in that window. A taxi that started before 23:00 may take off.
The **player** and **RFDS** (one Saab 340 stand-in for the real PC-12 /
King Air, `VH-FDA`) stay exempt. RFDS prefers the closed window so it does
not crowd the commercial day. AI first push hour is 06:00; last is
22:00–22:59.

### Short final no longer parks at 130 ft

The tower can withhold a landing for a vacate while the drawn aircraft has
already reached the 80 % pin (~130 ft AGL). ArrivalFinal now weaves a small
S-turn while that close, so a short wait is not a statue.

### Recorded engines

CC0 Freesound beds, not the procedural sine:

- Dash 8-400: Pack489 / mycompasstv PW100 takeoff (`eng_dash8_q400_pw100`)
- Saab / ATR: Pack489 Dash 8-300 twin turboprop (`eng_dash8_300_twin`)
- Jets: qubodup jet turbine, US Government source (`eng_jet_turbine`)

Pitch and volume still follow spool, taxi and takeoff so startup is not
the same sound as rotate. Procedural sine remains the fallback.

## Reason

Bailey asked for the real close-of-play, noted the 130 ft stall as worth
fixing, and wanted free recordings of the actual aircraft, matched to
phase. Exact Saab 340 / 737-8 field recordings under CC0 were not
available; family beds plus phase pitch are the honest substitute.

## Affected systems

Domain `AirportCurfew`, `Airline.Rfds`. Simulation hours, tower estimate,
RFDS fleet. Presentation engine audio and ArrivalFinal weave. Asset
register AUD-007/008/009.

## Migration

No save version change. Old saves gain RFDS on load if a regional bay is
free (`AddMissingEmergencyOperators`).
