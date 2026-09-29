# 0188 — Hangar berths for checks

Status: accepted (Unity look not yet verified)

A hangar holds only as many aircraft as fit side by side across its door (1 to 3, from its OSM outline and the type's
span). `HangarTow.Options` lists every hangar that fits a type, best first; `HangarBays.Assign` places the fleet's checks
in order of start, each in the first free berth of its best hangar. `StartCheck` refuses with "Every hangar that fits
VH-XXX is full until HH:MM" when none is free, before charging anything. Nothing is stored: berths derive from
`CheckUntil` and the check length, so a later check never moves an earlier one and saves are unchanged. The tow uses
the berth, so two aircraft in one hangar stand side by side. A type no hangar fits is not limited and is not towed.

Known: capacity is counted per hangar by slot number, so mixed types in one hangar can overlap slightly; the Fleet
card does not yet name the hangar.
