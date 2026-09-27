# 0128 — Delay attribution and actionable holds

Date: 27 September 2026. Author: Claude, at Bailey's request ("Delay feedback").

## Context

ADR 0127 made punctuality matter. An on-time streak pays challenges, and each pushback moves
reliability by between +1 and −2. ADR 0124 already said *why* an aircraft is waiting right now.
Two things were missing:

- **The hold line could not be acted on.** "Waiting to push back — Qantas 737-8 is on the taxi
  route" meant hunting for that aircraft by hand.
- **A settled flight never said why it was late.** The lateness was used for reliability and then
  thrown away, so the player saw the reliability move and nothing else.

## Decision — attribute every late player pushback (Simulation)

`Simulation/DelayAttribution.cs` defines three types:

- **`DelayCause`:** Turnaround, ApronBusy, LeadIn, Taxiway, RunwayCrossing, or Other. Curfew is not
  a cause, because the player is exempt from it.
- **`DelayBreakdown`:** the causes, largest first. The parts always add up to the lateness.
- **`DelayLedger`:** the current wait.

How the numbers are collected:

- **Turnaround** is the prep running past the booked time: `min(readyAt, pushback) − departAt`.
- **Ground causes are sampled at the pushback gates.** Each gate that holds a *ready* player
  departure returns through `AirlineOperations.NoteDelay` with its cause. A sample counts only at
  the ready moment and on the 5 s ground-control grid. A ready departure already wakes on every
  grid tick, and every step size visits both of those times, so the breakdown is the same however
  the clock is stepped (tested at 1 s steps and at uneven 7/61/13/29 s steps). Each interval goes
  to the cause seen at the sample before it. Several samples at the same instant add nothing.
- **At pushback** the ledger closes into `FleetAircraft.PushbackDelay`. Time the ledger did not see
  is booked as Other, for example waiting for the grid, or the time before a reload.
- **At settlement** the breakdown is copied into `FlightSettlement.Delay` and summed into the
  daily report ("2 late (9 min), mostly runway crossings").

The attribution never changes the economy. Pay, reliability and career pacing are unchanged.

**Save v16:** stores the pending breakdown next to `PushbackLatenessSeconds`, as
`"RunwayCrossing:180;Turnaround:60"`. Older saves load the lateness as all Other. The ledger for a
wait still in progress is not saved.

## Decision — show it and make it clickable (Presentation)

- **Paid toast:** `DelayText.SettlementToast` produces "… · on time (streak 7)." or "… · 4 min
  late: 3 min runway crossings, 1 min turnaround. \<tip\>".
  - Tone is caution when late, and negative past 15 minutes.
  - AI flights and older saves keep the plain line.
- **Selection card:** when a hold names another aircraft (the blocker, or the first of the
  aircraft filling the apron), the line gets a "›" and a hotspot that selects that aircraft.
  - A "‹ REG" chip returns for 30 s.
  - This works on AI cards too.
- **Ops workspace:** the same link on the selected aircraft's status line, plus a "Last flight: …"
  line.
- **Stats:** a Punctuality row showing the streak, how many recent flights were late, and the
  worst cause.
- **Flight Manual:** a "Why was I late?" entry.

## Not done

- A delay caused by a late *arrival* is not traced back through the turnaround. Prep aligns to
  the booked time, so that rarely shows as lateness.
- Stands the player cannot choose (NoStandFree) get no link.

## Verification

- `DelayAttributionTests`:
  - ledger arithmetic;
  - turnaround overrun;
  - a real ground hold that is the same at any step size;
  - a v16 save round trip, plus v15 loading as Other;
  - the settlement carrying the story;
  - wording and tone;
  - the painter's link and back chip.
- Headless suite 849/849; per-assembly type-check clean.
- Unity checks are listed in GAME.md.
