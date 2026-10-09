# Adelaide T1 opening and dependable identity

Date: 8 October 2026  
Status: Implemented at Bailey's request; native visual acceptance pending

## Decision

Replace the generic airport title illustration with a versioned Adelaide T1 architectural interpretation. Use an original departure-vector AIRSIDE lockup, dark departure card and separately rendered ADL/YPAD/T1 location signature. Keep the existing v03 application/Dock icon.

Brand PNGs fit rather than crop; a missing title lockup renders device-space AIRSIDE text instead of a blank slot. The new artwork falls back to the legacy illustration, then the ink title backing. Old assets remain available.

Continue/start retains the 2.8-second camera glide and adds a centre-opening aperture, lifting/fading identity, retracting letterbox and a transient low greeting. The camera intro clock drives all reveal timing. Existing skip input, soak bypass and OpeningAnimation preference remain intact.

## Reason

Bailey reported that the opening did not look like Adelaide Airport, the logo was still absent and Continue needed a better animation. The legacy illustration portrayed a generic regional airport. The old image renderer also cropped all imagery, including transparent identities, and had no missing-logo fallback. The new artwork represents the long low T1 concourse and repeated jetbridges with a flat Adelaide setting; this is stylised art, not an exact surveyed reconstruction.

## Affected systems

SplashScreen, AirsideTheme, HudPainter, AirsidePrototype.Intro; versioned UI/Brand PNGs and StreamingAssets mirrors. Generation/source evidence: `docs/art/prompts/adelaide-opening-2026-10-08.md`.

## Migration

None. No simulation, save schema, economy or runtime 3D geometry changes. Native Unity compile, interactive transition, packaged loading and Retina visual acceptance remain open. Earlier opening/identity decisions remain applicable except for their title artwork, lockup and dissolve-only transition.
