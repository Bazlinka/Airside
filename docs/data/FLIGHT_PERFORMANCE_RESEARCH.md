# Fleet flight performance research — 8 October 2026

Task: aircraft should climb, level and descend consistently in cockpit/window/exterior
views, with credible type-dependent speeds and no stale vertical-speed reading.
Scope: derived flight profile, map/camera handoffs, telemetry and representative regional
runway motion. Reservations, commands, saved arrival deadlines and save schema remain intact.
Newly scheduled jet legs carry a 20-minute climb/descent allowance (previously ten),
a game planning allowance for the constrained profile and reserved final. Turboprop and
helicopter allowances remain unchanged.

## Findings and interpretation

- E190 at 40,000 ft is possible: ANAC's ERJ170/190 operational evaluation gives a
  41,000 ft maximum. It does not establish the right level for a particular weight/trip.
- The game assigned an E190 a constant 2,000 ft/min throughout climb, not a VSI display
  range. Real climb performance varies with altitude. Its camera telemetry also ignored
  frames advancing >=0.75 simulation seconds, freezing readings at accelerated rates.
- Camera speed was GS, derived from map-route motion. IAS/CAS,
  TAS, Mach and GS must be distinguished before comparing a displayed number to a limit.
- Inbound horizontal paths reserved a 32km extended final, but altitude used the whole
  leg profile then blended down to the final over 40km. Both now reserve the same final.
- Regional takeoff used a universal 40-second/800m roll and ~78kt climb irrespective
  of type. The runtime now integrates each aircraft's representative Vr/roll instead.

## Primary sources and variant coverage

These are independently summarized factual anchors. No manual, database, graph,
photograph, BADA coefficient file or licensed performance dataset is redistributed.
Public EUROCONTROL tables are indicative training/planning data, not certified dispatch data.

| Game aircraft | Evidence checked | What the implementation uses / limits |
|---|---|---|
| ATR 42-600 | [ATR factsheet](https://www.atr-aircraft.com/wp-content/uploads/2020/07/Factsheets_-_ATR_42-600.pdf): 112 KCAS minimum V2 at MTOW, optimum climb 160 KCAS | Preserve existing representative runway speeds; 160 CAS climb. EUROCONTROL AT43 is the older -300/-320 and is **not** substituted for -600. Rates remain planning estimates. |
| Saab 340B | [Saab](https://www.saab.com/products/saab-340): 283kt maximum cruise, 25,000ft maximum operating altitude; [EUROCONTROL SF34](https://contentzone.eurocontrol.int/aircraftperformance/details.aspx?ICAO=SF34): climb decreases with level | 23,000ft normal upper target, ceiling unchanged; 210 CAS climb; bands taper toward upper level. |
| Dash 8-400 | [De Havilland](https://dehavilland.com/dash8-special-mission/): 360kt maximum cruise and 25,000ft ceiling; special-mission payload figures aren't passenger dispatch data. [EUROCONTROL DH8D](https://contentzone.eurocontrol.int/aircraftperformance/details.aspx?GroupFilter=12&ICAO=DH8D&ICAOFilter=&NameFilter=): 210 IAS climb and reducing rates | 25,000ft normal upper target; 210 CAS climb. Rates are conservative interpolated planning values. |
| Embraer E190 (original E-Jet) | [ANAC evaluation](https://www.gov.br/anac/pt-br/assuntos/regulados/aeronaves/avaliacao-operacional/RelAvOp_ERJ170_190.pdf): FL410 ceiling; [EUROCONTROL E190](https://contentzone.eurocontrol.int/aircraftperformance/details.aspx?ICAO=E190&ICAOFilter=e190): 300 IAS climb, M.78 cruise, decreasing climb rates | 37,000ft normal upper target; low-level climb can exceed 2,000ft/min, high-level climb tapers. E190-E2 is **not** used as the E190 reference. |
| A220-300 | [Airbus A220](https://www.aircraft.airbus.com/en/aircraft/a220/a220-300); [EUROCONTROL BCS3](https://contentzone.eurocontrol.int/aircraftperformance/details.aspx?ICAO=BCS3): 280 IAS climb, M.78 cruise | BCS3 is the original CSeries/A220-300 designation; 37,000ft normal upper target. |
| A320-200 | [Airbus aircraft characteristics](https://www.aircraft.airbus.com/en/customer-care/fleet-wide-care/airport-operations-and-aircraft-characteristics/aircraft-characteristics); [EUROCONTROL A320](https://contentzone.eurocontrol.int/aircraftperformance/details.aspx?ICAO=A320&ICAOFilter=A320): reducing climb rates | 290 CAS / M.78 planning cap, 37,000ft normal upper target; existing conservative ceiling retained. |
| Boeing 737-800 | [EUROCONTROL B738](https://contentzone.eurocontrol.int/aircraftperformance/default.aspx?ICAOFilter=738): 290 IAS climb, M.79 cruise, FL410 ceiling | 39,000ft normal upper target; retain calculated-profile runway speeds, taper climb with height. |
| Boeing 737-8 MAX | [Boeing ACAP](https://www.boeing.com/content/dam/boeing/v2/airports/acaps/737MAX_RevK.pdf); [EUROCONTROL B38M](https://learningzone.eurocontrol.int/ilp/customs/ATCPFDB/details.aspx?ICAO=B38M): 290 IAS climb, M.79 cruise, FL410 ceiling | Same normal upper target as -800, using MAX evidence rather than assuming its certification from NG. Climb/descent interpolation remains representative. |
| A321neo | [Airbus A321neo](https://www.aircraft.airbus.com/en/aircraft/a320-family/a321neo?pubDate=20260118): MMO .82; [Airbus speed guidance](https://safetyfirst.airbus.com/control-your-speed-in-cruise/?airbus-iframe=true&airbus-post=2139) | M.78 is a conservative family planning choice below MMO, not a manufacturer-mandated cruise. 290 CAS and climb-rate bands are A320-family estimates; exact neo mass/engine performance unavailable here. |
| A330-900neo | [Airbus A330-900](https://www.aircraft.airbus.com/en/aircraft/a330/a330-900): cruise M.82; [EUROCONTROL A339](https://contentzone.eurocontrol.int/aircraftperformance/details.aspx?ICAO=A339): 290 IAS climb and reducing rates | M.82 manufacturer cruise cap; 39,000ft normal upper target. |
| A350-900 | [Airbus A350-900](https://www.aircraft.airbus.com/en/aircraft/a350/a350-900): cruise M.85 | 39,000ft normal upper target; 300 CAS climb and rate bands are conservative game planning estimates, not measured A350 performance. |
| Boeing 787-9 | [Boeing 787 ACAP Rev P](https://www.boeing.com/content/dam/boeing/v2/airports/acaps/787_Rev_P.pdf): standard-day mission charts use M.85 cruise | 39,000ft normal upper target; 300 CAS and tapered widebody rates remain planning assumptions. |
| Boeing 787-10 | Same ACAP, distinct -10 payload/range and runway charts | Separate heavier-airframe climb-rate estimate; M.85 cap and 39,000ft normal upper target. No -9 weight data imported into -10. |
| Bell 412EP | [Bell 412EPI factsheet](https://www.bellflight.com/products/-/media/site-specific/bell-flight/documents/products/412/bell-412epi-fact-sheet.pdf?hash=7C610A253AE23962AB965ADB42FE25BC&la=en): related EPI max cruise 122kt, mission manual conditions apply | Existing 110kt, <=1,500ft local VTOL profile stays in its separate rotorcraft model. EPI/EPX ceilings/performance are not treated as exact EP certification. Common telemetry correction applies to Bell too. |

All normal upper cruise targets are **game planning choices**, not maximum operating
altitudes or a claim about a real airline's assigned level. Actual level depends on mass,
route length, temperature, winds, ATC and aircraft configuration. No operational AFM is
available in this project; no fixed V1/Vr/V2 combination can certify an actual takeoff.

## Airspeed rules and atmosphere

[CASA VFR Guide v8.2](https://www.casa.gov.au/sites/default/files/2022-02/visual-flight-rules-guide.pdf),
and [Part 91 MOS section 4.02](https://www.legislation.gov.au/F2020L01514/2024-02-10/2024-02-10/text/original/epub/OEBPS/document_1/document_1.html)
make the below-10,000ft limit an **indicated airspeed** rule with airspace/flight-rule
conditions. It is not a universal 250kt ground-speed law. Class C's cited row is VFR;
Class E/G include IFR and VFR. Class D adds a 200kt constraint near its primary aerodrome
and ATC exceptions. The game adopts a conservative **250 CAS below 10,000ft AMSL SOP**
for these fleet routes without claiming a complete airspace/ATC regulatory simulator.

Altitude is field-relative in existing flight HUDs; pressure-height conversion adds the
rounded YPAD 6m elevation. Route physics currently assume zero wind: TAS=GS. IAS is
represented by CAS without instrument/position error. ISA temperature/pressure and
subsonic pitot pressure conversion follow standard atmosphere relations and
[NASA's isentropic equations](https://www.grc.nasa.gov/www/k-12/airplane/isentrop.html).
This cannot certify live-wind cockpit IAS; modelling route winds remains future work.
