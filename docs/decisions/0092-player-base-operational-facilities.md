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

Routine-check capability follows the physical base:
- Starter: maintenance is outsourced.
- Expanded Regional: local turboprop checks.
- Jet Gate: local turboprop and narrowbody-jet checks.
- International: local checks for every player aircraft including widebodies.

The existing ADR 0085 check remains the only wear/check system. A local check uses its normal cost
and duration. Outsourcing costs 40% more and takes 50% longer. Fleet detail and the CHECK button use
the exact same effective cost/time as the command.

## Persistence

No new save field is required: stand access and maintenance capability derive deterministically from
the persisted v12 PlayerBaseLevel.

## Invariants

- AI schedules and fixed home gates remain intact.
- No player aircraft may occupy a stand that does not fit its type.
- Shared-pier lead-in reservations still apply.
- Save v12 remains compatible; no schema bump for derived capability.
