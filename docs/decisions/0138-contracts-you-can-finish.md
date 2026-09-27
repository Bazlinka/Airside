# 0138 — Contracts you can always finish

Date: 27 September 2026. Author: Claude, at Bailey's request ("random things and goals in contracts
not being achievable").

## Context

- **Fixed deadlines.** The market gave each contract kind a fixed deadline: charter 6 h, medical
  3 h, freight 12 h per flight and scheduled 10 h per flight. Nothing checked these against the
  aircraft. About 28% of widebody offers could not be flown in time: a 6-hour charter to Dubai
  is impossible. Medical calls went to towns a slow turboprop can't reach and return from in
  3 hours.
- **Featured contracts.** A featured authored contract was shown whenever its aircraft's tier was
  met, even when reliability, flights or the base still blocked the purchase.
- **Challenges.**
  - Medical calls were rare, so "fly 2 medical calls" waited on luck.
  - "Keep 95% reliability to 300 flights" counted every flight ever flown whenever reliability
    was 95% at that moment, and dropped to zero when it dipped.
  - The day's flights were forgotten on reload, so the profitable-day challenge could be lost.
- **Deadline tie.** A contract expired before a flight parking on the same second was settled.

## Decision

- **`ContractFeasibility` (Simulation, pure).**
  - One flight is both legs, plus the 40-minute destination turnaround, plus prep at the current
    base, plus 20 minutes of taxi and runway.
  - `FairDeadlineSeconds` is 1.25 times that minimum, rounded up to the hour.
  - The balance bot now uses the same rule, with its own caution on top.
- **The market (`ContractMarket.At(..., baseLevel)`).**
  - Deadlines shorter than the fair deadline are lengthened (`FitDeadline`), never offered
    impossible.
  - A medical call only goes where the aircraft can fly out and back in 3 hours with the margin.
    Otherwise it becomes a charter.
  - In one window a day, a turboprop owner is always offered a medical call.
  - Tasman-capable jets also draw national cities.
- **Featured contracts** appear only for an aircraft you own, or one you could buy now:
  `AirlineOperations.CouldBuyNow` checks tier, reliability, flights, base support and fleet room.
- **Accept guard.** `AcceptContract` refuses work your eligible aircraft can't finish before the
  deadline, with "You can't fly this in time with your aircraft."
  - Aircraft share the flights between them.
  - An aircraft that is away counts from when it should be back.
- **Deadline tie.** `ProcessDue` expires a contract only after nothing else is due at that instant,
  so a flight that parks on the deadline completes it. The outcome still doesn't depend on the step
  size.
- **Challenges.**
  - The reliability challenge is a streak: "Fly 300 flights in a row at 95% reliability"
    (`HighReliabilityStreak`).
  - The career state also keeps the last 20 post-flight reliability readings
    (`FlightsHeldAtOrAbove`). ADR 0139 uses them for held-reliability gates.
- **Save v17.** Adds the day so far, the streak and the reliability readings. Older saves start a
  fresh day with an empty streak. The per-cause delay split of the day isn't saved, so after a
  reload the daily report names no worst cause for the part of the day before the save.

## Verification

`ContractFeasibilityTests` covers:

- more than 10,000 market offers across every aircraft for sale, every tier, base and two
  reliabilities, all fair;
- a daily medical call for every turboprop;
- featured contracts only for aircraft you own or could buy now;
- the accept guard;
- a flight parking exactly on the deadline counts;
- the streak challenge and the reliability history;
- the day so far surviving a save, while a v16 save starts fresh.

Tests changed:

- a long-haul charter may exceed 6 h, up to the fair deadline;
- the lapse test now uses a contract that can be accepted;
- the save-version assertion follows `CurrentVersion`.
