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

## Second pass: real gates and terminals by airline

Gate numbers and terminal membership now come from OpenStreetMap for Melbourne, Sydney, Brisbane, Perth, Canberra, Gold Coast, Darwin
and Alice Springs (`scripts/generate-airport-gates.py`, checked in CI). Each gate joins the nearest mapped terminal. Terminals list the
airlines that use them (`TerminalTemplate.Carriers`, codes from `Airline.cs`) and the planner prefers an airline's own terminal: Virgin
Australia at Melbourne T3, Qantas at T1, Jetstar at Sydney T2. Gate ids are `<terminal>-<ref>`, such as `T3-5`, because refs repeat
across terminals.
- Perth's OSM terminals are airline-named buildings, so its gates are grouped by number range (10-24, 143-156, 201-219, 501-604). The
  groupings are inferred and labelled by range, not as T1-T4.
- The other airports (Hobart, Kingscote and the regional fields) keep Wikipedia/generic gates. Cairns has terminals but no mapped gate
  numbers. Player aircraft have no real airline, so they take any suitable gate.
- All main runways match OSM and OurAirports (OurAirports lengths match for the seven SA/Broken Hill fields).
