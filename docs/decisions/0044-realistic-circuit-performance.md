# 0044 — Circuit flown to ATR 42 performance

Date: 2026-09-13. Requested by Bailey: "movement in the air — realistic
adjustments — flaring for landing — live speeds — accurate for surroundings and
realistic for that plane."

## The problem

Phase durations were picked by hand and the speeds fell out of them. Nothing
checked the result, so the circuit was flying figures no ATR 42 has ever seen:

| | before | ATR 42 |
|---|---|---|
| speed at rotate | **179 kt** | 100 kt |
| speed leaving the takeoff phase | **257 kt** | ~120 kt |
| climb-out | **208 kt** | ~170 kt |
| last 600 m of final | **1.53° slope** | 3° |
| "flare" | **600 m of straight descent** | ~300 m round-out |

The approach *start* was already an exact 3.01° slope and the 1 050 m rollout
was right, so the geometry was sound — it was the timing that was wrong. There
was also no flare at all: the last 600 m was a shallower straight line into the
tarmac, which is why the landing never read as one.

## Decision

Invert the dependency. `CircuitProfile` (new, in `Simulation`, deliberately
UnityEngine-free) holds the stations and the reference speeds, and **derives**
every phase duration from `distance / mean speed`. `AirportCircuit` now reads
its durations from it, and `AirsideFlightPath` only turns them into positions —
each segment flown at constant acceleration between its entry and exit speed, so
the instantaneous speed at any progress is the scheduled one.

A speed can now only be wrong if the figure it came from is wrong, and the
figures are asserted directly by `CircuitProfileTests`.

Reference speeds (typical ATR 42-600 at a normal operating weight, not a
performance manual): Vr 100 kt, V2 105, Vapp/Vref 110, touchdown 95, initial
climb 120, climb-out 170.

Derived durations: approach 40 s, landing 54 s, takeoff 52 s, fly-out 32 s —
a 182 s circuit, against 162 s before. The takeoff phase nearly doubles,
because a 900 m roll to 100 kt genuinely takes 35 seconds.

## The flare

The round-out begins at 30 ft, which on the 3° slope is exactly as the threshold
passes underneath, and floats 300 m onto the touchdown-zone markings. Height
through it is a cubic Hermite that leaves the glideslope at precisely the
descent rate it was flying (584 ft/min) and arrives at 60 ft/min. Both height
and sink fall monotonically, so the aircraft never sinks faster mid-flare than
it did on the slope.

That monotonicity is the constraint that killed the first attempt. A flare over
the 172 m the glideslope gives you between 30 ft and the aim point is
geometrically a 4° descent — steeper than the approach — so the sink has to
*increase* before it can be arrested. The float is what makes a flare possible
at all, and `FlareIsSlowEnoughToArrestTheSinkMonotonically` locks it.

## The trade-off worth knowing about

Holding the wheels on the painted 300 m markings with a 300 m float puts the
threshold crossing at about 30 ft rather than the textbook 50. Crossing at 50 ft
instead would need the float cut to ~180 m, and the sink could then only be
arrested to about 430 ft/min — a firm arrival that would read as a thump.
Landing on the paint with a soft touchdown is the better-looking half of that
trade, and `ThresholdCrossingIsLowButDeliberate` pins it so it cannot drift into
being an accident.

## Affected systems

New `Simulation/CircuitProfile.cs`. `AirportCircuit` durations become derived.
`AirsideFlightPath` rewritten against it, gaining `AirspeedKnots` as the single
source of truth for speed (tyre spin now reads it instead of differentiating the
position curve) and `FlareHeight` for the round-out. Body attitudes retuned:
rotate to 9° and climb 7.5° rather than 12° and 10°, and the flare now raises
the nose progressively to 6.5° instead of snapping.

Nothing about the airfield or the aircraft model changes. The HUD gains one
thing: a live airspeed readout above the control bar, reading the same visual
progress that places the aircraft so the number always agrees with what is on
screen rather than with the simulation a fraction of a second behind it.

The speed schedule itself lives in `CircuitProfile.AirspeedKnots`, not in the
flight path, specifically so it is covered by the headless harness. The speeds
are the whole point of this model, and the one figure the player can actually
read should not be the one part nothing can test.

## Acceptance and evidence

`scripts/test-domain.sh` **140 passed, 0 failed**, including 20 new
`CircuitProfileTests` asserting the glideslope angle, the 584 ft/min descent
rate, the flare geometry and monotonic arrest, touchdown on the 300 m markings,
that each duration equals distance over mean speed, that whole-second rounding
distorts no phase by more than half a second, that the takeoff roll reaches Vr
rather than twice it, and that the airspeed schedule hits every reference figure,
stays continuous across the phase seams, and never runs backwards where it
should not.

`HudLayout`'s readout placement was checked against the `Rect`/`Mathf` shim from
320×240 to 3456×2168: it stays inside the viewport, never collapses and never
overlaps the control bar. Those cases are committed into `PresentationLayoutTests`.

`AirsideFlightPath` needs UnityEngine and cannot run headlessly, so its curves
were replicated numerically and differentiated: every sampled speed lands within
**2.3 kt** of schedule (the worst case being the fly-out, where rounding 31.5 s
to 32 costs 1.4%), all three phase seams are continuous to **0.0000 m**, and the
flare sink falls 581 → 0 ft/min monotonically.

**Still required:** Unity Play. The numbers are right; whether the landing
*looks* right — the round-out, the float, the derotation, and a takeoff roll
that now takes 35 seconds instead of 19 — is a judgement only a Mac session can
make. A 182 s circuit is also 12% longer than before, which is worth feeling at
1x before it is accepted.
