# 0056 — Career fleet, rotating contracts, departure prep, auto-stand

Date: 18 September 2026. Requested by Bailey on top of ADR 0055: map the
career loop, then implement it. Unlock planes with money; link routes to
those planes and pay more on them; contracts should come and go instead of
a fixed ladder; if the player does not assign a stand the aircraft parks
itself; a scheduled departure runs fuel → catering → boarding in order so
the player can see status. No new buildings or vehicles.

## The loop

```text
Fly (pay dispatch, earn FlightPay) → rotating contract offers (bonus)
       ^                                    |
       |                                    v
Buy a type you can afford ---------------- routes that type may operate
       ^                                    |
       reliability + funds + rotations      pay more on its band
```

There is no authored "complete A then B then C" chain. Offers are a
deterministic market of the injected clock plus owned types. Buying a
plane is the progression that opens routes.

## Route bands (linked to types)

| Band | Destinations (from Adelaide) | Types that may operate them |
|---|---|---|
| Regional | KGC PLO WYA MGB CED CPD MQL BHQ | ATR 42, Saab 340, Dash 8-400 |
| Domestic | MEL CBR SYD HBA | Dash 8-400 (range), 737-8, A321neo |
| National | BNE OOL ASP PER | 737-8, A321neo, widebodies |
| Tasman | AKL CHC | A321neo, widebodies |
| Long-haul | CNS DRW DPS SIN HKG | A350-900, 787-10 |

Range still applies. A Dash 8 cannot take Perth even though the band is
National. AI traffic is unchanged — only the player is gated.

Pay: existing type weight, times a band multiplier (Regional 1.0,
Domestic 1.25, National 1.45, Tasman 1.6, Long-haul 1.8). Bigger types
on bigger bands earn more.

## Buying aircraft

Authored prices and gates in `AircraftAcquisition`. The player may buy a
type when funds, reliability, completed rotations and operating tier all
clear, and a compatible stand is free *or* the aircraft can ferry in.

The new aircraft is added to the player fleet (generated `VH-P*`
registration). If a stand is free it parks; otherwise it is inbound as a
short delivery flight and auto-parks on landing.

Player turboprops that are away reserve that many regional bays from AI,
so buying a second ATR does not strand it. AI is not deleted to make
room. Jets take a free terminal gate when one exists (AI jets leave on
rotation). Purchase is refused with a reason when nothing can accept them.

Tier still exists as a capability label. It rises from rotations +
reliability + owned types (`AirlineCareerState.EvaluateTier`), not from
completing one named contract.

## Rotating contracts

`ContractMarket.At(now, ownedTypes, reliability, tier)` is a pure
function of simulation time (6-hour windows, three offers). Completing
or ignoring an offer does not lock a ladder; the next window is a new
draw from destinations the player can actually fly. Reliability below 70
hides everything above Regional. Offers expire at the window end.
Accepting snapshots the definition onto career state so settlement still
works after the window rolls.

One active contract at a time. Cancelling a counting flight still costs
reliability. Low reliability blocks better offers.

## Auto-stand

Player `AwaitingStand` uses `SuggestStand` the same way AI does. The
player can still tap a stand if they get there first; they are no longer
blocked if they do not. `AssignStand` remains. If no stand is free they
wait, same as AI.

## Departure prep (no vehicles, no buildings)

When a player departure is booked, a three-stage prep runs in order:

1. Fuel
2. Catering (food)
3. Boarding

Durations are type-scaled, owned by Simulation (`DeparturePrep`).
Pushback will not start until prep is ready. Presentation draws each
stage's 0–100% progress; it never decides when a stage finishes.
Engines still spool from `EngineStartSequence` in the last minutes.

## Save

Version 8: prep start time per aircraft; active-contract snapshot fields
for generated offers; completed player-rotation count. Pre-8 saves get
an empty market window (recomputed) and no in-progress prep.

## Guardrails

- Simulation owns money, offers, purchase, prep and stand choice.
- Presentation never awards funds or completes prep.
- Sky traffic still never reserves Adelaide resources.
- No restored airport-manager economy, wages, fuel market, loans or
  insolvency.
- Frame rate cannot change who is offered what: the market is a function
  of the injected clock.

## Acceptance

Headless `scripts/test-domain.sh`. Mac Play is not required for this
slice (Bailey: don't worry about playtests).
