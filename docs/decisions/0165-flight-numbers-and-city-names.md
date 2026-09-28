# 0165 — Flight numbers and city names

Date: 28 September 2026. Author: Cursor, at Bailey's request (flights should have flight
numbers and names, more like a real airport, using accurate routes where possible and
invented ones so the field stays busy).

## Decision

A flight is shown by its callsign and number, then the city, with the aircraft type in
the small print.

- Published Adelaide services use the operator's real callsign and a representative
  number for that city: Rex to Kingscote is ZL3482, Qantas to Sydney is QF680, Virgin to
  Melbourne is VA230, and the long-haul flights (Singapore, Emirates, Qatar and the rest)
  follow the same idea. These are real city pairs in the number band that airline uses.
  They are not a copy of today's timetable.
- The flight home is the next number (ZL3483).
- Up to four aircraft can fly the same city without sharing a number.
- Anything that is not a published service — extra routes, and every player flight —
  still gets a stable number in the 900s, so the board never falls back to a bare
  registration while a flight is booked.
- The player's own code is refused if it is one of those callsigns (QF, VA, ZL and the
  rest), as well as the operator ids already refused.

The selection card, the field tag, the map, the flights board and the toasts all use
that identity. A parked aircraft with nothing booked still shows its registration and
type. Nothing is saved: the number is worked out from the airline, the registration and
the city.

## Not changed

Schedules, stands, economy and which cities each airline flies. The aircraft type is
still on the card and under the board row.
