# ADR 0230 — Require freighters for freight contracts

Date: 6 October 2026. Status: merged in PR #526 under Bailey’s explicit approval. Native Unity verification remains outstanding.

A contract's existing `Kind == Freight` now implies a freighter refit. The pure
`MatchesAircraft(type, isFreighter)` predicate is shared by acceptance, completion,
deadline capacity, cancellation penalties and HUD eligibility. An aircraft of the
correct type carrying passengers cannot satisfy cargo work. Locked offers and
active terms explain the refit requirement. Ordinary contract behaviour stays
unchanged. Passenger-only outstation services do not satisfy freight work.

No persisted field or schema version changes. Existing freight contracts retain
progress/pay already earned and their deadlines, but future flights must use a
refitted freighter. Old saves that predate contract kinds retain their existing
scheduled-contract migration. Existing refit fees and exactly-once payment remain.

Scope: Domain contract definition, AirlineOperations career/settlement/cancellation
and Contracts workspace. Test evidence is retained alongside the interface refresh
in `docs/testing/interface-refresh-2026-10-06/`. AI freight, cargo stands and an
outstation cargo role are follow-up work.
