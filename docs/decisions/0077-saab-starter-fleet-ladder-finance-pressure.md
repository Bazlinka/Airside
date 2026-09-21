# 0077 — Saab starter, bit-by-bit fleet unlocks, light finance pressure

Date: 21 September 2026. Requested by Bailey: more contracts / finance pressure;
start with smaller planes and unlock bit by bit; smarter aircraft pricing; redo the
opening page.

## Decision

1. **Starter aircraft is the Saab 340B**, not the ATR 42. Saab is already in the
   catalogue, modeled, and smaller (34 seats vs 48). New games spawn `VH-PAX` as
   Saab 340 on bay 50A. Existing saves keep whatever type they already own.

2. **ATR 42 becomes the first hangar purchase** — the first "step up" after proving
   the airline on regional Saab work. Acquisition ladder (bit-by-bit):

   | Type | Price | Tier | Rel. | Rotations |
   |---|---:|---|---:|---:|
   | ATR 42 | $5,200 | Provisional | 70 | 5 |
   | Dash 8-400 | $14,500 | Regional | 75 | 12 |
   | 737-8 | $38,000 | Domestic | 82 | 20 |
   | A321neo | $55,000 | Domestic | 88 | 28 |
   | A350-900 | $98,000 | International | 92 | 40 |
   | 787-10 | $95,000 | International | 92 | 40 |

   Saab is no longer for sale (it is owned at start, like the old ATR).

3. **Authored intro contracts** (KGC / PLO / WYA) require a **Saab 340**. Melbourne
   still needs a Dash 8. Live `ContractMarket` offers already key off owned types.

4. **Light finance pressure:** starting float **$4,000 → $2,800**; dispatch distance
   coefficient **1.1 → 1.28**. Enough for several Saab hops; not enough to buy an ATR
   on day one without flying. Intro contract bonuses nudged up slightly so contracts
   are the clear path to the first hangar buy. No daily drain / insolvency this round.

5. **Opening page:** setup panel restyled — Provisional eyebrow, "Start small. Grow
   the airline." title, visible fleet ladder (`Saab → ATR → Dash 8 → jets`) and float
   callout, career blurb that names contracts as the unlock path. Intro wash line
   matches the ladder. Layout stays IMGUI this round — a full HudDrawList migration
   of setup is a follow-up, not blocked here.

## Reason

Bailey's play report: ATR-as-starter skips the "earn your way up" fantasy, hangar
prices did not read as a ladder, money felt soft, and the opening screen did not
sell the career. Using the existing Saab avoids inventing a new type/art kit.

## Affected systems

Domain (`AircraftAcquisition`, `RouteContractDefinition`), Simulation
(`AirlineOperations.StartAtAdelaide`, `FlightEconomics`), Presentation (setup +
intro copy). Save: no schema bump — type is already per-aircraft.

## Migration impact

Pre-existing careers that started with an ATR keep that ATR. New careers get a
Saab. Authored intro contracts that required ATR now require Saab — an old save
mid-REG-KGC on an ATR still matches via `EligibleType` on the active snapshot
fields if those were snapshotted; catalogue definitions used for new accepts are
Saab. Verify: active-contract snapshots store type id at accept time
(`AirlineSave` / accept path) — do not rewrite mid-contract snapshots.

## Guardrails

- Do not invent a type smaller than Saab without art + Bailey sign-off.
- Do not restore airport-manager wages / fuel markets / insolvency in this cut.
- Keep MaxPlayerAircraft at 4.
- Hero ATR art remains in the project for when the player buys one.
