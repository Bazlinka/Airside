# Bug hunt 2026-10-09: fix records (findings 12, 8, 13)

Per-finding status for the flight/arrival/return findings in [README.md](README.md), against current main (source unchanged in these
areas since the tested revision `aec64616`). Native runs below were played back from a clean stamped Mac build of the fix commit, at
the 40x QA clock, in isolated soak saves. Acceleration does not establish normal-speed behaviour or performance.

## Finding 12: Bell 412 never reaches the geographical destination (#727)

Reproduced on current main by source and by a headless probe: `HelicopterTrack.SiteWorld` projected the destination with
`SkyTraffic.ProjectLocal` (near field 1:1, then compressed into a 26 km draw radius), while the flight world, tracker and map use the
geographical `YpadFrame`. The drawn end of a Kingscote leg was 85.6 km short of the real Kingscote, matching the report's 86 km "still to go".

Fix: the site is `YpadFrame.ToWorld(lat, lon)`, the frame the pad spots, regional runways and flight world already share. While turning
round at a site beyond the airfield (more than 12 km from the pad, `HelicopterTrack.RemoteSiteMetres`) the helicopter holds over the real
place and does a pedal turn before the return leg, so the floating-origin flight world keeps a geographical pose. Near hospitals (RAH, FMC)
stay out of sight on the ground as before. Pure function of fleet state and the injected clock; no save or schema change.

Native run (`finding-12/plan.json`, flags `-airsideSoakAddType B412 -airsideSoakAddTo KGC -airsideReviewCockpit -airsideReviewJourney KGC
-airsideReviewJourneyRate 40`, build `e06806e2`): 23 steps passed, zero runtime errors during the scenario. The Bell crossed the strait
(`after-obs-04`: 84% of leg, 20 km from Kingscote, 35.59 S 137.68 E, Kangaroo Island on the horizon), and `after-obs-05/06` show it over the
Kingscote runway at 35.71 S 137.52 E, "At Kingscote", turning from 227 to 047 degrees for the return. `after-obs-12`: back near Adelaide
(34.96 S 138.51 E), origin recentred, 2 km out on the approach. The Bell then stayed Inbound to the end of the run (12,700 simulated
seconds) without landing; the original bug-hunt run shows the same Inbound hold (VH-TS1 Inbound from 4,200 s to past 19,000 s), so it
is not caused by this change, but its cause (arrival gate: weather, wind or pad) was not investigated.

Not verified: the Bell hovers 35 m over Kingscote for the turnaround rather than landing (a settle onto the runway would need the site's
elevation in the helicopter pose); normal-speed (1x) behaviour; a completed return landing.

## Finding 8: turnaround stays on the runway endpoint at Kingscote (#742)

Reproduced on current main: `JourneyWorld` held an AtDestination aircraft at `RegionalFlightPath.Landing(..., remaining 0)` for the whole
stay. Native 40x journey (Saab VH-PAX, `-airsideReviewJourney KGC`, build `e06806e2` = main + #741): every journey-trace sample of the 40-minute
turnaround is the runway end (-125005.9, -6576.2) (`finding-8/world-positions.txt`, `before-0116.4-AtDestination.jpg`). The same stop also
differed from the point the return departure rolls from (typed vs plain landing-roll end), so the aircraft jumped hundreds of metres at departure.

Fix (presentation only): `RegionalTurnaround` is a pure function of time into the stay. The aircraft taxis from the end of the landing roll to
its place on the mapped apron (`RegionalApron`, from the same offline airport map the flight world draws), parks there nose-in toward the
terminal, and taxis back to the exact point the departure roll starts from. Taxi paths are Dubins curves (22 m minimum turn radius, eased,
about 10 kt average), the first and last 30% of the stay at most, driving the existing TaxiIn/AtStand/TaxiOut visual phases. Each aircraft has
a stable-hash slot along the apron's long axis so aircraft turning round together do not stack (the first native pass showed a REX Saab
exactly overlapping the player's). Airports with no mapped apron, or a stay under two minutes, keep the old behaviour. Simulation timing,
reservations and saves are unchanged.

Native run on the fix (`08e8f30c`, same scenario): the Saab leaves the runway end, parks 1,020 m away at (-124440.2, -5723.6) on the apron facing
the terminal (HDG 008 T, `after-0112.8`), then rejoins the runway end for departure (`after-0182.4`: Inbound begins on the runway centreline,
HDG 022); the round trip completed through landing and overview with no runtime errors. Headless: `RegionalTurnaroundTests` (endpoints equal
the stop and departure start within 1 cm and 0.1 degrees, taxi speed under 12 m/s, nose rate under 25 deg/s, 200 random poses reached, short
stays, slot spreading).

Not verified: no frame was captured mid-taxi (the journey review samples every 15 real seconds, a taxi leg lasts about 5 at 40x); the
taxi paths ignore taxiways and can cross grass between runway and apron; no 1x run; other airports' aprons were not viewed natively (the
same loader and geometry are used; headless checks only).
