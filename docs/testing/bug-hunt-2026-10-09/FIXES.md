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
