# 0092 — Player base operational facilities

Date: 21 September 2026.

## Decision

Player Base v2 turns ADR 0091's capacity gates into operational Adelaide facilities.

### Stand and gate access

- Starter: dedicated regional home at 50D.
- Expanded Regional: dedicated 50D, 50G and 10A positions plus access to the shared regional apron
  when a larger turboprop cannot use Saab-only 10A.
- Jet Gate: adds dedicated terminal gates 27 and 29.
- International: adds pier 28 (28L/28R) for widebody-capable handling.

Player jet delivery, arrival and manual assignment must use the leased gate set. AI fallback stand
selection does not consume dedicated player positions. Existing occupancy, shared-pier lead-in and
ground-conflict rules stay authoritative; the base never teleports or displaces another aircraft.

### Maintenance

The second half of this ADR will make the facility level determine whether a routine check is local
or outsourced. Local capability must change real check cost/time and Fleet copy, using the existing
maintenance wear/save state rather than a second maintenance system.

## Persistence

No new save field is required: stand access and maintenance capability derive deterministically from
the persisted v12 PlayerBaseLevel.

## Invariants

- AI schedules and fixed home gates remain intact.
- No player aircraft may occupy a stand that does not fit its type.
- Shared-pier lead-in reservations still apply.
- Save v12 remains compatible; no schema bump for derived capability.
