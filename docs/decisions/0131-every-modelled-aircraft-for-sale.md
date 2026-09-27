# 0131 — Every modelled aircraft is for sale

Date: 27 September 2026. Author: Claude, at Bailey's request: "add em all if art exists".

## Context

Before this, the player could buy 6 types plus the starter Saab. Six more types were fully modelled
(3D model, thumbnail, specs, fuselage titles), but only other airlines flew them: E190,
A220-300, A320-200, 737-800, A330-900neo and 787-9.

The ladder had two gaps:
- a price-doubling jump from the Dash 8 to the 737-8;
- only large, expensive widebodies.

Separately, the Fleet market only ever showed the first three offers in list order. The A321neo and
the widebodies never appeared there at all.

## Decision

**All six are for sale** (`AircraftAcquisition`). The offer list is in career order: tier, then price.

| Type | Price | Tier | Reliability | Flights | Flies | Running cost |
|---|---|---|---|---|---|---|
| Embraer E190 | $20,000 | Domestic | 80 | 16 | domestic | ×0.92 |
| Airbus A220-300 | $24,000 | Domestic | 82 | 18 | Tasman | ×0.95 |
| Boeing 737-800 | $24,500 | Domestic | 80 | 18 | national | ×1.12 |
| Airbus A320-200 | $25,500 | Domestic | 80 | 18 | national | ×1.10 |
| Airbus A330-900neo | $66,000 | International | 90 | 36 | long-haul | ×1.02 |
| Boeing 787-9 | $74,000 | International | 91 | 38 | long-haul | ×0.98 |

- **Running cost:** `FlightEconomics.RunningCostFactor` scales only the dispatch cost, never the
  pay. The older narrowbodies are cheaper to buy than the 737-8 and dearer to fly.
- **Pay and fill:** fares depend on how full the aircraft is, not its size. A small jet on a thin
  route earns as much as a big one, so the E190 and A220 are sensible, not just cheaper.
- **Jet detection:** `AirlineCareerState.OwnsAnyJet` now counts any terminal-gate type, so the E190
  meets "Fly a jet". It used to be a hard-coded list of four types. The widebody goal already used
  `AircraftCatalogue.IsWidebody`, so the A330 and 787-9 count toward it.
- **Fleet market:**
  - Offers you can buy come first, then those unlocked but short of money, then the rest in career
    order.
  - A ‹ › pager ("1–3 of 12") pages through all offers.
  - A lock that every shown card shares is said once.
- **Flight Manual:** the aircraft section is updated.

## Balance

The CareerBot now meets "a jet" and "a widebody" goals with the cheapest type (the E190 and A330),
as a sensible player would. Report: `docs/testing/career-balance-2026-09-27/lineup/`.

| Style | Stage | Before | After | Target |
|---|---|---|---|---|
| Competent | International | 69.0 h | 61.5 h | 35–70 h |
| Competent | Finale | 139.3 h | 141.0 h | 110–150 h |
| Casual | International | 84.4 h | 74.3 h | 35–70 h |
| Casual | Finale | 172.6 h | 170.7 h | 110–150 h |

The regional and domestic stages are unchanged.

Watch this: the bot now runs its cash lower. Minimum cash is $1, and it is refused a flight it
can't afford about 2,600 times, against 240 before. The cheaper jets let it buy sooner. The rescue
contract means nobody is stranded, but playtesters should say whether the Domestic stage now feels
cash-tight.

## Verification

- `AircraftLineupTests`:
  - every modelled type except the Saab is for sale, with its art;
  - each type reaches somewhere in its own band;
  - the trade-offs hold: the E190 is the cheapest jet and counts as a jet, the older jets are
    cheaper and dearer to run, the new widebodies are cheaper, and pay is unaffected;
  - the market leads with buyable offers and pages.
- 863/863 headless tests pass; the type-check is clean.
- Mac checks:
  - each new type renders in the player's livery on the field;
  - fuselage titles fit;
  - the E190 and A220 use terminal gates.
