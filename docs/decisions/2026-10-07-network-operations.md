# All-base airline operations

Date: 2026-10-07. Status: implemented; native Unity verification pending. Task #577.

The Fleet improvements made Melbourne aircraft controllable, but Operations still concentrated on Adelaide movements. A growing airline needed a single live overview to see its aircraft without repeatedly switching base filters. Bailey asked to continue implementing the player flows and has authorized merging finished changes without another approval pause.

Operations defaults to **My airline**, a scrolling list of every owned aircraft at every base. Each row gives registration, base/type, route/location, current state, next relevant event and available phase progress. Network services distinguish departure, outbound arrival, destination turnaround departure and return to their own base. Maintenance and repeat exceptions remain visible. Only genuinely dispatchable aircraft count as available, consistently with Fleet's Available filter.

Selecting an owned aircraft opens Fleet with its base and registration selected. Clear conflicting status filters and stale route/cancellation reviews, hide the market, and retain existing supported controls and cameras. Aircraft inspection is reversible and does not submit a booking. The separate **Airport movements** view retains Adelaide arrivals/departures, automatic NOW scrolling, AI traffic, stand decisions and event history. Do not turn the dormant floating flight tiles back on: the overview remains quiet.

Affected systems: OperationsSummary, OperationsWorkspace model/layout/painter, FleetBoard focus and AirsidePrototype workspace dispatch; shared offline HUD exporter. Simulation, economics, assets, cameras and saves are unchanged. No persisted schema change or migration. Network progress is derived from the existing timetable; it neither advances time nor settles services. The existing event history is labelled Adelaide because it does not claim network payment history.

Verification: `docs/testing/network-operations-2026-10-07/README.md`. Painter renders are offline review evidence, not screenshots from Unity. Native input, font metrics and camera transitions still require a Mac run.
