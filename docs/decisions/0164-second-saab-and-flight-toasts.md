# 0164 — A second Saab from the opening cash, and flight toasts

Date: 28 September 2026. Author: Cursor, after Bailey asked to buy another Saab at the start for
extra money, and to be told when a flight lands away and when it is home waiting to be sent again.

## Decision

- The starter base holds two aircraft, on 50D and 50G. The first Saab is still given. A second
  Saab 340B is $1,600, Provisional, no flights required, paid from the $2,800 opening float.
  That leaves enough cash to dispatch it. Anything larger than a Saab still needs the expanded
  regional base.
- The only Saab cannot be sold. Once a second one is owned, either can be sold at the usual
  resale fraction, and the last remaining Saab cannot.
- Toasts: "{registration} has landed at {destination}." when a player flight arrives away, and
  "{registration} has returned home and is awaiting dispatch." when that flight is back on stand
  with no further booking. The on-final, choose-a-stand and parked lines stay for the other moments.

## Affected systems

`AircraftAcquisition`, `PlayerBase`, `AirlineOperations.BuyAircraft` / `CanResell`, the Fleet
market, setup and Flight Manual copy, `FlightNotices` and `AnnounceNewEvents`. No save schema
change: the gift is recognised as the player's only Saab, not as a stored flag.

## Migration

Existing careers keep their base level and fleet. A save that already expanded is unchanged. New
games can buy the second Saab before the first flight.

## Follow-up (28 September 2026, Claude)

Seven EditMode tests failed after this change: six were stale fixtures, one was a real regression.

- **Saab check price (regression).** `Maintenance.CheckCost` prices a check from the list price.
  The Saab used to have no offer and cost a flat $400. Its $1,600 sale price cut that to $300.
  The Saab check is now fixed at `Maintenance.SaabCheckCost` ($400).
- **Stale fixtures.** Several tests parked a player ATR 42 at the starter base. That base now
  operates Saabs only, so the ATR was never offered a stand. A new airline can't reach that state,
  so those fixtures now use the expanded regional base.
- **Stand suggestion.** 50G is now the player's second leased bay, so other airlines rightly skip
  it. The shortest-taxi test no longer compares against the player's bays.
- **Next aircraft and objective.** These tests now say the player owns a Saab, or both Saabs, so
  the second-Saab step is accounted for.
