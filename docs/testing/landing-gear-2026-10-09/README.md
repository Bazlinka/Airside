# Fleet landing gear — #757

Outcome: improve fleet gear mounting, deployment/retraction, doors and associated motion. All 13 passenger types are in scope; the trainer retains fixed tricycle gear and Bell 412 retains fixed skids.

Baseline `6cd232bd`: native runtime-builder folded frames show exposed widebody nose/main tyres. Source trace establishes an off-centre top pivot for canted struts, overlapping door/leg timing, independently reseeded part clocks after reclassification, and widebody bogie beams/axles left in the strut while their wheels tilt. Widebody kits lack animated bay leaves.

Implementation: top-vertex fitted mounting pivot; single per-root gear timeline retained across cache rebuilds; door fully open before/through leg travel; steering centres before folding; simplified kit stow angles fit above horizontal; actual beam/axle triangles follow the truck; missing widebody bay leaves are clipped to existing underside skin. Existing authored doors and Dash 8 nacelle repair retained. Gear-down wheel positions, ground datums, simulation, paths and saves unchanged.

Verification in progress. Baseline native gear-up side/front/under frames inspected, including all-fleet underside sheet and full-size A350/ATR views. No repaired appearance result claimed yet.

Reference: [ATSB Saab 340 gear investigation](https://www.atsb.gov.au/sites/default/files/investigation-reports/ao-2014-189-final.pdf) confirms nose/main forward retraction. Stow angles/cycle values here fit the simplified kits; they are not manufacturer maintenance procedures.
