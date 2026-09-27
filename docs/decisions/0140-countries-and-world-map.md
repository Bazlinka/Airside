# 0140 — Countries, a wider map, real coastlines, towns and runways

Date: 27 September 2026. Author: Claude, at Bailey's request ("improvements to the map ensuring
airports are correct in location wise including zoom functionality to view small towns and airport
detail. Add support for other countries"). Bailey chose Asia-Pacific plus a world map.

## Context

- **The coastline, not the airports, was wrong.** Checked against OurAirports, every catalogue
  airport was within 1.6 km. The problem was the coastline: about 75 hand-typed points. It had no
  Kangaroo Island, and it put Whyalla and Cairns out at sea.
- **The map stopped at Australia.** Its bounds were lon 112–155 and lat −44.5 to −9.5, so the
  overseas airports the game already served could never be seen.
- **No countries.** There was no country field: `State` held "New Zealand" or "Singapore", and
  the career code tested for that string.

## Decision

- **Countries (Domain).**
  - `Destination` gains `Country` (ISO-2), `CountryName`, `IsAustralian` and `Region`. `Region` is
    the state at home and the country overseas.
  - Overseas airports have an empty `State`.
  - The "State holds the country" checks in `CareerRoadmap` and the balance bot now use
    `IsAustralian`.
- **Twelve new airports**, with coordinates from OurAirports:
  - Tokyo NRT, Osaka KIX, Seoul ICN, Shanghai PVG, Bangkok BKK, Ho Chi Minh City SGN, Manila MNL,
    Jakarta CGK;
  - Port Moresby POM, Nouméa NOU, Honolulu HNL, Los Angeles LAX.
- **A Pacific band** sits between the Tasman and long-haul.
  - It covers Nadi, Nouméa, Port Moresby and Bali, and pays ×1.7.
  - The A321neo and A220 (previously Tasman) may fly it when it is in range.
  - Los Angeles (13,400 km) is long-haul, reachable only by the A350 (15,000 km) and the 787-9.
  - The market draws Pacific and the wider long-haul pool.
  - The long-haul challenge now counts every long-haul city (15), and its reward rises from
    $25,000 to $40,000.
- **Generated geography (`scripts/map/build_map_data.py` → `MapGeographyData.cs`).** Built from
  Natural Earth (MAP-001) and OurAirports (MAP-002), both public domain:
  - coast at 1:110m for the world view, 1:50m for the region, and 1:10m with islands for
    Australia, New Zealand, New Guinea and Indonesia;
  - fine 1:10m shoreline within 0.8° of every served airport;
  - Australian state borders;
  - 210 towns with a size rank;
  - runways and elevations for all 40 airports.
- **The map (`AustraliaMapLens`, `MapGeography`, `RouteMapWorkspacePainter`).**
  - Zoom 1 still frames Australia. Zoom runs from 0.2 (Doha to Los Angeles) to 400 (a single
    runway).
  - A map button switches between "World" and "Australia".
  - Longitudes west of 60°W are unwrapped east, so Honolulu and Los Angeles sit east of Australia
    and trans-Pacific routes stay continuous. Segments that would jump half the world are never
    joined.
  - The coastline's level of detail follows zoom. Rings outside the view are culled, and vertices
    closer than 2.5 px are skipped, so any view draws at most a few thousand segments.
  - From zoom 10, the fine airport shoreline replaces the detailed coast near each airport.
  - Overseas cities show a country chip (ISO code) and keep their names at wider zoom. Labels
    that would overlap are dropped, but the dot always stays.
  - From zoom 3, towns appear as grey dots, more of them as you zoom in. A town that is already
    an airport is not labelled twice.
  - From zoom 30, each airport draws its runways to scale with their numbers, plus its full
    name, code and elevation.

## Verification

- `MapGeographyTests` covers:
  - every destination has a country, with `State` kept for Australia;
  - Kingscote, Whyalla, Cairns and others sit on land. Every airport is on land or right at the
    fine coast (Kansai and Hong Kong are on reclaimed land);
  - the new bands and Los Angeles range;
  - the world view shows every destination;
  - the Adelaide–Los Angeles route never jumps;
  - level of detail by zoom, with a line budget at every zoom;
  - towns appear when zoomed in;
  - every airport has runways where it is;
  - runway detail appears only when zoomed right in.
- `AustraliaMapLensTests` is updated for the home zoom and the generated data.
- Offline renders of world, Australia, state and airport zoom are in
  `docs/testing/map-2026-09-27/`.
- Mac checks:
  - frame rate on the map at every zoom;
  - scroll-zooming all the way into Adelaide's runways;
  - the World/Australia button;
  - clicking overseas dots;
  - tracked flights to Los Angeles drawing across the Pacific.
