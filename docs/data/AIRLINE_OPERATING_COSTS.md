# Airline operating costs — source data for Economy v2

Date gathered: 9 October 2026. Purpose: real-world-scale inputs for the cost model in
`docs/plans/economy_realism_plan.md` and `docs/plans/economy-competitors-walkable-roadmap.md`. Game costs are
informed by these figures; the game is not a statement of any real airline's finances.

Licence / use: facts and numbers only (no prose or tables reproduced beyond the figures needed). No code, image or
asset enters the project from these sources. Acquisition cost: $0; no subscriptions or sign-ups used. Fallback: the
current game-dollar formulas in `FlightEconomics.cs`.

## How to read this file — confidence tiers

| Tier | Meaning |
|---|---|
| **A** | Primary: published by the charging body or airport itself. Safe to use as the source of a number. |
| **B** | Manufacturer or operator publication. Good for the type it describes. |
| **C** | Press or trade summary, or a figure derived from one. Use as an estimate; confirm before relying on it. |
| **D** | Not sourced. Indicative from general knowledge or a weak source; **must be replaced** before it ships in the game. |

Nothing in tier C or D should be quoted as fact in the game's UI or documentation.

## 1. Airport and navigation charges — tier A

### 1.1 Adelaide Airport Limited, Schedule of Aeronautical Fees, effective 1 July 2026

Source: [AAL schedule of fees 1 July 2026](https://corporate.adelaideairport.com.au/media/hddjuqwd/aal-schedule-of-fees-20260701.pdf).
Prices include GST, in AUD. Per passenger (infants and positioning crew excluded). Column D is a government-mandated
fee collected on departing passengers; treat it as a pass-through to the passenger, not an airline cost.

| Service | Landing | Terminal | Expansion | Govt (D) | Total |
|---|---:|---:|---:|---:|---:|
| Regional RPT arrival | $5.62 | $0.51 | — | $0.00 | $6.13 |
| Regional RPT departure | $5.62 | $0.51 | — | $9.80 | $15.93 |
| Domestic arrival | $9.58 | $5.13 | — | $0.00 | $14.71 |
| Domestic departure | $9.58 | $5.13 | — | $9.80 | $24.51 |
| International arrival | $18.01 | $5.13 | $15.71 | $0.00 | $38.85 |
| International departure | $18.01 | $5.13 | $15.71 | $18.40 | $57.25 |

General aviation (not RPT): fixed-wing and freighter arrival $11.25 per 1,000 kg MTOW, minimum $65.81; rotary-wing
$5.64 per 1,000 kg, minimum $32.87. Diversion arrivals: international $15.55, domestic $11.54 per 1,000 kg MTOW.

Aircraft parking per day (over 2 hours): Code A $24.53; Code B or C $167.17; Code D or E $668.65; Code F $1,123.33.
Short stays under 2 hours are free for Codes A–C, $334.33 for D/E and $561.64 for F.

Note: the schedule charges passenger-based fees for passenger services, so airport cost scales with load, not weight.
That matters for the load-factor model.

### 1.2 Airservices Australia, Statutory Charging Determination, effective 1 August 2025

Source: [Statutory Charging Determination – Services and Facilities](https://www.airservicesaustralia.com/wp-content/uploads/2025/07/Statutory-Charging-Determination-Services-and-Facilities.pdf)
(dated 22 July 2025). The text was extracted automatically from the PDF; table values below were read from that
extraction and should be spot-checked against the PDF before code relies on them. GST treatment is stated in the
interim addendum as "including GST" but is not stated for the main tables; confirm.

- **Terminal navigation**, per landing = rate x Chargeable Weight (tonnes). **Adelaide: $12.78 per tonne**; minimum
  $21.00 per activity at Adelaide. For comparison: Sydney $6.04, Melbourne $5.85, Brisbane $6.64, Perth $8.13,
  Hobart $9.10, Parafield $16.36.
- **Aviation rescue and firefighting (ARFF)**, per landing = rate x Chargeable Weight, by aircraft category. Adelaide
  columns as printed: 2.46, 3.46, 5.59, 8.87 per tonne (the extraction does not label the four category columns;
  confirm which category each belongs to). Applies to aircraft of 5.7 t Chargeable Weight or more.
- **En route navigation**: charge = rate x (Chargeable Distance / 100) x Chargeable Weight for under 20 t, or
  rate x (Chargeable Distance / 100) x sqrt(Chargeable Weight) for 20 t or more. Rates: **$0.90 up to 20 t, $4.04
  for 20 t or more**. Distance unit (km) to be confirmed against the document.
- **Chargeable Weight** (Schedule 5) examples as extracted: Dash 8-400 28.15 t; ATR 42-500 18.60 t; A320 73.50 t;
  A321 91.89 t; A330-300 229.55 t. The extraction truncated the table; read the PDF for the Saab 340, 737-800, E190,
  A220, A330-900, 787 and A350 values.

### 1.3 Worked examples (derived from 1.1 and 1.2, tier A inputs, arithmetic mine)

| Aircraft | Terminal navigation per landing at Adelaide | Note |
|---|---:|---|
| ATR 42-500 (18.60 t) | 18.60 x 12.78 = **$238** | Dash 8-400 (28.15 t): **$360** |
| A320 (73.50 t) | 73.50 x 12.78 = **$939** | A321 (91.89 t): **$1,174** |
| A330-300 (229.55 t) | 229.55 x 12.78 = **$2,934** | |

Add ARFF and the passenger-based airport fees above for a landing cost. For a 40-seat regional arrival at 75% load,
airport fees are about 30 x $6.13 = $184; for 180 domestic passengers at 80% load, about 144 x $14.71 = $2,118.

## 2. Fuel — tier B and C

- **Fuel burn (tier B, ATR 42-600):** [ATR 42-600 factsheet](https://atr-aircraft.com/wp-content/uploads/2020/07/Factsheets_-_ATR_42-600.pdf):
  fuel flow at cruise 811 kg/h; block fuel 584 kg for 200 nm (60 min) and 802 kg for 300 nm (81 min), so about
  580–595 kg per block hour.
- **Fuel burn (tier C, Saab 340B):** derived from a broker's sector table ([C&L Aerospace Saab 340B data](https://sales.cla.aero/aircraft/saab-340b-msn-446/)):
  about 515–555 kg per block hour. The figure is my division of their published sector fuel by block time.
- **Fuel burn (tier C, 737-800):** about 2,200–2,600 kg per cruise hour from general summaries
  ([example](https://flyawaysimulation.com/ask/answers/boeing-737-fuel-consumption-per-hour/)). Weak source; replace
  with an operator or manufacturer figure.
- **Fuel burn (tier D):** A330-900, 787, A350, E190, A220, A320, A321 and Dash 8: not sourced here. Use Airbus/Boeing/
  Embraer airport-planning documents or EASA/ICAO emissions data.
- **Fuel price (tier C):** press coverage of the IATA Jet Fuel Price Monitor reports a global average of about
  **$1,276 per tonne in May 2026** and roughly **$152 per barrel projected for the 2026 average, against about
  $90 in 2025**, with weekly readings of $116–159 per barrel between June and August 2026
  ([CAPA/IATA outlook summary](https://centreforaviation.com/news/iata-average-jet-fuel-prices-to-be-70-higher-adding-usd100bn-to-collective-fuel-bill-in-2026-1361900),
  [afm.aero](https://afm.aero/iata-reports-121percent-global-jet-fuel-price-shock-in-2026-with-india-and-japan-most-exposed)).
  I could not open IATA's own page. Confirm against IATA before using exact values.

Implication for the design: the real fuel price has recently been unusually high and volatile, which fits the planned
moving fuel price. Pick a baseline (a 2025-like $90–100 per barrel is a calmer start) and a volatility range as a
design choice, not a data fact.

Indicative fuel cost per block hour at $1,276 per tonne (tier C arithmetic): Saab 340 about $650–$710; ATR 42-600
about $740–$760; 737-800 about $2,800–$3,300.

## 3. Crew — tier C/D

- QantasLink direct-entry first officer: **A$79,252–94,383 plus super and allowances**, bases include Adelaide
  ([QantasLink listing](https://www.ziprecruiter.com.au/jobs/542978032-qantaslink-direct-entry-first-officer-at-qantas-group)). Tier B/C.
- Virgin Australia first officer: reported about A$180,206 from July 2025 ([aviationa2z](https://aviationa2z.com/index.php/2025/06/07/virgin-australia-pilot-salary-2025/)). Tier C, weak source.
- Cabin crew: covered by the Aircraft Cabin Crew Award 2020 (MA000047) with enterprise agreements above it. Rates not
  extracted. Fair Work Commission and airline agreements are the primary sources.
- Captain pay, crew-per-aircraft numbers, hours flown per year and on-costs (super, allowances, training): **tier D**,
  not sourced.

## 4. Aircraft values, leases and maintenance — tier D, mostly unsourced

- ATR 42-600 list price about US$20–22M ([Jettly](https://jettly.com/post/atr-airplane-price)), a charter broker's marketing
  page. Tier C/D.
- IBA reported new A320neo and 737 MAX 8 values of about US$55.5M rising to about US$57.8M (Oct 2024 to Jan 2026)
  ([Aircraft Interiors International](https://www.aircraftinteriorsinternational.com/industry-opinion/new-generation-aircraft-are-retaining-the-strongest-market-values.html)). Tier C.
- Used-market values for the Saab 340, ATR 42, Dash 8-400, E190, 737-800, A320ceo, A330-900, 787 and A350, lease
  rates, depreciation, insurance and maintenance reserves: **not found in free sources**. These come from paid
  services (IBA, Ishka, AVAC, Cirium). The ranges in the roadmap are from general knowledge and are tier D.

Options for these lines: (a) buy one report, (b) derive from public airline financial statements (annual reports list
aircraft book values and leases), or (c) keep them as clearly labelled design values.

## 5. Demand and fares — tier C

- BITRE Domestic Aviation Activity (monthly) gives passengers, aircraft trips and load factor per route. March 2024: network
  load factor **80.4%**; Adelaide–Gold Coast **91.3%** ([BITRE publication list](https://www.bitre.gov.au/sites/default/files/documents/Domestic%20Aviation%20Activity%20publication%20-%20June%202026.pdf)).
  The June 2026 edition covers 62 routes on pages 4–13; the route rows have not been read here.
- Average fares are not in BITRE's publication. The ACCC airline monitoring reports are the likely source; not yet read.
- Kingscote was not found in the search results; BITRE may not publish it as a route.

## 6. What is still needed before code

1. Read the Airservices PDF directly for Schedule 5 weights, the ARFF category headers, GST basis and en route units.
2. Source fuel burn for each type in the game from manufacturer or EASA/ICAO documents.
3. Decide how to cover aircraft values, leases and maintenance (section 4 options).
4. Read the BITRE June 2026 route pages and the ACCC fare data; choose a fare model.
5. Fix a baseline fuel price and volatility; record the choice in an ADR.
6. Only then write the pure cost model and its payback tests (roadmap phase 1).
