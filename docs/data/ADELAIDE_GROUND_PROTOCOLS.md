# Adelaide airport ground protocols — checked 6 October 2026

This compares published facts with the game. Facts below are attributed; no chart graphics or publication assets are copied into the game.

## Primary sources

- [Airservices current ERSA YPAD, effective 3 September 2026](https://www.airservicesaustralia.com/aip/current/ersa/FAC_YPAD_03SEP2026.pdf), Local Traffic Regulations §1; verified against the [current issue index](https://www.airservicesaustralia.com/aip/aip.asp?pg=40&vdate=03SEP2026&ver=1).
- [Adelaide Airside Vehicle Control Handbook, revision 10, 14 April 2026](https://corporate.adelaideairport.com.au/media/5zvdmnsu/adelaide-airside-vehicle-control-handbook.pdf).
- [CASR, compilation 30 June 2026](https://www.legislation.gov.au/F1998B00220/2026-06-30/2026-06-30/text/original/epub/OEBPS/document_2/document_2.html), 91.365/91.405: collision avoidance, right-of-way and ATC manoeuvring authority.
- [MOS Part 172, compilation inspected 29 April 2025](https://www.legislation.gov.au/F2006B00462/2025-04-29/2025-04-29/text/original/epub/OEBPS/document_1/document_1.html), §10.12: wake minima and measurement. The [Federal Register latest-version page](https://www.legislation.gov.au/F2006B00462/latest) was checked on 6 October 2026 and lists F2025C00447 C12, 29 April 2025, as the latest version.
- [Airservices runway safety](https://www.airservicesaustralia.com/industry-info/pilot-tools/pilot-and-airside-safety/runway-safety/) and [working with ATC](https://www.airservicesaustralia.com/industry-info/pilot-tools/pilot-and-airside-safety/working-with-atc/).
- [ATSB AO-2016-117](https://www.atsb.gov.au/investigations/ao-2016-117): apron clearance does not replace crew/ground-handler collision responsibility.
- [Adelaide curfew overview](https://www.infrastructure.gov.au/infrastructure-transport-vehicles/aviation/aviation-safety/aircraft-noise/airport-curfews/adelaide/overview).

## Local restrictions and game comparison

| Protocol | Before this change | Implemented / limit |
| --- | --- | --- |
| Waiting does not remove a stationary aircraft | Stationary holders could be ignored after 180 s; arrivals excluded them | Clearance and explanatory hold checks retain them permanently; manual stand choices reserve a stand and await the same clearance |
| Crossing needs ATC authorisation; clear the full aircraft | Predicted centre-point crossing windows | Crossing reservation includes full length/half-span + 3 m; release holds remain at stand/exit |
| 05/23 intersects 12/30 | Both could be cleared simultaneously | Shared occupancy guard; separate queues; conservative cross-flight wake |
| Wake depends on MTOW, lead/follow category and operation | Leader's wingspan selected 90/120/180 s, all followers alike | MOS pair minima, airborne/touchdown anchors, saved prior movement; 90 s physical scheduling buffer retained as game choice |
| T1 Code C push east only | Direction changed with taxi destination | Common eastbound push for 05 and 23, then route to assigned runway |
| Pushed Code C and below: bays 15–27 no B1; 22–27 no B1/L1 | Unrestricted shortest path | Filtered departure graph; combined map L treated as L1 |
| D1/E1/E2/H/F1 maximum C; A2 northeast of G1 maximum C | Shared E2 arrival exit, including widebodies | Restricted named edges; entire A2 excluded above C conservatively; compatible widebody exits |
| F4 maximum D; above D only A3–A6/B1/B2/F2–F6 except F4/T1–T3/K/L1/L2 | No route-specific airframe filter | Filtered named edges; unlabelled apron/map connections still need future label audit |
| R maximum 18 m span | No explicit check | Policy rejects all current fixed-wing types on R |
| 05 first exit E2; 23 first D1 unless ATC advises otherwise | E2 for both | C uses E2, with 23 treated as ATC variation; D1 early-rollout default remains future work. Above C uses compatible later exit |
| 12/30 maximum C takeoff, D landing | All terminal jets assigned main runway | Existing conservative assignment retained |
| Above-C 180-degree runway turns restricted; specific widebodies prohibited at 23 threshold | No explicit local rule | Compatible forward exits do not backtrack; this does not add a general runway-turn engine |
| Vehicle speeds: 25 apron/movement, 15 terminal road, 10 within 15 m aircraft, 60 perimeter unless signed | Shared service helper active 27 km/h, return 19.8 | Shared helper caps apron/terminal/aircraft zone; specialised bus/tug/frontage systems remain separate |
| Aircraft taxi speed | Type profiles 20–24 kt straight, 8–15 apron, 5 final lead-in; curves brake | Retained representative operating profiles: these are not statutory speed limits |
| Curfew normally 23:00–06:00, statutory exceptions | Intentional game 23:00–05:00; player/RFDS exemptions, ADR 0110 | Retained intentional design; not claimed to reproduce actual legislation |

Delivery 126.1, Ground 121.7, Tower 120.5. Report parked position/gate when acknowledging airways clearance. Engine starting above idle power requires ATC clearance. These remain reference facts; this patch does not add a simulated radio interface.

## Generic timing and right-of-way

A fixed number of seconds does not authorise movement. Wait until ATC grants the required manoeuvre/crossing clearance and the route/strip is physically clear. Aircraft/tows give way to aircraft landing/on final or taking off/preparing to take off. An overtaker keeps clear; converging traffic gives way to the aircraft on its right; a head-on situation requires stopping or turning right. Vehicles give way to aircraft including aircraft under tow; returning tugs/ground crew have priority on the rear-of-bay road. Avoid collisions even when holding a clearance.

The game uses central sequencing and pre-reserved routes to prevent conflicts. It does not model uncontrolled taxi encounters, spoken readbacks, emergency tug recovery, sign-dependent perimeter driving or low-visibility vehicle permits. No timeout is a substitute for clearance.

Aircraft timing assumptions remain curve/braking-derived: type-specific straight/apron limits, 5 kt final stand approach, 3 kt pushback, 25 s disconnect pause. A 30–60 s detach/clear interval and 2–5 s simulated communication response would be plausible **gameplay choices**, not Adelaide rules. No invented universal taxi speed or universal give-way wait is added.

## Wake time minima (MOS 172 §10.12)

Values are seconds. Full-length departure interval is airborne-to-airborne; arrival interval is touchdown-to-touchdown. Physical runway occupancy is an additional requirement.

| Leading → following | Departure | Arrival |
| --- | ---: | ---: |
| SUPER → HEAVY | 120 | 180 |
| SUPER → MEDIUM | 180 | 180 |
| SUPER → LIGHT | 180 | 240 |
| HEAVY → MEDIUM | 120 | 120 |
| HEAVY → LIGHT | 120 | 180 |
| MEDIUM fixed-wing >=25 tonnes / MEDIUM helicopter → LIGHT | 120 | 180 |

Other listed category pairs have no mandatory *time wake* minimum under this table, which does not remove physical/distance separation. Intermediate departures (>150 m beyond the leader's commencement point) require 240 s behind SUPER and 180 s for the applicable HEAVY/MEDIUM pairs. The game currently uses full-length lineups; the longer intermediate policy is available but not silently applied to full-length movements. Same-runway landing behind a departing aircraft uses §10.12.3.3(b)'s exception. Intersecting runways/flight paths are treated conservatively as needing applicable cross-wake minima.

The catalogue requires an explicit published weight band for each type. Current manufacturer/type weight bands: Bell 412EP is LIGHT (<7 t); ATR42/SF34 are MEDIUM below 25 t; DH8D/E190/A223/A320/B738/B38M/A21N are MEDIUM >=25 t and below 136 t; A359/A339/B789/B78X are HEAVY (>=136 t). Actual weights vary by variant/option, but do not cross these relevant bands. Manufacturer sources are catalogued per type in `AIRCRAFT_SPECIFICATIONS.md` (ATR factsheet, Saab specifications, De Havilland brochure, Airbus/Boeing airport-planning documents, Embraer and Bell documentation). Wingspan remains the correct input to taxiway/stand **code letter**, not wake category. Newly added types need explicit weight-band review; unknown IDs currently use the heavy fallback.

## Evidence boundaries

Headless tests validate deterministic policy, route connectivity/joins, wake pairings and save round trips. They do not establish native Unity compilation, actual clearance at painted intermediate holding positions, airframe swept-envelope collision certification or visual route quality. The existing collision proxy remains radial; dynamic node-by-node holds and deadlock recovery require a separate movement-state design. No full journey reproduction or Unity/player run was performed for this work.
