# Aircraft specifications and sources

Single reference for the figures in `Domain/AircraftCatalogue.cs` (ADR 0048). Dimensions and
manufacturer performance come only from the manufacturer or type-certificate sources below,
retrieved 2026-09-15–16. **Planning cruise** and **practical range** are gameplay planning figures:
they are kept at or below the manufacturer figure where one is recorded, and are labelled as
assumptions where none is.

| Source id | Type | Length | Wingspan | Height | Manufacturer cruise | Manufacturer range | Planning cruise / practical range | Source |
|---|---|---|---|---|---|---|---|---|
| SPEC-ATR42-600 | ATR 42-600 | 22.67 m | 24.57 m | 7.59 m | 300 KTAS / 556 km/h max cruise (95% MTOW, ISA, optimum FL) | 703 NM (1,302 km) with max passengers | 556 km/h / 1,100 km | ATR, *ATR 42-600 factsheet*, pp. 10–11 — https://www.atr-aircraft.com/wp-content/uploads/2020/07/Factsheets_-_ATR_42-600.pdf |
| SPEC-SAAB-340B | Saab 340B | 19.73 m | 21.44 m (22.75 m extended wingtips) | 6.97 m | 283 kt / 524 km/h max cruise | Not published on the cited page | 500 km/h / 1,000 km — **range is a planning assumption** | Saab, *Saab 340B* product page — https://www.saab.com/products/saab-340 ; dimensions corroborated by EASA TCDS EASA.A.068 (Saab SF340A/340B), issue 26 — https://www.easa.europa.eu/sites/default/files/dfu/TCDS_EASA_A_068__Saab_SF340A_340B_Iss26.pdf |
| SPEC-DASH8-400 | De Havilland Canada Dash 8-400 | 32.83 m | 28.42 m | 8.34 m | 360 kt / 667 km/h max cruise | 1,596 km (862 nm) full-passenger range, 102 kg per passenger | 667 km/h / 1,500 km (was 1,800 km, above the manufacturer figure) | De Havilland Canada, *Dash 8-400 spec sheet* v11, July 2026 — https://dehavilland.com/wp-content/uploads/2026/07/DHC_Dash8_Spec-Sheet_v11_Digital.pdf |
| SPEC-EMBRAER-E190 | Embraer E190 | 36.24 m | 28.72 m | 10.55 m | Not recorded here | Variant-dependent | 829 km/h / 3,500 km — **planning assumptions** | Embraer, *E190 Airport Planning Manual* APM 1901 Rev 20 — https://www.embraercommercialaviation.com/download/51/apm/8981/apm_e190.pdf |
| SPEC-AIRBUS-A220-300 | Airbus A220-300 | 38.70 m | 35.10 m | 11.50 m | M0.82 / ≈871 km/h | 3,400 nm / 6,297 km | 829 km/h / 5,500 km — **planning assumptions** | Airbus, *A220-300* — https://www.aircraft.airbus.com/en/aircraft/a220/a220-300 |
| SPEC-AIRBUS-A320-200 | Airbus A320-200 | 37.57 m | 35.80 m | 11.76 m | M0.82 / ≈871 km/h | Representative family figure 6,200 km | 830 km/h / 5,000 km — **planning assumptions** | Airbus, *A320 Family* and aircraft characteristics — https://www.aircraft.airbus.com/en/aircraft/a320-family ; https://www.aircraft.airbus.com/en/customer-care/fleet-wide-care/airport-operations-and-aircraft-characteristics/aircraft-characteristics |
| SPEC-BOEING-737-800 | Boeing 737-800 | 39.47 m | 35.80 m | 12.50 m | Not published on the cited page | Up to 2,800 nmi / 5,190 km | 839 km/h / 4,800 km — **planning cruise assumption** | Boeing, *737 Next Generation* and D6-58325-7 Rev C — https://www.boeing.com/commercial/737ng ; https://www.boeing.com/content/dam/boeing/v2/airports/acaps/737NG_REV_C.pdf |
| SPEC-BOEING-737-8 | Boeing 737-8 | 39.5 m (129 ft 6 in) | 35.9 m (117 ft 10 in) | 12.3 m (40 ft 4 in) | Not published on the cited page | Up to 3,500 nmi (6,480 km) | 839 km/h (≈ Mach 0.79) / 5,200 km — **cruise is a planning assumption** | Boeing, *737 MAX* specifications — https://www.boeing.com/commercial/737max ; tail height range 11.86–12.45 m in Boeing D6-38A004 *737 MAX Airplane Characteristics for Airport Planning*, Rev K (July 2025), §2.3.2 — https://www.boeing.com/content/dam/boeing/v2/airports/acaps/737MAX_RevK.pdf |
| SPEC-AIRBUS-A321NEO | Airbus A321neo | 44.51 m | 35.80 m | 11.76 m | M0.82 / ≈871 km/h at cruise altitude | 7,400 km (4,000 nm) | 833 km/h (≈M0.78) / 6,000 km — **planning assumptions** | Airbus, *A321neo* product page and airport-planning data — https://www.aircraft.airbus.com/en/aircraft/a320-family/a321neo ; https://www.aircraft.airbus.com/sites/g/files/jlcbta126/files/2025-07/AC_A321_20250715.pdf |
| SPEC-AIRBUS-A350-900 | Airbus A350-900 | 66.80 m | 64.75 m | 17.05 m | M0.85 / ≈903 km/h | 15,750 km | 903 km/h / 15,000 km | Airbus, *A350-900 key figures* — https://www.aircraft.airbus.com/en/aircraft/a350/a350-900 |
| SPEC-AIRBUS-A330-900 | Airbus A330-900neo | 63.66 m | 64.00 m | 16.79 m | M0.82 / ≈871 km/h | 7,200 nm / 13,334 km | 871 km/h / 12,000 km | Airbus, *A330 Family facts and figures*, April 2026 — https://mediaassets.airbus.com/pm_38_896_896589-6vwqkjd9gw.pdf?fileName=airbus-a330-family-facts-and-figures-april-2026.pdf |
| SPEC-BOEING-787-9 | Boeing 787-9 | 62.81 m | 60.12 m | 17.02 m | M0.85 long-range cruise condition; no maximum published on the cited product page | Up to 8,300 nmi / 15,370 km | 903 km/h / 15,000 km — **planning cruise assumption** | Boeing, *787 Dreamliner* and D6-58333 Rev Q — https://www.boeing.com/commercial/787/ ; https://www.boeing.com/content/dam/boeing/boeingdotcom/commercial/airports/acaps/787_ACAP_Rev_Q.pdf |
| SPEC-BOEING-787-10 | Boeing 787-10 | 68.30 m | 60.12 m | 17.02 m | M0.85 long-range cruise condition; no maximum published on the cited product page | Up to 7,500 nmi / 13,890 km | 903 km/h / 12,000 km — **planning cruise assumption** | Boeing, *787 Dreamliner* technical specifications and D6-58333 *787 Airplane Characteristics for Airport Planning*, Rev Q (October 2025) — https://www.boeing.com/commercial/787/ ; https://www.boeing.com/content/dam/boeing/boeingdotcom/commercial/airports/acaps/787_ACAP_Rev_Q.pdf |

## Flight behaviour (simulation)

`AircraftPerformance` uses type-specific normal-operating profiles. These are not dispatch
V-speeds: crews calculate Vr, V2 and Vref for the actual mass, flap, pressure, temperature,
wind and runway condition. Manufacturer-published anchors are used where available; the
remaining values are conservative representative planning values for believable motion.

| Type | Approach / touchdown | Rotate / initial climb / climb-out | Typical takeoff roll | Cruise ceiling used |
|---|---:|---:|---:|---:|
| ATR 42-600 | 110 / 95 kt | 104 / 120 / 160 kt | 900 m | FL250 |
| Saab 340B | 108 / 94 kt | 105 / 120 / 155 kt | 980 m | FL250 |
| Dash 8-400 | 125 / 110 kt | 116 / 135 / 185 kt | 1,150 m | FL250 |
| Embraer E190 | 134 / 123 kt | 140 / 160 / 205 kt | 1,450 m | FL410 |
| Airbus A220-300 | 136 / 125 kt | 142 / 162 / 208 kt | 1,500 m | FL410 |
| Airbus A320-200 | 139 / 127 kt | 144 / 164 / 209 kt | 1,600 m | FL398 |
| Boeing 737-800 | 143 / 131 kt | 145 / 165 / 210 kt | 1,700 m | FL410 |
| Boeing 737-8 | 145 / 132 kt | 145 / 165 / 210 kt | 1,650 m | FL410 |
| Airbus A321neo | 140 / 128 kt | 145 / 165 / 210 kt | 1,800 m | FL398 |
| Airbus A350-900 | 150 / 138 kt | 158 / 180 / 225 kt | 2,050 m | FL430 |
| Airbus A330-900neo | 148 / 137 kt | 157 / 179 / 224 kt | 2,100 m | FL415 |
| Boeing 787-9 | 149 / 138 kt | 158 / 180 / 225 kt | 2,150 m | FL430 |
| Boeing 787-10 | 151 / 139 kt | 159 / 181 / 226 kt | 2,200 m | FL430 |

ATR anchors: V2 minimum 112 KCAS, Vref 104 KIAS, optimum climb 160 KCAS and 1,107 m
takeoff distance at the published reference condition (ATR factsheet above). Dash 8 anchors:
1,277 m MTOW takeoff field length, 360 kt maximum cruise and 25,000 ft maximum operating
altitude (De Havilland spec sheet above). Boeing runway performance remains weight- and
condition-dependent; the ACAP is the authoritative airport-planning source, not a fixed V-speed table.

## Adelaide international traffic

### Scheduled-passenger type audit — 21 September 2026

The existing catalogue already covered ATR 42, Saab 340, Dash 8-400, 737-8,
A321neo, A350-900 and 787-10. Current Adelaide Airport releases exposed six recurring
passenger gaps, now AIR-011 through AIR-016:

- Jetstar based A320ceo and Indonesia AirAsia A320-200: Airbus A320-200.
- Qantas Adelaide–Auckland: Boeing 737-800.
- QantasLink Brisbane transition: Embraer E190 and Airbus A220-300.
- Malaysia Airlines Kuala Lumpur: Airbus A330-900neo.
- United seasonal San Francisco: Boeing 787-9.

Primary route/equipment evidence: https://corporate.adelaideairport.com.au/wp-content/uploads/2025/09/MEDIA-RELEASE-Jetstar-adds-more-than-350000-new-low-fares-seats-to-Adelaide-network.pdf ; https://corporate.adelaideairport.com.au/media-centre/indonesia-airasia-touches-down-in-adelaide-enabling-affordable-connectivity-across-asia-via-bali ; https://corporate.adelaideairport.com.au/media-centre/qantas-launches-new-international-route-and-business-lounge-for-customers ; https://corporate.adelaideairport.com.au/media-centre/qantas-brings-next-gen-a220-aircraft-to-south-australia ; https://corporate.adelaideairport.com.au/media-centre/malaysia-airlines-increases-to-daily-adelaide-flights-to-fly-new-a330neo-aircraft ; https://corporate.adelaideairport.com.au/media-centre/united-airlines-touches-down-in-adelaide-marking-the-first-ever-direct-flights-from-the-usa .

Charter-only Fokker types, Metroliners, freighters, private aircraft and one-off diversions are
not treated as catalogue gaps in this pass.

The international layer uses a real operator and live Adelaide routes rather than invented
airlines. Adelaide Airport's April 2026 route table records Air New Zealand to Auckland five
times weekly and seasonal Christchurch twice weekly. Its route announcement specifies A320 or
A321 Airbus narrowbodies for Christchurch; Air New Zealand's April 2026 fleet page confirms its
A320neo/A321neo fleet. The simulation uses one representative international A321neo rotation at
Gate 15, with Auckland as the opening arrival and Auckland/Christchurch as its network.

The widebody layer uses Gate 18L for Cathay Pacific's southern-summer Hong Kong service
and Gate 20L for the requested Singapore Airlines scenario. Cathay's Adelaide announcement
specifies the A350-900. Singapore Airlines' current Adelaide route page specifies a 787-10,
which the simulation now assigns to its representative Gate 20 rotation.

Sources: Adelaide Airport, *Our International Destinations — April 2026* —
https://corporate.adelaideairport.com.au/media-centre/adelaide-airport-passenger-statistics-march-2026 ;
Adelaide Airport, *Air New Zealand announces first Adelaide-Christchurch service* —
https://corporate.adelaideairport.com.au/media-centre/air-new-zealand-announces-first-adelaide-christchurch-service ;
Air New Zealand, *Operating fleet* — https://www.airnewzealand.com/fleet .

Widebody sources: Adelaide Airport, *Cathay Pacific returns to South Australia* —
https://corporate.adelaideairport.com.au/media-centre/cathay-pacific-returns-to-south-australia ;
Singapore Airlines, *Flights from Adelaide to Singapore* —
https://www.singaporeair.com/au/en/plan-travel/destinations/flights-from-adelaide/ .

## Ground taxi speeds (simulation)

Taxi is not published as a hard AFM limitation for these types; the figures below are
the verified operating bands used by `GroundSpeedLimits` (ADR 0045). Retrieved
2026-09-16, widebody columns added 2026-09-17.

| Regime | ATR 42 | Saab 340B | Dash 8-400 | Boeing 737-8 / Airbus A321neo | Airbus A350-900 / Boeing 787-10 | Primary sources |
|---|---:|---:|---:|---:|---:|---|
| Straight taxiway target | 22 kt | 20 kt | 24 kt | 20 kt | 20 kt | Type-specific targets stay inside the documented 20–25 kt normal regional band; Boeing FCTM normal ≈20 kt for narrowbody and widebody alike |
| Apron / ramp | 14 kt | 13 kt | 15 kt | 10 kt | 8 kt | Saab operator flow apron 15 kt; Boeing FCTM ramp / apron entry ≈10 kt narrowbody; widebodies taken slightly slower given wingtip/tail clearance margins on a tighter apron |
| Turns (≥~30°) | ≈10 kt | ≈10 kt | ≈10 kt | ≈10 kt | ≈10 kt | Boeing 737 FCTM turn entry; CAST / airline SOPs |
| Stand lead-in | 5 kt | 5 kt | 5 kt | 5 kt | 5 kt | Marshaller / walking-pace guidance |
| Pushback (tug) | 3 kt | 3 kt | 3 kt | 3 kt | 3 kt | Walking-pace tow |
| Lineup onto runway | 10 kt | 10 kt | 10 kt | 10 kt | 10 kt | Boeing turn-entry band; leg ends stopped at roll start |
| Vacate after landing | 12→22 kt | 12→20 kt | 14→24 kt | 15→20 kt | 14→20 kt | Type-specific runway-exit speed hands directly into the actual taxi profile; turn curvature may trim it slightly |

Breakaway / taxi acceleration uses ≈0.55 m/s² (turboprop ADS-B study average peak
≈0.5 m/s²; ETS literature ≈0.4 m/s² to 15 kt). Cornering uses 0.59 m/s² lateral so a
45 m fillet settles near 10 kt — this lateral limit and the turn/stand-lead-in/pushback/
lineup bands above are deliberately not type-specific: they are SOP/walking-pace figures
independent of aircraft size in real operations, not a gap in this data.

### Gate-turn steering geometry (nose-to-main-gear wheelbase)

`AdelaideGround.GateTaxiOut`/`GateTaxiIn` steers each jet's main gear — not its nose —
through terminal-gate turns, trailing the nose datum by the aircraft's own wheelbase
(`AircraftPerformanceProfile.NoseToMainGearMetres`) so its body tracks the pavement
instead of swinging its tail across the grass. Retrieved 2026-09-17.

| Type | Wheelbase (nose→main gear) | Source |
|---|---:|---|
| Boeing 737-8 | 17.68 m | Commonly published Boeing 737-800/-8 airport-planning figure. **Not independently confirmed against a fetched primary source** — cross-check against the Boeing 737 MAX ACAP (Rev K, cited above) "Ground Maneuvering" section before relying on this beyond taxi-turn visuals. |
| Airbus A321neo | 16.90 m | Airbus, *A321 Aircraft Characteristics* — https://www.aircraft.airbus.com/sites/g/files/jlcbta126/files/2023-02/Airbus-techdata-AC_A321_0322%20(2).pdf |
| Airbus A350-900 | 28.66 m | Airbus, *A350-900/-1000 Aircraft Characteristics* — https://www.aircraft.airbus.com/sites/g/files/jlcbta126/files/2023-02/Airbus-Commercial-Aircraft-AC-A350-900-1000.pdf |
| Boeing 787-10 | 28.88 m | Boeing 787 ACAP (787_Rev_P.pdf); its 68.30 m overall-length figure matches this project's own recorded AIR-010 runtime-model envelope exactly (see the specifications table above), corroborating the source. |
| Embraer E190 | 14.65 m | Embraer E190 Airport Planning Manual; used for gate-turn visuals. |
| Airbus A220-300 | 14.86 m | Airbus A220 aircraft characteristics; used for gate-turn visuals. |
| Airbus A320-200 | 12.64 m | Airbus A320 product/aircraft characteristics. |
| Boeing 737-800 | 15.60 m | Boeing 737NG ACAP Rev C. |
| Airbus A330-900neo | 25.38 m | Airbus A330 aircraft characteristics; used for gate-turn visuals. |
| Boeing 787-9 | 25.83 m | Boeing 787 ACAP Rev Q. |

Turboprops (ATR 42, Saab 340B, Dash 8-400) are not currently steered by this mechanism —
only the terminal-gate jets are — so their wheelbase is left at 0 in
`AircraftPerformance.cs` rather than recording an unsourced figure that nothing reads.

## Runtime model check

Genuine runtime models are measured from their glTF POSITION bounds by
`AircraftModelBounds` and must be within ±5% of the dimensions above (EditMode
`AircraftCatalogueTests`):

| Type | Runtime model | Measured length × span × height |
|---|---|---|
| ATR 42-600 | `Models/Aircraft/mdl_atr42_starter_v03.gltf` (v02/v01 fallback) | 22.67 × 24.57 × 7.59 m |
| Dash 8-400 | `Models/Aircraft/mdl_dash8_q400_v01.gltf` (AIR-006) | 32.83 × 28.42 × 8.34 m |
| Boeing 737-8 | `Models/Aircraft/mdl_737_8_narrowbody_v01.gltf` (AIR-005) | 39.47 × 35.92 × 12.42 m |
| Airbus A321neo | `Models/Aircraft/mdl_a321neo_v01.gltf` (AIR-008) | 44.51 × 35.80 × 11.76 m |
| Saab 340B | `Models/Aircraft/mdl_saab_340b_v01.gltf` (AIR-007) | 19.73 × 21.44 × 6.97 m |
| Airbus A350-900 | `Models/Aircraft/mdl_a350_900_v01.gltf` (AIR-009) | 66.80 × 64.75 × 17.05 m |
| Boeing 787-10 | `Models/Aircraft/mdl_787_10_v01.gltf` (AIR-010) | 68.30 × 60.12 × 17.02 m |
| Embraer E190 | `Models/Aircraft/mdl_e190_v01.gltf` (AIR-013) | 36.24 × 28.72 × 10.55 m |
| Airbus A220-300 | `Models/Aircraft/mdl_a220_300_v01.gltf` (AIR-014) | 38.70 × 35.10 × 11.50 m |
| Airbus A320-200 | `Models/Aircraft/mdl_a320_200_v01.gltf` (AIR-011) | 37.57 × 35.80 × 11.76 m |
| Boeing 737-800 | `Models/Aircraft/mdl_737_800_v01.gltf` (AIR-012) | 39.47 × 35.80 × 12.50 m |
| Airbus A330-900neo | `Models/Aircraft/mdl_a330_900neo_v01.gltf` (AIR-015) | 63.66 × 64.00 × 16.79 m |
| Boeing 787-9 | `Models/Aircraft/mdl_787_9_v01.gltf` (AIR-016) | 62.81 × 60.12 × 17.02 m |
