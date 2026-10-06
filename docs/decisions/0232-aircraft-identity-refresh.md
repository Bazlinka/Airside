# ADR 0232: Readable original aircraft identities

Date: 2026-10-06. Owner: Codex. Request: refresh every aircraft's visual identity.

The thirteen fixed-wing types have individually authored fuselage compositions,
with broad belly colour, rear blocks, split bands, rays and angular silhouettes
in place of the repeated narrow rising ribbon. Tail symbols are retained as
project-owned recognisable motifs, with larger A220 rays and 787-10 meridian marks;
the shared diagonal tail accent is replaced by family-specific root signatures.
Five existing player palettes and live operator names continue to apply. These
composition names describe art; they do not rename simulation airlines.

| Type | Identity | Hull composition |
|---|---|---|
| ATR 42 | Saltwater Wings | broad ascending coastal band with separated sand edge |
| Saab 340 | Ochre Rise | straight lower band and separate rear wedge |
| Q400 | Coastal Current | two separated currents |
| E190 | Starpoint | deep belly and angular rear colour block |
| A220 | Daybreak | rear quarter block and detached rays |
| A320 | Tidal Arc | stepped belly and long contrasting arc |
| 737-800 | Range Country | mountain-edged lower band |
| 737-8 | Crosswind | crossing primary and accent diagonals |
| A321neo | Long Coast | split belly with isolated aft stroke |
| A350 | Aurora | deep keel and two rising aurora panels |
| A330neo | Desert Dawn | ascending stepped horizon and detached peak |
| 787-9 | Ocean Reach | broad ocean-colour belly and aft crest |
| 787-10 | Meridian | level belt with two upright rear markers |
| Bell 412 | Mountain Rescue | fitted lower hull and boom, fin peak and accent edge |

Paint remains clipped to the true hull/fin triangles. Concave regions are
triangulated before clipping. Standalone fixed-wing repaint generation preserves
all non-paint position/index arrays, rather than restoring older generator parts.
The Bell replaces two box-shaped paint pieces with fitted panels and adds two
marking meshes; the rotors, glazing, hull and rig are retained. Its builder and
in-place fleet repaint now use the operator colour and matching preset accent.
The static rescue display retains its default emergency colour.

All new art is deterministic and project-authored. No airline logo, artwork,
exact livery or new external asset is introduced. Prior git revision is the
fallback. No save, economy, aircraft performance or flight-path changes.

Evidence: docs/testing/aircraft-identities-2026-10-06/README.md. Software proof
sheets establish geometry/colour composition; Unity review and packaged gameplay
coverage must be reported separately.
