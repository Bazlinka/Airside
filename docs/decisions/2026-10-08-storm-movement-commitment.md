# Storm holds at stands and committed movements continue

Status: Accepted — Bailey's explicit 8 October storm movement instruction
Date: 2026-10-08
Supersedes: ADR 0058 and the departure/inbound gate policy in ADR 0190

## Decision

Hold ready fixed-wing departures at their current gate or regional bay before taxi
release while authored operational weather is Storm. Record weather delay and explain
it as a stand wait. Next-event catch-up revisits at the next weather block, rather
than taxiing the aircraft out and stopping it at the runway for the rest of the storm.

A fixed-wing aircraft already released to taxi, including one restored holding short,
is committed to depart. Tower priority, runway occupancy, wake separation, crossings,
vacate safety and curfew still control its clearance. Storms do not add a new hold
at the runway. Arrival estimates include these departures during storms too.

An inbound that entered the displayed extended final before storm onset is likewise
committed. The old implementation only counted HoldingForLanding as on final,
although presentation also draws Inbound aircraft along the extended approach.
When that inbound timer expired in a storm, it was extended to the next weather
block; presentation then removed it from final or returned a watched journey to an
earlier route pose. Use the same 32 km entry distance in Simulation and Presentation,
and derive entry time from the existing inbound deadline and reference approach speed.
If entry weather was not Storm and entry has occurred, keep the queue estimate and
inbound transition through the storm. Do not remeter that committed inbound by
extending its timer; normal runway checks and missed-approach rules still apply.
An aircraft that has not entered final before the storm remains weather-held.

## Scope and migration

Simulation departure release, arrival commitment, landing estimates, next-event
selection and hold explanation; shared extended-final distance. No save fields,
version change or migration, no aircraft-camera or cloud/fog rendering changes.
Rotorcraft restrictions remain separate. Deterministic authored operational weather
remains the source for operational holds, as before.

## Evidence and limits

Focused regressions cover gate weather hold/release and weather delay attribution,
step-versus-skip agreement, taxi commitment, final arrivals and a still-Inbound
arrival crossing the storm boundary. Existing runway/hold/arrival-estimate checks
are run boundedly. Native cockpit-to-tower appearance remains unverified.
CockpitInterior.Leave restores forceRenderingOff; no fault found in that exit path.
The preceding night-light shader fix (#668/#669) addresses separate radiance/fog issues.
This change does not establish a native Unity or rendered storm landing pass.

Validation: headless compile and 48 focused RunwayWeather/HoldReason/ArrivalApproach/
OperationsWorkspace checks pass (2 s test execution); diff whitespace clean. A larger
selection that included multi-day ArrivalClearance checks was stopped at the 60 s
bound without a result; those checks remain unverified. No full suite or player
build/rendered gameplay review ran.
