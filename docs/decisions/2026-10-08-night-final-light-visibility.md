# Night final light-source visibility

Status: Accepted for implementation under Bailey's bug report
Date: 2026-10-08

## Decision

Use an original additive AircraftLightHalo shader for aircraft landing/taxi flares
and distant glows. It attenuates source radiance with the existing airfield-point
haze exponent (0.35) instead of URP Unlit surface MixFog. Keep depth testing against
opaque scenery and explicit fog attenuation; dense weather can still obscure lights.
Retain the shader through GraphicsSettings and retain the former halo as fallback.

Place each distant glow in front of its airframe in camera depth, along the exact
root-to-camera ray; size it from that new depth to preserve its pixel diameter.
Use catalogue length/span to clear the airframe. At night an airborne position-light/
strobe glow continues below 6 km, fading over 150–600 m; existing per-lamp landing
flares cover the close nose-on final. Ground aircraft do not gain this night floor.

## Reason

The old additive glow used URP Unlit fog, mixing its light into the dark night fog
like a surface. The distant glow was also placed inside the opaque fuselage.
The previous close-range fix only covered a 35-degree head-on sector, leaving
side-on night finals without distant position/strobe glows inside 6 km.

## Affected systems and migration

Aircraft exterior light presentation only; airport halo material, aircraft motion,
engine/phase/height lamp switching, graphics toggles and saves remain unchanged.
No migration. New original source is registered in the asset/data register.

## Evidence and limits

Bounded source checks cover shader retention/depth/haze and material routing;
numeric checks cover pixel sizing, clear-fog transmission and close night visibility.
Native Unity/Metal shader compilation, actual appearance and performance require
Bailey's Mac build/playtest. Check overview and tower, head-on/side/aft, clear/fog,
day/night, both glow toggles and the distant-to-individual-lamp transition.

Checked: changed C# parses with zero Roslyn syntax errors; diff whitespace clean;
new shader GUID retained in GraphicsSettings; depth/additive/haze material routing
checks pass; projected distant diameter stays 7 px after depth pull. At 10 km in
10 km nominal visibility, night surface transmission is about 0.05 and light-source
transmission about 0.35. The asset audit finds only the already recorded
`tx_adelaide_sentinel2_l2a_v02.jpg` mirror mismatch; no new metadata issues.
No Unity compile, shader compile, player build or rendered playtest ran here.
