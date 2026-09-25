# 0120 — Self-led long career and network growth

Date: 25 September 2026. Bailey approved the full career plan: 100–150 active hours, selectable goals, earned delegation, a 15–25 aircraft airline, recoverable setbacks, outstation bases, and a clear ending followed by sandbox play.

## Decision

Airside starts with one Saab 340 at Adelaide. The Provisional, Regional, Domestic and International tiers remain capability gates, but their proof comes from contracts, served destinations, fleet, bases, service count and reliability. Goals within the current tier may be pursued in any order and one can be pinned. The old five-chapter path and daily service bonus are no longer the active progression or HUD objective. Already earned tiers in older saves remain earned.

The final established-airline milestone requires at least 18 aircraft, three bases including Adelaide, 12 served destinations including three international destinations, 90% reliability, and a positive combined operating margin over the latest 30 completed services. Its completion is recorded once; later setbacks do not remove the ending. The player can continue in sandbox.

Adelaide retains its fully simulated runway, apron and six-aircraft player base limit. Up to three outstation bases can hold eight aircraft each. Outstation aircraft fly deterministic timed services between non-Adelaide cities and do not create virtual stands or renderer objects at Adelaide. Every playable Adelaide leg still uses the live Adelaide fleet and its existing reservation rules. The total airline ceiling is 25 aircraft.

Route forecasts use authored demand per destination and the type's actual seat count. Dispatch cost, expected occupied seats, return and margin are shown before planning. Settlement uses the same forecast formula; contracts and reliability still modify payment through the existing settlement path. The 30-service operating margin includes per-service contract pay but excludes one-off completion rewards. Thin routes can lose money with oversized aircraft. No detailed fares, wages, loans or passenger-by-passenger model is introduced.

Repeat schedules unlock after 12 manually planned services. They book one new service at a time only while the game is open. On restore, already committed one-off services can finish during away catch-up, then the repeat planner resumes; no repeat plan generates extra offline flights or charges. A refused automated booking pauses the plan and displays its reason.

## Persistence and migration

Save v13 records the chosen goal, destination proof, outstation bases and aircraft, repeat plans, manual-service count, recent service margins and active-play timing. Versions 1–12 retain funds, fleet, commitments and tiers. Old completed authored and market contract IDs credit only destinations they prove. Unknown old flight destinations and margins are not invented; migration pays no retroactive rewards.

## Pacing and verification

The intended active-play checkpoints are Provisional 0–8 hours, Regional 8–35, Domestic 35–70, International 70–110, and established airline 110–150. These are tuning targets, not time gates. Foreground play time and tier attainment are recorded for playtesting. Automated simulation checks dead ends and settlement correctness; human sessions are needed to validate actual duration and engagement. The known graphics-on performance defect remains a separate release blocker for large-fleet playtests.
