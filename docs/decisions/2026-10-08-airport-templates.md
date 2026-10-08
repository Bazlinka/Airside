# Generic airport templates for every Australian destination

Status: accepted (code only; unverified in Unity). Date: 2026-10-08.

Only Adelaide has a detailed scene. Every other Australian airport in `DestinationCatalogue.Australia` was a point on the map,
so a flight there had no runway to land on and no gate to park at. Bailey asked for generic templates, as accurate as the
sources allow, so every aircraft has both.

**Decision.** `AirportTemplate` (Domain) holds an airport's runways, terminals and gates; `AirportTemplates` (Simulation) lists
all 19 (ADL, MEL, SYD, BNE, PER, CBR, HBA, ASP, OOL, CNS, DRW, MQL, KGC, PLO, MGB, CED, WYA, CPD, BHQ). `AirportArrivalPlanner`
picks a runway end and a gate from the aircraft, the wind and a seed, deterministically.
- Runway: sealed, long enough (take-off roll x 1.3), the best headwind; in wind under 5 kt the airport's calm-wind end. Sydney's
  published practice (07/25 and 16L/34R for lighter aircraft) is a soft preference through `SoftMaxCodeLetter`.
- Gate: the right kind (international needs an international or swing gate), big enough by ICAO code letter, smallest first so
  widebody gates stay open, an aerobridge for jets when free. If all are taken it reuses one and reports it was not free.
- Helicopters use the apron: no gate.
- Every figure carries a `FactBasis` (Sourced / Inferred / Generic). Airports with no published gate count get a small generic one.
  Figures and gaps: `docs/data/AUSTRALIAN_AIRPORTS_RUNWAYS_TERMINALS_GATES.md`.
- Adelaide's template reuses the real stands in `AdelaideLayout`, so it cannot drift from the detailed scene.

**Calm-wind runway** is a planning default (the longest runway's lower end), not an official preference.

**Affected.** New Domain/Simulation files and tests. The network flight view HUD now shows "lands MEL RWY 16 · Gate T2-07".
Gate occupancy is not simulated away from Adelaide: callers may pass occupied ids; the HUD passes none.

**Migration.** None: no save schema change, nothing persisted. Data is from Wikipedia (CC BY-SA 4.0), not yet cross-checked against
AIP/ERSA; replace Generic gate counts as sources are found.
