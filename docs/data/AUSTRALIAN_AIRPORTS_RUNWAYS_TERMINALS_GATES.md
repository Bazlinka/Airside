# Australian airports: runways, terminals and gates

Covers the 19 Australian airports in `DestinationCatalogue.Australia` (`Domain/Destination.cs`), which includes the
five bases (ADL, MEL, SYD, BNE, PER). Gathered 2026-10-08 from the English Wikipedia article for each airport (CC BY-SA 4.0).
Not yet cross-checked against AIP/ERSA or the airports' own sites, and not yet in `ASSET_AND_DATA_REGISTER.md`.

**Primary runway** is derived: the longest sealed runway. It is not an official preference. Wikipedia gave a runway-use policy
only for Sydney. "n/s" means the source did not state it. Gate counts are the article's wording and several conflict.

| IATA | Airport | State | Runways (length, surface) | Primary | Terminals | Gates / stands |
|---|---|---|---|---|---|---|
| ADL | Adelaide | SA | 05/23 3,100 m asph; 12/30 1,652 m asph | 05/23 | One combined domestic and international terminal (2005) | n/s. Five more aerobridge gates due about June 2028 |
| MEL | Melbourne | VIC | 16/34 3,657 m asph; 09/27 2,286 m asph; third runway (3,000 m) planned | 16/34 | T1 Qantas; T2 international; T3 Virgin; T4 budget | Article total 68 (53 domestic, 15 international), but per-terminal figures conflict. T1 16 aerobridge + 5 non; T2 20; T3 11 aerobridge + 8 non; 5 freighter positions |
| SYD | Sydney | NSW | 16R/34L 3,963 m asph (text says 4,400 m); 07/25 2,530 m; 16L/34R 2,438 m | 16R/34L | T1 international; T2 and T3 domestic | T1 25 aerobridge gates (Pier B 13, Pier C 12); T2 20 bays; T3 14 bays. Remote bays too |
| BNE | Brisbane | QLD | 01R/19L 3,560 m asph; 01L/19R 3,300 m asph | 01R/19L | International; domestic (Qantas and Virgin concourses) | International 14 bays (4 take A380); Qantas 9 aerobridge bays; Virgin 11 bays (9 aerobridge) |
| PER | Perth | WA | 03/21 3,444 m asph; 06/24 2,163 m asph; 03R/21L (3,000 m) planned, due 2029 | 03/21 | T1 international and Virgin pier; T2 regional; T3 and T4 Qantas | T1 7 gates + Virgin pier 8; T2 14 bays; T3 9 gates; T4 9 gates |
| CBR | Canberra | ACT | 17/35 3,283 m asph; 12/30 1,679 m asph | 17/35 | Southern Concourse (Qantas); Western Concourse (Virgin, international via gate 5); GA terminal | n/s |
| HBA | Hobart | TAS | 12/30 2,727 m asph | 12/30 | One combined terminal, being doubled in size | 6 narrow-body bays plus 3 more; gate 6 is an international swing gate |
| ASP | Alice Springs | NT | 12/30 2,438 m asph; 17/35 1,133 m asph | 12/30 | One terminal (1991) | 9 commercial parking positions. Passengers walk |
| OOL | Gold Coast | QLD | 14/32 2,492 m asph (a second figure gives 2,482 m); 17/35 582 m asph | 14/32 | T1 (Eric Robinson Building); southern expansion | n/s. 4 aerobridges and 2 wide-body stands added; up to 19 aircraft at once |
| CNS | Cairns | QLD | 15/33 3,156 m asph; 12/30 closed by 2011 | 15/33 | International terminal (T1); domestic terminal (T2) | International 10 gates (6 jet bridges); domestic 17 gates (5 jet bridges) |
| DRW | Darwin | NT | 11/29 3,354 m asph; 18/36 1,524 m asph | 11/29 | Source contradicts itself: separate international and domestic, or a single terminal | n/s. 2015 expansion added 4 domestic and 2 international gates |
| MQL | Mildura | VIC | 09/27 1,830 m grooved asph; 18/36 1,139 m asph | 09/27 | One terminal (renovated 2012) | n/s |
| KGC | Kingscote | SA | 01/19 1,402 m asph; 06/24 1,134 m gravel; 15/33 1,164 m gravel | 01/19 | One terminal (2018) | n/s |
| PLO | Port Lincoln | SA | 01/19 1,499 m asph; 15/33 1,450 m gravel; 05/23 1,275 m gravel | 01/19 | One terminal (2013) | n/s |
| MGB | Mount Gambier | SA | 18/36 1,644 m asph; 11/29 922 m asph; 06/24 846 m asph | 18/36 | One terminal (2021) | n/s. Heavy-aircraft apron for fire bombers |
| CED | Ceduna | SA | 11/29 1,740 m asph; 17/35 1,014 m gravel (under 5,700 kg only) | 11/29 | One small terminal | n/s |
| WYA | Whyalla | SA | 17/35 1,686 m asph; 05/23 1,500 m asph | 17/35 | One terminal (refurbished 2014) | n/s |
| CPD | Coober Pedy | SA | 04/22 1,428 m asph; 14/32 829 m gravel | 04/22 | n/s | n/s |
| BHQ | Broken Hill | NSW | 05/23 2,515 m asph; 14/32 1,000 m grass | 05/23 | n/s | n/s |

## Runway-use policy found

- **Sydney:** 07/25 is mainly for lighter aircraft. 16L/34R is mainly for domestic and large aircraft up to about B767/A330 size.
  Larger types use it only when no other runway is free. Curfew 11 pm to 6 am and a cap of 80 movements an hour.
- **Cairns:** north-bound departures cross the northern beaches and south-bound cross central Cairns. No stated preference.
- **Ceduna:** prevailing winds and approaches that avoid overflying the town shape operations. No stated preference.
- No usage policy was stated for the other 16.

## Gaps

- No sourced preferred-runway policy for 16 of 19 airports, and no gate counts for 13.
- The seven regional airports with no terminal detail, plus the gate and policy gaps, need a second source (AIP/ERSA and airport sites).
- The existing `docs/data/sa-flight-runways-v01.json` (OurAirports, public domain) covers BHQ, CPD, YCDU, YKSC, YMTG, YPLC and YWHA with threshold coordinates.
