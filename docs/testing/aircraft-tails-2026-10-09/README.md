# Aircraft tail fit — 9 October 2026

Task #684; all 15 active airframes and the A320 authored fallback.

## What was found and corrected

- Flat fin roots bridged across curved tailcones; roots now sample the actual hull
  along their full chord and embed 45 mm into it.
- Rudders/elevators were separate rectangles or mismatched aerofoils. Their new
  swept sections share a 70%-chord hinge with the fixed surface. Root/tip strips
  remain static, and the existing articulated node names remain.
- Conventional stabiliser centre sections follow the actual aft hull and compress
  their root section where the tailcone is shallower than the aerofoil. ATR/Dash 8
  retain their T-tail attachment and a rounded centre bullet.
- Dash 8, E190, A320/A321, A350, A330-900 and 787 stabiliser widths now match the
  published dimensions below. A321/787-10 tails translate rather than stretch.
- Bell fin/stabilisers and trainer fin/stabilisers have tapered aerofoil sections
  instead of boxes; Bell boom and all rotor geometry remain unchanged.
- Tail-mounted lamps follow the revised fin; the Dash 8 HF antenna's base is fitted
  into the leading edge instead of floating ahead of it.

## Reviewed fleet

ATR42, SF34, DH8D, E190, A223, A320 (preferred v02 and authored v01), B738,
B38M, A21N, A359, A339, B789, B78X, B412 and TRAINER.

The before/after side, rear and top contact sheets read the actual runtime
POSITION/index buffers, without substituting concept art. Pink shows background;
forward fuselage is deliberately cropped. Neutral material colours make structural
edges readable; these images do not reproduce Unity lighting or transparency.

## Reference evidence

Manufacturer/certification documents were read as reference only; no page image,
model, logo or texture is imported. Fin station coordinates remain original
project modelling approximations. Existing sources/registers continue to cover
unchanged adapted A320 engines, pylons and wheels.

| Type | Checked reference | Tail evidence used |
|---|---|---|
| ATR42 | [ATR factsheet, PDF p. 1](https://www.atr-aircraft.com/wp-content/uploads/2020/07/Factsheets_-_ATR_42-600.pdf) | T-tail layout and overall silhouette; retain existing authored proportions |
| SF34 | [Saab product](https://www.saab.com/products/saab-340), [EASA TCDS issue 26](https://www.easa.europa.eu/sites/default/files/dfu/TCDS_EASA_A_068__Saab_SF340A_340B_Iss26.pdf) | Conventional layout/envelope; retain existing authored tail span, not a newly measured dimension |
| DH8D | [DHC recovery manual, 01-10-00 p. 2 / PDF p. 44](https://dehavillandportal.com/assets/public-documents/D8400-ARM.pdf) | 9.27 m stabiliser, T-tail layout and 8.34 m total height |
| E190 | [Embraer indexed APM, section 2 p. 2-4](https://www.embraercommercialaviation.com/wp-content/uploads/2017/06/APM_190.pdf) | 12.08 m stabiliser in official indexed text; document download now returns 404 |
| A223 | [Airbus characteristics](https://www.aircraft.airbus.com/en/customer-care/fleet-wide-care/airport-operations-and-aircraft-characteristics/aircraft-characteristics) | Conventional layout; retain existing authored A220 tail stations |
| A320/A21N | [Airbus A320 July 2025, 2-2-0 p. 2](https://aircraft.airbus.com/sites/g/files/jlcbta126/files/2025-07/AC_A320_20250715.pdf) | 12.45 m stabiliser; A320 fin sweep; A321 uses the same physical tail translated aft |
| B738/B38M | [Boeing planning manuals](https://www.boeing.com/commercial/airports/plan-manuals) | Retain existing 737 family envelope/station profiles; repair root/hinge geometry |
| A359 | [Airbus A350 May 2024, 2-2-0 pp. 2-3](https://aircraft.airbus.com/sites/g/files/jlcbta126/files/2024-06/AC_A350_0524.pdf) | 18.79 m stabiliser; 6.21/2.46 m root/tip chord; 3.04 m fin tip chord and swept silhouette |
| A339 | [Airbus A330 July 2023, A330-900 2-2-0 p. 7](https://aircraft.airbus.com/sites/g/files/jlcbta126/files/2023-08/ac_a330_jul2023_0.pdf) | 19.40 m stabiliser; 7.80 m fin root chord and 3.10 m fin tip chord |
| B789/B78X | [Boeing D6-58333 rev Q, 2.2.2 / PDF p. 22](https://www.boeing.com/content/dam/boeing/v2/airports/acaps/787_ACAP_Rev_Q.pdf) | 19.81 m stabiliser; swept/raked silhouette; same physical tail on stretched 787-10 |
| B412 | [Bell 412 specifications](https://www.bellflight.com/products/bell-412) | Tapered fin/boom layout; original rotor/hub datums retained |
| TRAINER | Existing original unbranded high-wing trainer | Tapered conventional tail, hull-fitted roots, existing 11 m main span retained |

## Verification commands

- `python3 scripts/check-aircraft-tails.py`: all shipped root rings inside their
  actual hull/boom, fixed/movable hinge distance below 6 mm, T-tail intersection,
  finite/valid indices, no UInt16 overflow, profile spans and identical mirrors.
- `python3 scripts/refine-aircraft-tails.py --check`: deterministic finishing.
- `python3 scripts/render-aircraft-tails.py work/tails/final`: all active tails,
  side/rear/top. The reviewed output is saved beside this document.
- `python3 scripts/test-bell-tail-rotor.py`: retains the rotor datum/radius and
  glTF/FBX/packaged equality.
- Python AST syntax and `git diff --check`.
- Compare actual mesh arrays against base `a049df54`: unrelated meshes and whole
  aircraft dimensional envelopes retained; tail lamps/HF antenna changes scoped.
- Metadata/mirror audit; tracked-asset audit isolates the existing satellite JPEG
  mismatch from ignored local numbered copies.

## Results

All 16 model versions pass attachment/hinge/mesh/span/mirror checks and deterministic
finishing. All 15 active tails were reviewed from side/rear/top. The Bell rotor
regression passes; eight changed Python files parse and the diff has no whitespace
errors. Actual mesh-array comparison against `a049df54` retains unrelated meshes
and overall aircraft envelopes.

The repository-wide audit reports local ignored numbered-copy files without
metadata, plus the already documented satellite JPEG mirror mismatch. An audit
of tracked assets plus the two new FBX/meta pairs reports only that same satellite
mismatch; aircraft metadata and mirrors pass. These unrelated files are retained.

## Not verified

Native Unity import/compile, player appearance in day/dusk/night, animated control
surface deflection and performance. No full domain/Unity suite or packaged build
was run. The focused geometry/render evidence establishes the asset correction;
it does not establish a native gameplay or GPU pass.
