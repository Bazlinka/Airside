# Fixed parked clearance and the BAY-4 apron route

Status: implemented on review branch; native acceptance pending
Date: 2026-10-07
Task: #576; owner Codex / Bazlinka

## Decision

Include parked fixed-wing aircraft in taxi clearance. Retain ADR 0153's three metres of extra planning clearance for moving/queued aircraft; use one metre of extra planning clearance for fixed AtStand poses. Those poses cannot shift when a runway queue changes. This preserves a positive gap rather than accepting touching airframes. Maintenance/holding/awaiting states retain the existing dynamic allowance.

Correct only BAY-4's inbound apron segment with a smooth local offset, at most eight metres north inside the existing 23 m lane. Preserve graph connectors, final stand stop, runway exit and every other bay's route. Existing runway/stand restrictions stay in place.

Preserve the accepted `2026-10-07-arrivals-never-hold-on-final.md`: landing clearance depends on runway/exit safety, not the taxi-in route. No pre-landing taxi check from the earlier sweep is retained. Main #579's accepted `StandingSpotClear` window remains unchanged; this task adds exit-conflict and distant-taxi regression coverage.

## Evidence and reason

The old BAY-4 route passed only about 25.7 m from a parked Q400 at BAY-2. The corrected Q400/Saab routes pass at least 33.1 m away on all four runway directions, with the stop unchanged; nearby BAY-1/BAY-3 routes are checked too. A separate terminal case had a 2.24 m non-overlap gap; applying the moving queue's extra three metres to that immutable pose caused an unnecessary overnight hold. The one-metre static allowance keeps positive clearance and permits the fitted apron traffic. Focused tests reject a 0.5 m static gap, accept 1.5 m, and still reject a queued 2.5 m gap while accepting 3.5 m.

The static route audit and soak/current-main verification are recorded in `docs/testing/five-area-bug-sweep-2026-10-07.md`. These are simulation geometry checks, not a Unity rendering result.

## Affected systems and compatibility

`AdelaideGround`, `GroundTraffic` and focused EditMode tests. Injected time, seeded randomness, exactly-once commands and resource reservations remain unchanged. No persisted field/version or authored asset changes; save v22 and its migrations remain compatible. Route positions are derived geometry. Native taxi appearance, speed/turn smoothness and saved in-progress journeys require Mac acceptance.

## Revert

Revert the BAY-4 local offset and fixed parked allowance/clearance changes together; preserve main's arrival policy and the independent save/rotor/UI fixes. No schema migration is needed.
