# 0154 — Ground-path point lookups no longer recurse down the path

Date: 28 September 2026. Author: Claude, at Bailey's request ("random stutters — it gets stuck and
starts stuttering").

## Context

The game froze for a moment at irregular times and then carried on. Aircraft positions are drawn
straight from the simulation (ADR 0152), so a long `Update` shows as a visible stall.

The simulation (`AirlineOperations.Update`, run once per simulated second) was timed headlessly over
36 simulated hours at 1 s steps (seed 2026, `StartAtAdelaide`), .NET 8 with a full-optimising JIT
(`DOTNET_TieredCompilation=0`), which is closer to Unity's Mono than tiered .NET. Before this change a
fresh session had **37 ticks over 16 ms** — one or more dropped frames each — with the worst at
**98 ms, 89 ms, 63 ms, 58 ms**. These were spread through the day, not only at start-up. A
CPU trace put **68%** of all simulation time in `GroundTraffic.PathClear` and **63%** in
`GroundPath.PointAtDistance`.

`GroundPath.PointAtDistance` only needs a point. It got it through `SampleAtDistance`, which builds a
full sample including a heading. The heading looks 16 m ahead, and it does that by calling
`PointAtDistance` again. That call built another full sample, which looked a further 16 m ahead, and
so on to the end of the path. One point on a 2 km taxi route was roughly 125 nested samples, all but
the first thrown away. The ground-traffic clearance checks (ADR 0153) ask for thousands of points in
one simulated second, and every frame each taxiing aircraft's drawn pose went through the same chain.

## Decision

`PointAtDistance` works out the position directly: the same binary search and the same interpolation
arithmetic as `Sample`, without the heading. Results are bit-identical, so no timing, clearance or
drawn position changes. `Sample` itself still looks ahead through `PointAtDistance`, which is now a
single step.

## Evidence

Same run, after the change: **4 ticks over 16 ms** (was 37), worst **61 ms** (was 98); total
simulation time for the 36 hours down from 3.4 s to 2.1 s. After the first simulated hour no tick
exceeded 8 ms, against 20–75 ms spikes throughout the day before. The ticks still over 16 ms are
one-off, cached route builds — the first time a type uses a particular runway exit, for example — and
do not repeat.

`GroundMotionTests.PointAtDistance_IsTheSamplesPositionWithoutRecursingDownThePath` checks the
positions are exactly the sample's, and that 20 000 lookups finish well under 250 ms; it fails on the
old code (≈ 870 ms) and passes on the new. Domain suite 934/934.

## Not covered

Render cost is separate. (An earlier version of this line called a ≈ 29 fps render problem still
open; that figure predates the 2026-09-26 glazing fix, which measured 60 fps — see ADR 0155.) This
fixes CPU stalls in the simulation and ground-pose code,
not GPU cost. A Unity run and a human playtest on the Mac are still needed to confirm the felt
difference. The one-off first-use route builds could be pre-warmed during loading if they are still
noticeable.
