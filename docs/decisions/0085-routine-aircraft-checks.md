# 0085 — Routine aircraft checks

Date: 21 September 2026. Bailey: "more things to plan as you play, not just
contracts" — aircraft need checks, and the player chooses when.

## Decision

Every player aircraft wears one rotation per completed trip. A routine check is
due every **8 rotations**. The player sends the aircraft for the check from
Fleet (or the selected-aircraft card when it is overdue):

- The aircraft must be parked at its stand with no booked flight.
- Cost is 6% of list price (floor $300; the starter Saab, which has no hangar
  listing, is $400).
- Duration is 2 hours on a regional bay, 4 hours for a gate airliner.
- Wear resets to zero when the check starts.

Flying past the interval is allowed. Each rotation that lands already overdue
costs **2 reliability**. A departure whose time is still inside the check is
refused ("pick a later time"); one booked for after the check is accepted, so
the player can plan the night around the downtime.

## Where the player sees it

- Fleet roster and capability list: "Check in N rotations" / "Check due next
  rotation" / "Check overdue" / "In check until HH:MM".
- Fleet detail: **CHECK $N** next to Plan flight. When overdue, Send for check
  is the primary action.
- Objective card: "Next: send VH-PAX for a check" (overdue) or "wait for …'s
  check until HH:MM" (in check).

## Save

Version **11**. `RotationsSinceCheck` and `CheckUntilSeconds` on each aircraft
record. Pre-11 saves load every aircraft as freshly checked — a reload does not
invent overdue penalties.

## Not in this slice

Pay figures, check cost and interval are first guesses to tune by play. No
unscheduled failures, no parts inventory, no AI-airline checks.
