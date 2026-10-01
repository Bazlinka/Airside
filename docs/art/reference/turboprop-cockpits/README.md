# Turboprop flight-deck layout references

Inspected 1 October 2026. References inform original simplified Airside geometry,
not licensed runtime textures or certified flight-deck models.

- ATR official [pilot brochure](https://www.atr-aircraft.com/wp-content/uploads/2023/04/Brochure-Pilots_A5_2025-Digital.pdf),
  PDF page 6: ATR -600 glass-cockpit photo, broad five-screen panel, paired yokes,
  guidance panel, overhead and central lever/FMS pedestal. The catalogue aircraft
  is an ATR 42-600. Also: [ATR cockpit overview](https://www.atr-aircraft.com/innovation/cockpit/).
- Bombardier official [DHC-8 crash/fire/rescue manual](https://customer.aero.bombardier.com/webd/BAG/CustSite/BRAD/RACSDocument.nsf/51aae8b2b3bfdf6685256c300045ff31/ec63f8639ff3ab9d85257c1500635bd8/$FILE/ATTWQNL4.pdf/D8400-CFRM.pdf),
  Model 400 section, printed page 10 / PDF page 28: forward flight-deck arrangement
  drawing, angled windscreens, pedestal, overhead and roof escape hatch. It is
  a rescue arrangement reference; it does not establish detailed avionics artwork.
- Existing Saab references remain in `../saab340-cockpit/README.md`.

PDF downloads and extracted pages are ignored research scratch under
`work/turboprop-references/`; no photo or diagram pixels are redistributed.
All new meshes and 192px display markings are generated from editable project
code in `GlassTurbopropCockpitInterior.cs`; existing generic font/shaders retained.
The reference photos do not prove the game geometry's correctness. Native kit
renders and actual packaged views remain required to accept the candidate.

Fitting: each eye station is behind its runtime kit's windscreen bounds, using
that kit's ground offset. ATR eye (-0.48, 2.67, 8.60) and Dash eye
(-0.47, 2.68, 13.32), in interior model space; ground offset -0.70 m.
These are game-kit fits, not surveyed aircraft measurements.
