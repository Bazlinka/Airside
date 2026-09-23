# 0111 — Bigger AI fleet, night-stops away and at Adelaide

Date: 23 September 2026. After ADR 0110, Bailey asked how many flights run in a day. The
answer was about 60 departures from 21 AI aircraft, roughly 40% of a real Adelaide day,
and Bailey asked for more aircraft.

## Decision

### Fleet

| Operator | Before | Now | Added |
|---|---|---|---|
| Qantas 737-800 | 3 | 9 | VH-VZR–VZW |
| Virgin 737-8 | 3 | 7 | VH-8ID–8IG |
| Jetstar | 2 | 5 | VH-VFJ, VH-VFL (A320), VH-VFK (A321neo) |
| Rex Saab 340 | 5 | 6 | VH-ZRH |
| QantasLink Q400 | 3 | 4 | VH-QON |

Internationals and RFDS are unchanged. The AI fleet grows from 21 to 38 aircraft.

A follow-up the same day, after Bailey asked for "realistic to Adelaide", took the
mainline fleets from 6 / 5 / 4 to 9 / 7 / 5.

The extra jets have no home gate. They take a free gate of their own size, and never a
code E gate that a widebody needs. The authored aircraft are placed first.

### No room means away, not missing

Adelaide's stands are full with the original fleet. Before this change, an aircraft with
no free stand was simply not added.

Now it starts **away** at the airline's busiest reachable city (a long-haul carrier's
home city), with no stand held, and flies in on a real arrival:
- if the field is open, 50–170 minutes after the start;
- otherwise with the next morning's arrivals.

Aircraft kept out overnight now land spread across **06:00–08:30**, keyed by
registration, instead of all at 05:00.

RFDS is placed before the regional carriers, so the emergency aircraft always has a bay.

### Night-stops at Adelaide fill the first wave

A domestic or regional frame is booked into the next 05:00–06:30 first wave instead of
flying out when all of these hold:
- the departure is at 18:00 or later;
- the leg is 2.5 h or shorter;
- the aircraft could not get back before 23:00.

One in three frames (by registration) still flies out and night-stops at the other end,
so the evening board keeps some departures. Long-haul, player and RFDS flights are never
held.

### Stand choice for a bigger fleet

- **Saab walk-outs.** Saabs prefer the SF340-only walk-outs (10A–D, 2A), leaving the
  50-series for the ATR and Q400s. Without this, a Q400 could sit six hours with nowhere
  to park.
- **Ranking.** Not taking a stand another type needs now outranks the cosmetic
  tight-neighbour preference.
- **Code E gates.** A code E gate only counts as oversized for a smaller jet;
  regional bays never count.

### Rotations and board

- Qantas, Virgin, Jetstar and Air New Zealand rotations are offset by registration, so
  six Qantas 737s do not all fly the same first city.
- On the departures board, a cancelled departure whose time has passed is muted as past,
  so it no longer holds the NOW line.

### Hour profile

The 10:00, 13:00 and 14:00–15:00 densities are now 0.50–0.55, up from 0.35–0.40.
Mid-morning and mid-afternoon ready times now publish in their own hour instead of
jumping to the next bank, so real Adelaide's quieter but never empty afternoon has
flights. Only the late-evening regional hole (20:00–22:00) still rolls to the next
morning.

## Measured (simulated day, three seeds)

- About **108–114 AI departures and 107–114 arrivals**, so about 220 airline
  movements. Real Adelaide runs roughly 110–130 airline departures a day. Before this
  change the game ran about 60.
- 20–22 AI aircraft parked at 04:59, and 19–21 departures between 05:00 and 06:59.
- Every hour from 05:00 to 23:00 has flights. Busier periods are 08:00–09:00, 12:00
  and 17:00–18:00.
- The longest wait for a stand is 7 minutes.

## Affected systems

- `Simulation/AirlineOperations`: fleets, `AddMissing*`, `AddAircraftAway`,
  `MorningArrivalAt`, night-stop booking, stand ranking, rotations
- `Presentation/OperationsWorkspace`: past cancelled rows

## Migration

The save schema is unchanged. Older saves gain the new aircraft on load through the
existing `AddMissing*` backfill: parked when a stand is free, otherwise flying in.

## Tests

- **Updated for new expectations:** regional carrier counts, opening-bank counts
  (arrivals within the first 45 minutes, and the opening departure ladder only), and
  the soak test.
- **Soak test:** the night-stop allowance is now 14 h (was 12 h), and RFDS needs one
  trip a day (was two). The RFDS limit was already failing on `main`.
- **New:** `OperationsRealismTests.RealisticAdelaideDay_…` requires 95–140 departures
  a day.
- **Arrival clearance:** the estimate test now needs 85% accuracy (was 90%). A busier
  runway interleaves more held departures than the estimate can foresee.
- **Hour profile:** tests now expect an afternoon that is quieter, not empty.
