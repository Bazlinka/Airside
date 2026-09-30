# 0186 — Ground Adelaide emergency aviation in the current rescue precinct

Date: 30 September 2026
Status: Accepted

## Decision

Place a project-authored, unbranded Bell 412EP-class rescue helicopter on the
committed OpenStreetMap `Helipad West` outline beside the SA Ambulance rescue,
retrieval and aviation-services base. Render the pad and aircraft as static world
presentation with a procedural helicopter fallback. Improve parked road vehicles
and mapped signal hardware deterministically within the same presentation-only
asset slice, without adding road-traffic or helicopter simulation.

## Reason

The west rescue precinct and its 37.9 m pad already exist in the licensed map
snapshot but were invisible, leaving a distinctive Adelaide operation absent.
Babcock's current contract evidence records a Bell 412EP configured for SA
Ambulance Service, while the South Australian Government dates the announced
AW139 replacement fleet to October 2027. The Bell silhouette is therefore the
appropriate current-state choice on 30 September 2026. Bell's official 412
literature provides the dimensional envelope.

The road map already has accurate coordinates and furniture data. Its main gap is
asset readability, so silhouette/hardware refinement is safer than introducing a
new traffic system or changing the imported road network.

## Affected systems

- AIR-017 source/runtime helicopter art and StreamingAssets mirror
- Adelaide road, helipad and static-world presentation
- deterministic parked-car and traffic-signal geometry
- EditMode presentation/asset tests and art/data records

## Migration impact

None. No saved schema, command, schedule, reservation, aircraft ownership,
mission, route or traffic state changes. Removing AIR-017 uses the procedural
fallback; removing the pad/road detail leaves simulation unchanged.
