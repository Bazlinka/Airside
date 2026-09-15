# Aircraft specifications and sources

Single reference for the figures in `Domain/AircraftCatalogue.cs` (ADR 0048). Dimensions and
manufacturer performance come only from the manufacturer or type-certificate sources below,
retrieved 2026-09-15. **Planning cruise** and **practical range** are gameplay planning figures:
they are kept at or below the manufacturer figure where one is recorded, and are labelled as
assumptions where none is.

| Source id | Type | Length | Wingspan | Height | Manufacturer cruise | Manufacturer range | Planning cruise / practical range | Source |
|---|---|---|---|---|---|---|---|---|
| SPEC-ATR42-600 | ATR 42-600 | 22.67 m | 24.57 m | 7.59 m | 300 KTAS / 556 km/h max cruise (95% MTOW, ISA, optimum FL) | 703 NM (1,302 km) with max passengers | 556 km/h / 1,100 km | ATR, *ATR 42-600 factsheet*, pp. 10–11 — https://www.atr-aircraft.com/wp-content/uploads/2020/07/Factsheets_-_ATR_42-600.pdf |
| SPEC-SAAB-340B | Saab 340B | 19.73 m | 21.44 m (22.75 m extended wingtips) | 6.97 m | 283 kt / 524 km/h max cruise | Not published on the cited page | 500 km/h / 1,000 km — **range is a planning assumption** | Saab, *Saab 340B* product page — https://www.saab.com/products/saab-340 ; dimensions corroborated by EASA TCDS EASA.A.068 (Saab SF340A/340B), issue 26 — https://www.easa.europa.eu/sites/default/files/dfu/TCDS_EASA_A_068__Saab_SF340A_340B_Iss26.pdf |
| SPEC-DASH8-400 | De Havilland Canada Dash 8-400 | 32.83 m | 28.42 m | 8.34 m | 360 kt / 667 km/h max cruise | 1,596 km (862 nm) full-passenger range, 102 kg per passenger | 667 km/h / 1,500 km (was 1,800 km, above the manufacturer figure) | De Havilland Canada, *Dash 8-400 spec sheet* v11, July 2026 — https://dehavilland.com/wp-content/uploads/2026/07/DHC_Dash8_Spec-Sheet_v11_Digital.pdf |
| SPEC-BOEING-737-8 | Boeing 737-8 | 39.5 m (129 ft 6 in) | 35.9 m (117 ft 10 in) | 12.3 m (40 ft 4 in) | Not published on the cited page | Up to 3,500 nmi (6,480 km) | 839 km/h (≈ Mach 0.79) / 5,200 km — **cruise is a planning assumption** | Boeing, *737 MAX* specifications — https://www.boeing.com/commercial/737max ; tail height range 11.86–12.45 m in Boeing D6-38A004 *737 MAX Airplane Characteristics for Airport Planning*, Rev K (July 2025), §2.3.2 — https://www.boeing.com/content/dam/boeing/v2/airports/acaps/737MAX_RevK.pdf |

## Runtime model check

Genuine runtime models are measured from their glTF POSITION bounds by
`AircraftModelBounds` and must be within ±5% of the dimensions above (EditMode
`AircraftCatalogueTests`):

| Type | Runtime model | Measured length × span × height |
|---|---|---|
| ATR 42-600 | `Models/Aircraft/mdl_atr42_starter_v02.gltf` | 22.67 × 24.57 × 7.59 m |
| Dash 8-400 | `Models/Aircraft/mdl_dash8_q400_v01.gltf` (AIR-006) | 32.83 × 28.42 × 8.34 m |
| Boeing 737-8 | `Models/Aircraft/mdl_737_8_narrowbody_v01.gltf` (AIR-005) | 39.47 × 35.92 × 12.42 m |
| Saab 340B | none — placeholder, drawn with the ATR stand-in | — |
