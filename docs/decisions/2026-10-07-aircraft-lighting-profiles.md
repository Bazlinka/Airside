# ADR 2026-10-07: Per-type aircraft lighting profiles

Date: 7 October 2026 (Adelaide). Status: proposed; unverified in Unity until the Mac playtest.

Every aircraft used the same lamp numbers: one landing beam (48°, 90 m), one taxi beam, one
double-flash wingtip strobe, one beacon rate, and strobes on the wingtips only. `AircraftLightingProfile`
(pure, `Presentation/`) now holds the numbers per type family, keyed from the catalogue type id.

- **Turboprop** (ATR 42, Saab 340B, Dash 8-400): wide short landing beam, single-flash wingtip strobes, no tail strobe, slower beacon.
- **Regional jet** (E190, A220-300): narrower longer beam, tail strobe, wing-root lamps toed out 2°.
- **Narrowbody** (A320, A321neo, 737-800, 737-8): narrow 120 m beam toed out 2.5°, tail strobe, nose-gear lamp doubles as the takeoff light.
- **Widebody** (A330-900neo, A350-900, 787-9, 787-10): narrowest, longest beams, brightest strobes, largest nav reach.
- **Helicopter** (Bell 412): nose searchlight angled 25° down, no tail strobe.
- Unknown or null type: the previous fleet-wide values, so sky traffic of unmapped types looks as before.

Landing lamps are aimed 2.5–3° below the fuselage axis; the taxi lamp 4–4.5° down. The nose-gear lamp of
the jets stays lit for the takeoff roll and through approach/landing, gear down, as the A320/737 nose lights
do on the T.O. setting; turboprops keep it taxi-only.

Strobe pattern (single versus double flash) and the per-family numbers are visual approximations of typical
installations, not manufacturer data. Simulation is untouched: lights are presentation only. No save change.

Not done: runway-turnoff lamps, logo/tail-illumination lamps and wing-inspection lamps (the models have no
nodes for them); landing lamps staying on above 10,000 ft; strobes at line-up rather than at the start of the roll.

Affected: `Presentation/AircraftLightingProfile.cs`, `AirsidePrototype.Lights.cs`, the sky and live traffic
light calls (now pass the type). Revert: delete the profile and restore the constants in `Lights.cs`.
