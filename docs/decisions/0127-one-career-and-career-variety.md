# 0127 — One career balance, and more to do in it

Date: 27 September 2026. Author: Claude, at Bailey's request: "improve career mode — forget about
difficulties".

## Decision — difficulty is removed

The Relaxed / Standard / Demanding choice from ADR 0123 is gone. The setup wizard is three cards:
identity, livery and briefing. Every airline plays the one balance tuned in ADR 0125 (Standard).
The title screen and the Flight Manual no longer mention difficulty.

**Saves:**
- v14 still writes the field, so older builds can read newer saves.
- Loading ignores the field: a game founded on Relaxed or Demanding continues on Standard.
- An unknown value no longer fails the load.

The `CareerDifficulty` plumbing stays internal and fixed at Standard, so the economy code paths are
unchanged. The balance simulator drops the difficulty axis.

## Consequences

- One balance to tune and to reason about.
- A player who founded on Relaxed earns a little less per flight after updating; one on Demanding,
  a little more. Their progress is kept.
